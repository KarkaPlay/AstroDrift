using System.Collections.Generic;

/// <summary>
/// Игровой контракт аналитики. Profile-методы обязательны: игра сегментирует
/// best_score, runs_total, used_continue_once (ScoreManager, GameManager, GameUI).
/// </summary>
public interface IAnalyticsService
{
    void LogEvent(string eventName);
    void LogEvent(string eventName, Dictionary<string, object> parameters);
    void ProfileSetNumber(string key, double value);
    void ProfileSetString(string key, string value);
}
