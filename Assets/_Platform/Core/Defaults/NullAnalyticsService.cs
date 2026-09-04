using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Заглушка аналитики: события и profile-атрибуты — в консоль.
/// Формат лога идентичен прежнему editor-режиму фасада Analytics:
/// «[Analytics] name { k: v, ... }» и «[Analytics] profile key = value».
/// </summary>
public class NullAnalyticsService : IAnalyticsService
{
    public void LogEvent(string eventName)
    {
        Debug.Log("[Analytics] " + eventName);
    }

    public void LogEvent(string eventName, Dictionary<string, object> parameters)
    {
        var sb = new System.Text.StringBuilder(eventName);
        if (parameters != null && parameters.Count > 0)
        {
            sb.Append(" {");
            bool first = true;
            foreach (var kv in parameters)
            {
                if (!first) sb.Append(", ");
                sb.Append(kv.Key).Append(": ").Append(kv.Value);
                first = false;
            }
            sb.Append('}');
        }
        Debug.Log("[Analytics] " + sb);
    }

    public void ProfileSetNumber(string key, double value)
        => Debug.Log($"[Analytics] profile {key} = {value}");

    public void ProfileSetString(string key, string value)
        => Debug.Log($"[Analytics] profile {key} = {value}");
}
