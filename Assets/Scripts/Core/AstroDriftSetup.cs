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
