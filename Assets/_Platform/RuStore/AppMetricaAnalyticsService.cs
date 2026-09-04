#if STORE_RUSTORE
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Io.AppMetrica;
using UnityEngine;

/// <summary>
/// Аналитика RuStore: AppMetrica. Порт Android-ветки бывшего Analytics.cs
/// (плоский JSON, profile-атрибуты) + активация SDK.
/// Activate — ровно один раз, в конструкторе (инстанс создаёт RuStoreInstaller).
/// Сигнатуры UserProfile свёрены с установленным io.appmetrica.analytics 6.10.0.
/// </summary>
public class AppMetricaAnalyticsService : IAnalyticsService
{
    private const string ApiKey = "83958072-9e35-4a72-a83f-fadbb6402037";

    public AppMetricaAnalyticsService()
    {
        AppMetrica.Activate(new AppMetricaConfig(ApiKey)
        {
            // Сбор крэшей
            CrashReporting = true,

            // Таймаут сессии (в секундах)
            // Если игрок свернул игру на 10+ сек — считается новая сессия
            SessionTimeout = 10,

            // Не собирать геолокацию (для гиперказуалки не нужно)
            LocationTracking = false,

            // Включить логи в дебаг-билдах
            Logs = Debug.isDebugBuild,

            // Для нового приложения — ставим true
            FirstActivationAsUpdate = false,

            // Разрешить отправку данных
            DataSendingEnabled = true,
        });
    }

    public void LogEvent(string eventName)
    {
        // AppMetrica сам буферизует и флешит — своего слоя буферизации нет (ТЗ §4.5)
        AppMetrica.ReportEvent(eventName);
    }

    public void LogEvent(string eventName, Dictionary<string, object> parameters)
    {
        if (parameters == null || parameters.Count == 0)
        {
            AppMetrica.ReportEvent(eventName);
            return;
        }
        AppMetrica.ReportEvent(eventName, ToFlatJson(parameters));
    }

    // ——— Профиль игрока (ТЗ §3) ———
    // Точная сигнатура сверена по установленному плагину Io.AppMetrica:
    // AppMetrica.ReportUserProfile(UserProfile) + Attribute.CustomNumber/CustomString(key).WithValue(v)

    /// <summary>Числовой кастом-атрибут профиля (например best_score / runs_total).</summary>
    public void ProfileSetNumber(string key, double value)
    {
        AppMetrica.ReportUserProfile(new Io.AppMetrica.Profile.UserProfile()
            .Apply(Io.AppMetrica.Profile.Attribute.CustomNumber(key).WithValue(value)));
    }

    /// <summary>Строковый кастом-атрибут профиля (used_continue_once).</summary>
    public void ProfileSetString(string key, string value)
    {
        AppMetrica.ReportUserProfile(new Io.AppMetrica.Profile.UserProfile()
            .Apply(Io.AppMetrica.Profile.Attribute.CustomString(key).WithValue(value)));
    }

    // ——— Формат событий ———

    /// <summary>Плоский JSON: bool → 1/0, float → округление до 0.1 (ТЗ §4.1). Порт 1:1 из Analytics.cs.</summary>
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
}
#endif
