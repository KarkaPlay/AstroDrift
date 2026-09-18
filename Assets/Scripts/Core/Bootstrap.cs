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
        // C2 (ТЗ Localization §6.1): сцену собираем по «И» — платформенный слой готов
        // (ЯИ: SDK/сейвы; RuStore/редактор: сразу) И LanguageService применил локаль.
        // Иначе UI построится в дефолтной локали и будет видимая вспышка языка при
        // позднем применении (на WebGL PlatformBoot.Ready приходит позже BeforeSceneLoad).
        if (PlatformBoot.IsReady && LanguageService.StartupApplied)
        {
            Build();
            return;
        }

        if (!PlatformBoot.IsReady) PlatformBoot.Ready += TryBuild;
        if (!LanguageService.StartupApplied) LanguageService.StartupAppliedChanged += TryBuild;
    }

    private void TryBuild()
    {
        if (PlatformBoot.IsReady && LanguageService.StartupApplied) Build();
    }

    private void Build()
    {
        // --- app_first_launch (ТЗ PlatformServices §9.1): строго ЗДЕСЬ, в начале Awake,
        // а НЕ в RuntimeInitializeOnLoadMethod(BeforeSceneLoad). Порядок вызова разных
        // BeforeSceneLoad-методов Unity не гарантирует: событие в BeforeSceneLoad
        // могло уйти раньше, чем RuStoreInstaller
        // регистрировал AppMetricaAnalyticsService, оно терялось в NullAnalyticsService
        // (только лог). Событие одно на установку — потеря невосполнима. Awake сцены
        // гарантированно позже ВСЕХ BeforeSceneLoad → сервисы уже зарегистрированы.
        // Не «оптимизировать» назад в BeforeSceneLoad!
        if (!PlatformServices.Save.HasKey("analytics_first_launch"))
        {
            PlatformServices.Save.SetInt("analytics_first_launch", 1);
            PlatformServices.Save.Flush();
            Analytics.Log("app_first_launch");
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // --- Guard профиля (ТЗ PlatformServices §9.2): забыли Switch Build Profile →
        // APK компилируется и запускается с заглушками, молча, с нулевым доходом.
        // В редакторе заглушки штатны — проверяем только девайс-билды.
        // WebGL (itch.io) исключён: там Null-реклама ШТАТНА (ТЗ ItchTZ §0 — без рекламы).
        if (PlatformServices.Ads is NullAdsService)
            Debug.LogError("[Platform] Ads service is a STUB in a device build! Wrong Build Profile?");
#endif

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

        // Реклама: игровой флоу (формула interstitial, сессии, mute) — AdsFlow.
        // Singleton с DontDestroyOnLoad; SDK подключается через PlatformServices.Ads
        // (RuStore-инсталлер либо заглушка в редакторе).
        var adsGo = new GameObject("AdsFlow");
        adsGo.AddComponent<AdsFlow>();

        // Мета-прогрессия (Волна 1, GDD §5bis): XP/уровень пилота. До AdsFlow не критично,
        // но должен существовать до GameManager.Init (первый забег уже читает PilotLevel).
        var pilotCfg = Resources.Load<PilotProgressConfig>("PilotProgressConfig");
        if (pilotCfg != null)
        {
            var pilot = new GameObject("PilotProgress").AddComponent<PilotProgressManager>();
            pilot.InitFrom(pilotCfg);
        }
        else
        {
            Debug.LogError("Bootstrap: PilotProgressConfig не найден. Запустите AstroDrift → Setup Assets.");
        }

        var poolParent = transform;
        var spawnerGo = new GameObject("Spawners");
        spawnerGo.transform.SetParent(poolParent);
        var astSpawner = spawnerGo.AddComponent<AsteroidSpawner>();
        astSpawner.Init(config, difficulty, Camera.main, poolParent);
        var missileSpawner = spawnerGo.AddComponent<MissileSpawner>();
        missileSpawner.Init(config, difficulty, Camera.main, poolParent);

        // Пикапы (Волна 1, GDD §4.6): дроп/эффекты/щит
        var pickupCfg = Resources.Load<PickupConfig>("PickupConfig");
        if (pickupCfg != null)
        {
            var pickupGo = new GameObject("PickupManager");
            pickupGo.transform.SetParent(poolParent);
            pickupGo.AddComponent<PickupManager>().Init(pickupCfg, difficulty, poolParent);
        }
        else
        {
            Debug.LogError("Bootstrap: PickupConfig не найден. Запустите AstroDrift → Setup Assets.");
        }

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

        // Перки (Волна 1, GDD §15): модификаторы поверх GameConfig. До GameManager.Init.
        var perkCfg = Resources.Load<PerkConfig>("PerkConfig");
        if (perkCfg != null)
        {
            var perkGo = new GameObject("PerkManager");
            var perks = perkGo.AddComponent<PerkManager>();
            perks.InitFrom(perkCfg, config);

            // Оверлей выбора перка строится в сцене (AstroDrift → Setup Scene UI);
            // PerkManager сам включает фриз, PerkChoiceUI показывает карты и вызывает Choose.
            var perkUi = FindFirstObjectByType<PerkChoiceUI>();
            if (perkUi != null)
                perks.OnLevelUpOffer += offers =>
                {
                    perkUi.Show(offers);
                    ui.RefreshHud(); // порог уже сдвинут → HUD-бар перка начинается с 0 (GDD §15.3)
                };
        }
        else
        {
            Debug.LogError("Bootstrap: PerkConfig не найден. Запустите AstroDrift → Setup Assets.");
        }

        // GameManager получает ссылки
        gm.Init(config, difficulty, ship, Camera.main.GetComponent<CameraFollow>(),
                weapon, astSpawner, missileSpawner, score, diff);

        // Площадка: игра загружена и интерактивна — стартовый экран собран (Яндекс: лоадер скрывается,
        // без этого вызова модерация не пройдёт). RuStore — no-op.
        PlatformServices.Lifecycle.GameReady();
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
