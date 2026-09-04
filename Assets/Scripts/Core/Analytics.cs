using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Io.AppMetrica;
using UnityEngine;

/// <summary>
/// Тонкая статическая обёртка аналитики (GDD_DeathScreen_Continue §8/§10.7,
/// ТЗ_Analytics_AppMetrica §4.1). Точки вызова не трогаем — меняется только тело Log().
/// Android → AppMetrica.ReportEvent (плоский JSON); редактор/не-Android → Debug.Log.
/// </summary>
public static class Analytics
{
    public static void Log(string name, Dictionary<string, object> props = null)
    {
#if UNITY_EDITOR || !UNITY_ANDROID
        var sb = new StringBuilder(name);
        if (props != null && props.Count > 0)
        {
            sb.Append(" {");
            bool first = true;
            foreach (var kv in props)
            {
                if (!first) sb.Append(", ");
                sb.Append(kv.Key).Append(": ").Append(kv.Value);
                first = false;
            }
            sb.Append('}');
        }
        Debug.Log("[Analytics] " + sb);
#else
        // AppMetrica сам буферизует и флешит — своего слоя буферизации нет (ТЗ §4.5)
        if (props == null || props.Count == 0)
        {
            AppMetrica.ReportEvent(name);
            return;
        }
        AppMetrica.ReportEvent(name, ToFlatJson(props));
#endif
    }

#if !UNITY_EDITOR && UNITY_ANDROID
    /// <summary>Плоский JSON: bool → 1/0, float → округление до 0.1 (ТЗ §4.1).</summary>
    private static string ToFlatJson(Dictionary<string, object> props)
    {
        var sb = new StringBuilder(128);
        sb.Append('{');
        bool first = true;
        foreach (var kv in props)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(Escape(kv.Key)).Append("\":");
            switch (kv.Value)
            {
                case bool b: sb.Append(b ? '1' : '0'); break;
                case float f: sb.Append(System.Math.Round(f, 1).ToString(CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(System.Math.Round(d, 1).ToString(CultureInfo.InvariantCulture)); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case string s: sb.Append('"').Append(Escape(s)).Append('"'); break;
                default: sb.Append('"').Append(Escape(kv.Value?.ToString() ?? "")).Append('"'); break;
            }
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static string Escape(string s)
    {
        // Наши проперти — короткие snake_case/enum-строки; экранируем только спецсимволы JSON
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
#endif

    // ——— Профиль игрока (ТЗ §3) ———
    // Точная сигнатура сверкана по установленному плагину Io.AppMetrica:
    // AppMetrica.ReportUserProfile(UserProfile) + Attribute.CustomNumber/CustomString(key).WithValue(v)
    // (класса UserProfileCustomAttributes из примера §4.1 ТЗ в плагине нет).

    /// <summary>Числовой кастом-атрибут профиля (например best_score / runs_total).</summary>
    public static void ProfileSetNumber(string key, double value)
    {
#if UNITY_EDITOR || !UNITY_ANDROID
        Debug.Log($"[Analytics] profile {key} = {value}");
#else
        AppMetrica.ReportUserProfile(new Io.AppMetrica.Profile.UserProfile()
            .Apply(Io.AppMetrica.Profile.Attribute.CustomNumber(key).WithValue(value)));
#endif
    }

    /// <summary>Строковый кастом-атрибут профиля (used_continue_once).</summary>
    public static void ProfileSetString(string key, string value)
    {
#if UNITY_EDITOR || !UNITY_ANDROID
        Debug.Log($"[Analytics] profile {key} = {value}");
#else
        AppMetrica.ReportUserProfile(new Io.AppMetrica.Profile.UserProfile()
            .Apply(Io.AppMetrica.Profile.Attribute.CustomString(key).WithValue(value)));
#endif
    }
}
