#if STORE_RUSTORE || UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;
using YG;

namespace RuStore.Editor
{
    /// <summary>
    /// Guard RuStore-сборки: молчаливый выпуск APK с заглушечной рекламой недопустим.
    ///
    /// Проблема: defines и платформа InfoYG выбираются на этапе КОМПИЛЯЦИИ, а не сборки.
    /// Если в InfoYG выбрана платформа YandexGames, плагин вешает asmdef на YandexMobileAds
    /// (Platforms/YandexMobileAds/SDK/YandexMobileAdsPlatform.asmdef: "includePlatforms": ["XboxOne"])
    /// и весь код YMA исчезает из компиляции — даже при правильных defines реклама молча
    /// становится no-op, а выручка нулевой. Поэтому здесь мы ЖЁСТКО падаем.
    ///
    /// callbackOrder -999: после YG2 ProcessBuild (-1000), который при autoApplySettings
    /// перезаписывает PlayerSettings выбранной платформы.
    /// </summary>
    public class RuStoreBuildGuard : IPreprocessBuildWithReport
    {
        private const string StoreDefine = "STORE_RUSTORE";
        private const string PlatformDefine = "YandexMobileAdsPlatform_yg";
        private const string ExpectedPlatform = "YandexMobileAds";

        public int callbackOrder => -999;

        public void OnPreprocessBuild(BuildReport report)
        {
            var profile = BuildProfile.GetActiveBuildProfile();
            if (profile == null || profile.name != "RuStore")
                return;

            // ВАЖНО: active defines сборки = глобальные (ProjectSettings.asset) + m_ScriptingDefines профиля.
            // STORE_RUSTORE лежит только в профиле, поэтому читать одни глобальные defines нельзя —
            // guard падал бы на каждой корректной RuStore-сборке.
            var defines = EffectiveDefines(NamedBuildTarget.Android, profile);

            if (!HasDefine(defines, StoreDefine) || !HasDefine(defines, PlatformDefine))
            {
                throw new BuildFailedException(
                    "[RuStoreBuildGuard] RuStore-сборка без рабочей рекламы.\n" +
                    $"В активных defines нет '{StoreDefine}' и/или '{PlatformDefine}'.\n" +
                    "Проверь m_ScriptingDefines в Assets/Settings/Build Profiles/RuStore.asset " +
                    "(нужно m_HasScriptingDefines: 1; Unity объединяет defines профиля с глобальными).");
            }

            var platform = YG.InfoYG.Inst()?.Basic?.platform;
            string platformName = platform != null ? platform.NameBase() : "(нет)";

            if (platformName != ExpectedPlatform)
            {
                throw new BuildFailedException(
                    "[RuStoreBuildGuard] В InfoYG выбрана платформа '" + platformName + "', а нужна '" + ExpectedPlatform + "'.\n" +
                    "Пока платформа не переключена, плагин снимает asmdef с YandexMobileAds и ВЕСЬ код рекламы " +
                    "(interstitial/rewarded/banner) выпадает из компиляции — сборка соберётся, но показов не будет.\n" +
                    "Перед сборкой RuStore: Assets/PluginYourGames/Resources/SettingsYG2.asset → Basic.platform = " +
                    "Assets/PluginYourGames/Platforms/YandexMobileAds/YandexMobileAds.asset, дождаться рекомпиляции, " +
                    "затем вернуть YandexGames перед WebGL-сборкой.");
            }

            var asmdefPath = "Assets/PluginYourGames/Platforms/YandexMobileAds/SDK/YandexMobileAdsPlatform.asmdef";
            if (File.Exists(asmdefPath) && File.ReadAllText(asmdefPath).Contains("XboxOne"))
            {
                throw new BuildFailedException(
                    "[RuStoreBuildGuard] " + asmdefPath + " всё ещё отрезает YandexMobileAds от компиляции " +
                    "(includePlatforms: XboxOne). Выбери платформу YandexMobileAds в InfoYG, чтобы плагин снял asmdef.");
            }

            Debug.Log("[RuStoreBuildGuard] RuStore-сборка: платформа InfoYG YandexMobileAds, defines на месте.");
        }

        /// <summary>
        /// Фактический набор defines сборки: глобальные для таргета + defines активного профиля.
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
