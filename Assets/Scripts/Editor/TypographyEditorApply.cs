#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Редакторное применение стилей TypeRoleTag — Edit Mode, без запуска игры.
/// Одна точка входа и для инспектора (смена стиля), и для меню (переприменение после
/// правки самого стиля в конфиге).
///
/// Про override шрифта: он намеренно НЕ выставляется вручную. Проверено на инстансе префаба
/// и на ассете варианта — m_fontAsset попадает в m_Modifications и переживает переоткрытие
/// сцены/перезагрузку ассета сам, а ручной prefabOverride лишь рисковал бы вернуть шрифт
/// стиля поверх осознанного сброса шрифта к родительскому.
///
/// Рантайм не затронут: в игре источник истины — TypeRoleTag.OnEnable.
/// </summary>
public static class TypographyEditorApply
{
    private const string FontProp = "m_fontAsset";
    private const string FontStyleProp = "m_fontStyle";

    private const string RootFolder = "Assets/Prefabs";

    /// <summary>Стим-гуард: обновляем инспектор один раз за прогон меню, а не на каждый тег.</summary>
    private static bool _streamDirty;

    // ————————————————————— применение —————————————————————

    /// <summary>
    /// Применить стиль к ноде: шрифт (+ вес, если стиль разрешает). Размеры/трекинг не трогаются.
    /// recordUndo выключаем для объектов из LoadPrefabContents — они не принадлежат ни сцене,
    /// ни ассету, и Undo по ним даёт ошибки в консоли.
    /// </summary>
    public static bool ApplyTag(TypeRoleTag tag, bool recordUndo)
    {
        if (tag == null || string.IsNullOrEmpty(tag.styleId)) return false;

        var tmp = tag.GetComponent<TextMeshProUGUI>();
        if (tmp == null) return false;

        var cfg = Typography.Config;
        if (cfg == null || cfg.GetStyle(tag.styleId) == null) return false;

        int beforeFont = FontId(tmp);
        int beforeStyle = tmp.fontStyle.GetHashCode();

        var so = new SerializedObject(tmp);
        var fontProp = so.FindProperty(FontProp);
        var styleProp = so.FindProperty(FontStyleProp);

        var objects = new UnityEngine.Object[] { tag, tmp };
        if (recordUndo) Undo.RecordObjects(objects, "Change Typography Style");

        if (fontProp != null)
        {
            var font = cfg.ResolveFont(tag.styleId, LocaleCode());
            if (font == null) font = TMP_Settings.defaultFontAsset;
            if (font != null) fontProp.objectReferenceValue = font;
            if (styleProp != null && cfg.ResolveApplyFontStyle(tag.styleId))
                styleProp.intValue = (int)cfg.ResolveFontStyle(tag.styleId);

            so.ApplyModifiedProperties();
        }
        else
        {
            // Страховка: если SerializedObject почему-то не дал m_fontAsset — напрямую через API Typography.
            Typography.ApplyFontOnly(tmp, tag.styleId);
        }

        bool changed = FontId(tmp) != beforeFont || tmp.fontStyle.GetHashCode() != beforeStyle;
        if (changed) _streamDirty = true;
        return changed;
    }

    /// <summary>Дописать и пометить грязным родительский ассет/сцену.</summary>
    public static void FinalizeChanges(TypeRoleTag tag, TextMeshProUGUI tmp)
    {
        if (tag != null) FinalizeOwner(tag.gameObject);
        if (tmp != null) EditorUtility.SetDirty(tmp);
        if (tag != null) EditorUtility.SetDirty(tag);
        FlushStream();
    }

    /// <summary>Зафиксировать изменение в родительском префабе/сцене, чтобы правка пережила переоткрытие.</summary>
    public static void FinalizeOwner(GameObject go)
    {
        if (go == null) return;

        var stage = PrefabStageUtility.GetPrefabStage(go);
        if (stage != null)
        {
            // Prefab Stage: правка идёт прямо в ассет префаба — помечаем грязным содержимое и стадию.
            if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);
            EditorUtility.SetDirty(stage.prefabContentsRoot);
            return;
        }

        if (PrefabUtility.IsPartOfPrefabAsset(go))
        {
            EditorUtility.SetDirty(go); // ассет префаба/варианта сохранит m_Modifications как есть
            return;
        }

        if (PrefabUtility.IsPartOfPrefabInstance(go))
            PrefabUtility.RecordPrefabInstancePropertyModifications(go); // запись override в m_Modifications

