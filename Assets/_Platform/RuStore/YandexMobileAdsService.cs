#if STORE_RUSTORE
using System;
using System.Collections;
using UnityEngine;
using YG;

/// <summary>
/// IAdsService поверх PluginYG2 (интеграция Yandex Mobile Ads, РСЯ) — платформенный слой RuStore.
/// Собственного доступа к Yandex Mobile Ads SDK здесь НЕТ: только публичное API YG2 и его события.
/// Контракт AdsFlow сохранён:
/// • ShowInterstitial(): false = рекламу не показываем (AdsFlow идёт домой мгновенно, без ожидания);
///   true = показ, InterstitialClosed придёт РОВНО один раз (закрытие / ошибка / watchdog).
/// • ShowRewarded(cb): cb ровно один раз; true — только при реально выданной награде.
///   Награда (onRewardAdv) приходит ДО закрытия — копим флаг, результат отдаём по закрытию/ошибке.
/// • Баннер = модуль BannerAdv плагина (bottom, показывается при старте SDK).
/// Мьют/пауза игры — НЕ здесь (AdsFlow), автопауза плагина в InfoYG выключена (autoPauseGame: 0).
/// Живёт на DontDestroyOnLoad-объекте, регистрируется RuStoreInstaller (BeforeSceneLoad).
/// </summary>
public class YandexMobileAdsService : MonoBehaviour, IAdsService
{
    private const string RewardId = "continue";
    private const float InterstitialOpenWatchdog = 4f; // плагин молча не открыл рекламу → не вешаем «Домой»
    private const float RewardedShowWatchdog = 3f;     // YMA молча не показал (ad == null) → не вешаем mute и «Продолжить»

    public event Action InterstitialClosed;

    // В Яндексе нет честного «загружен ли rewarded»: оцениваем готовность по SDK/занятости показа.
    // Если реклама не загрузится — YG2 пришлёт onErrorRewardedAdv и onResult(false).
    public bool IsRewardedReady => YG2.isSDKEnabled && !YG2.nowAdsShow && !_rewardedShowing;

    private bool _interShowing, _interOpened;
    private bool _rewardedShowing, _rewarded, _rewardedOpened;
    private Action<bool> _onRewardedResult;
    private Coroutine _watchdog, _rewardWatchdog;

    private void Awake()
    {
        YG2.onOpenInterAdv += OnInterOpened;
        YG2.onCloseInterAdv += OnInterClosed;
        YG2.onErrorInterAdv += OnInterClosed;

        YG2.onOpenRewardedAdv += OnRewardedOpened;
        YG2.onRewardAdv += OnReward;
        YG2.onCloseRewardedAdv += OnRewardedClosed;
        YG2.onErrorRewardedAdv += OnRewardedClosed;

        if (YG2.isSDKEnabled) ShowBanner();
        else YG2.onGetSDKData += OnSdkReady;
    }

    private void OnDestroy()
    {
        YG2.onOpenInterAdv -= OnInterOpened;
        YG2.onCloseInterAdv -= OnInterClosed;
        YG2.onErrorInterAdv -= OnInterClosed;

        YG2.onOpenRewardedAdv -= OnRewardedOpened;
        YG2.onRewardAdv -= OnReward;
        YG2.onCloseRewardedAdv -= OnRewardedClosed;
        YG2.onErrorRewardedAdv -= OnRewardedClosed;

        YG2.onGetSDKData -= OnSdkReady;
    }

    private void OnSdkReady()
    {
        YG2.onGetSDKData -= OnSdkReady;
        ShowBanner();
    }

    // ——— Interstitial (raw-показ; частотная формула — в AdsFlow) ———

    public bool ShowInterstitial()
    {
        if (!YG2.isSDKEnabled || YG2.nowAdsShow || _interShowing) return false;
        if (!YG2.isTimerAdvCompleted)
        {
            // InterstitialAdvShow() при не вышедшем таймере плагина молча ничего не покажет
            // и события не пришлёт → без этой проверки GameUI ждал бы InterstitialClosed вечно.
            Debug.Log("[YandexAds] Interstitial: таймер плагина не вышел — домой без рекламы.");
            return false;
        }

        _interShowing = true;
        _interOpened = false;
        Debug.Log("[YandexAds] Показ interstitial…");
        YG2.InterstitialAdvShow();

        // Реклама могла не открыться и промолчать (нет загрузки / ошибка без колбэка).
        _watchdog = StartCoroutine(WatchdogRoutine());
        return true;
    }

