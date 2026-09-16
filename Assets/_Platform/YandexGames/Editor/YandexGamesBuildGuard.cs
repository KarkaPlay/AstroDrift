#if STORE_YANDEX || UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;
using YG;

namespace YandexGames.Editor
{
    /// <summary>
    /// Guard WebGL-сборки Яндекс Игр.
    ///
    /// Проблема: InfoYG.Basic.platform — глобальная настройка, а YG2 умеет ровно одну активную
    /// платформу: плагин переписывает ГЛОБАЛЬНЫЕ defines всех таргетов под неё. После RuStore-миграции
    /// глобально стоит YandexMobileAdsPlatform_yg, поэтому сборка ЯИ молча собирается с мобильной
    /// реализацией YG2: JS-мост Яндекс Игр не вызывается, InterstitialAdvShow()/SaveProgress() уезжают
    /// в YMA (на вебе — Dummy-клиент), рекламы и облачных сейвов нет, ошибок в консоли тоже нет.
    ///
    /// Платформа переключается вручную перед каждой сборкой (SettingsYG2.asset → Basic.platform),
    /// поэтому этот хук должен падать, а не пропускать такой билд.
    ///
    /// callbackOrder -999: после YG2 ProcessBuild (-1000), который при autoApplySettings
    /// перезаписывает PlayerSettings выбранной платформы.
    /// </summary>
    public class YandexGamesBuildGuard : IPreprocessBuildWithReport
    {
        private const string StoreDefine = "STORE_YANDEX";
        private const string ForeignPlatformDefine = "YandexMobileAdsPlatform_yg";
        private const string EmptyWebGLDefine = "EmptyWebGLPlatform_yg";
        private const string ExpectedPlatform = "YandexGames";

        public int callbackOrder => -999;

        public void OnPreprocessBuild(BuildReport report)
        {
            var profile = BuildProfile.GetActiveBuildProfile();
            if (profile == null || profile.name != "YandexGames")
                return;

            var defines = EffectiveDefines(NamedBuildTarget.WebGL, profile);

            if (!HasDefine(defines, StoreDefine))
            {
                throw new BuildFailedException(
                    "[YandexGamesBuildGuard] Сборка ЯИ без платформенного слоя Яндекс Игр.\n" +
                    $"В активных defines нет '{StoreDefine}': профиль Assets/Settings/Build Profiles/YandexGames.asset " +
                    "не применяет свои m_ScriptingDefines (нужно m_HasScriptingDefines: 1).\n" +
                    "Без него YandexGamesInstaller / YandexGamesAdsService / YandexGamesSaveService выпадают " +
                    "из компиляции — реклама и облачные сейвы молча не работают.");
            }

            if (HasDefine(defines, EmptyWebGLDefine))
            {
                throw new BuildFailedException(
                    "[YandexGamesBuildGuard] Сборка ЯИ с пустой web-реализацией YG2.\n" +
                    $"В активных defines остался '{EmptyWebGLDefine}' — глобальный набор из " +
                    "ProjectSettings.asset, выставленный плагином под платформу EmptyWebGL (itch.io).\n" +
                    "В этом режиме YG2 не вызывает JS-мосты Яндекс Игр (ysdk/InitGame/облачные сейвы/рекламу), " +
                    "хотя шаблон ЯИ их стабит: рекламы и облачных сейвов не будет, ошибок в консоли тоже.\n" +
                    "Перед сборкой ЯИ: Assets/PluginYourGames/Resources/SettingsYG2.asset → Basic.platform = " +
                    "Assets/PluginYourGames/Platforms/YandexGames/YandexGames.asset, дождаться рекомпиляции, " +
                    "затем вернуть EmptyWebGL перед сборкой itch.io.");
            }

            if (HasDefine(defines, ForeignPlatformDefine))
            {
                throw new BuildFailedException(
                    "[YandexGamesBuildGuard] Сборка ЯИ с мобильной реализацией YG2.\n" +
                    $"В активных defines остался '{ForeignPlatformDefine}' — глобальный набор из " +
                    "ProjectSettings.asset, выставленный плагином под платформу YandexMobileAds.\n" +
                    "Перед сборкой ЯИ: Assets/PluginYourGames/Resources/SettingsYG2.asset → Basic.platform = " +
                    "Assets/PluginYourGames/Platforms/YandexGames/YandexGames.asset, дождаться рекомпиляции, " +
                    "затем вернуть YandexMobileAds перед сборкой RuStore.");
            }

            var platform = YG.InfoYG.Inst()?.Basic?.platform;
            string platformName = platform != null ? platform.NameBase() : "(нет)";

            if (platformName != ExpectedPlatform)
            {
                throw new BuildFailedException(
                    "[YandexGamesBuildGuard] В InfoYG выбрана платформа '" + platformName + "', а нужна '" + ExpectedPlatform + "'.\n" +
                    "Пока платформа не переключена, весь код YandexGamesPlatform_yg выпадает из компиляции: " +
                    "YG2.InterstitialAdvShow() / YG2.SaveProgress() уходят в реализацию YandexMobileAds и на вебе " +
                    "превращаются в no-op (Dummy-клиент) — сборка соберётся, но без рекламы и облачных сейвов.\n" +
                    "Переключи: Assets/PluginYourGames/Resources/SettingsYG2.asset → Basic.platform = " +
                    "Assets/PluginYourGames/Platforms/YandexGames/YandexGames.asset, дождись рекомпиляции.");
            }

            RestoreWebGLSettings();

            Debug.Log("[YandexGamesBuildGuard] WebGL-сборка ЯИ: платформа InfoYG YandexGames, defines на месте.");
        }

        /// <summary>
        /// InfoYG.Basic.platform глобально указывает на EmptyWebGL (itch.io), а ProcessBuild
        /// (YG2, callbackOrder -1000) применяет PlayerSettings именно этой платформы — на КАЖДОЙ
        /// WebGL-сборке. Поэтому здесь возвращаем то, что применила бы платформа YandexGames.
        /// Значения читаем из самого ассета платформы, чтобы не разъезжаться с ним.
        /// </summary>
        private static void RestoreWebGLSettings()
        {
            const string path = "Assets/PluginYourGames/Platforms/YandexGames/YandexGames.asset";
            var platform = AssetDatabase.LoadAssetAtPath<YG.Insides.PlatformSettings>(path);

            if (platform == null)
            {
                Debug.LogWarning("[YandexGamesBuildGuard] Не найден " + path +
                                 " — шаблон/настройки WebGL не восстановлены.");
                return;
            }

            var ps = platform.projectSettings;

            PlayerSettings.WebGL.template = "PROJECT:YandexGames";
            PlayerSettings.runInBackground = ps.runInBackground;
            PlayerSettings.WebGL.compressionFormat = ps.compressionFormat;
            PlayerSettings.WebGL.decompressionFallback = ps.decompressionFallback;
        }

        /// <summary>
        /// Фактический набор defines сборки: глобальные для таргета + defines активного профиля
        /// (Unity объединяет их, а не заменяет).
        /// </summary>
        private static string EffectiveDefines(NamedBuildTarget target, BuildProfile profile)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(target);
            if (profile.scriptingDefines == null)
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
