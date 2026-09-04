using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameState { Ready, Playing, Dead }

/// <summary>
/// Стейт-машина забега (GDD §7) + хореография «Menu & Transitions v2» (ArtDirection §5–§6):
/// • Start (§5): корабль уже в стартовой позиции; тап → UI уходит (заголовок вверх, CTA fade),
///   камера 2.0 s EaseOutSoft отъезжает до игрового зума, корабль сразу разгоняется
///   0 → нормальная скорость за startAccelerateTime (управление с t = 0).
///   Стрельба/спавн угроз/HUD — на t = startSystemsTime (= разгону = полёту камеры:
///   один параметр, чтобы хореография не разъезжалась). Без чёрных фейдов, без мигания.
/// • Retry (§6.1): камера 0.6 s EaseOutSoft летит «место смерти (zoom-in) → стартовый кадр»,
///   сброс мира — во время полёта; управление t = 0.5 s, спавн с t ≥ 0.6 s.
///   Точка входа RetryWithInterstitial сохранена: переход строго после InterstitialClosed.
/// • Home (§4.5): панели fade-out, камера 0.8 s в меню-кадр, стартовый UI каскадом.
/// Все цифры — GameConfig («Menu & Transitions v2»).
/// </summary>
public class GameManager : MonoBehaviour
{
    private GameConfig _config;
    private DifficultyConfig _difficulty;
    private ShipController _ship;
    private CameraFollow _cameraFollow;
    private ShipWeapon _weapon;
    private AsteroidSpawner _asteroidSpawner;
    private MissileSpawner _missileSpawner;
    private ScoreManager _score;
    private DifficultyManager _difficultyManager;
    private CameraDirector _cameraDirector;

    private CameraShake _shake;
    private ParticlePool _particles;
    private FloatingTextPool _floatingText;
    private GameUI _ui;
    private float _elapsed;
    private Coroutine _choreo;
    private Vector3 _lastDeathPos;   // место смерти (continue: чистка зоны + возврат корабля, GDD §10.3.2)
    private float _continueResumedAt = -1f; // _elapsed в момент continue (метрики §8)
    private bool _continueSurvivedLogged;

    public static GameManager Instance { get; private set; }
    public GameState State { get; private set; } = GameState.Ready;
    public GameConfig Config => _config;
    public float RunTime => _elapsed;

    /// <summary>
    /// Разблокировка управления (ArtDirection §5/§6): false, пока камера в стартовом
    /// полёте (t < 1.6 s при первом старте / t < 0.5 s при рестарте).
    /// Читается ShipController/ShipWeapon — корабль стоит, стрельбы нет.
    /// </summary>
    public bool InputEnabled { get; private set; }

    /// <summary>
    /// Гейт стрельбы: управление с t=0 (первый старт), но стрельба/системы —
    /// на t = startSystemsTime. На рестарте/continue включается сразу.
    /// </summary>
    public bool WeaponEnabled { get; private set; }

    private void Awake()
    {
        Instance = this;
        Application.targetFrameRate = 60;
        State = GameState.Ready;
    }

