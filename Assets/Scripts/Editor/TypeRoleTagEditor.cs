#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Инспектор TypeRoleTag: стиль выбирается выпадающим списком из TypographyConfig
/// (пункт «— нет —» = пустой styleId, тег ничего не делает), рядом кнопка-ссылка на конфиг.
///
/// Смена стиля применяет шрифт к TextMeshProUGUI СРАЗУ, в Edit Mode (TypographyEditorApply):
/// играть для проверки не нужно. styleId и шрифт меняются в одной группе Undo, поэтому
/// Ctrl+Z откатывает их вместе. Мультивыделение: правка идёт по всем выбранным тегам,
/// а пока стили различаются, popup показывает «— разные стили —» и ничего не меняет.
///
/// Значение styleId пишется напрямую в SerializedProperty: побочный PropertyField добавил бы
/// второй шаг Undo поверх того же поля.
/// </summary>
[CustomEditor(typeof(TypeRoleTag))]
[CanEditMultipleObjects]
public class TypeRoleTagEditor : Editor
{
    private const string NoneLabel = "— нет —";
    private const string MixedLabel = "— разные стили —";

    /// <summary>Служебный пункт popup для мультивыделения: выбор = «ничего не менять».</summary>
    private const string MixedSentinel = "\u0000mixed";

    public override void OnInspectorGUI()
    {
        var tags = CollectTargets();
        var cfg = Typography.Config;

        if (cfg == null)
        {
            EditorGUILayout.HelpBox("TypographyConfig не найден в Assets/Resources — список стилей недоступен.", MessageType.Warning);
            var prop = serializedObject.FindProperty("styleId");
            if (prop != null)
            {
                EditorGUILayout.PropertyField(prop, new GUIContent("Style Id"));
                serializedObject.ApplyModifiedProperties();
            }
            return;
        }

        bool mixed = IsMixed(tags);
        var ids = new List<string> { mixed ? MixedSentinel : string.Empty };
        var labels = new List<string> { mixed ? MixedLabel : NoneLabel };

        if (cfg.styles != null)
            for (int i = 0; i < cfg.styles.Length; i++)
            {
                var s = cfg.styles[i];
                if (s == null || string.IsNullOrEmpty(s.id)) continue;
                ids.Add(s.id);
                labels.Add(StyleLabel(s));
            }

        // Стиль, которого нет в конфиге, оставляем отдельным пунктом — иначе popup молча обнулит ссылку.
        foreach (var tag in tags)
        {
            var id = tag.styleId;
            if (string.IsNullOrEmpty(id) || ids.Contains(id)) continue;
            ids.Add(id);
            labels.Add($"{id} (нет в конфиге)");
        }

        string current = mixed ? MixedSentinel : (tags.Count > 0 ? tags[0].styleId : string.Empty);
        int index = Mathf.Max(0, ids.IndexOf(current));

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        int picked = EditorGUILayout.Popup(new GUIContent("Style", "Стиль из TypographyConfig"), index, labels.ToArray(), GUILayout.MinWidth(120f));
        DrawConfigLink(cfg);
        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck())
        {
            var newId = ids[picked];
            // Служебный пункт и «значение не изменилось» не порождают ни Undo, ни грязи.
            if (newId != MixedSentinel && !SameStyle(tags, newId)) ApplyStyle(tags, newId);
        }

        DrawHints(tags, cfg);
    }

    /// <summary>Записать styleId и применить шрифт ко всем выбранным — одним шагом Undo.</summary>
    private void ApplyStyle(List<TypeRoleTag> tags, string newStyleId)
    {
        if (tags.Count == 0) return;

        Undo.RecordObjects(tags.ToArray(), "Change Typography Style");
        Undo.SetCurrentGroupName("Change Typography Style");

        foreach (var tag in tags)
        {
            tag.styleId = newStyleId;
            TypographyEditorApply.ApplyTag(tag, true);   // та же группа Undo, что и styleId
            TypographyEditorApply.FinalizeChanges(tag, tag.GetComponent<TextMeshProUGUI>());
        }

        Repaint();
    }

    private static string StyleLabel(TypographyStyle s)
    {
        return string.IsNullOrEmpty(s.displayName) ? s.id : $"{s.displayName} ({s.id})";
    }

    private static bool IsMixed(List<TypeRoleTag> tags)
    {
        if (tags.Count < 2) return false;
        var first = tags[0].styleId;
        for (int i = 1; i < tags.Count; i++)
            if (!string.Equals(tags[i].styleId, first, StringComparison.Ordinal)) return true;
        return false;
    }

    private static bool SameStyle(List<TypeRoleTag> tags, string id)
    {
        if (tags.Count == 0) return true;
        foreach (var tag in tags)
            if (!string.Equals(tag.styleId, id, StringComparison.Ordinal)) return false;
        return true;
    }

    private static void DrawHints(List<TypeRoleTag> tags, TypographyConfig cfg)
    {
        bool allEmpty = tags.Count > 0;
        string unknownId = null;

        foreach (var tag in tags)
        {
            if (string.IsNullOrEmpty(tag.styleId)) continue;
            allEmpty = false;
            if (cfg.GetStyle(tag.styleId) == null && unknownId == null) unknownId = tag.styleId;
        }

        if (allEmpty)
            EditorGUILayout.HelpBox("Стиль не выбран: тег ничего не делает, шрифт ноды из префаба сохраняется.", MessageType.Info);
        else if (unknownId != null)
            EditorGUILayout.HelpBox($"Стиля '{unknownId}' нет в конфиге — шрифт ноды не изменится.", MessageType.Warning);
    }

    private static void DrawConfigLink(TypographyConfig cfg)
    {
        if (!GUILayout.Button("TypographyConfig", GUILayout.Width(130f))) return;
        Selection.activeObject = cfg;
        EditorGUIUtility.PingObject(cfg);
    }

    private List<TypeRoleTag> CollectTargets()
    {
        var list = new List<TypeRoleTag>(targets.Length);
        foreach (var o in targets)
            if (o is TypeRoleTag tag && tag != null) list.Add(tag);
        return list;
    }
}
#endif
