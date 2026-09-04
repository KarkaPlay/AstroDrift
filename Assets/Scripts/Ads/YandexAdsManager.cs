using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Рекламный менеджер AstroDrift: нижний sticky-баннер + interstitial Яндекса (РСЯ).
/// Загружает баннер при старте, авто-перезагружается при ошибке
/// (retry с задержкой, не более maxLoadRetries попыток подряд).
/// SDK доступен только на Android-устройстве; в редакторе — заглушка с логом.
/// Доступ из любого скрипта: YandexAdsManager.Instance.ShowBanner() / HideBanner().
///
/// Interstitial (ТЗ, утверждено Game Designer'ом — числа менять нельзя):
/// единственная точка показа — тап «Заново» на Death-экране. Формула показа
/// (все 4 условия): totalSessionCount > 3  И  timeSinceLastInterstitial >= 180с
/// И  lastSessionDuration >= 20с  И  timeSinceAnyAd >= adsQuietSeconds
/// (тихое окно после ЛЮБОЙ рекламы — interstitial ИЛИ rewarded; фикс дефекта:
/// раньше окно считалось только от rewarded, и interstitial мог показаться
/// через ~10 с после него). Иначе — рестарт без рекламы.
/// Rewarded-путь таймер НЕ проверяет вовсе: Rewarded показывается всегда по клику.
/// Счётчики сессий и метки последних показов — в PlayerPrefs (за всё время установки).
///
/// Ad Unit ID: поля bannerAdUnitId / interstitialAdUnitId в инспекторе
/// (default = demo-banner-yandex / demo-interstitial-yandex).
/// Реальные ID берутся из партнёрского интерфейса РСЯ (реклама в мобильных приложениях),
/// формат R-M-XXXXXXX-Y.
/// </summary>
public class YandexAdsManager : MonoBehaviour
{
    public static YandexAdsManager Instance { get; private set; }

    [Header("Ad Unit ID (партнёрский интерфейс РСЯ)")]
    [SerializeField] private string bannerAdUnitId = "R-M-19858053-1";
    [SerializeField] private string interstitialAdUnitId = "R-M-19858053-2";
    // Используется только в Android-сборке (в редакторе код под #if вырезается)
#pragma warning disable 0414
    [SerializeField] private string rewardedAdUnitId = "R-M-19858053-3";
#pragma warning restore 0414

    [Header("Авто-перезагрузка при ошибке")]
    // Используются только в Android-сборке (в редакторе код под #if вырезается)
#pragma warning disable 0414
    [SerializeField] private int maxLoadRetries = 3;
    [SerializeField] private float retryDelaySeconds = 10f;
#pragma warning restore 0414

    [Header("Interstitial: формула показа (ТЗ, не менять)")]
    [SerializeField] private float minIntervalSeconds = 180f;   // не чаще 1 показа / 180 с
    [SerializeField] private int graceSessions = 3;             // показ только когда totalSessionCount > 3
    [SerializeField] private float minSessionDurationSeconds = 20f; // сессия (забег) должна длиться >= 20 с

    // ——— PlayerPrefs-ключи (персистентность за всё время установки) ———
    private const string PrefsSessionCount = "totalSessionCount";
    private const string PrefsLastInterstitial = "lastInterstitialTimestamp";
    private const string PrefsLastRewarded = "lastRewardedTimestamp";

    /// <summary>Баннер показан (после успешной загрузки и Show).</summary>
    public bool IsBannerVisible { get; private set; }

    /// <summary>
    /// Rewarded загружен и готов к показу (GDD_DeathScreen_Continue §7).
    /// В редакторе — всегда true (fake-режим).
    /// </summary>
    public bool IsRewardedReady
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return _rewarded != null;
#else
            return true;
#endif
        }
    }

    /// <summary>
    /// Срабатывает после закрытия interstitial (или при ошибке показа — чтобы игра не подвисла).
    /// GameUI подписывается и делает рестарт по этому сигналу.
    /// </summary>
    public event Action InterstitialClosed;

    // ——— Состояние сессии (в памяти; счётчики — в PlayerPrefs) ———
    private double _sessionStartRealtime;
    private float _lastSessionDurationSeconds;
    private bool _sessionActive;

#if UNITY_ANDROID && !UNITY_EDITOR
    private YandexMobileAds.Banner banner;
    private YandexMobileAds.Interstitial interstitial;
    private YandexMobileAds.RewardedAd _rewarded;
    private bool interstitialLoadInProgress;
    private bool rewardedLoadInProgress;
    private bool interstitialShowing;
    private bool rewardedShowing;
    private int loadAttempts;      // подряд неуспешных попыток
    private bool loadInProgress;
    private bool destroyPending;