    /// <summary>Сборка подсистем из Bootstrap. Вызывается один раз после создания сцены.</summary>
    public void Init(GameConfig config, DifficultyConfig difficulty, ShipController ship,
                     CameraFollow cameraFollow, ShipWeapon weapon, AsteroidSpawner asteroidSpawner,
                     MissileSpawner missileSpawner, ScoreManager score, DifficultyManager diffManager)
    {
        _config = config;
        _difficulty = difficulty;
        _ship = ship;
        _cameraFollow = cameraFollow;
        _weapon = weapon;
        _asteroidSpawner = asteroidSpawner;
        _missileSpawner = missileSpawner;
        _score = score;
        _difficultyManager = diffManager;

        // Подсистемы VFX
        var vfx = FindFirstObjectByType<ParticlePool>();
        _particles = vfx != null ? vfx : new GameObject("ParticlePool").AddComponent<ParticlePool>();
        var ft = FindFirstObjectByType<FloatingTextPool>();
        _floatingText = ft != null ? ft : new GameObject("FloatingTextPool").AddComponent<FloatingTextPool>();
        var uiComp = FindFirstObjectByType<GameUI>();
        _ui = uiComp;

        if (cameraFollow != null)
        {
            _shake = cameraFollow.gameObject.GetComponent<CameraShake>();
            if (_shake == null)
                _shake = cameraFollow.gameObject.AddComponent<CameraShake>();
        }

        // CameraDirector — хореография камеры (может отсутствовать в старых сценах: fallback на старое поведение)
        var cd = FindFirstObjectByType<CameraDirector>();
        if (cd == null) cd = cameraFollow.gameObject.AddComponent<CameraDirector>();
        _cameraDirector = cd;
        _cameraDirector.Init(config, cameraFollow.GetComponent<Camera>(), cameraFollow);

        // События juice
        GameEvents.AsteroidDestroyed += OnAsteroidDestroyed;
        GameEvents.Combo += OnCombo;
        GameEvents.MissileDestroyed += OnMissileDestroyed;
        GameEvents.MissileTimeout += OnMissileTimeout;
        GameEvents.AsteroidHit += OnAsteroidHit;

        EnterMenu(immediate: true);
    }

    // ——— Меню (§2/§4.5) ———

    /// <summary>Вход в меню: камера в меню-кадр, стартовый UI. immediate = инициализация сцены.</summary>
    private void EnterMenu(bool immediate)
    {
        StopChoreo();
        Time.timeScale = 1f;
        State = GameState.Ready;
        InputEnabled = false;
        WeaponEnabled = false;

        // Корабль: в меню он главный герой кадра (§2) — стоит в игровой стартовой позиции.
        // BeginRun восстанавливает рендер/коллайдер/трейл после Kill() (смерть → Home)
        // и ставит нос вверх: без анимации, корабль уже «на взлётной полосе».
        _ship.BeginRun(Vector3.zero, _difficultyManager != null ? _difficultyManager.ShipSpeed : 5f);
        _cameraFollow.SetTarget(_ship.transform);

        if (immediate)
        {
            _cameraDirector.SnapMenu();
            _ui.ShowStartImmediate();
        }
        else
        {
            // §4.5: панели fade-out уже проиграл GameUI.PlayPanelOut; камера 0.8 s в меню-кадр,
            // стартовый UI каскадом (150/220/300 мс). Корабль уже в позиции — без телепортов на глазах.
            _choreo = StartCoroutine(HomeChoreo());
        }
        _ui.RefreshHud();
    }

    private IEnumerator HomeChoreo()
    {
        // Сброс мира тихо (игрок смотрит на уезжающую панель)
        ResetWorld();
        ApplySafeZone(0f); // в меню угроз нет, зона не нужна
        Coroutine fly = StartCoroutine(_cameraDirector.FlyToMenuRoutine(_config.homeFlyDuration));
        yield return new WaitForSecondsRealtime(0.25f); // панели уходят 0.25 s
        _ui.ShowStartCascade();
        yield return fly;
    }