    private IEnumerator WatchdogRoutine()
    {
        yield return new WaitForSecondsRealtime(InterstitialOpenWatchdog);
        if (_interShowing && !_interOpened)
        {
            Debug.LogWarning("[YandexAds] Interstitial не открылся за 4 с — считаем закрытым (игра не подвиснет).");
            OnInterClosed();
        }
    }

    private void OnInterOpened() => _interOpened = true;

    private void OnInterClosed()
    {
        if (!_interShowing) return; // дедуп: закрытие / ошибка / watchdog дают событие строго один раз
        _interShowing = false;
        if (_watchdog != null) StopCoroutine(_watchdog);
        _watchdog = null;
        InterstitialClosed?.Invoke();
    }

    // ——— Rewarded ———

    public void ShowRewarded(Action<bool> onResult)
    {
        if (onResult == null) return;
        if (!YG2.isSDKEnabled || YG2.nowAdsShow || _rewardedShowing)
        {
            Debug.Log("[YandexAds] Rewarded: SDK не готов / реклама уже идёт — onResult(false).");
            onResult(false);
            return;
        }

        _rewardedShowing = true;
        _rewarded = false;
        _rewardedOpened = false;
        _onRewardedResult = onResult;
        Debug.Log("[YandexAds] Показ rewarded…");
        YG2.RewardedAdvShow(RewardId);

        // YMA-реализация при недогруженной рекламе выходит молча, без колбэков
        // (ShowRewardedAd: rewardedAd == null && !autoLoad → return). Без watchdog
        // AdsFlow никогда не снимет mute и кнопка «Продолжить» повиснет навсегда.
        _rewardWatchdog = StartCoroutine(RewardedWatchdogRoutine());
    }

    private IEnumerator RewardedWatchdogRoutine()
    {
        yield return new WaitForSecondsRealtime(RewardedShowWatchdog);
        if (_rewardedShowing && !_rewardedOpened)
        {
            Debug.LogWarning("[YandexAds] Rewarded не открылся за 3 с — считаем неудачей (без награды).");
            OnRewardedClosed();
        }
    }

    private void OnRewardedOpened() => _rewardedOpened = true;

    private void OnReward(string id)
    {
        // Награда приходит ДО закрытия — только копим флаг.
        if (_rewardedShowing && id == RewardId)
        {
            _rewarded = true;
            Debug.Log("[YandexAds] Rewarded: награда получена.");
        }
    }

    private void OnRewardedClosed()
    {
        if (!_rewardedShowing) return; // колбэк строго один раз
        _rewardedShowing = false;
        if (_rewardWatchdog != null) StopCoroutine(_rewardWatchdog);
        _rewardWatchdog = null;
        var cb = _onRewardedResult;
        _onRewardedResult = null;
        Debug.Log($"[YandexAds] Rewarded результат: {(_rewarded ? "НАГРАДА выдана" : "без награды/ошибка")}.");
        cb?.Invoke(_rewarded);
    }

    // ——— Баннер (модуль BannerAdv плагина; на мобильной интеграции YMA — sticky) ———

    public void ShowBanner()
    {
#if BannerAdv_yg
        if (!YG2.isSDKEnabled) return;
        YG2.SetBannerPosition(YG2.BannerPosition.Bottom);
        YG2.ShowBanner();
        Debug.Log("[YandexAds] Баннер: показ (bottom).");
#else
        Debug.Log("[YandexAds] Баннер: модуль BannerAdv не установлен — ShowBanner() no-op.");
#endif
    }

    public void HideBanner()
    {
#if BannerAdv_yg
        if (!YG2.isSDKEnabled) return;
        YG2.HideBanner();
        Debug.Log("[YandexAds] Баннер: скрыт.");
#else
        Debug.Log("[YandexAds] Баннер: модуль BannerAdv не установлен — HideBanner() no-op.");
#endif
    }
}
#endif
