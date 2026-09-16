#if STORE_ITCH
using UnityEngine;

/// <summary>
/// Регистрация сервисов itch.io: аналитика (Метрика через модуль Metrica плагина YG2) + политика «рекламы нет».
/// Save/Lifecycle берутся из Defaults (PlayerPrefs / Null) — регистрировать не нужно.
/// Ads регистрируется ЯВНО: иначе PlatformServices.Ads лениво создал бы NullAdsService
/// с фейк-показом и фейк-наградой (в шипнутой сборке — награды без рекламы).
/// Синхронная инициализация: PlatformBoot.IsReady = true по умолчанию, Bootstrap.Build()
/// выполнится в первом Awake без ожидания.
/// </summary>
public static class ItchInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        PlatformServices.Register((IAnalyticsService)new ItchAnalyticsService());
        PlatformServices.Register((IAdsService)new ItchAdsService());
        Debug.Log("[Platform] itch.io ready. Analytics: Yandex Metrika (YG2 Metrica module). Ads: disabled.");
    }
}
#endif