    public void BeginRun()
    {
        // ux4-3: рантайм-гейт — UI-зона CTA (даже «просочившийся» тап в gameplay)
        // не должна перезапускать забег. Start-экран = единственный источник BeginRun.
        if (State != GameState.Ready) return;

        // Новый забег: флаг continue сбрасывается (1 continue за забег, GDD §3)
        _ui.ResetContinueFlag();

        // ТЗ §2.2/§3: воронка сессии + профиль (счётчик забегов с начала установки)
        int sessionRuns = PlayerPrefs.GetInt("totalSessionCount", 0);
        Analytics.Log("run_started", new Dictionary<string, object> { { "session_runs", sessionRuns } });
        Analytics.ProfileSetNumber("runs_total", PlayerPrefs.GetInt("analytics_runs_total", 0) + 1);
        PlayerPrefs.SetInt("analytics_runs_total", PlayerPrefs.GetInt("analytics_runs_total", 0) + 1);
        PlayerPrefs.Save();

        // Реклама: старт сессии — точка отсчёта длительности забега
        if (AdsFlow.Instance != null) AdsFlow.Instance.RegisterSessionStart();

        StopChoreo();
        _elapsed = 0f;
        _score.ResetRun();
        _difficultyManager.ResetRun();
        Time.timeScale = 1f;
        State = GameState.Playing;
        InputEnabled = true;  // решение владельца: управление с t = 0 — корабль уже в разгоне
        WeaponEnabled = false; // стрельба — только на t = startSystemsTime (§5)

        // Первый старт: корабль УЖЕ стоит в стартовой позиции из меню (§2) — не двигаем его.
        // BeginRun по текущей позиции; разгон 0 → норм за startAccelerateTime (отклик на тап
        // мгновенный, без мигания — стартовая неуязвимость убрана).
        _ship.BeginRun(_ship.transform.position, _difficultyManager.ShipSpeed,
            _config.startAccelerateTime);
        // Сброс таймеров/пулов — мгновенно, до полёта камеры (видимого «мигания» нет:
        // в кадре только корабль и звёзды).
        ResetWorld();
        // §5: стрельба/спавн угроз/HUD — единое t активации систем (= длительности разгона
        // и полёта камеры), чтобы сейф-зона не разъезжалась с хореографией.
        ApplySafeZone(_config.startSystemsTime);
        _cameraFollow.SetTarget(_ship.transform);
        _cameraDirector.SnapMenu(); // страховка: кадр точно меню-зум до полёта

        // §4.1 UI: заголовок улетает вверх, CTA fade-out (кривые UiAnim)
        _ui.PlayStartToGame();
        _ui.RefreshHud();

        // §5: камера летит те же startSystemsTime сек; управление уже разблокировано (t=0)
        _choreo = StartCoroutine(StartChoreo());
    }

    private IEnumerator StartChoreo()
    {
        // Камера: полёт меню → игра; разгон и активация систем — той же длительности,
        // кадр непрерывно движется с t = 0 (никаких «застываний»).
        Coroutine fly = StartCoroutine(_cameraDirector.FlyToGameRoutine(_config.startFlyDuration));
        // §5: HUD fade-in 0.35 s на t = startSystemsTime (конец сейф-зоны = стрельба/спавн)
        _ui.HudIn(_config.startSystemsTime, _config.hudFadeDuration);
        yield return new WaitForSecondsRealtime(_config.startSystemsTime);
        WeaponEnabled = true; // стрельба + конец сейф-зоны: системы активируются синхронно
        yield return fly;
    }

