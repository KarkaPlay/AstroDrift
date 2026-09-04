#if STORE_RUSTORE
using System;
using System.Collections;
using UnityEngine;
using YandexMobileAds;
using YandexMobileAds.Base;

/// <summary>
/// Платформенная обёртка рекламы RuStore: Yandex Mobile Ads (РСЯ).
/// Платформенная ПОЛОВИНА бывшего YandexAdsManager: sticky-баннер с retry,
/// interstitial/rewarded лоадеры и обработчики. Игровой логики (формула, сессии,
/// mute, аналитика) здесь НЕТ — она в AdsFlow (Assets/Scripts/Ads/AdsFlow.cs).
/// Живёт на DontDestroyOnLoad-объекте, регистрируется RuStoreInstaller (BeforeSceneLoad).
/// </summary>
public class YandexMobileAdsService : MonoBehaviour, IAdsService
{
    [Header("Ad Unit ID (партнёрский интерфейс РСЯ)")]
    [SerializeField] private string bannerAdUnitId = "R-M-19858053-1";
    [SerializeField] private string interstitialAdUnitId = "R-M-19858053-2";
    [SerializeField] private string rewardedAdUnitId = "R-M-19858053-3";

    [Header("Авто-перезагрузка при ошибке")]
    [SerializeField] private int maxLoadRetries = 3;
    [SerializeField] private float retryDelaySeconds = 10f;

    public event Action InterstitialClosed;

    /// <summary>Баннер показан (после успешной загрузки; sticky показывается автоматически).</summary>
    public bool IsBannerVisible { get; private set; }

    public bool IsRewardedReady => _rewarded != null;

    private Banner banner;
    private Interstitial interstitial;
    private RewardedAd _rewarded;
    private bool interstitialLoadInProgress;
    private bool rewardedLoadInProgress;
    private bool interstitialShowing;
    private bool rewardedShowing;
    private int loadAttempts;      // подряд неуспешных попыток
    private bool loadInProgress;
    private bool destroyPending;

    // Состояние ожидающего rewarded-колбэка (колбэк — строго один раз, §10.1.3)
    private Action<bool> _rewardedResult;
    private bool _rewardedRewarded;
    private bool _rewardedCallbackFired;

    private void Awake()
    {
        // Примечание: в com.yandex.mobileads 8.3.0 нет C# API MobileAds.Initialize —
        // SDK инициализируется лениво при первом использовании (как и в прежнем менеджере).
        RequestBanner();
        RequestInterstitial();
        RequestRewarded();
    }

    // ——— IAdsService: interstitial (raw-показ; формула — в AdsFlow) ———

    public bool ShowInterstitial()
    {
        if (interstitialShowing)
        {
            Debug.Log("[YandexAds] Interstitial уже показывается — рестарт без рекламы.");
            return false;
        }
        if (interstitial == null)
        {
            Debug.Log("[YandexAds] Interstitial не загружен — рестарт мгновенный, без ожидания.");
            return false; // НЕ блокируем: перезагрузка уже в процессе
        }

        interstitialShowing = true;
        Debug.Log("[YandexAds] Показ interstitial…");
        interstitial.Show();
        return true;
    }

    private void HandleInterstitialDismissed(object sender, EventArgs args)
    {
        interstitialShowing = false;
        Debug.Log("[YandexAds] Interstitial закрыт → перезагрузка + сигнал рестарта.");
        DestroyInterstitialObject();
        RequestInterstitial();
        InterstitialClosed?.Invoke();
    }

    private void HandleInterstitialFailedToShow(object sender, EventArgs args)
    {
        interstitialShowing = false;
        Debug.LogWarning("[YandexAds] Ошибка показа interstitial → перезагрузка + сигнал рестарта (игра не подвисает).");
        DestroyInterstitialObject();
        RequestInterstitial();
        InterstitialClosed?.Invoke();
    }

    // ——— IAdsService: rewarded ———

