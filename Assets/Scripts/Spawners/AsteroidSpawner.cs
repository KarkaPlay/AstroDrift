using UnityEngine;

/// <summary>
/// Спавнер астероидов (GDD §4.3): таймер спавна, выбор размера (доля крупных),
/// расположение за экраном (60% в передней полусфере), проверка OverlapCircle
/// (мин. дистанция 2.5), лимит 15, первый астероид на 3-й секунде по курсу.
/// </summary>
public class AsteroidSpawner : MonoBehaviour
{
    [SerializeField] private GameConfig config;
    [SerializeField] private DifficultyConfig difficulty;
    [SerializeField] private Camera cam;
    [SerializeField] private GameObject asteroidPrefab; // Assets/Resources/Prefabs/Asteroid.prefab

    private ObjectPool _largePool;
    private ObjectPool _mediumPool;
    private ObjectPool _smallPool;
    private float _spawnTimer;
    private float _elapsed;
    private bool _firstSpawned;
    private int _activeCount;

    public void Init(GameConfig cfg, DifficultyConfig diff, Camera camera, Transform poolParent)
    {
        config = cfg;
        difficulty = diff;
        cam = camera;

        if (asteroidPrefab == null)
            asteroidPrefab = Resources.Load<GameObject>("Prefabs/Asteroid");

        _largePool = CreatePool(AsteroidSize.Large, poolParent);
        _mediumPool = CreatePool(AsteroidSize.Medium, poolParent);
        _smallPool = CreatePool(AsteroidSize.Small, poolParent);

        GameEvents.AsteroidDestroyed += OnAsteroidDestroyed;
        GameEvents.AsteroidDespawned += OnAsteroidDespawned;
        GameEvents.Combo += OnAsteroidCombo;
    }

    private ObjectPool CreatePool(AsteroidSize size, Transform parent)
    {
        GameConfig.AsteroidSizeDef def = Def(size);
        int prewarm = size == AsteroidSize.Large ? 4 : (size == AsteroidSize.Medium ? 8 : 12);
        return new ObjectPool(() =>
        {
            // Базовый префаб — источник структуры; меш/коллайдер по размеру
            // генерирует Asteroid.Spawn (вариация остаётся код-драйв).
            var go = Instantiate(asteroidPrefab, parent);
            go.name = "Asteroid_" + size;
            var ast = go.GetComponent<Asteroid>();
            ast.Init(config);
            return ast;
        }, parent, prewarm);
    }

    private GameConfig.AsteroidSizeDef Def(AsteroidSize s)
    {
        switch (s)
        {
            case AsteroidSize.Large: return config.asteroidSizes[0];
            case AsteroidSize.Medium: return config.asteroidSizes[1];
            default: return config.asteroidSizes[2];
        }
    }

    private void OnAsteroidDestroyed(Vector3 pos, AsteroidSize size) => _activeCount--;
    private void OnAsteroidDespawned(Vector3 pos) => _activeCount--;
    private void OnAsteroidCombo(Vector3 pos) => _activeCount--; // астероид уничтожен комбо (без очков)