    /// <summary>Попадание по кораблю = смерть (1 HP).</summary>
    public void OnShipHit(ShipController ship, Collider2D other)
    {
        if (State != GameState.Playing) return;
        State = GameState.Dead;
        InputEnabled = false;

        // ТЗ §2.3: забег как единица (death_screen_shown — про экран, они в паре 1:1).
        // cause по коллайдеру: у Asteroid/Missile различимые компоненты; иначе "other" (не ронять лог).
        string cause = other != null && other.GetComponent<Missile>() != null ? "missile"
            : other != null && other.GetComponent<Asteroid>() != null ? "asteroid" : "other";
        Analytics.Log("run_ended", new Dictionary<string, object>
        {
            { "score", _score != null ? _score.Score : 0 },
            { "run_time", _elapsed },
            { "cause", cause },
            { "new_best", _score != null && _score.NewBest },
            { "used_continue", _ui != null && _ui.ContinueUsedThisRun },
        });

        // Метрика §8: смерть в первые 3 с после continue — сигнал, что grace слаб
        if (_continueResumedAt >= 0f && _elapsed - _continueResumedAt <= 3f)
            Analytics.Log("continue_died_early", new Dictionary<string, object>
            {
                { "score", _score != null ? _score.Score : 0 },
                { "since_continue", _elapsed - _continueResumedAt },
            });

        if (AdsFlow.Instance != null) AdsFlow.Instance.RegisterSessionEnd();

        Vector3 deathPos = ship.transform.position;
        _lastDeathPos = deathPos;
        ship.Kill();
        _particles.Burst(deathPos, Palette.Ship, 8, 2f, 4f, 0.6f, 0.15f, 0.3f);
        _particles.Burst(deathPos, Palette.Bullet, 4, 2f, 4f, 0.6f, 0.1f, 0.2f);
        _shake?.Shake(_config.shakeDeath.amplitude, _config.shakeDeath.duration);

        // §4.2: камера zoom-in +15 % к месту смерти (0.4 s, задержка 0.25 s после вспышки),
        // HUD score fade-out 0.20 s сразу.
        _cameraDirector.DeathZoomIn(deathPos);
        _ui.HudOut();

        Time.timeScale = _config.deathSlowmoScale;
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        yield return new WaitForSecondsRealtime(_config.deathSlowmoDuration);
        // Continue v2 (GDD_DeathScreen_Continue §10.3.1): мир НЕ размораживается —
        // остаётся заморожен (timeScale = 0), пока State == Dead: «картина смерти»
        // живёт для continue, игрок сидит на экране сколько угодно.
        Time.timeScale = 0f;
        // Страховка от спавнеров на unscaled-времени: спавн угроз на Death-экране погашен
        _asteroidSpawner.SetSafeZone(float.MaxValue);
        _missileSpawner.SetSafeZone(float.MaxValue);
        yield return new WaitForSecondsRealtime(0.15f);
        // §4.2: панель появляется каскадом (Score 0 мс → Best 70 мс → предложение/Домой 140/210 мс)
        _ui.PlayDeathIn(_score.Score, _score.Best, _score.NewBest);
        AudioManager.Instance?.PlayDeath();
    }

    /// <summary>
    /// Continue (GDD_DeathScreen_Continue §5.2/§10.3.3): продолжение забега БЕЗ рестарта.
    /// Вызывается из GameUI после успешного reward. Счёт/множитель/волна сохраняются;
    /// чистка зоны вокруг места смерти + неуязвимость + safe zone; камера zoom-out на месте.
    /// </summary>
    public void ContinueRun()
    {
        var ads = AdsFlow.Instance;
        if (ads != null) ads.ResumeSession(); // откат инкремента сессии смерти (§10.1.4)

        StopChoreo();
        State = GameState.Playing;
        Time.timeScale = 1f;
        InputEnabled = false; // §5.2: разблокировка на t = 0.5 s

        // НЕ вызываем _score.ResetRun() / _difficultyManager.ResetRun() / _elapsed = 0:
        // счёт, множитель и волна сложности продолжаются с момента смерти (§3 GDD)
        _score.RestartComboWindow();

        // Чистка (§5.2): астероиды в радиусе 8 ю — тихо, через пул, без GameEvents;
        // ракеты + предупреждения — все; пули — все
        _asteroidSpawner.ClearNear(_lastDeathPos, _config.continueClearRadius);
        _missileSpawner.ResetSpawner();
        _weapon.ResetWeapon();

        // Корабль: на месте смерти, носом вверх; неуязвимость 2.0 с с миганием (§6)
        _ship.transform.position = _lastDeathPos;
        _ship.transform.rotation = Quaternion.identity;
        _ship.gameObject.SetActive(true);
        _ship.BeginRun(_lastDeathPos, _difficultyManager.ShipSpeed);
        _ship.SetInvulnerable(_config.continueInvulnerableTime);
        _cameraFollow.SetTarget(_ship.transform);

        // Спавн угроз не раньше 1.5 s (§6)
        ApplySafeZone(_config.continueSafeZone);

        // Камера: zoom-out «кадр смерти → игровой» НА МЕСТЕ (§5.2), позиция не летит
        _continueResumedAt = _elapsed;      // метрики survived_10s / died_early (§8)
        _continueSurvivedLogged = false;
        _choreo = StartCoroutine(ContinueChoreo());
    }

