using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

/// <summary>
/// ТЗ §11.2 "Missing translations report": валидатор ключей локализации + coverage чарсета.
///
/// Множество используемых ключей собирается НЕ из Entries таблицы (неполон, §1.6), а из 6 источников:
///   1. все LocalizeStringEvent в сценах и префабах (реальный TableEntryReference) + рантайм-смены entry;
///   2. LocalizedString-поля данных (явное перечисление: перки, пикапы, конфиг перков);
///   3. PilotProgressManager.AllUnlocks → unlock_<id> + unlock_soon;
///   4. белый список синхронных ключей (§3.3);
///   5. ключи AstroDriftSceneSetup (карта §8.2);
///   6. coverage чарсета — все кодпоинты строк таблиц против ОБЪЕДИНЕНИЯ назначенных шрифтов.
///
/// Трактовка покрытия (§11.2 п.6, решение продюсера): кодпоинт покрыт, если он есть хотя бы
/// в одном шрифте, назначенном ролям (заполненные слоты TypographyConfig + fallback-набор §7.1 п.6).
/// Отсутствие во всех — варн.
///
/// Отчёт: ключи без перевода в любой локали + ключи таблицы без использования (кандидаты в сироты)
/// + непокрытые чарсетом символы.
/// </summary>
public static class LocalizationValidator
{
    private const string CollectionName = "GameTexts";

    public class Report
    {
        public readonly List<string> Missing = new List<string>();
        public readonly List<string> Orphans = new List<string>();
        public readonly List<string> UncoveredChars = new List<string>();
        public readonly List<string> Sources = new List<string>();
        public readonly HashSet<string> UsedKeys = new HashSet<string>();
        public int TableKeys;
        public int TableStrings;
        public int CheckedCodePoints;
        public int UnionFontCount;

        public bool IsGreen => Missing.Count == 0 && Orphans.Count == 0 && UncoveredChars.Count == 0;
    }

    // --- 6. Регистрация источников LocalizedString-полей (новый тип SO = одна строка) -------------

    private static void CollectLocalizedStringData(HashSet<string> used, List<string> sources)
    {
        // Resources/Perks/*.asset → PerkDefinition.title/desc
        foreach (var perk in Resources.LoadAll<PerkDefinition>("Perks"))
        {
            if (perk == null) continue;
            Add(used, perk.title);
            Add(used, perk.desc);
        }
        sources.Add("Resources/Perks/*.asset → PerkDefinition.title/desc");

        // Resources/PickupConfig.asset → pickups[].name (PickupDef — вложенный [Serializable], не SO)
        var pickups = Resources.Load<PickupConfig>("PickupConfig");
        if (pickups != null && pickups.pickups != null)
            foreach (var p in pickups.pickups) Add(used, p?.name);
        sources.Add("Resources/PickupConfig.asset → pickups[].name");

        // Resources/PerkConfig.asset → perks[] (PerkDefinition-ассеты, те же поля)
        var perkConfig = Resources.Load<PerkConfig>("PerkConfig");
        if (perkConfig != null && perkConfig.perks != null)
            foreach (var perk in perkConfig.perks)
            {
                if (perk == null) continue;
                Add(used, perk.title);
                Add(used, perk.desc);
            }
        sources.Add("Resources/PerkConfig.asset → perks[].title/desc");
    }

    // --- 3. Рантайм-ключи дерева разблокировок (§11.2 п.3) ----------------------------------------

    private static void CollectRuntimeUnlockKeys(HashSet<string> used, List<string> sources)
    {
        const string configPath = "Assets/Resources/PilotProgressConfig.asset";
        var config = AssetDatabase.LoadAssetAtPath<PilotProgressConfig>(configPath);
        int ids = 0;
        if (config != null && config.unlocks != null)
            foreach (var u in config.unlocks)
                if (u != null && !string.IsNullOrEmpty(u.id))
                {
                    used.Add("unlock_" + u.id);
                    ids++;
                }
        used.Add("unlock_soon");
        sources.Add($"PilotProgressConfig.unlocks → unlock_<id> ×{ids} + unlock_soon");
    }