#endif

    private void Awake()
    {
        // Защита от дублей (например, при повторном добавлении из Bootstrap)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        RequestBanner();
        RequestInterstitial();
        RequestRewarded();
#else
        Debug.Log("[YandexAds] Editor-заглушка: Mobile Ads SDK доступен только на Android-устройстве. " +
                  $"Баннер (adUnitId='{bannerAdUnitId}') и interstitial (adUnitId='{interstitialAdUnitId}') " +
                  "загрузятся при сборке на устройство. TryShowInterstitial() в редакторе работает в fake-режиме " +
                  "по формуле ТЗ (см. логи). ShowRewarded() в редакторе — fake-показ: 2 с ожидания → награда.");
#endif
    }

    // ——— Публичное API: rewarded (GDD_DeathScreen_Continue §7/§10.1) ———

    /// <summary>
    /// Показ rewarded (единственная точка — тап «ПРОДОЛЖИТЬ» на Death-экране).
    /// НЕ блокирует: если реклама не загружена — onResult(false) синхронно.
    /// Иначе показ; onResult(true) — только при выдаче награды (OnRewarded),
    /// onResult(false) — при закрытии без награды / ошибке показа. Колбэк — ровно один раз.
    /// В редакторе — fake-показ: 2.0 с ожидания → onResult(true).
    /// </summary>
    public void ShowRewarded(Action<bool> onResult)
    {
        if (onResult == null) return;

        // mute на время показа, unmute — по колбэку (§10.1.6)
        AudioManager.Instance?.SetMuted(true);

#if UNITY_ANDROID && !UNITY_EDITOR
        if (rewardedShowing)
        {
            Debug.Log("[YandexAds] Rewarded уже показывается — отказ без показа.");
            AudioManager.Instance?.SetMuted(false);
            onResult(false);
            return;
        }
        if (_rewarded == null)
        {
            Debug.Log("[YandexAds] Rewarded не загружен — onResult(false) мгновенно (перезагрузка уже в процессе).");
            AudioManager.Instance?.SetMuted(false);
            onResult(false);
            return;
        }

        rewardedShowing = true;
        _rewardedResult = onResult;
        _rewardedRewarded = false;
        _rewardedCallbackFired = false;
        Debug.Log("[YandexAds] Показ rewarded…");
        _rewarded.Show();
#else
        Debug.Log("[YandexAds] EDITOR fake-показ rewarded: 2.0 с ожидания → onResult(true).");
        StartCoroutine(FakeRewardedRoutine(onResult));
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private Action<bool> _rewardedResult;
    private bool _rewardedRewarded;
    private bool _rewardedCallbackFired;

    private void FireRewardedResult(bool result)
    {
        if (_rewardedCallbackFired) return; // колбэк — строго один раз (§10.1.3)
        _rewardedCallbackFired = true;
        rewardedShowing = false;
        AudioManager.Instance?.SetMuted(false);
        // Завершённый просмотр = УСПЕШНЫЙ показ rewarded → общий тихий таймер тикает
        // (фикс дефекта: без этого on Android метка не писалась и гейт её не видел).
        if (result) MarkRewardedShown();
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
#else
    private IEnumerator FakeRewardedRoutine(Action<bool> onResult)
    {
        yield return new WaitForSecondsRealtime(2.0f);
        AudioManager.Instance?.SetMuted(false);
        MarkRewardedShown();
        Debug.Log("[YandexAds] EDITOR fake-rewarded завершён → onResult(true) (перезагрузка симулируется).");
        onResult(true);
    }
#endif

    /// <summary>Записать метку показа rewarded (Unix-сек, строкой — точность double, §10.1.5).</summary>
    private void MarkRewardedShown()
    {
        double now = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        PlayerPrefs.SetString(PrefsLastRewarded, now.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    // ——— Публичное API: баннер ———

    /// <summary>Показать баннер (если он загружен и скрыт).</summary>
    public void ShowBanner()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (banner != null && !IsBannerVisible)
        {
            banner.Show();
            IsBannerVisible = true;
        }
#else
        Debug.Log("[YandexAds] Editor-заглушка: ShowBanner()");
#endif
    }

    /// <summary>Скрыть баннер (объект остаётся загруженным — повторный показ мгновенный).</summary>
    public void HideBanner()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (banner != null && IsBannerVisible)
        {
            banner.Hide();
            IsBannerVisible = false;
        }
#else
        Debug.Log("[YandexAds] Editor-заглушка: HideBanner()");
#endif
    }

    // ——— Публичное API: interstitial (частотная логика по ТЗ) ———

    /// <summary>
    /// Вызвать из GameManager при старте сессии (забега): запоминает время старта.
    /// </summary>
    public void RegisterSessionStart()
    {
        _sessionStartRealtime = Time.realtimeSinceStartupAsDouble;
        _sessionActive = true;
        Debug.Log("[YandexAds] Session start.");
    }

    /// <summary>
    /// Вызвать из GameManager при смерти: считает длительность сессии
    /// и инкрементит totalSessionCount (PlayerPrefs, за всё время установки).
    /// Сворачивание приложения счетчики не сбрасывает — они персистентны.
    /// </summary>
    public void RegisterSessionEnd()
    {
        if (!_sessionActive)
        {
            // Сессия не была зарегистрирована (например, первый запуск без BeginRun) — считаем от старта приложения
            _sessionStartRealtime = 0.0;
        }
        _sessionActive = false;

        _lastSessionDurationSeconds = (float)(Time.realtimeSinceStartupAsDouble - _sessionStartRealtime);

        int total = PlayerPrefs.GetInt(PrefsSessionCount, 0) + 1;
        PlayerPrefs.SetInt(PrefsSessionCount, total);
        PlayerPrefs.Save();

        Debug.Log($"[YandexAds] Session end: длительность={_lastSessionDurationSeconds:F1}с, " +
                  $"totalSessionCount={total} (grace: показ при >{graceSessions}).");
    }

    /// <summary>
    /// Continue (GDD_DeathScreen_Continue §9/§10.1.4): откат последнего инкремента
    /// totalSessionCount (сделанного RegisterSessionEnd при смерти) и возврат сессии
    /// в активное состояние с первичным _sessionStartRealtime — забег с continue
    /// считается ровно один раз, grace-формула interstitial не сбивается.
    /// </summary>
    public void ResumeSession()
    {
        int total = PlayerPrefs.GetInt(PrefsSessionCount, 0);
        if (total > 0)
        {
            total--;
            PlayerPrefs.SetInt(PrefsSessionCount, total);
            PlayerPrefs.Save();
        }
        _sessionActive = true; // _sessionStartRealtime не трогаем: длительность считается с первичного старта
        Debug.Log($"[YandexAds] Session resumed (continue): totalSessionCount={total}, сессия снова активна.");
    }

    /// <summary>
    /// Вызвать из GameUI при тапе «Заново» на Death-экране. НЕ блокирует:
    /// если реклама не загружена или формула не выполнена — сразу false (рестарт мгновенный).
    /// При показе true; рестарт — по подписке на InterstitialClosed.
    /// В редакторе — fake-режим: проверяет ту же формулу, логирует и сразу стреляет InterstitialClosed.
    /// </summary>
    public bool TryShowInterstitial()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (interstitialShowing)
        {
            Debug.Log("[YandexAds] Interstitial уже показывается — рестарт без рекламы.");
            return false;
        }
        if (!PassesFrequencyFormula(log: true))
        {
            return false;
        }
        if (interstitial == null)
        {
            Debug.Log("[YandexAds] Interstitial не загружен — рестарт мгновенный, без ожидания.");
            return false; // НЕ блокируем: перезагрузка уже в процессе
        }

        interstitialShowing = true;
        MarkInterstitialShown();
        // ТЗ §2.6: фактическая частота interstitial против формулы (долг GDD §8)
        Analytics.Log("interstitial_home_shown", new Dictionary<string, object>
        {
            { "session_count", PlayerPrefs.GetInt(PrefsSessionCount, 0) },
        });
        Debug.Log("[YandexAds] Показ interstitial…");
        interstitial.Show();
        return true;
#else
        if (!PassesFrequencyFormula(log: true))
        {
            return false;
        }

        MarkInterstitialShown();
        // ТЗ §2.6: Editor-ветка — та же воронка, что и на Android
        Analytics.Log("interstitial_home_shown", new Dictionary<string, object>
        {
            { "session_count", PlayerPrefs.GetInt(PrefsSessionCount, 0) },
        });
        Debug.Log("[YandexAds] EDITOR fake-показ interstitial (формула ТЗ выполнена). " +
                  "InterstitialClosed выстрелит в следующем кадре.");
        StartCoroutine(FireInterstitialClosedNextFrame());
        return true;
#endif
    }

    /// <summary>
    /// Проверка формулы ТЗ (4 условия, GDD §10.1.5 + фикс дефекта «тихое окно»).
    /// 4-е условие — ОБЩИЙ таймер: sinceAd = время с ЛЮБОЙ успешной рекламы
    /// (interstitial ИЛИ rewarded). Раньше считался только rewarded — из-за этого
    /// interstitial мог показаться через ~10 с после interstitial'а. Метки — из PlayerPrefs.
    /// </summary>
    private bool PassesFrequencyFormula(bool log)
    {
        int total = PlayerPrefs.GetInt(PrefsSessionCount, 0);
        double last = double.TryParse(PlayerPrefs.GetString(PrefsLastInterstitial, "0"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : 0.0;
        double lastRew = double.TryParse(PlayerPrefs.GetString(PrefsLastRewarded, "0"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double rw) ? rw : 0.0;
        double now = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        double sinceLast = now - last;
        double sinceRewarded = lastRew > 0.0 ? now - lastRew : double.PositiveInfinity;

        // Общий гейт: время с ПОСЛЕДНЕЙ рекламы любого типа (min меток = самая свежая).
        double sinceAd = Math.Min(
            last > 0.0 ? now - last : double.PositiveInfinity,
            sinceRewarded);

        bool pastGrace = total > graceSessions;
        bool intervalOk = sinceLast >= minIntervalSeconds;
        bool durationOk = _lastSessionDurationSeconds >= minSessionDurationSeconds;
        float quiet = GameManager.Instance != null && GameManager.Instance.Config != null
            ? GameManager.Instance.Config.adsQuietSeconds
            : 60f;
        bool quietOk = sinceAd >= quiet;

        if (log && !(pastGrace && intervalOk && durationOk && quietOk))
        {
            Debug.Log($"[YandexAds] Interstitial НЕ показываем: totalSessionCount={total} (>3: {pastGrace}), " +
                      $"sinceLast={sinceLast:F0}с (>=180: {intervalOk}), " +
                      $"lastSessionDuration={_lastSessionDurationSeconds:F1}с (>=20: {durationOk}), " +
                      $"sinceAd={(double.IsPositiveInfinity(sinceAd) ? "-" : sinceAd.ToString("F0") + "с")} (>={quiet:F0} тихое окно: {quietOk}).");
        }
        return pastGrace && intervalOk && durationOk && quietOk;
    }

    /// <summary>Записать метку показа (Unix-секунды, строкой — точность double).</summary>
    private void MarkInterstitialShown()
    {
        double now = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        PlayerPrefs.SetString(PrefsLastInterstitial, now.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

#if UNITY_ANDROID && !UNITY_EDITOR
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
#else
    private IEnumerator FireInterstitialClosedNextFrame()
    {
        yield return null;
        InterstitialClosed?.Invoke();
    }
#endif

    // ——— Загрузка баннера (Android) ———

#if UNITY_ANDROID && !UNITY_EDITOR
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

        banner = new YandexMobileAds.Banner(
            YandexMobileAds.Base.BannerAdSize.Sticky(widthDp),
            YandexMobileAds.Base.AdPosition.BottomCenter);

        banner.OnAdLoaded += HandleAdLoaded;
        banner.OnAdFailedToLoad += HandleAdFailedToLoad;

        banner.LoadAd(new YandexMobileAds.Base.AdRequest(bannerAdUnitId));
        Debug.Log($"[YandexAds] Запрос баннера (sticky, {widthDp}dp, '{bannerAdUnitId}')…");
    }

    private void HandleAdLoaded(object sender, EventArgs args)
    {
        loadInProgress = false;
        loadAttempts = 0;
        IsBannerVisible = true; // Banner.Show() при загрузке sticky показывается автоматически
        Debug.Log("[YandexAds] Баннер загружен и показан (низ экрана).");
    }

    private void HandleAdFailedToLoad(object sender, YandexMobileAds.Base.AdFailureEventArgs args)
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

    // ——— Загрузка interstitial (Android) ———

    private void RequestInterstitial()
    {
        if (interstitialLoadInProgress || destroyPending) return;
        interstitialLoadInProgress = true;

        var loader = new YandexMobileAds.InterstitialAdLoader();
        loader.LoadAd(
            new YandexMobileAds.Base.AdRequest(interstitialAdUnitId),
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

    // ——— Загрузка rewarded (Android, GDD_DeathScreen_Continue §10.1.2) ———

    private void RequestRewarded()
    {
        if (rewardedLoadInProgress || destroyPending) return;
        rewardedLoadInProgress = true;

        var loader = new YandexMobileAds.RewardedAdLoader();
        loader.LoadAd(
            new YandexMobileAds.Base.AdRequest(rewardedAdUnitId),
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
#endif

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
#if UNITY_ANDROID && !UNITY_EDITOR
        destroyPending = true;
        StopAllCoroutines();
        DestroyBannerObject();
        DestroyInterstitialObject();
        DestroyRewardedObject();
#endif
    }
}