    private IEnumerator ContinueChoreo()
    {
        // Камера 1.2 s; UI: панель fade-out 0.25 s (уже запущен PlayContinueOut), HUD на t = 0.4 s
        Coroutine zoom = StartCoroutine(_cameraDirector.ContinueZoomOutRoutine(_config.continueZoomOutDuration));
        _ui.PlayContinueOut();
        _ui.HudIn(0.4f, 0.3f);
        _ui.RefreshHud();

        Analytics.Log("continue_used", new Dictionary<string, object>
        {
            { "score", _score.Score },
            { "multiplier", _score.Multiplier },
            { "run_time", RunTime },
        });

        WeaponEnabled = true; // continue: стрельба сразу (гейт только для первого старта)
        yield return new WaitForSecondsRealtime(_config.continueUnlockTime);
        InputEnabled = true;
        yield return zoom;
    }

    /// <summary>
    /// §6.1 Retry: точка входа — строго после InterstitialClosed (GameUI.RetryWithInterstitial).
    /// Состояние сцены до рекламы не менялось: камера всё ещё в кадре «место смерти, zoom-in».
    /// </summary>
    public void Retry() => RestartRun();

    private void RestartRun()
    {
        StopChoreo();
        _elapsed = 0f;
        _score.ResetRun();
        _difficultyManager.ResetRun();
        Time.timeScale = 1f;
        State = GameState.Playing;
        InputEnabled = false;  // §6.1: разблокировка на t = 0.5 s
        WeaponEnabled = false; // §6.1: стрельба вместе с управлением, как было

        // §6.1: корабль телепортируется в стартовую позицию ВО ВРЕМЯ полёта камеры —
        // двигающийся кадр скрывает смену содержимого.
        _ship.transform.position = Vector3.zero;
        _ship.transform.rotation = Quaternion.identity;
        _ship.gameObject.SetActive(true);
        _ship.BeginRun(Vector3.zero, _difficultyManager.ShipSpeed);
        _cameraFollow.SetTarget(_ship.transform);

        // Сброс мира — во время полёта камеры (пулы астероидов/пуль/ракет, звёзды):
        // игрок не видит «мигание», потому что взгляд ведёт камера.
        ResetWorld();
        ApplySafeZone(_config.restartSafeZone); // §6.1: спавн угроз не раньше t = 0.6 s

        // §4.3 UI: Death panel fade-out 0.25 s EaseInQuick, HUD fade-in 0.3 s на t = 0.45 s
        _ui.PlayDeathOut();
        _ui.HudIn(0.45f, 0.3f);
        _ui.RefreshHud();

        float unlock = _config.restartUnlockTime;
        float safe = _config.restartSafeZone;
        _choreo = StartCoroutine(RestartChoreo(unlock, safe));
    }

    private IEnumerator RestartChoreo(float unlock, float safe)
    {
        // §6.1: камера 0.6 s EaseOutSoft «место смерти (zoom-in) → стартовый кадр (меню-зум)»,
        // затем дожим зума «меню → игра» до конца безопасной зоны.
        Coroutine fly = StartCoroutine(_cameraDirector.FlyRestartRoutine(
            _config.restartFlyDuration, _config.restartZoomOutDuration));
        yield return new WaitForSecondsRealtime(unlock);
        InputEnabled = true;
        WeaponEnabled = true; // рестарт: стрельба как раньше — вместе с управлением
        yield return fly;
    }

    public void GoHome()
    {
        // ТЗ §2.5: exit_to_home — только если continue_declined в этой смерти НЕ отправлялся
        // (иначе двойной счёт одного тапа «Домой» с живым предложением continue)
        bool declinedLogged = State == GameState.Dead && _ui != null && _ui.ContinueDeclinedLogged;
        if (!declinedLogged)
        {
            string from = State == GameState.Dead ? "death"
                : _ui != null && _ui.IsPauseScreen ? "pause" : "start";
            Analytics.Log("exit_to_home", new Dictionary<string, object>
            {
                { "from", from },
                { "was_alive", State != GameState.Dead },
            });
        }

        if (State == GameState.Dead)
            _ui.PlayPanelOut(); // §4.5: панели fade-out 0.25 s EaseInQuick
        else
            _ui.PlayPanelOut(); // из паузы — тот же путь
        EnterMenu(immediate: false);
    }

