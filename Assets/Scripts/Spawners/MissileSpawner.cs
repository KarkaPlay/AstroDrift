using UnityEngine;

/// <summary>
/// Спавнер ракет (GDD §4.4): интервал по фазам, лимит, спавн в задней полусфере,
/// предупреждение за 1.5 с до входа в кадр (красный мигающий треугольник на орбите).
/// UX5.8: пул индикаторов MissileWarning — по одному на каждую внеэкранную ракету,
/// прогрев по max-лимиту ракет (без аллокаций в рантайме).
/// </summary>
public class MissileSpawner : MonoBehaviour
{
    [SerializeField] private GameConfig config;
    [SerializeField] private DifficultyConfig difficulty;
    [SerializeField] private Camera cam;
    [SerializeField] private GameObject missilePrefab;     // Assets/Resources/Prefabs/Missile.prefab

    private ObjectPool _pool;
    private ObjectPool _warningPool; // UX5.8: пул индикаторов (MissileWarning : Poolable)
    private float _timer;
    private float _elapsed;
    private int _activeCount;
    private float _safeZone; // ArtDirection §5/§6: спавн угроз не раньше конца безопасной зоны

    /// <summary>Безопасная зона забега: 1.6 s (первый старт) / 0.6 s (рестарт). ArtDirection §5–§6.</summary>
    public void SetSafeZone(float seconds) => _safeZone = Mathf.Max(0f, seconds);

    public void Init(GameConfig cfg, DifficultyConfig diff, Camera camera, Transform poolParent)
    {
        config = cfg;
        difficulty = diff;
        cam = camera;

        // Префаб — источник структуры (меш/трейл/коллайдер/RB); числа — из конфигов.
        if (missilePrefab == null)
            missilePrefab = Resources.Load<GameObject>("Prefabs/Missile");

        _pool = new ObjectPool(Create, poolParent, 4);
        // UX5.8: пул предупреждений, прогрев = максимум maxMissiles по всем фазам —
        // лимит ракет покрыт, Get() в рантайме не аллоцирует.
        _warningPool = new ObjectPool(CreateWarning, poolParent, difficulty.MaxMissilesOverall());
        GameEvents.MissileDestroyed += OnMissileGone;
        GameEvents.MissileTimeout += OnMissileGone;
        GameEvents.Combo += OnMissileGone;
    }

    private Poolable Create()
    {
        var go = Instantiate(missilePrefab, transform);
        go.name = "Missile";
        var missile = go.GetComponent<Missile>();
        missile.Init(config); // числа (hp/life/radius) — из GameConfig
        return missile;
    }

    private Poolable CreateWarning() => MissileWarning.Create(cam, config, transform);

    private void OnMissileGone(Vector3 pos) => _activeCount--;

    /// <summary>Перезапуск забега: гасим все активные ракеты и предупреждения, сброс таймеров и счётчика.
    /// ReleaseAll не спамит GameEvents — очки/juice при рестарте не нужны.</summary>
    public void ResetSpawner()
    {
        _pool.ReleaseAll();
        MissileWarning.HideAll(); // UX5.8: все индикаторы обратно в пул (рестарт/continue/Home)
        _activeCount = 0;
        _elapsed = 0f;
        _timer = 0f;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
        _elapsed += Time.deltaTime;

        // Безопасная зона (ArtDirection §5/§6): первые 1.6 s старта / 0.6 s рестарта — пустота.
        if (_elapsed < _safeZone) return;

        float interval = difficulty.MissileIntervalAt(_elapsed);
        if (interval <= 0f) return; // фаза 1 — ракет нет

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _timer = interval;
            TrySpawn();
        }
    }

    private void TrySpawn()
    {
        int max = difficulty.MaxMissilesAt(_elapsed);
        if (_activeCount >= max) return;

        // Спавн в задней полусфере: за спиной корабля. Адаптив (любой аспект):
        // точка обязана быть за видимой рамкой — её считает ScreenBounds.PointOutside
        // от КАМЕРЫ (полуширина кадра = ortho·aspect, фиксированный радиус ortho + margin
        // на альбомных экранах выводил ракету прямо в кадр).
        Vector2 ship = ShipController.Instance != null ? (Vector2)ShipController.Instance.transform.position : Vector2.zero;
        Vector2 forward = ShipController.Instance != null ? (Vector2)ShipController.Instance.transform.up : Vector2.up;
        Vector2 spawnDir = -forward;
        // Случайный разброс ±40° от «за спиной»
        float baseAngle = Mathf.Atan2(spawnDir.y, spawnDir.x) * Mathf.Rad2Deg;
        float angle = baseAngle + Random.Range(-40f, 40f);
        Vector2 finalDir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        Vector2 spawnPos = ScreenBounds.PointOutside(cam, cam.transform.position, finalDir, config.asteroidSpawnMargin);

        var missile = _pool.Get() as Missile;
        missile.Spawn(spawnPos, difficulty.MissileSpeedAt(_elapsed), difficulty.MissileTurnRateAt(_elapsed), config.missileLife);
        _activeCount++;

        // Предупреждение за 1.5 с до входа в кадр (UX5.8: СВОЙ индикатор на каждую ракету).
        // HideFor — страховка от переиспользования ракеты пулом при живом старом индикаторе.
        MissileWarning.HideFor(missile);
        var warn = _warningPool.Get() as MissileWarning;
        warn.ShowFor(spawnPos, missile);
    }
}