    // --- 4. Белый список синхронных ключей (§3.3) ------------------------------------------------

    private static readonly string[] SyncKeysWhitelist =
    {
        "combo",           // GameManager комбо-флоатер
        "unlocked_title",  // GameUI.DeathUnlocked
        "unlock_soon",     // GameUI дерево
        "unlock_tree_title",
        "level_up_line",   // GameUI.FillDeathMeta (рантайм-смена entry)
        "level_line",
        "xp_gain",
        "score",
        "best",
    };

    // --- 5. Ключи, проставляемые AstroDriftSceneSetup (карта §8.2) -------------------------------

    private static readonly string[] SceneSetupKeys =
    {
        "score", "best", "new_best", "continue_cta", "continue_caption",
        "home", "xp_gain", "level_line", "pause_title", "resume",
    };

    // --- Запуск -----------------------------------------------------------------------------------

    [MenuItem("AstroDrift/Validate Localization (Missing translations report)")]
    public static void RunFromMenu() => Run();

    public static Report Run()
    {
        var report = new Report();

        // Источник 1: LocalizeStringEvent в сценах и префабах (реальный TableEntryReference).
        // ВАЖНО: FindAssets("t:LocalizeStringEvent") возвращает 0 — компонент не является типом ассета,
        // поэтому префабы обходим через PrefabUtility.LoadPrefabContents, сцены — аддитивным открытием.
        int lseCount = 0;

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;

            GameObject root = null;
            try { root = PrefabUtility.LoadPrefabContents(path); }
            catch { root = null; }
            if (root == null) continue;

            foreach (var lse in root.GetComponentsInChildren<LocalizeStringEvent>(true))
                CountLse(lse, report.UsedKeys, ref lseCount);

            PrefabUtility.UnloadPrefabContents(root);
        }

        foreach (var guid in AssetDatabase.FindAssets("t:Scene"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;

            var scene = SceneManager.GetSceneByPath(path);
            bool opened = false;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                try { scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); opened = true; }
                catch { continue; }
            }

            foreach (var root in scene.GetRootGameObjects())
                foreach (var lse in root.GetComponentsInChildren<LocalizeStringEvent>(true))
                    CountLse(lse, report.UsedKeys, ref lseCount);

