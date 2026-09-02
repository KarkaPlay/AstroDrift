using System.Collections;
using UnityEngine;

public enum GameState { Ready, Playing, Dead }

/// <summary>
/// Стейт-машина забега (GDD §7 / DevTask шаг 5): Ready → Playing → Dead.
/// Собирает все подсистемы через Init() (вызывается Bootstrap'ом), управляет
/// slow-motion смертью, retry и возвратом на старт. Все juice-эффекты на события.
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

    private CameraShake _shake;
    private ParticlePool _particles;
    private FloatingTextPool _floatingText;
    private GameUI _ui;
    private float _elapsed;

    public static GameManager Instance { get; private set; }
    public GameState State { get; private set; } = GameState.Ready;
    public GameConfig Config => _config;
    public float RunTime => _elapsed;

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
        // GameUI — сценовый объект (Bootstrap уже вызвал ui.Init(score))
        var uiComp = FindFirstObjectByType<GameUI>();
        _ui = uiComp; // может быть null только при отсутствии Canvas в сцене — Bootstrap уже залогировал

        if (cameraFollow != null)
        {
            _shake = cameraFollow.gameObject.GetComponent<CameraShake>();
            if (_shake == null)
                _shake = cameraFollow.gameObject.AddComponent<CameraShake>(); // fallback для старых сцен
        }

        // События juice
        GameEvents.AsteroidDestroyed += OnAsteroidDestroyed;
        GameEvents.Combo += OnCombo;
        GameEvents.MissileDestroyed += OnMissileDestroyed;
        GameEvents.MissileTimeout += OnMissileTimeout;
        GameEvents.AsteroidHit += OnAsteroidHit;

        _ui.SetScreen(GameUI.Screen.Start);
        _ui.RefreshHud();
    }

    public void BeginRun()
    {
        // Реклама (ТЗ interstitial): старт сессии — точка отсчёта длительности забега
        if (YandexAdsManager.Instance != null) YandexAdsManager.Instance.RegisterSessionStart();

        _elapsed = 0f;
        _score.ResetRun();
        _difficultyManager.ResetRun();
        Time.timeScale = 1f;

        // 1) Очистка сущностей ДО телепорта корабля: иначе выживший снаряд
        //    может убить корабль сразу на новой позиции.
        _asteroidSpawner.ResetSpawner();   // 3 пула астероидов + _activeCount = 0
        _missileSpawner.ResetSpawner();    // пул ракет + MissileWarning.HideAll + _activeCount = 0
        _weapon.ResetWeapon();             // гасит активные пули + кулдаун

        // 2) Корабль: телепорт в начало координат (нос вверх). Мир не должен
        //    уезжать бесконечно — каждый забег стартует из (0,0,0).
        _ship.BeginRun(Vector3.zero, _difficultyManager.ShipSpeed);
        _ship.gameObject.SetActive(true);

        // 3) Камера: снап на корабль в origin (сброс lookahead-кэша Cinemachine
        //    происходит внутри SnapToShip через OnTargetObjectWarped).
        _cameraFollow.SetTarget(_ship.transform);
        _cameraFollow.SnapToShip();

        State = GameState.Playing;
        _ui.SetScreen(GameUI.Screen.Hud);
        _ui.RefreshHud();
    }

    /// <summary>Попадание по кораблю = смерть (1 HP).</summary>
    public void OnShipHit(ShipController ship, Collider2D other)
    {
        if (State != GameState.Playing) return;
        State = GameState.Dead;

        // Реклама (ТЗ interstitial): смерть = конец сессии (длительность + инкремент счётчика)
        if (YandexAdsManager.Instance != null) YandexAdsManager.Instance.RegisterSessionEnd();

        ship.Kill();
        _particles.Burst(ship.transform.position, Palette.Ship, 8, 2f, 4f, 0.6f, 0.15f, 0.3f);
        _particles.Burst(ship.transform.position, Palette.Bullet, 4, 2f, 4f, 0.6f, 0.1f, 0.2f);
        _shake?.Shake(_config.shakeDeath.amplitude, _config.shakeDeath.duration);

        // Slow-motion 0.3 с (GDD §7) + красная виньетка
        Time.timeScale = _config.deathSlowmoScale;
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        yield return new WaitForSecondsRealtime(_config.deathSlowmoDuration);
        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(0.15f);
        _ui.ShowDeathScreen(_score.Score, _score.Best, _score.NewBest);
        AudioManager.Instance?.PlayDeath();
    }

    public void Retry() => BeginRun();

    public void GoHome()
    {
        Time.timeScale = 1f;
        State = GameState.Ready;
        _ship.gameObject.SetActive(false);
        _ui.SetScreen(GameUI.Screen.Start);
        _ui.RefreshHud();
    }

    private void Update()
    {
        if (State == GameState.Playing)
        {
            _elapsed += Time.deltaTime;
            _difficultyManager.Tick(Time.deltaTime);
            _ship.SetSpeed(_difficultyManager.ShipSpeed);
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
        _floatingText.Spawn(pos + Vector3.up * 0.5f, "COMBO! +200", Palette.Gold, 4.2f, 0.9f);
        AudioManager.Instance?.PlayBigExplosion();
    }
}
