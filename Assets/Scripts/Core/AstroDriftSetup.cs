#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Редакторная утилита: создаёт ScriptableObject-ассеты GameConfig и DifficultyConfig
/// со значениями из GDD (таблицы §4.3 и §6), а также единственный общий материал.
/// Меню: AstroDrift → Setup Assets
/// </summary>
public static class AstroDriftSetup
{
    [MenuItem("AstroDrift/Setup Assets")]
    public static void Setup()
    {
        EnsureFolder("Assets/Settings");
        EnsureFolder("Assets/Audio");
        EnsureFolder("Assets/Resources");

        // ——— GameConfig ———
        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Settings/GameConfig.asset");
        if (cfg == null)
        {
            cfg = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(cfg, "Assets/Settings/GameConfig.asset");
        }

        // Размеры астероидов (GDD §4.3)
        cfg.asteroidSizes = new[]
        {
            new GameConfig.AsteroidSizeDef
            {
                sizeName = "Крупный", radius = 1.0f, minVerts = 7, maxVerts = 8, hp = 3,
                childSize = AsteroidSize.Medium, childCount = 2,
            },
            new GameConfig.AsteroidSizeDef
            {
                sizeName = "Средний", radius = 0.6f, minVerts = 5, maxVerts = 6, hp = 2,
                childSize = AsteroidSize.Small, childCount = 2,
            },
            new GameConfig.AsteroidSizeDef
            {
                sizeName = "Мелкий", radius = 0.3f, minVerts = 4, maxVerts = 5, hp = 1,
                childSize = AsteroidSize.Small, childCount = 0,
            },
        };
        EditorUtility.SetDirty(cfg);

        // ——— DifficultyConfig ———
        var diff = AssetDatabase.LoadAssetAtPath<DifficultyConfig>("Assets/Settings/DifficultyConfig.asset");
        if (diff == null)
        {
            diff = ScriptableObject.CreateInstance<DifficultyConfig>();
            AssetDatabase.CreateAsset(diff, "Assets/Settings/DifficultyConfig.asset");
        }
        diff.ResetToGddDefaults();
        EditorUtility.SetDirty(diff);

        // ——— PilotProgressConfig (GDD §5bis) ———
        var pilot = AssetDatabase.LoadAssetAtPath<PilotProgressConfig>("Assets/Resources/PilotProgressConfig.asset");
        if (pilot == null)
        {
            pilot = ScriptableObject.CreateInstance<PilotProgressConfig>();
            AssetDatabase.CreateAsset(pilot, "Assets/Resources/PilotProgressConfig.asset");
        }
        pilot.xpPerScoreDivisor = 10;
        pilot.unlocks = new[]
        {
            new UnlockEntry { pilotLevel = 0,  type = UnlockType.Perk,     id = PerkId.BulletSpeed.ToString(),      implementedInWave1 = true },
            new UnlockEntry { pilotLevel = 0,  type = UnlockType.Perk,     id = PerkId.FireRate.ToString(),         implementedInWave1 = true },
            new UnlockEntry { pilotLevel = 0,  type = UnlockType.Pickup,   id = PickupType.RapidFire.ToString(),    implementedInWave1 = true },
            new UnlockEntry { pilotLevel = 1,  type = UnlockType.Perk,     id = PerkId.TurnSpeed.ToString(),        implementedInWave1 = true },
            new UnlockEntry { pilotLevel = 2,  type = UnlockType.Perk,     id = PerkId.ComboExtension.ToString(),   implementedInWave1 = true },
            new UnlockEntry { pilotLevel = 3,  type = UnlockType.Cosmetic, id = "Skin_Ship_Diamond",                implementedInWave1 = false },
            new UnlockEntry { pilotLevel = 4,  type = UnlockType.Pickup,   id = PickupType.SpreadShot.ToString(),   implementedInWave1 = true },
            new UnlockEntry { pilotLevel = 5,  type = UnlockType.Perk,     id = PerkId.BiggerBullets.ToString(),    implementedInWave1 = true },
            new UnlockEntry { pilotLevel = 6,  type = UnlockType.Perk,     id = PerkId.ScoreMultiplier.ToString(),  implementedInWave1 = true },
            new UnlockEntry { pilotLevel = 7,  type = UnlockType.Cosmetic, id = "Skin_Trail_Blue",                  implementedInWave1 = false },
            new UnlockEntry { pilotLevel = 8,  type = UnlockType.Pickup,   id = PickupType.Shield.ToString(),       implementedInWave1 = true },
            new UnlockEntry { pilotLevel = 9,  type = UnlockType.Perk,     id = PerkId.MissileJammer.ToString(),    implementedInWave1 = true },
            new UnlockEntry { pilotLevel = 10, type = UnlockType.Enemy,    id = "Drones",                           implementedInWave1 = false },
            new UnlockEntry { pilotLevel = 11, type = UnlockType.Perk,     id = PerkId.Piercing.ToString(),         implementedInWave1 = true },
            new UnlockEntry { pilotLevel = 12, type = UnlockType.Cosmetic, id = "Skin_Ship_Arrow",                  implementedInWave1 = false },
            new UnlockEntry { pilotLevel = 13, type = UnlockType.Pickup,   id = "Magnet",                           implementedInWave1 = false },
            new UnlockEntry { pilotLevel = 14, type = UnlockType.Pickup,   id = "SlowField",                        implementedInWave1 = false },
            new UnlockEntry { pilotLevel = 15, type = UnlockType.Enemy,    id = "Turrets",                          implementedInWave1 = false },
            new UnlockEntry { pilotLevel = 16, type = UnlockType.Cosmetic, id = "Skin_Trail_Red",                   implementedInWave1 = false },
            new UnlockEntry { pilotLevel = 17, type = UnlockType.Perk,     id = "ExplosionOnKill",                  implementedInWave1 = false },
            new UnlockEntry { pilotLevel = 18, type = UnlockType.Perk,     id = "SideGuns",                         implementedInWave1 = false },
            new UnlockEntry { pilotLevel = 19, type = UnlockType.Cosmetic, id = "Skin_Ship_Cross",                  implementedInWave1 = false },
            new UnlockEntry { pilotLevel = 20, type = UnlockType.Mechanic, id = "Loadout",                          implementedInWave1 = false },
        };
        EditorUtility.SetDirty(pilot);

        // ——— PickupConfig (GDD §4.6) ———
        var pickupCfg = AssetDatabase.LoadAssetAtPath<PickupConfig>("Assets/Resources/PickupConfig.asset");
        if (pickupCfg == null)
        {
            pickupCfg = ScriptableObject.CreateInstance<PickupConfig>();
            AssetDatabase.CreateAsset(pickupCfg, "Assets/Resources/PickupConfig.asset");
        }
        pickupCfg.pickupLifetime = 6f;
        pickupCfg.pickupBlinkLastSeconds = 2f;
        pickupCfg.pickupRadius = 0.35f;
        pickupCfg.maxActiveEffects = 2;
        pickupCfg.maxPickupsOnGround = 4;
        pickupCfg.pickups = new[]
        {
            new PickupDef { type = PickupType.RapidFire,  duration = 5f, dropChanceAsteroid = 0.08f, dropChanceMissile = 0f,    color = Palette.Hex("#FFD700"), unlockedByDefault = true,  unlockPilotLevel = 0 },
            new PickupDef { type = PickupType.SpreadShot, duration = 5f, dropChanceAsteroid = 0.06f, dropChanceMissile = 0f,    color = Palette.Hex("#FFD700"), unlockedByDefault = false, unlockPilotLevel = 4 },
            new PickupDef { type = PickupType.Shield,     duration = 0f, dropChanceAsteroid = 0f,    dropChanceMissile = 0.03f, color = Palette.Hex("#66CCFF"), unlockedByDefault = false, unlockPilotLevel = 8 },
        };
        EditorUtility.SetDirty(pickupCfg);

        // ——— PerkConfig (GDD §15) ———
        var perkCfg = AssetDatabase.LoadAssetAtPath<PerkConfig>("Assets/Resources/PerkConfig.asset");
        if (perkCfg == null)
        {
            perkCfg = ScriptableObject.CreateInstance<PerkConfig>();
            AssetDatabase.CreateAsset(perkCfg, "Assets/Resources/PerkConfig.asset");
        }
        perkCfg.offerCount = 3;
        perkCfg.stubScoreBonus = 500;
        perkCfg.rerollPerRun = 1;
        perkCfg.perks = new[]
        {
            new PerkDef { id = PerkId.BulletSpeed,     titleKey = "perk_bullet_speed_title",     descKey = "perk_bullet_speed_desc",     rarity = Rarity.Common, maxStacks = 3, unlockedByDefault = true,  unlockPilotLevel = 0,  valuePerStack = 0.20f },
            new PerkDef { id = PerkId.FireRate,        titleKey = "perk_fire_rate_title",        descKey = "perk_fire_rate_desc",        rarity = Rarity.Common, maxStacks = 3, unlockedByDefault = true,  unlockPilotLevel = 0,  valuePerStack = -0.15f },
            new PerkDef { id = PerkId.TurnSpeed,       titleKey = "perk_turn_speed_title",       descKey = "perk_turn_speed_desc",       rarity = Rarity.Common, maxStacks = 2, unlockedByDefault = false, unlockPilotLevel = 1,  valuePerStack = 0.25f },
            new PerkDef { id = PerkId.ComboExtension,  titleKey = "perk_combo_ext_title",        descKey = "perk_combo_ext_desc",        rarity = Rarity.Common, maxStacks = 2, unlockedByDefault = false, unlockPilotLevel = 2,  valuePerStack = 2.0f },
            new PerkDef { id = PerkId.BiggerBullets,   titleKey = "perk_bigger_bullets_title",   descKey = "perk_bigger_bullets_desc",   rarity = Rarity.Common, maxStacks = 2, unlockedByDefault = false, unlockPilotLevel = 5,  valuePerStack = 0.50f },
            new PerkDef { id = PerkId.ScoreMultiplier, titleKey = "perk_score_mult_title",       descKey = "perk_score_mult_desc",       rarity = Rarity.Common, maxStacks = 3, unlockedByDefault = false, unlockPilotLevel = 6,  valuePerStack = 0.25f },
            new PerkDef { id = PerkId.MissileJammer,   titleKey = "perk_missile_jammer_title",   descKey = "perk_missile_jammer_desc",   rarity = Rarity.Common, maxStacks = 2, unlockedByDefault = false, unlockPilotLevel = 9,  valuePerStack = -0.40f },
            new PerkDef { id = PerkId.Piercing,        titleKey = "perk_piercing_title",         descKey = "perk_piercing_desc",         rarity = Rarity.Rare,   maxStacks = 2, unlockedByDefault = false, unlockPilotLevel = 11, valuePerStack = 1f },
        };
        EditorUtility.SetDirty(perkCfg);

        // ——— Материал ———
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/ProceduralShared.mat");
        if (mat == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            mat = new Material(shader) { name = "ProceduralShared" };
            AssetDatabase.CreateAsset(mat, "Assets/Settings/ProceduralShared.mat");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("AstroDrift: GameConfig, DifficultyConfig, Material созданы (Assets/Settings/).");
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