    public void ShowRewarded(Action<bool> onResult)
    {
        if (onResult == null) return;

        if (rewardedShowing)
        {
            Debug.Log("[YandexAds] Rewarded уже показывается — отказ без показа.");
            onResult(false);
            return;
        }
        if (_rewarded == null)
        {
            Debug.Log("[YandexAds] Rewarded не загружен — onResult(false) мгновенно (перезагрузка уже в процессе).");
            onResult(false);
            return;
        }

        rewardedShowing = true;
        _rewardedResult = onResult;
        _rewardedRewarded = false;
        _rewardedCallbackFired = false;
        Debug.Log("[YandexAds] Показ rewarded…");
        _rewarded.Show();
    }

    private void FireRewardedResult(bool result)
    {
        if (_rewardedCallbackFired) return; // колбэк — строго один раз (§10.1.3)
        _rewardedCallbackFired = true;
        rewardedShowing = false;
        var cb = _rewardedResult;
        _rewardedResult = null;
        Debug.Log($"[YandexAds] Rewarded результат: {(result ? "НАГРАДА выдана" : "без награды/ошибка")}.");
        cb?.Invoke(result);
    }

    private void HandleRewardedRewarded(object sender, YandexMobileAds.Base.Reward args)
    {
        _rewardedRewarded = true;
        Debug.Log("[YandexAds] Rewarded: награда получена.");
    }

    private void HandleRewardedDismissed(object sender, EventArgs args)
    {
        Debug.Log("[YandexAds] Rewarded закрыт → перезагрузка.");
        bool result = _rewardedRewarded;
        DestroyRewardedObject();
        RequestRewarded();
        FireRewardedResult(result);
    }

    private void HandleRewardedFailedToShow(object sender, EventArgs args)
    {
        Debug.LogWarning("[YandexAds] Ошибка показа rewarded → перезагрузка.");
        DestroyRewardedObject();
        RequestRewarded();
        FireRewardedResult(false);
    }

    // ——— IAdsService: баннер ———

    public void ShowBanner()
    {
        if (banner != null && !IsBannerVisible)
        {
            banner.Show();
            IsBannerVisible = true;
        }
    }

    public void HideBanner()
    {
        if (banner != null && IsBannerVisible)
        {
            banner.Hide();
            IsBannerVisible = false;
        }
    }

    // ——— Загрузка баннера ———

    private void RequestBanner()
    {
        if (loadInProgress || destroyPending) return;
        loadInProgress = true;

        // Пересоздание: старый баннер (даже после ошибки) уничтожаем
        DestroyBannerObject();

        // Sticky: ширина во dp под текущий экран (безопасная зона снизу, HUD — вверху).
        // density = dpi / 160 (Android-эквивалент); Screen.dpi может быть 0 при старте — берём минимум 160.
        float dpi = Mathf.Max(Screen.dpi, 160f);
        int widthDp = Mathf.Max(320, Mathf.RoundToInt(Screen.width / (dpi / 160f)));

        banner = new Banner(
            BannerAdSize.Sticky(widthDp),
            AdPosition.BottomCenter);

        banner.OnAdLoaded += HandleAdLoaded;
        banner.OnAdFailedToLoad += HandleAdFailedToLoad;

        banner.LoadAd(new AdRequest(bannerAdUnitId));
        Debug.Log($"[YandexAds] Запрос баннера (sticky, {widthDp}dp, '{bannerAdUnitId}')…");
    }

    private void HandleAdLoaded(object sender, EventArgs args)
    {
        loadInProgress = false;
        loadAttempts = 0;
        IsBannerVisible = true; // Banner.Show() при загрузке sticky показывается автоматически
        Debug.Log("[YandexAds] Баннер загружен и показан (низ экрана).");
    }