        if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);
    }

    /// <summary>Обновить открытые инспекторы один раз (безопасен и в Edit Mode, и в Play Mode).</summary>
    public static void FlushStream()
    {
        if (!_streamDirty) return;
        _streamDirty = false;
        EditorApplication.delayCall += () =>
        {
            if (Selection.activeObject == null) return;
            EditorApplication.RepaintHierarchyWindow();
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        };
    }

    // ————————————————————— контекст применения —————————————————————

    private static int FontId(TextMeshProUGUI tmp)
    {
        return tmp.font != null ? tmp.font.GetInstanceID() : 0;
    }

    private static string LocaleCode()
    {
        try
        {
            var locale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
            return locale != null ? locale.Identifier.Code : null;
        }
        catch (Exception)
        {
            return null; // локали нет — базовый шрифт стиля
        }
    }

    // ————————————————————— меню: применить ко всему —————————————————————

    // ОТКЛЮЧЕНО (инцидент 2026-09-22): массово перезаписывает шрифты во ВСЕХ префабах и в сцене
    // и вызывает AssetDatabase.SaveAssets() — теряются ручные правки типографики владельца.
    // Класс оставлен: TypeRoleTagEditor.ApplyTag использует его для точечного применения стиля.
    // [MenuItem("AstroDrift/Typography/Apply Styles (Scene + Prefabs)")]
    public static void ApplyAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Typography] Apply Styles недоступен в Play Mode — применяйте из инспектора.");
            return;
        }

        int sceneCount = SweepScene();

        var prefabLog = new StringBuilder();
        int prefabCount = SweepPrefabs(prefabLog);

        AssetDatabase.SaveAssets();
        FlushStream();

        // Проверяем КЛЮЧЕВЫЕ ассеты владельца: 0 расхождений = прогон идемпотентен и записался на диск.
        var verifyLog = new StringBuilder();
        int broke = Verify(VerifyAsserts, verifyLog);

        Debug.Log($"[Typography] Apply Styles готово. Сцена: {sceneCount} тегов, префабы: {prefabCount} тегов." +
                  (broke == 0 ? " Проверка: расхождений нет." : $" ПРОВЕРКА: РАСХОЖДЕНИЙ {broke}.") +
                  (verifyLog.Length > 0 ? "\n" + verifyLog : string.Empty) +
                  (prefabLog.Length > 0 ? "\n" + prefabLog : string.Empty));
    }

    private static readonly string[] VerifyAsserts =
    {
        "Assets/Prefabs/Death/DeathPanel.prefab",
        "Assets/Prefabs/Menu/MenuButton.prefab",
        "Assets/Scenes/Game.unity",
    };

    private static int SweepScene()
    {
        int count = 0;
        var touched = new HashSet<Scene>();

        foreach (var tag in UnityEngine.Object.FindObjectsByType<TypeRoleTag>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tag == null || string.IsNullOrEmpty(tag.styleId)) continue;
            if (!ApplyTag(tag, true)) continue;

            FinalizeChanges(tag, tag.GetComponent<TextMeshProUGUI>());
            count++;
            if (tag.gameObject.scene.IsValid()) touched.Add(tag.gameObject.scene);
        }

        foreach (var scene in touched) EditorSceneManager.MarkSceneDirty(scene);

        // Объекты Prefab Stage живут в preview-сцене: dirty помечаем сам ассет.
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null) EditorUtility.SetDirty(stage.prefabContentsRoot);

        return count;
    }

    private static int SweepPrefabs(StringBuilder log)
    {
        if (!AssetDatabase.IsValidFolder(RootFolder)) return 0;

        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { RootFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = null;
            try { root = PrefabUtility.LoadPrefabContents(path); }
            catch (Exception e) { log.AppendLine($"ПРОПУСК {path}: {e.Message}"); continue; }
            if (root == null) continue;
            // LoadPrefabContents не вызывает OnEnable (проверено зондом), поэтому применяем явно.
            // SaveAsPrefabAsset пишет только этот ассет, а существующие m_Modifications варианта
            // сохраняются как есть — база-владелец не пересобирается.
            int applied = 0;
            foreach (var tag in root.GetComponentsInChildren<TypeRoleTag>(true))
            {
                if (tag == null || string.IsNullOrEmpty(tag.styleId)) continue;
                if (ApplyTag(tag, false)) applied++;
            }

            if (applied > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path); // официальный паттерн LoadPrefabContents → SaveAsPrefabAsset
                count += applied;
            }

            PrefabUtility.UnloadPrefabContents(root);
        }
        return count;
    }

    // ————————————————————— проверка —————————————————————

    /// <summary>Ноль расхождений = прогон применён и идемпотентен. Проверяются ассеты владельца.</summary>
    private static int Verify(string[] assetPaths, StringBuilder log)
    {
        var cfg = Typography.Config;
        if (cfg == null) return 0;

        int mismatches = 0;
        foreach (var path in assetPaths)
        {
            if (path.EndsWith(".unity"))
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (!scene.IsValid() || !scene.isLoaded) { log.AppendLine($"ПРОПУСК проверки {path}: сцена не открыта."); continue; }
                foreach (var r in scene.GetRootGameObjects())
                    mismatches += CheckRoot(r, path, cfg, log);
                continue;
            }

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) { log.AppendLine($"ПРОПУСК проверки {path}: ассет не загрузился."); continue; }
            mismatches += CheckRoot(asset, path, cfg, log);
        }
        return mismatches;
    }

    private static int CheckRoot(GameObject root, string path, TypographyConfig cfg, StringBuilder log)
    {
        int mismatches = 0;
        foreach (var tag in root.GetComponentsInChildren<TypeRoleTag>(true))
        {
            if (tag == null || string.IsNullOrEmpty(tag.styleId)) continue;
            var style = cfg.GetStyle(tag.styleId);
            if (style == null)
            {
                mismatches++;
                log.AppendLine($"ПРОВЕРКА {path} :: {tag.gameObject.name}: стиля '{tag.styleId}' нет в конфиге.");
                continue;
            }

            var tmp = tag.GetComponent<TextMeshProUGUI>();
            if (tmp == null) continue;

            var expect = cfg.ResolveFont(tag.styleId, LocaleCode()) ?? TMP_Settings.defaultFontAsset;
            if (expect != null && tmp.font != expect)
            {
                mismatches++;
                log.AppendLine($"ПРОВЕРКА {path} :: {tag.gameObject.name}: шрифт {Name(tmp.font)} ≠ стиль {Name(expect)}");
            }

            if (cfg.ResolveApplyFontStyle(tag.styleId))
            {
                var expectWeight = cfg.ResolveFontStyle(tag.styleId);
                if (tmp.fontStyle != expectWeight)
                {
                    mismatches++;
                    log.AppendLine($"ПРОВЕРКА {path} :: {tag.gameObject.name}: вес {tmp.fontStyle} ≠ стиль {expectWeight}");
                }
            }
        }
        return mismatches;
    }

    private static string Name(TMP_FontAsset f) => f != null ? f.name : "null";
}
#endif
