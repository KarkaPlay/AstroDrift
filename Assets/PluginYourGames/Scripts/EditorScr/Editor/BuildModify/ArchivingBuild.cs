using System;
using System.IO;
using System.IO.Compression;
using YG.Insides;

namespace YG.EditorScr.BuildModify
{
    public static class ArchivingBuild
    {
        public static void Archiving(string pathToBuiltProject)
        {
#if PLATFORM_WEBGL
            var archiveFileName = $"{pathToBuiltProject}_{ArchivePlatformName()}_Build({BuildLog.GetBuildNumber() + 1})";

            if (File.Exists($"{archiveFileName}.zip"))
            {
                string dateNow = $"{DateTime.Now.Year}-{DateTime.Now.Month}-{DateTime.Now.Day}";
                string timeNow = $"{DateTime.Now.Hour}-{DateTime.Now.Minute}-{DateTime.Now.Second}";
                archiveFileName += $"_{dateNow}_{timeNow}";
            }

            archiveFileName += ".zip";

            ZipFile.CreateFromDirectory(
                pathToBuiltProject,
                archiveFileName,
                CompressionLevel.Optimal,
                false
            );
#endif
        }

        /// <summary>
        /// Имя площадки в имени zip-архива. Приоритет — активный Build Profile (Unity 6):
        /// itch-билд (профиль ItchIO) получит ..._Itch_Build(N).zip, а не ..._YandexGames_...
        /// Для YandexGames имя профиля совпадает с прежним (currentPlatformBaseName) — поведение не меняется.
        /// Фолбэк на платформу YG2 — если Build Profile недоступен.
        /// </summary>
        private static string ArchivePlatformName()
        {
            var profile = UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
            if (profile != null)
            {
                if (profile.name == "ItchIO")
                    return "Itch";
                return profile.name;
            }
            return PlatformSettings.currentPlatformBaseName;
        }
    }
}