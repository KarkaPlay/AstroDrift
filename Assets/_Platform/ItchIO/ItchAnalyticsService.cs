#if STORE_ITCH
using System;
using System.Collections.Generic;
using System.Globalization;
using YG;

/// <summary>
/// Аналитика itch.io: Яндекс.Метрика через модуль Metrica плагина YG2 (AppMetrica под WebGL нет).
/// Номер счётчика и включение модуля живут в InfoYG (SettingsYG2.asset / EmptyWebGL.asset);
/// ym() с tag.js инжектит YandexMetrica.jslib модуля — своего JS-моста у itch нет.
/// Формат значений повторяет AppMetricaAnalyticsService: bool → 1/0, дробные → округление 0.1,
/// InvariantCulture. Все ~30 точек Analytics.Log работают без изменений.
/// Профильных атрибутов у Метрики нет — no-op (best_score / runs_total / used_continue_once на вебе теряются).
/// Важно: JsonUtils.ToJson(Dictionary<string, string>) выбрасывает пары с пустым значением —
/// событие с пустой строкой уедет без этого параметра (для Метрики это ок).
/// </summary>
public sealed class ItchAnalyticsService : IAnalyticsService
{
    // Платформа в каждом событии: владелец сегментирует отчёты по площадке
    // (счётчик Метрики общий с Яндекс Играми — разрез по этому параметру).
    private const string PlatformTag = "itch";

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