    private void HandleAdFailedToLoad(object sender, AdFailureEventArgs args)
    {
        loadInProgress = false;
        Debug.LogWarning("[YandexAds] Ошибка загрузки баннера: " +
                         (args != null ? args.Message : "(нет деталей)"));

        if (loadAttempts < maxLoadRetries)
        {
            loadAttempts++;
            StartCoroutine(RetryLoad(retryDelaySeconds * loadAttempts));
        }
        else
        {
            Debug.LogWarning($"[YandexAds] Превышен лимит попыток ({maxLoadRetries}). " +
                             "Баннер не будет перезагружаться до конца сессии.");
        }
    }

    private IEnumerator RetryLoad(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!destroyPending) RequestBanner();
    }

    private void DestroyBannerObject()
    {
        if (banner == null) return;
        banner.OnAdLoaded -= HandleAdLoaded;
        banner.OnAdFailedToLoad -= HandleAdFailedToLoad;
        banner.Destroy();
        banner = null;
        IsBannerVisible = false;
    }

    // ——— Загрузка interstitial ———

    private void RequestInterstitial()
    {
        if (interstitialLoadInProgress || destroyPending) return;
        interstitialLoadInProgress = true;

        var loader = new InterstitialAdLoader();
        loader.LoadAd(
            new AdRequest(interstitialAdUnitId),
            onLoaded: ad =>
            {
                interstitialLoadInProgress = false;
                DestroyInterstitialObject();
                interstitial = ad;
                interstitial.OnAdDismissed += HandleInterstitialDismissed;
                interstitial.OnAdFailedToShow += HandleInterstitialFailedToShow;
                Debug.Log($"[YandexAds] Interstitial загружен ('{interstitialAdUnitId}') — готов к показу.");
            },
            onFailed: err =>
            {
                interstitialLoadInProgress = false;
                Debug.LogWarning("[YandexAds] Ошибка загрузки interstitial: " +
                                 (err != null ? err.Message : "(нет деталей)"));
            });
        Debug.Log($"[YandexAds] Запрос interstitial ('{interstitialAdUnitId}')…");
    }

    private void DestroyInterstitialObject()
    {
        if (interstitial == null) return;
        interstitial.OnAdDismissed -= HandleInterstitialDismissed;
        interstitial.OnAdFailedToShow -= HandleInterstitialFailedToShow;
        interstitial.Destroy();
        interstitial = null;
    }

    // ——— Загрузка rewarded (GDD_DeathScreen_Continue §10.1.2) ———

    private void RequestRewarded()
    {
        if (rewardedLoadInProgress || destroyPending) return;
        rewardedLoadInProgress = true;

        var loader = new RewardedAdLoader();
        loader.LoadAd(
            new AdRequest(rewardedAdUnitId),
            onLoaded: ad =>
            {
                rewardedLoadInProgress = false;
                DestroyRewardedObject();
                _rewarded = ad;
                _rewarded.OnRewarded += HandleRewardedRewarded;
                _rewarded.OnAdDismissed += HandleRewardedDismissed;
                _rewarded.OnAdFailedToShow += HandleRewardedFailedToShow;
                Debug.Log($"[YandexAds] Rewarded загружен ('{rewardedAdUnitId}') — готов к показу.");
            },
            onFailed: err =>
            {
                rewardedLoadInProgress = false;
                Debug.LogWarning("[YandexAds] Ошибка загрузки rewarded: " +
                                 (err != null ? err.Message : "(нет деталей)"));
            });
        Debug.Log($"[YandexAds] Запрос rewarded ('{rewardedAdUnitId}')…");
    }

    private void DestroyRewardedObject()
    {
        if (_rewarded == null) return;
        // Снятие подписок до Destroy — колбэки не стреляют в мёртвые объекты (§10.1)
        _rewarded.OnRewarded -= HandleRewardedRewarded;
        _rewarded.OnAdDismissed -= HandleRewardedDismissed;
        _rewarded.OnAdFailedToShow -= HandleRewardedFailedToShow;
        _rewarded.Destroy();
        _rewarded = null;
    }

    private void OnDestroy()
    {
        destroyPending = true;
        StopAllCoroutines();
        DestroyBannerObject();
        DestroyInterstitialObject();
        DestroyRewardedObject();
    }
}
#endif