    /// <summary>Перезапуск забега: гасим все активные астероиды (3 пула), сброс таймеров и счётчика.
    /// ReleaseAll не спамит GameEvents — очки/juice при рестарте не нужны.</summary>
    public void ResetSpawner()
    {
        _largePool.ReleaseAll();
        _mediumPool.ReleaseAll();
        _smallPool.ReleaseAll();
        _activeCount = 0;
        _elapsed = 0f;
        _spawnTimer = 0f;
        _firstSpawned = false;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
        _elapsed += Time.deltaTime;

        // Первый астероид: 3-я секунда, прямо по курсу (GDD §6 «риски»)
        if (!_firstSpawned && _elapsed >= config.firstAsteroidDelay)
        {
            _firstSpawned = true;
            SpawnFirstOnCourse();
            return;
        }

        float rate = difficulty.AsteroidSpawnRateAt(_elapsed);
        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f)
        {
            _spawnTimer = 1f / Mathf.Max(rate, 0.01f);
            TrySpawn();
        }
    }

    private void SpawnFirstOnCourse()
    {
        if (_activeCount >= config.maxAsteroids) return;
        Vector2 forward = ShipController.Instance != null ? (Vector2)ShipController.Instance.transform.up : Vector2.up;
        // ТЗ v3 (Доработка 1): дистанция от КАМЕРЫ = ortho + margin (GDD §4.3) —
        // от корабля дистанция до верхнего края кадра больше ortho (камера выше
        // корабля на 0.4·ortho), спавн от корабля попадал в кадр по курсу.
        Vector2 camPos = cam.transform.position;
        float dist = cam.orthographicSize + config.asteroidSpawnMargin;
        Vector2 spawnPos = camPos + forward * dist;
        if (Physics2D.OverlapCircle(spawnPos, config.minSpawnDistance) != null) return;
        SpawnAt(AsteroidSize.Medium, spawnPos, forward);
    }

    private void TrySpawn()
    {
        if (_activeCount >= config.maxAsteroids) return;

        // Выбор размера: крупный с вероятностью difficulty.bigAsteroidChance, иначе средний/мелкий
        AsteroidSize size;
        float bigChance = difficulty.BigAsteroidChanceAt(_elapsed);
        if (Random.value < bigChance) size = AsteroidSize.Large;
        else size = Random.value < 0.6f ? AsteroidSize.Medium : AsteroidSize.Small;

        // Скорость в диапазоне фазы
        float speed = Random.Range(difficulty.AsteroidSpeedMinAt(_elapsed), difficulty.AsteroidSpeedMaxAt(_elapsed));

        Vector2 spawnPos = PickSpawnPosition();
        if (spawnPos == Vector2.zero) return; // не нашли свободную точку
        Vector2 dir = DirectionFrom(shipPos, spawnPos);

        SpawnAt(size, spawnPos, dir.normalized, speed);
    }

    private Vector2 shipPos => ShipController.Instance != null ? (Vector2)ShipController.Instance.transform.position : Vector2.zero;

    private Vector2 PickSpawnPosition()
    {
        // ТЗ v3 (Доработка 1): радиус спавна от КАМЕРЫ, не от корабля.
        // По диагонали до угла экрана дальше ortho — спавн за углом тоже вне кадра.
        Vector2 camPos = cam.transform.position;
        float dist = cam.orthographicSize + config.asteroidSpawnMargin;
        Vector2 ship = shipPos;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            // 60% спавна в передней полусфере (GDD §4.3)
            bool front = Random.value < 0.6f;
            Vector2 dir;
            if (front)
            {
                float baseAngle = Mathf.Atan2(ship.y, ship.x) * Mathf.Rad2Deg; // forward корабля
                float spread = 50f;
                dir = AngleToVector(baseAngle + Random.Range(-spread, spread));
            }
            else
            {
                dir = Random.insideUnitCircle.normalized;
            }
            Vector2 candidate = camPos + dir * dist;
            if (Physics2D.OverlapCircle(candidate, config.minSpawnDistance) == null)
                return candidate;
        }
        return Vector2.zero;
    }

    private Vector2 DirectionFrom(Vector2 from, Vector2 to) => (to - from).normalized;

    private Vector2 AngleToVector(float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void SpawnAt(AsteroidSize size, Vector2 pos, Vector2 dir, float speed = 0f)
    {
        if (speed <= 0f) speed = Random.Range(0.5f, 2f);
        float angular = Random.Range(20f, 90f);
        var pool = PoolFor(size);
        var asteroid = pool.Get() as Asteroid;
        asteroid.Spawn(size, pos, dir, speed, angular);
        _activeCount++;
    }

    private ObjectPool PoolFor(AsteroidSize s)
    {
        switch (s)
        {
            case AsteroidSize.Large: return _largePool;
            case AsteroidSize.Medium: return _mediumPool;
            default: return _smallPool;
        }
    }
}
