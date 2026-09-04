using System.Collections.Generic;

/// <summary>
/// Тонкий статический фасад аналитики (GDD_DeathScreen_Continue §8/§10.7,
/// ТЗ_Analytics_AppMetrica §4.1). ~30 точек вызова не тронуты — тело форвардит
/// в PlatformServices.Analytics: на RuStore это AppMetrica (плоский JSON),
/// в редакторе/без платформы — NullAnalyticsService (Debug.Log, как раньше).
/// </summary>
public static class Analytics
{
    public static void Log(string name, Dictionary<string, object> props = null)
    {
        if (props == null) PlatformServices.Analytics.LogEvent(name);
        else PlatformServices.Analytics.LogEvent(name, props);
    }

    /// <summary>Числовой кастом-атрибут профиля (например best_score / runs_total).</summary>
    public static void ProfileSetNumber(string key, double value)
        => PlatformServices.Analytics.ProfileSetNumber(key, value);

    /// <summary>Строковый кастом-атрибут профиля (used_continue_once).</summary>
    public static void ProfileSetString(string key, string value)
        => PlatformServices.Analytics.ProfileSetString(key, value);
}
