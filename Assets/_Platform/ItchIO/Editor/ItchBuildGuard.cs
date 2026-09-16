#if STORE_ITCH || UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;
using YG;

namespace ItchIO.Editor
{
    /// <summary>
    /// Guard itch-сборки от перезаписи настроек плагином YG2.
    ///
    /// Проблема 1: ProcessBuild (YG2, callbackOrder -1000) в OnPreprocessBuild вызывает
    /// ApplyProjectSettings() выбранной в InfoYG платформы на КАЖДОЙ WebGL-сборке —
    /// включая itch: шаблон → PROJECT:EmptyWebGL, runInBackground → off,
    /// decompressionFallback → off, stripping → High.
    ///
    /// Проблема 2 (проверяется здесь): платформа InfoYG должна быть EmptyWebGL.
    /// Если осталась YandexGames, у нас:
    ///   • jsLib-мосты Яндекс Игр в билде, а шаблон ItchIO их не стабит → ReferenceError;
    ///   • InitSDKComplete-получатель без отправителя и наоборот.
    /// Если осталась YandexMobileAds — на WebGL реклама уходит в Dummy-клиент, а
    /// InterstitialAdv/RewardedAdv-модули тянут YMA-путь на вебе.
    ///
    /// Проблема 3: если профиль ItchIO не применит STORE_ITCH, весь код
    /// Assets/_Platform/ItchIO выпадает из компиляции и PlatformServices.Ads молча
    /// деградирует до NullAdsService — заглушки редактора с ФЕЙК-наградой после 2 секунд.
    /// На itch это означало бы «награду за просмотр несуществующей рекламы».
    ///
    /// Решение: этот хук с callbackOrder -999 (после YG2) возвращает настройки
    /// активного профиля ItchIO и ЖЁСТКО падает на любом расхождении.
    /// Глобальные PlayerSettings при этом меняются только в памяти процесса сборки —
    /// в YAML профиля ничего не пишется.
    /// </summary>
    public class ItchBuildGuard : IPreprocessBuildWithReport
    {
        private const string StoreDefine = "STORE_ITCH";
        private const string ExpectedPlatform = "EmptyWebGL";
        private const string MobAdsDefine = "YandexMobileAdsPlatform_yg";
        private const string YandexGamesDefine = "YandexGamesPlatform_yg";

        public int callbackOrder => -999;

        public void OnPreprocessBuild(BuildReport report)
        {
            var profile = BuildProfile.GetActiveBuildProfile();
            if (profile == null || profile.name != "ItchIO")
                return;

            CheckPlatform();

            // Шаблон — наш, с кодом Метрики и мостом YG2Instance
            PlayerSettings.WebGL.template = "PROJECT:ItchIO";
            // §2.2 ТЗ — то, что YG2 только что перезаписал
            PlayerSettings.runInBackground = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);

            Debug.Log("[ItchBuildGuard] itch-сборка: настройки профиля ItchIO восстановлены после ApplyProjectSettings YG2.");
        }

        private static void CheckPlatform()
        {
            var profile = BuildProfile.GetActiveBuildProfile();
            var defines = EffectiveDefines(NamedBuildTarget.WebGL, profile);

            if (!HasDefine(defines, StoreDefine))
            {
                throw new BuildFailedException(
                    "[ItchBuildGuard] itch-сборка без платформенного слоя itch.io.\n" +
                    $"В активных defines нет '{StoreDefine}': профиль Assets/Settings/Build Profiles/ItchIO.asset " +
                    "не применяет свои m_ScriptingDefines (нужно m_HasScriptingDefines: 1).\n" +
                    "Без него ItchInstaller / ItchAdsService / ItchAnalyticsService выпадают из компиляции, " +
                    "а PlatformServices.Ads молча падает в NullAdsService — заглушку редактора с ФЕЙК-наградой " +
                    "после 2 секунд. На itch игрок получал бы награду за просмотр несуществующей рекламы.");
            }

            if (HasDefine(defines, MobAdsDefine))
            {
                throw new BuildFailedException(
                    "[ItchBuildGuard] itch-сборка с мобильной реализацией YG2.\n" +
                    $"В активных defines остался '{MobAdsDefine}' (набор от платформы YandexMobileAds).\n" +
                    "На WebGL YMA-путь — Dummy-клиент: сборка соберётся, но реклама и облачные сейвы будут no-op.\n" +
                    "Переключи платформу: Assets/PluginYourGames/Resources/SettingsYG2.asset → Basic.platform = " +
                    "Assets/PluginYourGames/Platforms/EmptyWebGL/EmptyWebGL.asset, дождись рекомпиляции, " +
                    "затем вернуть YandexMobileAds перед сборкой RuStore.");
            }

            if (HasDefine(defines, YandexGamesDefine))
            {
                throw new BuildFailedException(
                    "[ItchBuildGuard] itch-сборка с реализацией YG2 для Яндекс Игр.\n" +
                    $"В активных defines остался '{YandexGamesDefine}'.\n" +
                    "В этом режиме YG2 зовёт JS-мосты Яндекс Игр (SaveCloud_js, RewardedAdvShow_js, " +
                    "StickyAdActivity_js, RequestAuth_js), которых в шаблоне itch.io нет — это ReferenceError " +
                    "на вебе и остановка игры после загрузки.\n" +
                    "Переключи платформу: Assets/PluginYourGames/Resources/SettingsYG2.asset → Basic.platform = " +
                    "Assets/PluginYourGames/Platforms/EmptyWebGL/EmptyWebGL.asset, дождись рекомпиляции.");
            }

            var platform = InfoYG.Inst()?.Basic?.platform;
            string platformName = platform != null ? platform.NameBase() : "(нет)";

            if (platformName != ExpectedPlatform)
            {
                throw new BuildFailedException(
                    "[ItchBuildGuard] В InfoYG выбрана платформа '" + platformName + "', а нужна '" + ExpectedPlatform + "'.\n" +
                    $"itch-сборка компилируется с '{ExpectedPlatform}Platform' ('{ExpectedPlatform}Platform_yg'): " +
                    "эта реализация не требует JS-API площадки (локальные сейвы через localStorage, " +
                    "реклама выключена на уровне PlatformServices).\n" +
                    "Пока платформа не переключена, в билд попадает чужая реализация YG2 со своими jslib-мостами, " +
                    "для которых в шаблоне Assets/WebGLTemplates/ItchIO/index.html нет стабов.\n" +
                    "Переключи: Assets/PluginYourGames/Resources/SettingsYG2.asset → Basic.platform = " +
                    "Assets/PluginYourGames/Platforms/EmptyWebGL/EmptyWebGL.asset, дождись рекомпиляции.");
            }

            Debug.Log("[ItchBuildGuard] itch-сборка: платформа InfoYG EmptyWebGL, defines на месте.");
        }

        /// <summary>
        /// Фактический набор defines сборки: глобальные для таргета + defines активного профиля
        /// (Unity объединяет их, а не заменяет).
        /// </summary>
        private static string EffectiveDefines(NamedBuildTarget target, BuildProfile profile)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(target);
            if (profile == null || profile.scriptingDefines == null)
                return defines;

            foreach (var d in profile.scriptingDefines)
                if (!HasDefine(defines, d))
                    defines = string.IsNullOrEmpty(defines) ? d : defines + ";" + d;

            return defines;
        }

        private static bool HasDefine(string defines, string define)
        {
            if (string.IsNullOrEmpty(defines)) return false;
            foreach (var d in defines.Split(';'))
                if (d.Trim() == define) return true;
            return false;
        }
    }
}
#endif
