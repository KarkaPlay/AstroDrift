using System;
using System.Collections;
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
/// (все 3 условия): totalSessionCount > 3  И  timeSinceLastInterstitial >= 180с
/// И  lastSessionDuration >= 20с. Иначе — рестарт без рекламы.
/// Счётчики сессий и метка последнего показа — в PlayerPrefs (за всё время установки).
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

    /// <summary>Баннер показан (после успешной загрузки и Show).</summary>
    public bool IsBannerVisible { get; private set; }

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
    private bool interstitialLoadInProgress;
    private bool interstitialShowing;
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
#else
        Debug.Log("[YandexAds] Editor-заглушка: Mobile Ads SDK доступен только на Android-устройстве. " +
                  $"Баннер (adUnitId='{bannerAdUnitId}') и interstitial (adUnitId='{interstitialAdUnitId}') " +
                  "загрузятся при сборке на устройство. TryShowInterstitial() в редакторе работает в fake-режиме " +
                  "по формуле ТЗ (см. логи).");
#endif
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
        Debug.Log("[YandexAds] Показ interstitial…");
        interstitial.Show();
        return true;
#else
        if (!PassesFrequencyFormula(log: true))
        {
            return false;
        }

        MarkInterstitialShown();
        Debug.Log("[YandexAds] EDITOR fake-показ interstitial (формула ТЗ выполнена). " +
                  "InterstitialClosed выстрелит в следующем кадре.");
        StartCoroutine(FireInterstitialClosedNextFrame());
        return true;
#endif
    }

    /// <summary>Проверка формулы ТЗ (все 3 условия). totalSessionCount/метка — из PlayerPrefs.</summary>
    private bool PassesFrequencyFormula(bool log)
    {
        int total = PlayerPrefs.GetInt(PrefsSessionCount, 0);
        double last = double.TryParse(PlayerPrefs.GetString(PrefsLastInterstitial, "0"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : 0.0;
        double now = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        double sinceLast = now - last;

        bool pastGrace = total > graceSessions;
        bool intervalOk = sinceLast >= minIntervalSeconds;
        bool durationOk = _lastSessionDurationSeconds >= minSessionDurationSeconds;

        if (log && !(pastGrace && intervalOk && durationOk))
        {
            Debug.Log($"[YandexAds] Interstitial НЕ показываем: totalSessionCount={total} (>3: {pastGrace}), " +
                      $"sinceLast={sinceLast:F0}с (>=180: {intervalOk}), " +
                      $"lastSessionDuration={_lastSessionDurationSeconds:F1}с (>=20: {durationOk}).");
        }
        return pastGrace && intervalOk && durationOk;
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
#endif

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
#if UNITY_ANDROID && !UNITY_EDITOR
        destroyPending = true;
        StopAllCoroutines();
        DestroyBannerObject();
        DestroyInterstitialObject();
#endif
    }
}
