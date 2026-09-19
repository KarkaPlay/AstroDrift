#if STORE_YANDEX
using System;
using UnityEngine;
using YG;

/// <summary>
/// IAdsService поверх PluginYG2 — зеркало YandexMobileAdsService (RuStore).
/// Контракт AdsFlow сохранён:
/// • ShowInterstitial(): false = не показываем (AdsFlow идёт домой мгновенно); true = показ,
///   InterstitialClosed придёт РОВНО один раз (закрытие / ошибка / watchdog).
/// • ShowRewarded(cb): cb ровно один раз; true — только при выданной награде.
///   Награда (onRewardAdv) приходит ДО закрытия — копим флаг, отдаём результат по закрытию.
/// • У Яндекса нет «загружен ли rewarded» — показ всегда пробуется, отказ придёт ошибкой.
/// • Баннер = Sticky площадки. Показ НЕ автоматический (§4.5/§5): владелец видимости —
///   GameManager; sticky гасится явным StickyAdActivity(false) уже на старте (меню первым).
/// Мьют/пауза игры — НЕ здесь (AdsFlow), автопауза плагина в InfoYG выключена.
/// </summary>
public sealed class YandexGamesAdsService : IAdsService
{
    private const string RewardId = "continue";
    private const float InterstitialOpenWatchdog = 4f; // плагин молча не открыл рекламу → не вешаем «Домой»

    public event Action InterstitialClosed;
    public bool IsRewardedReady => YG2.isSDKEnabled && !YG2.nowAdsShow && !_rewardedShowing;

    private bool _interShowing, _interOpened;
    private bool _rewardedShowing, _rewarded;
    private Action<bool> _onRewardedResult;
    private Coroutine _watchdog;

    // Баннер: запрос GameManager может прийти до готовности SDK — храним его и применяем в onGetSDKData.
    private bool _wantVisible;
    private bool _bannerApplied, _bannerShown;

    public YandexGamesAdsService()
    {
        YG2.onOpenInterAdv += OnInterOpened;
        YG2.onCloseInterAdv += OnInterClosed;
        YG2.onErrorInterAdv += OnInterClosed;

        YG2.onRewardAdv += OnReward;
        YG2.onCloseRewardedAdv += OnRewardedClosed;
        YG2.onErrorRewardedAdv += OnRewardedClosed;

        // §4.5: начальное состояние — «скрыто»: sticky площадки активна от старта, гасим её
        // до первого входа в геймплей (меню баннера не показывает).
        if (YG2.isSDKEnabled) ApplyBanner();
        else YG2.onGetSDKData += OnSdkReady;
    }

    private void OnSdkReady()
    {
        YG2.onGetSDKData -= OnSdkReady;
        ApplyBanner(); // применит ровно то состояние, что успел запросить GameManager
    }

    // ——— Interstitial (raw-показ; частотная формула — в AdsFlow) ———

    public bool ShowInterstitial()
    {
        if (!YG2.isSDKEnabled || YG2.nowAdsShow || _interShowing) return false;
        if (!YG2.isTimerAdvCompleted)
        {
            // Таймер плагина/Яндекса не вышел: InterstitialAdvShow() молча ничего не покажет
            // и события не пришлёт → без этой проверки GameUI ждал бы InterstitialClosed вечно.
            Debug.Log("[YandexGames] Interstitial: таймер плагина не вышел — домой без рекламы.");
            return false;
        }

        _interShowing = true;
        _interOpened = false;
        Debug.Log("[YandexGames] Показ interstitial…");
        YG2.InterstitialAdvShow();

        _watchdog = YandexGamesRuntime.Ensure().RunAfter(InterstitialOpenWatchdog, () =>
        {
            if (_interShowing && !_interOpened)
            {
                Debug.LogWarning("[YandexGames] Interstitial не открылся за 4 с — считаем закрытым.");
                OnInterClosed();
            }
        });
        return true;
    }

    private void OnInterOpened() => _interOpened = true;

    private void OnInterClosed()
    {
        if (!_interShowing) return; // событие уже обработано (дедуп close/error/watchdog)
        _interShowing = false;
        if (_watchdog != null && YandexGamesRuntime.Instance != null)
            YandexGamesRuntime.Instance.StopCoroutine(_watchdog);
        _watchdog = null;
        InterstitialClosed?.Invoke();
    }

    // ——— Rewarded ———

    public void ShowRewarded(Action<bool> onResult)
    {
        if (onResult == null) return;
        if (!YG2.isSDKEnabled || YG2.nowAdsShow || _rewardedShowing)
        {
            Debug.Log("[YandexGames] Rewarded: SDK не готов / реклама уже идёт — onResult(false).");
            onResult(false);
            return;
        }
        _rewardedShowing = true;
        _rewarded = false;
        _onRewardedResult = onResult;
        Debug.Log("[YandexGames] Показ rewarded…");
        YG2.RewardedAdvShow(RewardId);
    }

    private void OnReward(string id)
    {
        if (_rewardedShowing && id == RewardId) _rewarded = true;
    }

    private void OnRewardedClosed()
    {
        if (!_rewardedShowing) return; // колбэк строго один раз
        _rewardedShowing = false;
        var cb = _onRewardedResult;
        _onRewardedResult = null;
        Debug.Log($"[YandexGames] Rewarded результат: {(_rewarded ? "НАГРАДА выдана" : "без награды/ошибка")}.");
        cb?.Invoke(_rewarded);
    }

    // ——— Sticky-баннер ———
    // Видимость — только по запросу GameManager (§4.5: баннер виден ровно в геймплее).

    public void ShowBanner()
    {
        _wantVisible = true;
        ApplyBanner();
    }

    public void HideBanner()
    {
        _wantVisible = false;
        ApplyBanner();
    }

    /// <summary>
    /// Применяет запрошенную видимость к SDK. Идемпотентно: при неизменной видимости
    /// SDK не трогаем; запрос до готовности SDK переживает OnSdkReady.
    /// </summary>
    private void ApplyBanner()
    {
        if (!YG2.isSDKEnabled) return; // SDK не готов: _wantVisible применится в OnSdkReady
        if (_bannerApplied && _bannerShown == _wantVisible) return; // видимость не менялась
        _bannerApplied = true;
        _bannerShown = _wantVisible;

        YG2.StickyAdActivity(_wantVisible);
    }
}
#endif
