using UnityEngine;

/// <summary>
/// Bootstrap: собирает всю сцену «Game» из кода — камера (portrait, ortho 12),
/// Bloom-Volume (Threshold 0.6 / Intensity 0.5 / Scatter 0.6), корабль с
/// процедурным мешем и следом, спавнеры, VFX-подсистемы, GameManager.
/// Достаточно одного GameObject с этим компонентом в пустой сцене.
/// </summary>
public class Bootstrap : MonoBehaviour
{
    [SerializeField] private GameConfig config;
    [SerializeField] private DifficultyConfig difficulty;

    [Header("Префабы (структура; числа — в конфигах)")]
    [SerializeField] private GameObject shipPrefab;      // Assets/Resources/Prefabs/Ship.prefab

    private void Awake()
    {
        // Конфиги: если не назначены в инспекторе — грузим из Resources (создаёт AstroDriftSetup)
        if (config == null) config = Resources.Load<GameConfig>("GameConfig");
        if (difficulty == null) difficulty = Resources.Load<DifficultyConfig>("DifficultyConfig");
        if (shipPrefab == null) shipPrefab = Resources.Load<GameObject>("Prefabs/Ship");
        if (config == null)
        {
            Debug.LogError("Bootstrap: GameConfig не найден. Запустите AstroDrift → Setup Assets.");
            return;
        }

        BuildCameraAndBloom();
        var ship = BuildShip();
        // StarField — сценовый объект с StarFieldConfig в инспекторе.
        if (FindFirstObjectByType<StarField>() == null)
            Debug.LogError("Bootstrap: в сцене нет StarField. Добавь GameObject с компонентом " +
                           "StarField и назначь StarFieldConfig (Assets/Resources/StarFieldConfig.asset).");

        // Подсистемы
        var gmGo = new GameObject("GameManager");
        var gm = gmGo.AddComponent<GameManager>();

        var scoreGo = new GameObject("ScoreManager");
        var score = scoreGo.AddComponent<ScoreManager>();
        score.InitFrom(config);

        var diffGo = new GameObject("DifficultyManager");
        var diff = diffGo.AddComponent<DifficultyManager>();
        diff.InitFrom(difficulty);

        var audioGo = new GameObject("AudioManager");
        audioGo.AddComponent<AudioManager>();

        var poolParent = transform;
        var spawnerGo = new GameObject("Spawners");
        spawnerGo.transform.SetParent(poolParent);
        var astSpawner = spawnerGo.AddComponent<AsteroidSpawner>();
        astSpawner.Init(config, difficulty, Camera.main, poolParent);
        var missileSpawner = spawnerGo.AddComponent<MissileSpawner>();
        missileSpawner.Init(config, difficulty, Camera.main, poolParent);

        // UI — сценовый объект (Canvas/панели сохранены в Game.unity);
        // Bootstrap только подключает поведение к ScoreManager.
        var ui = FindFirstObjectByType<GameUI>();
        if (ui == null)
        {
            Debug.LogError("Bootstrap: GameUI не найден в сцене (Canvas с панелями Start/Death/Pause).");
            return;
        }
        ui.Init(score);

        // VFX
        var vfxGo = new GameObject("VFX");
        vfxGo.transform.SetParent(poolParent);
        vfxGo.AddComponent<ParticlePool>();
        vfxGo.AddComponent<FloatingTextPool>();

        // Оружие корабля
        var weapon = ship.gameObject.AddComponent<ShipWeapon>();
        weapon.Init(config, ship, poolParent);

        // GameManager получает ссылки
        gm.Init(config, difficulty, ship, Camera.main.GetComponent<CameraFollow>(),
                weapon, astSpawner, missileSpawner, score, diff);
    }

    private void BuildCameraAndBloom()
    {
        // Все компоненты и параметры камеры выставлены вручную в сцене Game.unity
        // (Camera, Brain, ImpulseSource, CameraFollow, CameraShake, Volume + профиль,
        // CM_VCam с PositionComposer/Listener) — код ничего не создаёт и не настраивает.
        if (Camera.main == null)
        {
            Debug.LogError("Bootstrap: в сцене нет Main Camera. Добавь её вручную " +
                           "вместе с CM_VCam и остальными компонентами.");
        }
    }

    private ShipController BuildShip()
    {
        // Префаб — источник СТРУКТУРЫ (меш/материал/трейл/коллайдер/RB);
        // числа (радиус, скорость, trail life) — из GameConfig через InitFrom.
        var ship = Instantiate(shipPrefab).GetComponent<ShipController>();
        ship.InitFrom(config);
        return ship;
    }

}
