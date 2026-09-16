#if STORE_YANDEX
using System;
using System.Collections.Generic;
using System.Globalization;
using YG;

/// <summary>
/// Аналитика на вебе: Яндекс.Метрика через модуль Metrica YG2 (AppMetrica под WebGL нет).
/// Формат значений повторяет AppMetricaAnalyticsService: bool → 1/0, дробные → округление 0.1,
/// InvariantCulture. Все ~30 точек Analytics.Log работают без изменений.
/// Профильных атрибутов у Метрики нет — no-op (best_score / runs_total / used_continue_once на вебе теряются).
/// </summary>
public sealed class YandexGamesAnalyticsService : IAnalyticsService
{
    // Платформа в каждом событии: владелец сегментирует отчёты по площадке
    // (счётчик Метрики общий с itch.io — разрез по этому параметру).
    private const string PlatformTag = "yandex";

    public void LogEvent(string eventName)
        => YG2.MetricaSend(eventName, new Dictionary<string, string> { { "platform", PlatformTag } });

    public void LogEvent(string eventName, Dictionary<string, object> parameters)
    {
        var p = new Dictionary<string, string>((parameters?.Count ?? 0) + 1)
        {
            ["platform"] = PlatformTag
        };
        if (parameters == null || parameters.Count == 0) { YG2.MetricaSend(eventName, p); return; }
        foreach (var kv in parameters)
            p[kv.Key] = kv.Value switch
            {
                bool b => b ? "1" : "0",
                float f => Math.Round(f, 1).ToString(CultureInfo.InvariantCulture),
                double d => Math.Round(d, 1).ToString(CultureInfo.InvariantCulture),
                IFormattable x => x.ToString(null, CultureInfo.InvariantCulture),
                _ => kv.Value?.ToString() ?? "",
            };
        YG2.MetricaSend(eventName, p);
    }

    public void ProfileSetNumber(string key, double value) { }
    public void ProfileSetString(string key, string value) { }
}
#endif
