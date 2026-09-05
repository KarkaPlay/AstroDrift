// Assets/Plugins/Project2Markdown/Editor/CodebaseExporterContextMenu.cs
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CodebaseExporter
{
    public static class CodebaseExporterContextMenu
    {
        [MenuItem("Assets/Export to Markdown/Copy Selected to Clipboard", false, 2000)]
        private static void CopySelectedToClipboard()
        {
            var selected = Selection.objects;
            if (selected == null || selected.Length == 0) return;

            var sb = new StringBuilder(4096);
            sb.AppendLine("# Selected Files Export");
            sb.AppendLine();

            int fileCount = 0;

            foreach (var obj in selected)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                string fullPath = Path.GetFullPath(path);

                if (Directory.Exists(fullPath))
                {
                    var files = Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories)
                        .Where(f => !f.EndsWith(".meta"))
                        .OrderBy(f => f);

                    foreach (var file in files)
                    {
                        AppendFile(sb, file);
                        fileCount++;
                    }
                }
                else if (File.Exists(fullPath))
                {
                    AppendFile(sb, fullPath);
                    fileCount++;
                }
            }

            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log($"[CodebaseExporter] Copied {fileCount} files ({sb.Length:N0} chars) to clipboard");
        }

        [MenuItem("Assets/Export to Markdown/Open Exporter Window", false, 2001)]
        private static void OpenExporterWindow()
        {
            CodebaseExporterWindow.ShowWindow();
        }

        private static void AppendFile(StringBuilder sb, string fullPath)
        {
            string dataPath = Application.dataPath;
            string relativePath = fullPath.StartsWith(dataPath)
                ? "Assets" + fullPath.Substring(dataPath.Length).Replace('\\', '/')
                : fullPath;

            string ext = Path.GetExtension(fullPath).ToLower();
            sb.Append("## `").Append(relativePath).AppendLine("`");
            sb.AppendLine();

            if (IsTextFile(ext))
            {
                string content = File.ReadAllText(fullPath);
                string lang = GetLang(ext);
                sb.Append("```").AppendLine(lang);
                sb.AppendLine(content);
                sb.AppendLine("```");
            }
            else
            {
                var fi = new FileInfo(fullPath);
                sb.Append("> Binary file (").Append(fi.Length.ToString("N0")).AppendLine(" bytes)");
            }

            sb.AppendLine();
        }

        private static bool IsTextFile(string ext)
        {
            return ext switch
            {
                ".cs" or ".js" or ".json" or ".xml" or ".yaml" or ".yml" or ".txt" or ".md"
                or ".shader" or ".cginc" or ".hlsl" or ".compute" or ".css" or ".html"
                or ".uxml" or ".uss" or ".asmdef" or ".cfg" or ".ini" => true,
                _ => false
            };
        }

        private static string GetLang(string ext) => ext switch
        {
            ".cs" => "csharp",
            ".json" => "json",
            ".xml" or ".uxml" => "xml",
            ".yaml" or ".yml" => "yaml",
            ".shader" or ".hlsl" or ".cginc" or ".compute" => "hlsl",
            ".html" or ".htm" => "html",
            ".css" or ".uss" => "css",
            _ => ""
        };
    }
}