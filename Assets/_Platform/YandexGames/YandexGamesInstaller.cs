#if STORE_YANDEX
using UnityEngine;
using UnityEngine.Localization.Settings;
using YG;

/// <summary>
/// Регистрация сервисов Яндекс Игр — зеркало RuStoreInstaller.
/// BeforeSceneLoad: регистрируем сервисы и объявляем асинхронную инициализацию (PlatformBoot);
/// по готовности SDK (сейвы загружены, язык известен) — применяем локаль и MarkReady().
/// В отличие от RuStore регистрируем и Save (облако вместо PlayerPrefs) и Lifecycle.
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
        ApplyLocale(YG2.envir.language); // ⚠️ v1.1: свойства YG2.lang в YG2 v2.0092 НЕТ — язык живёт в envir
        PlatformBoot.MarkReady();
        Debug.Log($"[Platform] Yandex Games ready. lang={YG2.envir.language}, mobile={YG2.envir.isMobile}");
    }

    /// <summary>Язык игрока из Яндекса (двухбуквенный код) → Unity Localization. Нет такой локали — английский.</summary>
    private static void ApplyLocale(string code)
    {
        LocalizationSettings.InitializationOperation.Completed += _ =>
        {
            var locales = LocalizationSettings.AvailableLocales;
            var locale = locales.GetLocale(code) ?? locales.GetLocale("en");
            if (locale != null) LocalizationSettings.SelectedLocale = locale;
        };
    }
}
#endif
