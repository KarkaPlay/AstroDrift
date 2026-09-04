#if STORE_RUSTORE
using UnityEngine;

/// <summary>
/// Регистрация платформенных сервисов RuStore (Yandex Mobile Ads + AppMetrica).
/// BeforeSceneLoad — раньше любого Awake() сцены: к первому вызову PlatformServices.*
/// всё зарегистрировано. Save не регистрируем: PlayerPrefsSaveService уже стоит по умолчанию.
/// </summary>
public static class RuStoreInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        var go = new GameObject("[Platform] YandexMobileAds");
        UnityEngine.Object.DontDestroyOnLoad(go);
        PlatformServices.Register(go.AddComponent<YandexMobileAdsService>());
        PlatformServices.Register(new AppMetricaAnalyticsService());
    }
}
#endif
