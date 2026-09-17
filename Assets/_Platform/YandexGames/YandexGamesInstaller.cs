#if STORE_YANDEX
using UnityEngine;
using YG;

/// <summary>
/// Регистрация сервисов Яндекс Игр — зеркало RuStoreInstaller.
/// BeforeSceneLoad: регистрируем сервисы и объявляем асинхронную инициализацию (PlatformBoot);
/// по готовности SDK (сейвы загружены, язык известен) — MarkReady().
/// В отличие от RuStore регистрируем и Save (облако вместо PlayerPrefs) и Lifecycle.
/// Локаль по языку игрока НЕ трогаем: за это отвечает AstroDriftLanguageBridge
/// (единый мост YG2 → Unity Locale для всех платформ).
/// </summary>
public static class YandexGamesInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        PlatformBoot.BeginAsyncInit();
        YandexGamesRuntime.Ensure();
        Application.runInBackground = true; // страховка к Player Settings: колбэки рекламы при потере фокуса

        PlatformServices.Register((IAdsService)new YandexGamesAdsService());
        PlatformServices.Register((ISaveService)new YandexGamesSaveService());
        PlatformServices.Register((IAnalyticsService)new YandexGamesAnalyticsService());
        PlatformServices.Register((IPlatformLifecycle)new YandexGamesLifecycle());

        if (YG2.isSDKEnabled) OnSdkReady();
        else YG2.onGetSDKData += OnSdkReady;
    }

    private static void OnSdkReady()
    {
        YG2.onGetSDKData -= OnSdkReady;
        PlatformBoot.MarkReady();
        Debug.Log($"[Platform] Yandex Games ready. lang={YG2.lang}, mobile={YG2.envir.isMobile}");
    }
}
#endif
