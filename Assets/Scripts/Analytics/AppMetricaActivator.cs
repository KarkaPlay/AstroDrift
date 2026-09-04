using Io.AppMetrica;
using UnityEngine;

public static class AppMetricaActivator
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Activate()
    {
        AppMetrica.Activate(new AppMetricaConfig("83958072-9e35-4a72-a83f-fadbb6402037")
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

        // ТЗ §2.1: ровно один раз на установку — сегмент «новички первой недели»
        if (!PlayerPrefs.HasKey("analytics_first_launch"))
        {
            PlayerPrefs.SetInt("analytics_first_launch", 1);
            PlayerPrefs.Save();
            Analytics.Log("app_first_launch");
        }
    }
}