            if (opened) EditorSceneManager.CloseScene(scene, true);
        }

        report.Sources.Add($"LocalizeStringEvent (префабы+сцены): компонентов {lseCount}");

        // Рантайм-смены entry (§8.1/§8.2) — статическим списком
        foreach (var k in new[] { "shield_used_today", "shield_caption", "level_up_line", "level_line", "xp_gain" })
            report.UsedKeys.Add(k);
        report.Sources.Add("§8.1/§8.2 рантайм-смены entry (стат. список)");

        CollectLocalizedStringData(report.UsedKeys, report.Sources);
        CollectRuntimeUnlockKeys(report.UsedKeys, report.Sources);

        foreach (var k in SyncKeysWhitelist) report.UsedKeys.Add(k);
        report.Sources.Add($"§3.3 белый список синхронных ключей ×{SyncKeysWhitelist.Length}");

        foreach (var k in SceneSetupKeys) report.UsedKeys.Add(k);
        report.Sources.Add($"AstroDriftSceneSetup карта §8.2 ×{SceneSetupKeys.Length}");

        // Таблицы локалей
        var collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(CollectionPath);
        if (collection == null)
        {
            Debug.LogError($"[LocValidate] Коллекция '{CollectionName}' не найдена: {CollectionPath}");
            return report;
        }

        var shared = collection.SharedData;
        report.TableKeys = shared != null ? shared.Entries.Count : 0;

        // Ключи таблицы → значения по локалям
        var tableKeys = new HashSet<string>();
        if (shared != null)
            foreach (var e in shared.Entries)
                if (!string.IsNullOrEmpty(e.Key)) tableKeys.Add(e.Key);

        var localeValues = new Dictionary<string, Dictionary<string, string>>();
        foreach (var table in collection.StringTables)
        {
            var code = table.LocaleIdentifier.Code;
            var map = new Dictionary<string, string>();
            foreach (var entry in table.Values)
                map[entry.Key] = entry.Value;
            localeValues[code] = map;
        }

        // 1. Ключи без перевода в любой локали
        foreach (var key in report.UsedKeys)
        {
            if (!tableKeys.Contains(key))
            {
                report.Missing.Add($"{key} — нет ключа в таблице");
                continue;
            }
            foreach (var kv in localeValues)
            {
                if (!kv.Value.TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
                    report.Missing.Add($"{key} — пусто/отсутствует в '{kv.Key}'");
            }
        }
        report.Missing.Sort(StringComparer.Ordinal);

        // 2. Ключи таблицы без использования (кандидаты в сироты)
        foreach (var key in tableKeys)
            if (!report.UsedKeys.Contains(key))
                report.Orphans.Add(key);
        report.Orphans.Sort(StringComparer.Ordinal);

        // 6. Coverage чарсета — ОБЪЕДИНЕНИЕ назначенных шрифтов
        var fonts = CollectAssignedFonts();
        report.UnionFontCount = fonts.Count;
        var codePoints = new SortedSet<int>();
        foreach (var kv in localeValues)
            foreach (var value in kv.Value.Values)
            {
                report.TableStrings++;
                CollectCodePoints(value, codePoints, kv.Key);
            }
        report.CheckedCodePoints = codePoints.Count;

        foreach (var cp in codePoints)
        {
            bool covered = false;
            foreach (var font in fonts)
                if (font != null && font.HasCharacter(cp)) { covered = true; break; }
            if (!covered)
                report.UncoveredChars.Add($"U+{cp:X4} ({Describe(cp)})");
        }

        LogReport(report, localeValues.Keys);
        WriteReportFile(report, localeValues.Keys);
        return report;
    }

    // --- Coverage: объединение назначенных шрифтов ------------------------------------------------

    private static List<TMP_FontAsset> CollectAssignedFonts()
    {
        var result = new List<TMP_FontAsset>();
        var visited = new HashSet<TMP_FontAsset>();

        void AddWithFallbacks(TMP_FontAsset font)
        {
            if (font == null || !visited.Add(font)) return;
            result.Add(font);
            var fallbacks = font.fallbackFontAssetTable;
            if (fallbacks == null) return;
            foreach (var f in fallbacks) AddWithFallbacks(f);
        }

        var config = Resources.Load<TypographyConfig>("TypographyConfig");
        if (config == null)
        {
            Debug.LogWarning("[LocValidate] TypographyConfig не найден — coverage считается только по TMP default.");
            AddWithFallbacks(TMP_Settings.defaultFontAsset);
            return result;
        }

        AddWithFallbacks(config.headingLight);
        AddWithFallbacks(config.titleBold);
        AddWithFallbacks(config.bodyRegular);
        AddWithFallbacks(config.ctaSemiBold);
        if (config.languageOverrides != null)
            foreach (var o in config.languageOverrides)
            {
                if (o == null) continue;
                AddWithFallbacks(o.heading);
                AddWithFallbacks(o.titleBold);
                AddWithFallbacks(o.body);
                AddWithFallbacks(o.cta);
            }
        AddWithFallbacks(TMP_Settings.defaultFontAsset);
        return result;
    }

    private static void CollectCodePoints(string value, SortedSet<int> into, string locale)
    {
        if (string.IsNullOrEmpty(value)) return;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '\n' || c == '\r' || c == '\t') continue; // служебные, не рендерятся через атлас
            int cp;
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                cp = char.ConvertToUtf32(c, value[i + 1]);
                i++;
            }
            else cp = c;
            into.Add(cp);
        }
    }

    private static string Describe(int cp)
    {
        var s = char.ConvertFromUtf32(cp);
        if (!char.IsControl(s[0]) && !char.IsWhiteSpace(s[0])) return $"'{s}'";
        return char.IsWhiteSpace(s[0]) ? "whitespace" : "control";
    }

    // --- Вывод ------------------------------------------------------------------------------------

    private static string CollectionPath => $"Assets/Localizations/{CollectionName}.asset";

    private static void CountLse(LocalizeStringEvent lse, HashSet<string> used, ref int counter)
    {
        if (lse == null) return;
        counter++;
        var key = lse.StringReference.TableEntryReference.Key;
        if (!string.IsNullOrEmpty(key)) used.Add(key);
    }

    private static void Add(HashSet<string> used, LocalizedString ls)
    {
        if (ls == null) return;
        var key = ls.TableEntryReference.Key;
        if (!string.IsNullOrEmpty(key)) used.Add(key);
    }

    private static void LogReport(Report r, IEnumerable<string> locales)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Missing translations report (ТЗ §11.2) ===");
        sb.AppendLine($"Локали: {string.Join(", ", locales)} | ключей в таблице: {r.TableKeys} | используемых: {r.UsedKeys.Count}");
        sb.AppendLine($"Строк проверено: {r.TableStrings} | кодпоинтов: {r.CheckedCodePoints} | шрифтов в объединении: {r.UnionFontCount}");
        sb.AppendLine("-- Источники ключей --");
        foreach (var s in r.Sources) sb.AppendLine($"  • {s}");
        sb.AppendLine($"-- Ключи без перевода: {r.Missing.Count} --");
        foreach (var m in r.Missing) sb.AppendLine($"  ! {m}");
        sb.AppendLine($"-- Кандидаты в сироты: {r.Orphans.Count} --");
        foreach (var o in r.Orphans) sb.AppendLine($"  ? {o}");
        sb.AppendLine($"-- Непокрытые чарсетом символы: {r.UncoveredChars.Count} --");
        foreach (var u in r.UncoveredChars) sb.AppendLine($"  # {u}");
        sb.AppendLine(r.IsGreen ? "ИТОГ: ЗЕЛЁНЫЙ" : "ИТОГ: ЕСТЬ ЗАМЕЧАНИЯ");

        if (r.IsGreen) Debug.Log(sb.ToString());
        else Debug.LogWarning(sb.ToString());
    }

    private static void WriteReportFile(Report r, IEnumerable<string> locales)
    {
        try
        {
            var dir = Path.Combine(Application.dataPath, "../Temp");
            Directory.CreateDirectory(dir);
            var sb = new StringBuilder();
            sb.AppendLine($"locales={string.Join(",", locales)} tableKeys={r.TableKeys} used={r.UsedKeys.Count} green={r.IsGreen}");
            sb.AppendLine($"strings={r.TableStrings} codepoints={r.CheckedCodePoints} unionFonts={r.UnionFontCount}");
            foreach (var s in r.Sources) sb.AppendLine("  src: " + s);
            sb.AppendLine($"missing={r.Missing.Count}");
            foreach (var m in r.Missing) sb.AppendLine("  " + m);
            sb.AppendLine($"orphans={r.Orphans.Count}");
            foreach (var o in r.Orphans) sb.AppendLine("  " + o);
            sb.AppendLine($"uncovered={r.UncoveredChars.Count}");
            foreach (var u in r.UncoveredChars) sb.AppendLine("  " + u);
            File.WriteAllText(Path.Combine(dir, "LocalizationReport.txt"), sb.ToString());
        }
        catch (Exception e) { Debug.LogWarning($"[LocValidate] Не записал отчёт: {e.Message}"); }
    }
}
