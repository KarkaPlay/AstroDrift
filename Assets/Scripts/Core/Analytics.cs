using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Тонкая статическая обёртка аналитики (GDD_DeathScreen_Continue §8/§10.7).
/// Сейчас — Debug.Log в едином формате «[Analytics] name {props}»;
/// при интеграции Яндекс AppMetrica меняется только тело Log() — точки вызова не трогаем.
/// </summary>
public static class Analytics
{
    public static void Log(string name, Dictionary<string, object> props = null)
    {
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
    }
}
