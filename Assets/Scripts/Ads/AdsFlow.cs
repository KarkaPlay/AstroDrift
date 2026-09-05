using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Игровой рекламный флоу AstroDrift — игровой ПОЛОВИНА бывшего YandexAdsManager.
/// SDK-деталей не знает: весь доступ к платформе — только через PlatformServices.Ads.
///
/// Interstitial (ТЗ, утверждено Game Designer'ом — числа менять нельзя):
/// единственная точка показа — тап «Домой» на Death-экране. Формула показа
/// (все 4 условия): totalSessionCount > 3  И  timeSinceLastInterstitial >= 180с
/// И  lastSessionDuration >= 20с  И  timeSinceAnyAd >= adsQuietSeconds
/// (тихое окно после ЛЮБОЙ рекламы — interstitial ИЛИ rewarded). Иначе — выход без рекламы.
/// Rewarded-путь таймер НЕ проверяет вовсе: Rewarded показывается всегда по клику.
/// Счётчики сессий и метки последних показов — в PlayerPrefs (за всё время установки);
/// ключи НЕ переименовывать — совместимость со старыми инсталлами.
/// </summary>
public class AdsFlow : MonoBehaviour
{
    public static AdsFlow Instance { get; private set; }

    [Header("Interstitial: формула показа (ТЗ, не менять)")]
    [SerializeField] private float minIntervalSeconds = 180f;   // не чаще 1 показа / 180 с
    [SerializeField] private int graceSessions = 3;             // показ только когда totalSessionCount > 3
    [SerializeField] private float minSessionDurationSeconds = 20f; // сессия (забег) должна длиться >= 20 с

    // ——— PlayerPrefs-ключи (персистентность за всё время установки; НЕ переименовывать) ———
    private const string PrefsSessionCount = "totalSessionCount";
    private const string PrefsLastInterstitial = "lastInterstitialTimestamp";
    private const string PrefsLastRewarded = "lastRewardedTimestamp";

    /// <summary>
    /// Rewarded загружен и готов к показу (GDD_DeathScreen_Continue §7).
    /// В редакторе (заглушка) — всегда true (fake-режим).
    /// </summary>
    public bool IsRewardedReady => PlatformServices.Ads.IsRewardedReady;

    /// <summary>
    /// Срабатывает после закрытия interstitial (или при ошибке показа — чтобы игра не подвисла).
    /// GameUI подписывается и делает переход по этому сигналу. Проброс события сервиса.
    /// </summary>
    public event Action InterstitialClosed;

    // ——— Состояние сессии (в памяти; счётчики — в PlayerPrefs) ———
    private double _sessionStartRealtime;
    private float _lastSessionDurationSeconds;
    private bool _sessionActive;

    // Ссылка на сервис, захваченная в OnEnable: OnDisable не должен обращаться к
    // PlatformServices.Ads повторно (при выходе из приложения ленивое создание
    // заглушки во время teardown дало бы предупреждения Unity).
    private IAdsService _ads;

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

    private void OnEnable()
    {
        _ads = PlatformServices.Ads;
        _ads.InterstitialClosed += OnServiceInterstitialClosed;
    }

    private void OnDisable()
    {
        if (_ads != null) _ads.InterstitialClosed -= OnServiceInterstitialClosed;
    }

    private void OnServiceInterstitialClosed()
    {
        AudioManager.Instance?.SetMuted(false);
        InterstitialClosed?.Invoke();
    }

    // ——— Публичное API: rewarded (GDD_DeathScreen_Continue §7/§10.1) ———

    /// <summary>
    /// Показ rewarded (единственная точка — тап «ПРОДОЛЖИТЬ» на Death-экране).
    /// НЕ блокирует: если реклама не загружена — onResult(false) синхронно.
    /// Иначе показ; onResult(true) — только при выдаче награды, onResult(false) —
    /// при закрытии без награды / ошибке показа. Колбэк — ровно один раз.
    /// В редакторе — fake-показ: 2.0 с ожидания → onResult(true).
    /// Mute звука — игровая логика, поэтому здесь (не в сервисе).
    /// </summary>
    public void ShowRewarded(Action<bool> onResult)
    {
        if (onResult == null) return;

        // mute на время показа, unmute — по колбэку (§10.1.6)
        AudioManager.Instance?.SetMuted(true);

        PlatformServices.Ads.ShowRewarded(result =>
        {
            AudioManager.Instance?.SetMuted(false);
            // Завершённый просмотр = УСПЕШНЫЙ показ rewarded → общий тихий таймер тикает
            // (фикс дефекта: без этого on Android метка не писалась и гейт её не видел).
            if (result) MarkRewardedShown();
            onResult(result);
        });
    }

    /// <summary>Записать метку показа rewarded (Unix-сек, строкой — точность double, §10.1.5).</summary>
    private void MarkRewardedShown()
    {
        double now = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        PlayerPrefs.SetString(PrefsLastRewarded, now.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    // ——— Публичное API: сессии (забеги) ———

    /// <summary>
    /// Вызвать из GameManager при старте сессии (забега): запоминает время старта.
    /// </summary>
    public void RegisterSessionStart()
    {
        _sessionStartRealtime = Time.realtimeSinceStartupAsDouble;
        _sessionActive = true;
        Debug.Log("[AdsFlow] Session start.");
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

        Debug.Log($"[AdsFlow] Session end: длительность={_lastSessionDurationSeconds:F1}с, " +
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
        Debug.Log($"[AdsFlow] Session resumed (continue): totalSessionCount={total}, сессия снова активна.");
    }

    // ——— Публичное API: interstitial (частотная логика по ТЗ) ———

    /// <summary>
    /// Вызвать из GameUI при тапе «Домой» на Death-экране. НЕ блокирует:
    /// если реклама не загружена или формула не выполнена — сразу false (переход мгновенный).
    /// При показе true; переход — по подписке на InterstitialClosed.
    /// В редакторе — fake-режим: та же формула, InterstitialClosed в следующем кадре (заглушка).
    /// </summary>
    public bool TryShowInterstitial()
    {
        if (!PassesFrequencyFormula(log: true))
        {
            return false;
        }
        if (!PlatformServices.Ads.ShowInterstitial())
        {
            return false; // НЕ блокируем: не загружен / уже показывается
        }

        // Тишина во время ЛЮБОЙ рекламы (модерация ЯИ проверяет на interstitial тоже).
        // Unmute — в OnServiceInterstitialClosed (событие гарантировано: close/ошибка/watchdog).
        AudioManager.Instance?.SetMuted(true);

        MarkInterstitialShown();
        // ТЗ §2.6: фактическая частота interstitial против формулы (долг GDD §8)
        Analytics.Log("interstitial_home_shown", new Dictionary<string, object>
        {
            { "session_count", PlayerPrefs.GetInt(PrefsSessionCount, 0) },
        });
        return true;
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
            Debug.Log($"[AdsFlow] Interstitial НЕ показываем: totalSessionCount={total} (>3: {pastGrace}), " +
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

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