    /// <summary>Сброс мира: пулы астероидов/ракет/пуль (§6.1 — во время полёта камеры).</summary>
    private void ResetWorld()
    {
        _asteroidSpawner.ResetSpawner();
        _missileSpawner.ResetSpawner();
        _weapon.ResetWeapon();
    }

    /// <summary>Гейт спавна угроз: выставляется в BeginRun (§5) / RestartRun (§6.1).</summary>
    private void ApplySafeZone(float seconds)
    {
        _asteroidSpawner.SetSafeZone(seconds);
        _missileSpawner.SetSafeZone(seconds);
    }

    private void StopChoreo()
    {
        if (_choreo != null) { StopCoroutine(_choreo); _choreo = null; }
    }

    private void Update()
    {
        if (State == GameState.Playing)
        {
            _elapsed += Time.deltaTime;
            _difficultyManager.Tick(Time.deltaTime);
            _ship.SetSpeed(_difficultyManager.ShipSpeed);

            // Метрики §8 (GDD_DeathScreen_Continue): survived_10s — один раз за продолжение
            if (_continueResumedAt >= 0f && !_continueSurvivedLogged &&
                _elapsed - _continueResumedAt >= 10f)
            {
                _continueSurvivedLogged = true;
                Analytics.Log("continue_survived_10s", new Dictionary<string, object> { { "survived", 1 } });
            }
        }
    }

    // ——— События juice ———

    private void OnAsteroidHit(Vector3 pos)
        => _shake?.Shake(_config.shakeHit.amplitude, _config.shakeHit.duration);

    private void OnAsteroidDestroyed(Vector3 pos, AsteroidSize size)
    {
        _shake?.Shake(_config.shakeAsteroidBreak.amplitude, _config.shakeAsteroidBreak.duration);
        int points = size switch
        {
            AsteroidSize.Large => _config.largeAsteroidScore,
            AsteroidSize.Medium => _config.mediumAsteroidScore,
            _ => _config.smallAsteroidScore,
        };
        _particles.Burst(pos, Palette.AsteroidShade(Random.value), Random.Range(6, 9), 2f, 3f, 0.4f, 0.1f, 0.2f);
        _floatingText.Spawn(pos + Vector3.up * 0.4f, "+" + points, Palette.ScoreText, 3.2f, 0.8f);
        AudioManager.Instance?.PlaySmallExplosion();
    }

    private void OnMissileDestroyed(Vector3 pos)
    {
        _shake?.Shake(_config.shakeMissileDestroy.amplitude, _config.shakeMissileDestroy.duration);
        _particles.Burst(pos, Palette.Missile, 10, 2f, 3.5f, 0.5f, 0.1f, 0.2f);
        AudioManager.Instance?.PlayBigExplosion();
    }

    private void OnMissileTimeout(Vector3 pos)
        => _particles.Burst(pos, Palette.Missile, 4, 1f, 2f, 0.4f, 0.08f, 0.15f);

    private void OnCombo(Vector3 pos)
    {
        _shake?.Shake(_config.shakeCombo.amplitude, _config.shakeCombo.duration);
        _particles.Burst(pos, Palette.Missile, 8, 3f, 5f, 0.6f, 0.12f, 0.25f);
        _particles.Burst(pos, Palette.Bullet, 6, 3f, 5f, 0.6f, 0.1f, 0.2f);
        // Строку форматируем до Spawn (FloatingTextPool берёт готовую строку);
        // 200 — фиксированный бонус комбо (формат ключа combo: "COMBO! +{0}")
        string comboText = L10n.GetFormatted("combo", "200");
        _floatingText.Spawn(pos + Vector3.up * 0.5f,
            string.IsNullOrEmpty(comboText) ? "COMBO! +200" : comboText,
            Palette.Gold, 4.2f, 0.9f);
        AudioManager.Instance?.PlayBigExplosion();
    }
}
