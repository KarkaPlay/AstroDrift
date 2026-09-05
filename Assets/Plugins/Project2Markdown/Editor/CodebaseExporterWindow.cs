using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CodebaseExporter
{
    public class CodebaseExporterWindow : EditorWindow
    {
        private TreeNode rootNode;
        private Vector2 treeScrollPos;
        private Vector2 previewScrollPos;

        // Preview data — pre-split lines for virtualized rendering
        private string[] previewLines;
        private string[] _previewLinesLower; // FIX #6: cached lowercase
        private string previewFullText;
        private int previewTotalChars;
        private bool hasPreview;

        private ExportSettings settings = new ExportSettings();
        private int selectedTabIndex;
        private readonly string[] tabNames = { "📂 Files", "⚙ Settings", "👁 Preview" };

        private string searchFilter = "";
        private string statusMessage = "";
        private MessageType statusType = MessageType.Info;
        private double statusTime;

        // Cache
        private readonly Dictionary<string, Texture> iconCache = new Dictionary<string, Texture>();
        private readonly Dictionary<string, long> fileSizeCache = new Dictionary<string, long>();
        private readonly Dictionary<string, string> relativePathCache = new Dictionary<string, string>();
        private List<TreeNode> flatVisibleNodes;
        private bool flatListDirty = true;

        // Selection stats
        private int cachedSelectedCount;
        private long cachedSelectedSize;
        private int cachedEstimatedTokens;
        private bool statsDirty = true;

        // FIX #12: cached stat labels (avoid string allocation each frame)
        private string _statsFilesLabel = "📄 0 files";
        private string _statsSizeLabel = "💾 0 B";
        private string _statsTokensLabel = "🔤 ~0 tokens";

        // Deferred menu
        private GenericMenu pendingMenu;
        private Rect pendingMenuRect;

        // Profiles
        private string[] profileNames = Array.Empty<string>();
        private int selectedProfileIndex;
        private string newProfileName = "";

        // Tree virtualization
        private const float ROW_HEIGHT = 20f;
        private const float INDENT_WIDTH = 18f;

        // Preview virtualization
        private const float PREVIEW_LINE_HEIGHT = 15f;
        private const float PREVIEW_GUTTER_WIDTH = 50f;
        private GUIStyle previewLineStyle;
        private Font _monoFont; // FIX #9: stored separately for proper disposal
        private string previewSearchText = "";
        private List<int> previewSearchResults = new List<int>();
        private readonly HashSet<int> _previewSearchSet = new HashSet<int>(); // FIX #2: O(1) lookup
        private int previewSearchIndex;

        // Extension stats
        private Dictionary<string, int> extensionCounts = new Dictionary<string, int>();

        // FIX #11: cached colors to avoid struct allocation each frame
        private static readonly Color s_HoverColor = new Color(1f, 1f, 1f, 0.04f);
        private static readonly Color s_UncheckedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        private static readonly Color s_AltRowColor = new Color(0f, 0f, 0f, 0.03f);
        private static readonly Color s_SearchHiColor = new Color(1f, 0.9f, 0.2f, 0.25f);
        private static readonly Color s_SearchHiColorBg = new Color(1f, 0.9f, 0.2f, 0.10f);
        private static readonly Color s_PreviewBgDark = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color s_PreviewBgLight = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color s_TextDark = Color.white;
        private static readonly Color s_TextLight = Color.white;

        [MenuItem("Tools/Codebase Exporter %#e")]
        public static void ShowWindow()
        {
            var window = GetWindow<CodebaseExporterWindow>("Codebase Exporter");
            window.minSize = new Vector2(550, 650);
            window.Show();
        }

        private void OnEnable()
        {
            LoadSettings();
            RefreshTree();
            RefreshProfiles();
        }

        private void OnDisable()
        {
            SaveSettings();

            // FIX #9: properly destroy created font to prevent Unity object leaks
            if (_monoFont != null)
            {
                DestroyImmediate(_monoFont);
                _monoFont = null;
            }
            previewLineStyle = null;
        }

        private void EnsurePreviewStyle()
        {
            if (previewLineStyle != null) return;

            previewLineStyle = new GUIStyle(EditorStyles.label)
            {
                richText = false,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(4, 4, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                fontSize = 12
            };

            if (_monoFont == null)
            {
                _monoFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Consolas", "Courier New", "Monaco", "Menlo", "DejaVu Sans Mono" }, 12);
            }
            if (_monoFont != null)
                previewLineStyle.font = _monoFont;

            previewLineStyle.normal.textColor = EditorGUIUtility.isProSkin ? s_TextDark : s_TextLight;
        }

        private void OnGUI()
        {
            HandleKeyboardShortcuts();
            HandleDragAndDrop();

            DrawToolbar();
            DrawStatsBar();

            selectedTabIndex = GUILayout.Toolbar(selectedTabIndex, tabNames, GUILayout.Height(28));

            switch (selectedTabIndex)
            {
                case 0: DrawFileSelectionTab(); break;
                case 1: DrawSettingsTab(); break;
                case 2: DrawPreviewTab(); break;
            }

            DrawFooter();

            if (!string.IsNullOrEmpty(statusMessage) && EditorApplication.timeSinceStartup - statusTime > 4)
            {
                statusMessage = "";
                Repaint();
            }

            if (pendingMenu != null)
            {
                pendingMenu.DropDown(pendingMenuRect);
                pendingMenu = null;
            }
        }

        // ==================== KEYBOARD SHORTCUTS ====================
        private void HandleKeyboardShortcuts()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;

            if (e.control || e.command)
            {
                switch (e.keyCode)
                {
                    case KeyCode.A:
                        SetAllChecked(rootNode, true);
                        e.Use();
                        break;
                    case KeyCode.D:
                        SetAllChecked(rootNode, false);
                        e.Use();
                        break;
                    case KeyCode.I:
                        InvertSelection(rootNode);
                        MarkDirty();
                        e.Use();
                        break;
                    case KeyCode.F:
                        GUI.FocusControl("SearchField");
                        e.Use();
                        break;
                    case KeyCode.C when e.shift:
                        CopyToClipboard();
                        e.Use();
                        break;
                }
            }

            if (e.keyCode == KeyCode.F5)
            {
                RefreshTree();
                e.Use();
            }
        }

        // ==================== DRAG & DROP ====================
        private void HandleDragAndDrop()
        {
            Event e = Event.current;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;

            if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;

                if (e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (string path in DragAndDrop.paths)
                        SelectByAssetPath(rootNode, path);
                    MarkDirty();
                    SetStatus($"Added {DragAndDrop.paths.Length} items from drag & drop", MessageType.Info);
                }
                e.Use();
            }
        }

        private void SelectByAssetPath(TreeNode node, string assetPath)
        {
            if (node == null) return;

            string nodeRelPath = GetRelativePath(node.fullPath);

            if (nodeRelPath.Equals(assetPath, StringComparison.OrdinalIgnoreCase) ||
                node.fullPath.Replace('\\', '/').EndsWith(assetPath.Replace('\\', '/')))
            {
                SetNodeChecked(node, true);
                return;
            }

            if (nodeRelPath.StartsWith(assetPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                SetNodeChecked(node, true);
                return;
            }

            foreach (var child in node.children)
                SelectByAssetPath(child, assetPath);
        }

        // ==================== TOOLBAR ====================
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("↻ Refresh", EditorStyles.toolbarButton, GUILayout.Width(65)))
            {
                RefreshTree();
                SetStatus("Tree refreshed (F5)", MessageType.Info);
            }

            DrawSelectionDropdown();

            GUILayout.FlexibleSpace();

            GUI.SetNextControlName("SearchField");
            string newFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField,
                GUILayout.Width(220));

            if (newFilter != searchFilter)
            {
                searchFilter = newFilter;
                flatListDirty = true;
            }

            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                searchFilter = "";
                flatListDirty = true;
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectionDropdown()
        {
            if (GUILayout.Button("Select ▾", EditorStyles.toolbarDropDown, GUILayout.Width(65)))
            {
                Rect buttonRect = GUILayoutUtility.GetLastRect();

                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("All (Ctrl+A)"), false, () => { SetAllChecked(rootNode, true); MarkDirty(); });
                menu.AddItem(new GUIContent("None (Ctrl+D)"), false, () => { SetAllChecked(rootNode, false); MarkDirty(); });
                menu.AddItem(new GUIContent("Invert (Ctrl+I)"), false, () => { InvertSelection(rootNode); MarkDirty(); });

                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Scripts Only"), false, () =>
                {
                    SetAllChecked(rootNode, false);
                    SelectByExtensions(rootNode, settings.scriptExtensions);
                    MarkDirty();
                });

                menu.AddSeparator("By Extension/");

                RebuildExtensionCounts();
                foreach (var kvp in extensionCounts.OrderByDescending(x => x.Value))
                {
                    string ext = kvp.Key;
                    int count = kvp.Value;
                    menu.AddItem(new GUIContent($"By Extension/{ext} ({count})"), false, () =>
                    {
                        SelectByExtensions(rootNode, new HashSet<string> { ext });
                        MarkDirty();
                    });
                }

                pendingMenu = menu;
                pendingMenuRect = buttonRect;
            }

            if (GUILayout.Button("Fold ▾", EditorStyles.toolbarDropDown, GUILayout.Width(55)))
            {
                Rect buttonRect = GUILayoutUtility.GetLastRect();

                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Expand All"), false, () => { SetAllExpanded(rootNode, true); flatListDirty = true; });
                menu.AddItem(new GUIContent("Collapse All"), false, () => { SetAllExpanded(rootNode, false); flatListDirty = true; });
                menu.AddItem(new GUIContent("Expand Selected"), false, () => { ExpandSelected(rootNode); flatListDirty = true; });

                pendingMenu = menu;
                pendingMenuRect = buttonRect;
            }
        }

        // ==================== STATS BAR ====================
        private void DrawStatsBar()
        {
            if (statsDirty)
            {
                RecalculateStats();
                statsDirty = false;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // FIX #12: use cached label strings
            GUILayout.Label(_statsFilesLabel, GUILayout.Width(80));
            GUILayout.Label(_statsSizeLabel, GUILayout.Width(100));
            GUILayout.Label(_statsTokensLabel, GUILayout.Width(120));

            GUILayout.FlexibleSpace();
            GUILayout.Label("Drag files from Project window here", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void RecalculateStats()
        {
            cachedSelectedCount = 0;
            cachedSelectedSize = 0;
            cachedEstimatedTokens = 0;
            CountStatsRecursive(rootNode);

            // FIX #12: rebuild labels only on actual changes
            _statsFilesLabel = $"📄 {cachedSelectedCount} files";
            _statsSizeLabel = $"💾 {FormatFileSize(cachedSelectedSize)}";
            _statsTokensLabel = $"🔤 ~{cachedEstimatedTokens:N0} tokens";
        }

        private void CountStatsRecursive(TreeNode node)
        {
            if (node == null) return;
            if (!node.isDirectory && node.isChecked)
            {
                cachedSelectedCount++;
                long size = GetFileSize(node.fullPath);
                cachedSelectedSize += size;
                if (IsTextFile(node.extension))
                    cachedEstimatedTokens += (int)(size / 4);
                else
                    cachedEstimatedTokens += 50;
            }
            foreach (var child in node.children)
                CountStatsRecursive(child);
        }

        // ==================== FILE SELECTION TAB ====================
        private void DrawFileSelectionTab()
        {
            if (flatListDirty)
            {
                RebuildFlatList();
                flatListDirty = false;
            }

            if (flatVisibleNodes == null || flatVisibleNodes.Count == 0)
            {
                EditorGUILayout.HelpBox("No files found. Press Refresh or adjust excluded folders in Settings.", MessageType.Info);
                return;
            }

            DrawProfilesBar();

            float totalHeight = flatVisibleNodes.Count * ROW_HEIGHT;

            // Robust height calculation (similar approach as preview fix)
            float availableHeight = Mathf.Max(50f, position.height - 220f);
            Rect scrollViewRect = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(availableHeight));

            float contentWidth = Mathf.Max(scrollViewRect.width - 16f, 100f);

            treeScrollPos = GUI.BeginScrollView(scrollViewRect, treeScrollPos,
                new Rect(0, 0, contentWidth, totalHeight));

            int firstVisible = Mathf.Max(0, (int)(treeScrollPos.y / ROW_HEIGHT) - 2);
            int lastVisible = Mathf.Min(flatVisibleNodes.Count - 1,
                firstVisible + (int)(scrollViewRect.height / ROW_HEIGHT) + 4);

            for (int i = firstVisible; i <= lastVisible; i++)
            {
                Rect rowRect = new Rect(0, i * ROW_HEIGHT, contentWidth, ROW_HEIGHT);
                DrawTreeRow(rowRect, flatVisibleNodes[i]);
            }

            GUI.EndScrollView();
        }

        private void DrawTreeRow(Rect rect, TreeNode node)
        {
            if (rect.Contains(Event.current.mousePosition))
                EditorGUI.DrawRect(rect, s_HoverColor); // FIX #11

            float x = rect.x + node.depth * INDENT_WIDTH + 4;

            if (node.isDirectory && node.children.Count > 0)
            {
                Rect foldRect = new Rect(x, rect.y, 14, ROW_HEIGHT);
                EditorGUI.BeginChangeCheck();
                bool newExpanded = EditorGUI.Foldout(foldRect, node.isExpanded, "", true);
                if (EditorGUI.EndChangeCheck())
                {
                    node.isExpanded = newExpanded;
                    flatListDirty = true;
                }
            }
            x += 16;

            Rect toggleRect = new Rect(x, rect.y, 16, ROW_HEIGHT);

            if (node.isDirectory)
            {
                var state = GetFolderCheckState(node);
                EditorGUI.showMixedValue = (state == CheckState.Mixed);
                EditorGUI.BeginChangeCheck();
                bool toggled = EditorGUI.Toggle(toggleRect, state != CheckState.Unchecked);
                EditorGUI.showMixedValue = false;

                if (EditorGUI.EndChangeCheck())
                {
                    SetNodeChecked(node, toggled);
                    MarkDirty();
                }
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                bool newChecked = EditorGUI.Toggle(toggleRect, node.isChecked);
                if (EditorGUI.EndChangeCheck())
                {
                    node.isChecked = newChecked;
                    MarkDirty();
                }
            }
            x += 20;

            Texture icon = GetCachedIcon(node);
            if (icon != null)
                GUI.DrawTexture(new Rect(x, rect.y + 2, 16, 16), icon, ScaleMode.ScaleToFit);
            x += 20;

            string label = node.name;
            if (!node.isDirectory)
            {
                long size = GetFileSize(node.fullPath);
                label += $"  ({FormatFileSize(size)})";
            }

            GUIStyle style = node.isDirectory ? EditorStyles.boldLabel : EditorStyles.label;
            Color prevColor = GUI.contentColor;
            if (!node.isChecked && !node.isDirectory)
                GUI.contentColor = s_UncheckedColor; // FIX #11

            GUI.Label(new Rect(x, rect.y, rect.width - x, ROW_HEIGHT), label, style);
            GUI.contentColor = prevColor;
        }

        // ==================== PROFILES BAR ====================
        private void DrawProfilesBar()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Profiles:", GUILayout.Width(50));

            if (profileNames.Length > 0)
            {
                selectedProfileIndex = EditorGUILayout.Popup(selectedProfileIndex, profileNames, GUILayout.Width(150));

                if (GUILayout.Button("Load", GUILayout.Width(45)))
                {
                    LoadProfile(profileNames[selectedProfileIndex]);
                    SetStatus($"Profile '{profileNames[selectedProfileIndex]}' loaded", MessageType.Info);
                }
            }

            GUILayout.FlexibleSpace();

            newProfileName = EditorGUILayout.TextField(newProfileName, GUILayout.Width(120));

            if (GUILayout.Button("Save", GUILayout.Width(45)))
            {
                if (!string.IsNullOrEmpty(newProfileName))
                {
                    SaveProfile(newProfileName);
                    RefreshProfiles();
                    SetStatus($"Profile '{newProfileName}' saved", MessageType.Info);
                }
            }

            if (profileNames.Length > 0 && GUILayout.Button("Del", GUILayout.Width(35)))
            {
                DeleteProfile(profileNames[selectedProfileIndex]);
                RefreshProfiles();
            }

            EditorGUILayout.EndHorizontal();
        }

        // ==================== SETTINGS TAB ====================
        private void DrawSettingsTab()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📋 Export Content", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            settings.includeFileTree = EditorGUILayout.Toggle(
                new GUIContent("File Tree", "Include visual file structure diagram"), settings.includeFileTree);
            settings.includeFileContents = EditorGUILayout.Toggle(
                new GUIContent("File Contents", "Include full source code"), settings.includeFileContents);
            settings.includeAssetInfo = EditorGUILayout.Toggle(
                new GUIContent("Asset Metadata", "Include type-specific metadata"), settings.includeAssetInfo);
            settings.includeLineNumbers = EditorGUILayout.Toggle(
                new GUIContent("Line Numbers", "Prefix each line with its number"), settings.includeLineNumbers);
            settings.groupByFolder = EditorGUILayout.Toggle(
                new GUIContent("Group by Folder", "Organize files under folder headers"), settings.groupByFolder);
            settings.includeTOC = EditorGUILayout.Toggle(
                new GUIContent("Table of Contents", "Add clickable TOC at the top"), settings.includeTOC);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎨 Formatting", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            settings.headerDepth = EditorGUILayout.IntSlider("Header Start Level (#)", settings.headerDepth, 1, 4);
            settings.maxFileSizeKB = EditorGUILayout.IntSlider(
                new GUIContent("Max File Size (KB)", "Files larger than this will be truncated"),
                settings.maxFileSizeKB, 32, 2048);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("ℹ️ Project Info", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            settings.includeProjectName = EditorGUILayout.Toggle("Project Name", settings.includeProjectName);
            settings.includeUnityVersion = EditorGUILayout.Toggle("Unity Version", settings.includeUnityVersion);
            settings.includeExportDate = EditorGUILayout.Toggle("Export Date", settings.includeExportDate);
            settings.customHeader = EditorGUILayout.TextField("Custom Header", settings.customHeader);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📁 File Filters", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.LabelField("Script Extensions (comma-separated)");
            EditorGUI.BeginChangeCheck();
            settings.scriptExtensionsRaw = EditorGUILayout.TextField(settings.scriptExtensionsRaw);
            if (EditorGUI.EndChangeCheck())
                settings.ParseExtensions(); // FIX #4: parse only on actual change

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Excluded Folders (comma-separated)");

            EditorGUI.BeginChangeCheck();
            settings.excludedFoldersRaw = EditorGUILayout.TextField(settings.excludedFoldersRaw);
            if (EditorGUI.EndChangeCheck())
            {
                settings.ParseExcludedFolders(); // FIX #4: parse once
                RefreshTree();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Reset to Defaults"))
            {
                settings = new ExportSettings();
                SaveSettings();
            }

            EditorGUILayout.EndVertical();
        }

        // ==================== PREVIEW TAB (Virtualized) ====================
        private void DrawPreviewTab()
        {
            EnsurePreviewStyle();

            // Top bar
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate Preview", GUILayout.Height(28), GUILayout.Width(140)))
            {
                GeneratePreview();
            }

            if (hasPreview)
            {
                int tokenEst = previewTotalChars / 4;
                GUILayout.Label($"{previewTotalChars:N0} chars | ~{tokenEst:N0} tokens | {previewLines.Length:N0} lines",
                    EditorStyles.miniLabel, GUILayout.Width(280));
            }

            GUILayout.FlexibleSpace();

            if (hasPreview && GUILayout.Button("Copy Full Text", GUILayout.Width(100)))
            {
                EditorGUIUtility.systemCopyBuffer = previewFullText;
                SetStatus("Preview text copied to clipboard", MessageType.Info);
            }

            EditorGUILayout.EndHorizontal();

            // Search bar for preview
            if (hasPreview)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Find:", GUILayout.Width(35));

                EditorGUI.BeginChangeCheck();
                previewSearchText = EditorGUILayout.TextField(previewSearchText, GUILayout.Width(200));
                if (EditorGUI.EndChangeCheck())
                {
                    UpdatePreviewSearch();
                }

                if (previewSearchResults.Count > 0)
                {
                    GUILayout.Label($"{previewSearchIndex + 1}/{previewSearchResults.Count}", GUILayout.Width(60));

                    if (GUILayout.Button("▲", GUILayout.Width(25)))
                    {
                        previewSearchIndex = (previewSearchIndex - 1 + previewSearchResults.Count) % previewSearchResults.Count;
                        ScrollToPreviewLine(previewSearchResults[previewSearchIndex]);
                    }
                    if (GUILayout.Button("▼", GUILayout.Width(25)))
                    {
                        previewSearchIndex = (previewSearchIndex + 1) % previewSearchResults.Count;
                        ScrollToPreviewLine(previewSearchResults[previewSearchIndex]);
                    }
                }
                else if (!string.IsNullOrEmpty(previewSearchText))
                {
                    GUILayout.Label("No results", EditorStyles.miniLabel, GUILayout.Width(60));
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            if (!hasPreview)
            {
                EditorGUILayout.HelpBox("Click 'Generate Preview' to see the output. The preview is virtualized — even large files render smoothly.", MessageType.Info);
                return;
            }

            // Virtualized preview rendering
            DrawVirtualizedPreview();
        }

        // FIX #1: full rewrite — robust height & width calculation
        private void DrawVirtualizedPreview()
        {
            // Reserve area through GUILayout.Height so layout engine knows actual size
            const float toolbarH = 28f;
            const float statsBarH = 24f;
            const float tabBarH = 28f;
            const float previewHeaderH = 32f;
            const float searchBarH = 22f;
            const float footerH = 70f;
            const float padding = 10f;

            float usedHeight = toolbarH + statsBarH + tabBarH + previewHeaderH + searchBarH + footerH + padding;
            float availableHeight = Mathf.Max(80f, position.height - usedHeight);

            Rect scrollViewRect = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(availableHeight));

            // Skip rendering until layout has resolved a real rect
            if (scrollViewRect.width < 1f || scrollViewRect.height < 1f)
                return;

            EditorGUI.DrawRect(scrollViewRect,
                EditorGUIUtility.isProSkin ? s_PreviewBgDark : s_PreviewBgLight);

            float contentWidth = Mathf.Max(scrollViewRect.width - 16f, 100f);
            float totalHeight = previewLines.Length * PREVIEW_LINE_HEIGHT;

            previewScrollPos = GUI.BeginScrollView(scrollViewRect, previewScrollPos,
                new Rect(0, 0, contentWidth, totalHeight));

            int firstVisible = Mathf.Max(0, (int)(previewScrollPos.y / PREVIEW_LINE_HEIGHT) - 1);
            int lastVisible = Mathf.Min(previewLines.Length - 1,
                firstVisible + (int)(scrollViewRect.height / PREVIEW_LINE_HEIGHT) + 2);

            bool hasSearchHighlight = !string.IsNullOrEmpty(previewSearchText) && previewSearchResults.Count > 0;
            int highlightLine = hasSearchHighlight ? previewSearchResults[previewSearchIndex] : -1;

            for (int i = firstVisible; i <= lastVisible; i++)
            {
                float y = i * PREVIEW_LINE_HEIGHT;
                Rect lineRect = new Rect(0, y, contentWidth, PREVIEW_LINE_HEIGHT);

                // FIX #2: O(1) HashSet lookup instead of List.Contains
                if (hasSearchHighlight && i == highlightLine)
                    EditorGUI.DrawRect(lineRect, s_SearchHiColor);
                else if (hasSearchHighlight && _previewSearchSet.Contains(i))
                    EditorGUI.DrawRect(lineRect, s_SearchHiColorBg);

                if ((i & 1) == 0) // FIX #11: bit-and slightly faster than modulo
                    EditorGUI.DrawRect(lineRect, s_AltRowColor);

                Rect gutterRect = new Rect(0, y, PREVIEW_GUTTER_WIDTH, PREVIEW_LINE_HEIGHT);
                Rect contentRect = new Rect(PREVIEW_GUTTER_WIDTH, y, contentWidth - PREVIEW_GUTTER_WIDTH, PREVIEW_LINE_HEIGHT);

                GUI.Label(gutterRect, (i + 1).ToString(), EditorStyles.centeredGreyMiniLabel);
                GUI.Label(contentRect, previewLines[i], previewLineStyle);
            }

            GUI.EndScrollView();
        }

        private void GeneratePreview()
        {
            previewFullText = GenerateMarkdown();
            previewTotalChars = previewFullText.Length;

            // FIX #3: handle both \r\n and \n line endings properly
            previewLines = previewFullText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // FIX #6: cache lowercase versions once instead of allocating per search
            _previewLinesLower = new string[previewLines.Length];
            for (int i = 0; i < previewLines.Length; i++)
                _previewLinesLower[i] = previewLines[i].ToLowerInvariant();

            hasPreview = true;
            previewSearchText = "";
            previewSearchResults.Clear();
            _previewSearchSet.Clear();
            previewSearchIndex = 0;
            previewScrollPos = Vector2.zero;
        }

        private void UpdatePreviewSearch()
        {
            previewSearchResults.Clear();
            _previewSearchSet.Clear();
            previewSearchIndex = 0;

            if (string.IsNullOrEmpty(previewSearchText) || _previewLinesLower == null) return;

            string searchLower = previewSearchText.ToLowerInvariant();
            for (int i = 0; i < _previewLinesLower.Length; i++)
            {
                if (_previewLinesLower[i].Contains(searchLower))
                {
                    previewSearchResults.Add(i);
                    _previewSearchSet.Add(i);
                }
            }

            if (previewSearchResults.Count > 0)
                ScrollToPreviewLine(previewSearchResults[0]);
        }

        private void ScrollToPreviewLine(int lineIndex)
        {
            float targetY = lineIndex * PREVIEW_LINE_HEIGHT - 100;
            previewScrollPos.y = Mathf.Max(0, targetY);
        }

        // ==================== FOOTER ====================
        private void DrawFooter()
        {
            EditorGUILayout.Space(3);

            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }
            else
            {
                EditorGUILayout.HelpBox(" ", MessageType.None);
            }

            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Copy to Clipboard (Ctrl+Shift+C)", GUILayout.Width(250), GUILayout.Height(30)))
            {
                CopyToClipboard();
            }

            if (GUILayout.Button("Save to File", GUILayout.Width(120), GUILayout.Height(30)))
            {
                SaveToFile();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(3);
        }

        // ==================== TREE BUILDING ====================
        private void RefreshTree()
        {
            ClearCaches();
            string assetsPath = Application.dataPath;
            rootNode = BuildTreeNode(assetsPath, "Assets", 0);
            RestoreCheckedStates();
            RebuildExtensionCounts();
            MarkDirty();
        }

        private TreeNode BuildTreeNode(string fullPath, string displayName, int depth)
        {
            bool isDir = Directory.Exists(fullPath);

            var node = new TreeNode
            {
                name = displayName,
                fullPath = fullPath,
                isDirectory = isDir,
                isExpanded = depth < 1,
                isChecked = false,
                depth = depth,
                extension = isDir ? "" : Path.GetExtension(fullPath).ToLowerInvariant()
            };

            if (!isDir) return node;

            string folderName = Path.GetFileName(fullPath);
            if (IsExcludedFolder(folderName) && depth > 0) return null;

            try
            {
                var dirs = Directory.GetDirectories(fullPath);
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);

                foreach (var dir in dirs)
                {
                    var child = BuildTreeNode(dir, Path.GetFileName(dir), depth + 1);
                    if (child != null) node.children.Add(child);
                }

                var files = Directory.GetFiles(fullPath);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);

                foreach (var file in files)
                {
                    if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;

                    node.children.Add(new TreeNode
                    {
                        name = Path.GetFileName(file),
                        fullPath = file,
                        isDirectory = false,
                        extension = Path.GetExtension(file).ToLowerInvariant(),
                        isChecked = false,
                        depth = depth + 1
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CodebaseExporter] Error reading {fullPath}: {e.Message}");
            }

            return node;
        }

        // ==================== FLAT LIST ====================
        private void RebuildFlatList()
        {
            if (flatVisibleNodes == null)
                flatVisibleNodes = new List<TreeNode>(256);
            else
                flatVisibleNodes.Clear();

            if (rootNode != null)
            {
                bool hasFilter = !string.IsNullOrEmpty(searchFilter);
                string filterLower = hasFilter ? searchFilter.ToLowerInvariant() : null;
                BuildFlatListRecursive(rootNode, hasFilter, filterLower);
            }
        }

        private void BuildFlatListRecursive(TreeNode node, bool hasFilter, string filterLower)
        {
            if (hasFilter && !node.MatchesFilter(filterLower)) return;

            flatVisibleNodes.Add(node);

            if (node.isDirectory && (node.isExpanded || hasFilter))
            {
                foreach (var child in node.children)
                    BuildFlatListRecursive(child, hasFilter, filterLower);
            }
        }

        // ==================== MARKDOWN GENERATION ====================
        private string GenerateMarkdown()
        {
            var selectedFiles = new List<TreeNode>(128);
            CollectSelectedFiles(rootNode, selectedFiles);

            var sb = new StringBuilder(selectedFiles.Count * 2048 + 4096);

            string h1 = new string('#', settings.headerDepth);
            string h2 = new string('#', settings.headerDepth + 1);
            string h3 = new string('#', settings.headerDepth + 2);

            string title = !string.IsNullOrEmpty(settings.customHeader) ? settings.customHeader : "Project Codebase Export";
            sb.Append(h1).Append(' ').AppendLine(title);
            sb.AppendLine();

            if (settings.includeProjectName)
                sb.Append("**Project:** ").AppendLine(Application.productName);
            if (settings.includeUnityVersion)
                sb.Append("**Unity Version:** ").AppendLine(Application.unityVersion);
            if (settings.includeExportDate)
                sb.Append("**Export Date:** ").AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            if (settings.includeProjectName || settings.includeUnityVersion || settings.includeExportDate)
                sb.AppendLine();

            if (selectedFiles.Count == 0)
            {
                sb.AppendLine("> No files selected for export.");
                return sb.ToString();
            }

            sb.Append("**Total files:** ").Append(selectedFiles.Count).AppendLine();
            sb.AppendLine();

            // TOC
            if (settings.includeTOC && settings.includeFileContents)
            {
                sb.Append(h2).AppendLine(" Table of Contents");
                sb.AppendLine();

                foreach (var file in selectedFiles)
                {
                    string relPath = GetRelativePath(file.fullPath);
                    string anchor = relPath.Replace("/", "").Replace(".", "").Replace(" ", "-").ToLowerInvariant();
                    sb.Append("- [`").Append(relPath).Append("`](#").Append(anchor).AppendLine(")");
                }
                sb.AppendLine();
            }

            // File tree
            if (settings.includeFileTree)
            {
                sb.Append(h2).AppendLine(" File Structure");
                sb.AppendLine();
                sb.AppendLine("```");
                GenerateFileTree(rootNode, sb, "", true);
                sb.AppendLine("```");
                sb.AppendLine();
            }

            // File contents
            if (settings.includeFileContents)
            {
                sb.Append(h2).AppendLine(" File Contents");
                sb.AppendLine();

                if (settings.groupByFolder)
                {
                    var grouped = selectedFiles
                        .GroupBy(f => GetRelativeDirectory(f.fullPath))
                        .OrderBy(g => g.Key);

                    foreach (var group in grouped)
                    {
                        string folderPath = string.IsNullOrEmpty(group.Key) ? "Root" : group.Key;
                        sb.Append(h3).Append(" 📁 ").AppendLine(folderPath);
                        sb.AppendLine();

                        foreach (var file in group.OrderBy(f => f.name))
                            AppendFileContent(sb, file, settings.headerDepth + 3);
                    }
                }
                else
                {
                    foreach (var file in selectedFiles.OrderBy(f => GetRelativePath(f.fullPath)))
                        AppendFileContent(sb, file, settings.headerDepth + 2);
                }
            }

            return sb.ToString();
        }

        // FIX #8: no List<TreeNode> allocation per call
        private void GenerateFileTree(TreeNode node, StringBuilder sb, string prefix, bool isLast)
        {
            if (node == rootNode)
            {
                if (!HasSelectedDescendants(node)) return;
                sb.Append(node.name).AppendLine("/");

                int visibleCount = CountVisibleChildren(node);
                int idx = 0;
                foreach (var child in node.children)
                {
                    if (!child.isChecked && !HasSelectedDescendants(child)) continue;
                    idx++;
                    GenerateFileTree(child, sb, "", idx == visibleCount);
                }
            }
            else
            {
                if (!node.isChecked && !HasSelectedDescendants(node)) return;

                sb.Append(prefix).Append(isLast ? "└── " : "├── ").Append(node.name);
                if (node.isDirectory) sb.Append('/');
                sb.AppendLine();

                if (node.isDirectory)
                {
                    string childPrefix = prefix + (isLast ? "    " : "│   ");

                    int visibleCount = CountVisibleChildren(node);
                    int idx = 0;
                    foreach (var child in node.children)
                    {
                        if (!child.isChecked && !HasSelectedDescendants(child)) continue;
                        idx++;
                        GenerateFileTree(child, sb, childPrefix, idx == visibleCount);
                    }
                }
            }
        }

        private int CountVisibleChildren(TreeNode node)
        {
            int count = 0;
            foreach (var child in node.children)
                if (child.isChecked || HasSelectedDescendants(child)) count++;
            return count;
        }

        private bool HasSelectedDescendants(TreeNode node)
        {
            if (node.cachedHasSelectedValid)
                return node.cachedHasSelected;

            bool result;
            if (!node.isDirectory)
            {
                result = node.isChecked;
            }
            else
            {
                result = false;
                foreach (var child in node.children)
                {
                    if (child.isChecked || HasSelectedDescendants(child))
                    {
                        result = true;
                        break;
                    }
                }
            }

            node.cachedHasSelected = result;
            node.cachedHasSelectedValid = true;
            return result;
        }

        private void AppendFileContent(StringBuilder sb, TreeNode fileNode, int headerLevel)
        {
            string relativePath = GetRelativePath(fileNode.fullPath);
            string header = new string('#', Math.Min(headerLevel, 6));

            sb.Append(header).Append(" `").Append(relativePath).AppendLine("`");
            sb.AppendLine();

            if (settings.includeAssetInfo)
                AppendAssetMetadata(sb, fileNode);

            if (IsTextFile(fileNode.extension))
            {
                try
                {
                    long fileSize = GetFileSize(fileNode.fullPath);
                    long maxBytes = settings.maxFileSizeKB * 1024L;

                    string content;
                    bool truncated = false;

                    if (fileSize > maxBytes)
                    {
                        using (var reader = new StreamReader(fileNode.fullPath))
                        {
                            char[] buffer = new char[maxBytes];
                            int read = reader.Read(buffer, 0, buffer.Length);
                            content = new string(buffer, 0, read);
                        }
                        truncated = true;
                    }
                    else
                    {
                        content = File.ReadAllText(fileNode.fullPath);
                    }

                    if (settings.includeLineNumbers)
                        content = AddLineNumbers(content);

                    string lang = GetLanguageIdentifier(fileNode.extension);
                    sb.Append("```").AppendLine(lang);
                    sb.AppendLine(content);
                    sb.AppendLine("```");

                    if (truncated)
                    {
                        sb.Append("> ⚠️ File truncated (").Append(FormatFileSize(fileSize))
                            .Append(", max ").Append(settings.maxFileSizeKB).AppendLine("KB)");
                    }
                }
                catch (Exception e)
                {
                    sb.Append("> ⚠️ Error reading file: ").AppendLine(e.Message);
                }
            }
            else if (IsBinaryAsset(fileNode.extension))
            {
                AppendBinaryAssetInfo(sb, fileNode);
            }
            else
            {
                long size = GetFileSize(fileNode.fullPath);
                sb.Append("> Binary file (").Append(FormatFileSize(size)).AppendLine(")");
            }

            sb.AppendLine();
        }

        private void AppendAssetMetadata(StringBuilder sb, TreeNode fileNode)
        {
            string assetPath = GetRelativePath(fileNode.fullPath);
            var obj = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (obj == null) return;

            sb.AppendLine("<details>");
            sb.AppendLine("<summary>Asset Metadata</summary>");
            sb.AppendLine();
            sb.AppendLine("| Property | Value |");
            sb.AppendLine("|----------|-------|");
            sb.Append("| **Type** | ").Append(obj.GetType().Name).AppendLine(" |");
            sb.Append("| **Name** | ").Append(obj.name).AppendLine(" |");

            long size = GetFileSize(fileNode.fullPath);
            sb.Append("| **Size** | ").Append(FormatFileSize(size)).AppendLine(" |");

            AppendTypeSpecificMetadata(sb, obj, assetPath);

            sb.AppendLine();
            sb.AppendLine("</details>");
            sb.AppendLine();
        }

        private void AppendTypeSpecificMetadata(StringBuilder sb, UnityEngine.Object obj, string assetPath)
        {
            switch (obj)
            {
                case Texture2D tex:
                    sb.Append("| **Dimensions** | ").Append(tex.width).Append("x").Append(tex.height).AppendLine(" |");
                    sb.Append("| **Format** | ").Append(tex.format).AppendLine(" |");
                    sb.Append("| **Mip Maps** | ").Append(tex.mipmapCount > 1).AppendLine(" |");
                    sb.Append("| **Filter Mode** | ").Append(tex.filterMode).AppendLine(" |");
                    sb.Append("| **Wrap Mode** | ").Append(tex.wrapMode).AppendLine(" |");

                    if (AssetImporter.GetAtPath(assetPath) is TextureImporter texImp)
                    {
                        sb.Append("| **Texture Type** | ").Append(texImp.textureType).AppendLine(" |");
                        sb.Append("| **sRGB** | ").Append(texImp.sRGBTexture).AppendLine(" |");
                        sb.Append("| **Max Size** | ").Append(texImp.maxTextureSize).AppendLine(" |");
                        sb.Append("| **Compression** | ").Append(texImp.textureCompression).AppendLine(" |");
                    }
                    break;

                case AudioClip audio:
                    sb.Append("| **Length** | ").AppendFormat("{0:F2}", audio.length).AppendLine("s |");
                    sb.Append("| **Channels** | ").Append(audio.channels).AppendLine(" |");
                    sb.Append("| **Frequency** | ").Append(audio.frequency).AppendLine("Hz |");
                    sb.Append("| **Samples** | ").Append(audio.samples).AppendLine(" |");

                    if (AssetImporter.GetAtPath(assetPath) is AudioImporter audioImp)
                    {
                        sb.Append("| **Load in Background** | ").Append(audioImp.loadInBackground).AppendLine(" |");
                        var ds = audioImp.defaultSampleSettings;
                        sb.Append("| **Load Type** | ").Append(ds.loadType).AppendLine(" |");
                        sb.Append("| **Compression** | ").Append(ds.compressionFormat).AppendLine(" |");
                    }
                    break;

                case Mesh mesh:
                    sb.Append("| **Vertices** | ").Append(mesh.vertexCount).AppendLine(" |");

                    // FIX #14: GetIndexCount doesn't allocate, mesh.triangles does
                    int triCount = 0;
                    for (int s = 0; s < mesh.subMeshCount; s++)
                        triCount += (int)(mesh.GetIndexCount(s) / 3);
                    sb.Append("| **Triangles** | ").Append(triCount).AppendLine(" |");

                    sb.Append("| **Sub Meshes** | ").Append(mesh.subMeshCount).AppendLine(" |");
                    sb.Append("| **Bounds** | ").Append(mesh.bounds).AppendLine(" |");
                    break;

                case Material mat:
                    sb.Append("| **Shader** | ").Append(mat.shader != null ? mat.shader.name : "None").AppendLine(" |");
                    sb.Append("| **Render Queue** | ").Append(mat.renderQueue).AppendLine(" |");
                    break;

                case AnimationClip clip:
                    sb.Append("| **Length** | ").AppendFormat("{0:F2}", clip.length).AppendLine("s |");
                    sb.Append("| **Frame Rate** | ").Append(clip.frameRate).AppendLine(" |");
                    sb.Append("| **Wrap Mode** | ").Append(clip.wrapMode).AppendLine(" |");
                    sb.Append("| **Looping** | ").Append(clip.isLooping).AppendLine(" |");
                    break;

                case GameObject go:
                    sb.Append("| **Components** | ").Append(go.GetComponents<Component>().Length).AppendLine(" |");
                    sb.Append("| **Children** | ").Append(go.transform.childCount).AppendLine(" |");
                    break;

                case ScriptableObject so:
                    sb.Append("| **Script Type** | ").Append(so.GetType().FullName).AppendLine(" |");
                    break;

                case SceneAsset _:
                    sb.AppendLine("| **Asset Type** | Scene |");
                    break;
            }
        }

        private void AppendBinaryAssetInfo(StringBuilder sb, TreeNode fileNode)
        {
            string assetPath = GetRelativePath(fileNode.fullPath);
            var obj = AssetDatabase.LoadMainAssetAtPath(assetPath);
            long size = GetFileSize(fileNode.fullPath);

            sb.Append("> **Asset:** ").Append(obj != null ? obj.GetType().Name : "Unknown")
                .Append(" — ").AppendLine(FormatFileSize(size));

            switch (obj)
            {
                case Texture2D tex:
                    sb.Append("> **Dimensions:** ").Append(tex.width).Append("x").Append(tex.height)
                        .Append(", **Format:** ").AppendLine(tex.format.ToString());
                    break;
                case AudioClip audio:
                    sb.Append("> **Duration:** ").AppendFormat("{0:F2}", audio.length).Append("s")
                        .Append(", **Channels:** ").Append(audio.channels)
                        .Append(", **Frequency:** ").Append(audio.frequency).AppendLine("Hz");
                    break;
                case Mesh mesh:
                    int triCount = 0; // FIX #14
                    for (int s = 0; s < mesh.subMeshCount; s++)
                        triCount += (int)(mesh.GetIndexCount(s) / 3);
                    sb.Append("> **Vertices:** ").Append(mesh.vertexCount)
                        .Append(", **Triangles:** ").AppendLine(triCount.ToString());
                    break;
            }
        }

        // ==================== SAVE / COPY ====================
        private void CopyToClipboard()
        {
            string md = GenerateMarkdown();
            EditorGUIUtility.systemCopyBuffer = md;
            int tokens = md.Length / 4;
            SetStatus($"Copied! {md.Length:N0} chars, ~{tokens:N0} tokens", MessageType.Info);
        }

        private void SaveToFile()
        {
            string defaultName = $"{Application.productName}_codebase_{DateTime.Now:yyyyMMdd_HHmmss}.md";
            string path = EditorUtility.SaveFilePanel("Save Codebase Markdown",
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), defaultName, "md");

            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string md = GenerateMarkdown();
                File.WriteAllText(path, md, Encoding.UTF8);
                SetStatus($"Saved to {path} ({md.Length:N0} chars)", MessageType.Info);
            }
            catch (Exception e)
            {
                SetStatus($"Error: {e.Message}", MessageType.Error);
            }
        }

        // ==================== HELPERS ====================
        private void MarkDirty()
        {
            flatListDirty = true;
            statsDirty = true;
            InvalidateHasSelectedCache(rootNode);
        }

        private void InvalidateHasSelectedCache(TreeNode node)
        {
            if (node == null) return;
            node.cachedHasSelectedValid = false;
            foreach (var child in node.children)
                InvalidateHasSelectedCache(child);
        }

        // FIX #13: early exit on unselected branches
        private void CollectSelectedFiles(TreeNode node, List<TreeNode> result)
        {
            if (node == null) return;
            if (!node.isChecked && !HasSelectedDescendants(node)) return;

            if (!node.isDirectory && node.isChecked) result.Add(node);
            foreach (var child in node.children)
                CollectSelectedFiles(child, result);
        }

        private void SetNodeChecked(TreeNode node, bool value)
        {
            node.isChecked = value;
            if (node.isDirectory)
                foreach (var child in node.children)
                    SetNodeChecked(child, value);
        }

        private void SetAllChecked(TreeNode node, bool value)
        {
            if (node == null) return;
            SetNodeChecked(node, value);
            MarkDirty();
        }

        private void InvertSelection(TreeNode node)
        {
            if (node == null) return;
            if (!node.isDirectory) node.isChecked = !node.isChecked;
            foreach (var child in node.children) InvertSelection(child);
        }

        private void SelectByExtensions(TreeNode node, HashSet<string> extensions)
        {
            if (node == null) return;
            if (!node.isDirectory) node.isChecked = node.isChecked || extensions.Contains(node.extension);
            foreach (var child in node.children) SelectByExtensions(child, extensions);
        }

        private void SetAllExpanded(TreeNode node, bool expanded)
        {
            if (node == null) return;
            if (node.isDirectory) node.isExpanded = expanded;
            foreach (var child in node.children) SetAllExpanded(child, expanded);
        }

        private void ExpandSelected(TreeNode node)
        {
            if (node == null) return;
            if (node.isDirectory && HasSelectedDescendants(node))
                node.isExpanded = true;
            foreach (var child in node.children) ExpandSelected(child);
        }

        private enum CheckState { Unchecked, Checked, Mixed }

        private CheckState GetFolderCheckState(TreeNode folder)
        {
            bool anyChecked = false, anyUnchecked = false;
            CheckFolderRecursive(folder, ref anyChecked, ref anyUnchecked);

            if (anyChecked && anyUnchecked) return CheckState.Mixed;
            if (anyChecked) return CheckState.Checked;
            return CheckState.Unchecked;
        }

        private void CheckFolderRecursive(TreeNode node, ref bool anyChecked, ref bool anyUnchecked)
        {
            foreach (var child in node.children)
            {
                if (!child.isDirectory)
                {
                    if (child.isChecked) anyChecked = true;
                    else anyUnchecked = true;
                }
                else
                {
                    CheckFolderRecursive(child, ref anyChecked, ref anyUnchecked);
                }
                if (anyChecked && anyUnchecked) return;
            }
        }

        private void RebuildExtensionCounts()
        {
            extensionCounts.Clear();
            CountExtensions(rootNode);
        }

        // FIX #7: single dictionary lookup
        private void CountExtensions(TreeNode node)
        {
            if (node == null) return;
            if (!node.isDirectory && !string.IsNullOrEmpty(node.extension))
            {
                extensionCounts.TryGetValue(node.extension, out int count);
                extensionCounts[node.extension] = count + 1;
            }
            foreach (var child in node.children)
                CountExtensions(child);
        }

        // ==================== CACHING ====================
        private Texture GetCachedIcon(TreeNode node)
        {
            string key = node.isDirectory ? "__dir__" : node.extension;

            if (iconCache.TryGetValue(key, out Texture cached))
                return cached;

            Texture icon = null;

            if (node.isDirectory)
            {
                icon = EditorGUIUtility.IconContent("Folder Icon")?.image;
            }
            else
            {
                string iconName = node.extension switch
                {
                    ".cs" => "cs Script Icon",
                    ".shader" or ".cginc" or ".hlsl" or ".compute" => "Shader Icon",
                    ".json" or ".xml" or ".yaml" or ".yml" or ".txt" => "TextAsset Icon",
                    ".png" or ".jpg" or ".jpeg" or ".tga" or ".psd" => "Texture Icon",
                    ".mat" => "Material Icon",
                    ".prefab" => "Prefab Icon",
                    ".unity" => "SceneAsset Icon",
                    ".anim" => "AnimationClip Icon",
                    ".controller" => "AnimatorController Icon",
                    ".wav" or ".mp3" or ".ogg" => "AudioClip Icon",
                    ".fbx" or ".obj" => "Mesh Icon",
                    ".asset" => "ScriptableObject Icon",
                    _ => "DefaultAsset Icon"
                };
                icon = EditorGUIUtility.IconContent(iconName)?.image;
            }

            iconCache[key] = icon;
            return icon;
        }

        private long GetFileSize(string path)
        {
            if (fileSizeCache.TryGetValue(path, out long cached))
                return cached;

            try
            {
                long size = new FileInfo(path).Length;
                fileSizeCache[path] = size;
                return size;
            }
            catch
            {
                fileSizeCache[path] = 0;
                return 0;
            }
        }

        private string GetRelativePath(string fullPath)
        {
            if (relativePathCache.TryGetValue(fullPath, out string cached))
                return cached;

            string dataPath = Application.dataPath;
            string result = fullPath.StartsWith(dataPath)
                ? "Assets" + fullPath.Substring(dataPath.Length).Replace('\\', '/')
                : fullPath.Replace('\\', '/');

            relativePathCache[fullPath] = result;
            return result;
        }

        private string GetRelativeDirectory(string fullPath)
        {
            string rel = GetRelativePath(fullPath);
            int lastSlash = rel.LastIndexOf('/');
            return lastSlash >= 0 ? rel.Substring(0, lastSlash) : "";
        }

        private void ClearCaches()
        {
            fileSizeCache.Clear();
            relativePathCache.Clear();
        }

        // ==================== FILE TYPE DETECTION ====================
        private static readonly HashSet<string> TextExtensions = new HashSet<string>
        {
            ".cs", ".js", ".ts", ".json", ".xml", ".yaml", ".yml",
            ".txt", ".md", ".csv", ".html", ".htm", ".css",
            ".shader", ".cginc", ".hlsl", ".glsl", ".compute",
            ".asmdef", ".asmref", ".inputactions", ".preset",
            ".uxml", ".uss", ".tss",
            ".cfg", ".config", ".ini", ".log",
            ".py", ".lua", ".sh", ".bat", ".cmd",
            ".sql", ".graphql", ".raytrace", ".cg", ".rsp"
        };

        private static readonly HashSet<string> BinaryAssetExtensions = new HashSet<string>
        {
            ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".gif", ".psd", ".tiff", ".webp", ".exr", ".hdr",
            ".wav", ".mp3", ".ogg", ".aiff", ".flac",
            ".fbx", ".obj", ".dae", ".3ds", ".blend",
            ".mat", ".prefab", ".unity", ".asset",
            ".controller", ".anim", ".overrideController",
            ".physicMaterial", ".physicsMaterial2D",
            ".ttf", ".otf", ".fnt",
            ".mp4", ".avi", ".mov", ".webm",
            ".dll", ".so", ".dylib",
            ".cubemap", ".flare", ".lighting",
            ".mask", ".spriteatlas", ".spriteatlasv2",
            ".terrainlayer", ".brush", ".mixer", ".signal", ".playable", ".renderTexture"
        };

        private bool IsTextFile(string ext) => TextExtensions.Contains(ext);
        private bool IsBinaryAsset(string ext) => BinaryAssetExtensions.Contains(ext);

        // FIX #4: O(1) HashSet lookup instead of per-call Split + Trim
        private bool IsExcludedFolder(string folderName)
            => settings.excludedFolders.Contains(folderName);

        private string GetLanguageIdentifier(string ext) => ext switch
        {
            ".cs" => "csharp",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".json" => "json",
            ".xml" or ".uxml" => "xml",
            ".yaml" or ".yml" => "yaml",
            ".html" or ".htm" => "html",
            ".css" or ".uss" => "css",
            ".shader" or ".cginc" or ".hlsl" or ".compute" => "hlsl",
            ".glsl" => "glsl",
            ".py" => "python",
            ".lua" => "lua",
            ".sh" => "bash",
            ".bat" or ".cmd" => "batch",
            ".sql" => "sql",
            ".md" => "markdown",
            _ => ""
        };

        // FIX #10: single pass + StringReader, no Split array allocation
        private string AddLineNumbers(string content)
        {
            int lineCount = 1;
            for (int i = 0; i < content.Length; i++)
                if (content[i] == '\n') lineCount++;

            int width = lineCount.ToString().Length;
            var sb = new StringBuilder(content.Length + lineCount * (width + 3));

            using (var reader = new StringReader(content))
            {
                string line;
                int n = 1;
                while ((line = reader.ReadLine()) != null)
                {
                    if (n > 1) sb.Append('\n');
                    sb.Append(n.ToString().PadLeft(width)).Append(" | ").Append(line);
                    n++;
                }
            }
            return sb.ToString();
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
            return $"{bytes / 1073741824.0:F1} GB";
        }

        private void SetStatus(string message, MessageType type)
        {
            statusMessage = message;
            statusType = type;
            statusTime = EditorApplication.timeSinceStartup;
        }

        // ==================== PROFILES ====================
        private const string PROFILE_PREFIX = "CodebaseExporter_Profile_";

        // FIX #13: optimized — skip whole subtrees
        private void SaveProfile(string profileName)
        {
            var paths = new List<string>();
            CollectCheckedPaths(rootNode, paths);
            EditorPrefs.SetString(PROFILE_PREFIX + profileName, string.Join("|", paths));

            var list = new List<string>(profileNames);
            if (!list.Contains(profileName)) list.Add(profileName);
            EditorPrefs.SetString("CodebaseExporter_ProfileList", string.Join("|", list));
        }

        private void LoadProfile(string profileName)
        {
            string saved = EditorPrefs.GetString(PROFILE_PREFIX + profileName, "");
            if (string.IsNullOrEmpty(saved)) return;

            SetAllChecked(rootNode, false);
            var checkedPaths = new HashSet<string>(saved.Split('|'));
            RestoreCheckedRecursive(rootNode, checkedPaths);
            MarkDirty();
        }

        private void DeleteProfile(string profileName)
        {
            EditorPrefs.DeleteKey(PROFILE_PREFIX + profileName);
            var list = new List<string>(profileNames);
            list.Remove(profileName);
            EditorPrefs.SetString("CodebaseExporter_ProfileList", string.Join("|", list));
            RefreshProfiles();
        }

        private void RefreshProfiles()
        {
            string saved = EditorPrefs.GetString("CodebaseExporter_ProfileList", "");
            profileNames = string.IsNullOrEmpty(saved) ? Array.Empty<string>() :
                saved.Split('|').Where(s => !string.IsNullOrEmpty(s)).ToArray();
            selectedProfileIndex = Mathf.Clamp(selectedProfileIndex, 0, Mathf.Max(0, profileNames.Length - 1));
        }

        // ==================== PERSISTENCE ====================
        private void SaveSettings()
        {
            string json = JsonUtility.ToJson(settings);
            EditorPrefs.SetString("CodebaseExporter_Settings", json);

            if (rootNode != null)
            {
                var checkedPaths = new List<string>();
                CollectCheckedPaths(rootNode, checkedPaths);
                EditorPrefs.SetString("CodebaseExporter_CheckedPaths", string.Join("|", checkedPaths));
            }
        }

        private void LoadSettings()
        {
            string json = EditorPrefs.GetString("CodebaseExporter_Settings", "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    settings = JsonUtility.FromJson<ExportSettings>(json);
                    settings.ParseExtensions();
                    settings.ParseExcludedFolders();
                }
                catch { settings = new ExportSettings(); }
            }
        }

        // FIX #13: early exit on dead branches
        private void CollectCheckedPaths(TreeNode node, List<string> paths)
        {
            if (node == null) return;
            if (!node.isChecked && !HasSelectedDescendants(node)) return;

            if (node.isChecked) paths.Add(node.fullPath);
            foreach (var child in node.children) CollectCheckedPaths(child, paths);
        }

        private void RestoreCheckedStates()
        {
            string saved = EditorPrefs.GetString("CodebaseExporter_CheckedPaths", "");
            if (string.IsNullOrEmpty(saved)) return;

            var checkedPaths = new HashSet<string>(saved.Split('|'));
            RestoreCheckedRecursive(rootNode, checkedPaths);
        }

        private void RestoreCheckedRecursive(TreeNode node, HashSet<string> paths)
        {
            if (node == null) return;
            if (paths.Contains(node.fullPath)) node.isChecked = true;
            foreach (var child in node.children) RestoreCheckedRecursive(child, paths);
        }
    }

    // ==================== DATA CLASSES ====================
    public class TreeNode
    {
        public string name;
        public string fullPath;
        public string extension = "";
        public bool isDirectory;
        public bool isExpanded;
        public bool isChecked;
        public int depth;
        public List<TreeNode> children = new List<TreeNode>();

        public bool cachedHasSelected;
        public bool cachedHasSelectedValid;

        // FIX #5: cached lowercase name to avoid per-filter ToLower allocation
        private string _nameLower;
        public string NameLower => _nameLower ?? (_nameLower = name.ToLowerInvariant());

        public bool MatchesFilter(string filterLower)
        {
            if (NameLower.Contains(filterLower)) return true;

            if (isDirectory)
            {
                foreach (var child in children)
                    if (child.MatchesFilter(filterLower)) return true;
            }
            return false;
        }
    }

    [Serializable]
    public class ExportSettings
    {
        public bool includeFileTree = true;
        public bool includeFileContents = true;
        public bool includeAssetInfo = true;
        public bool includeLineNumbers = false;
        public bool groupByFolder = true;
        public bool includeTOC = false;
        public int headerDepth = 1;
        public int maxFileSizeKB = 512;

        public bool includeProjectName = true;
        public bool includeUnityVersion = true;
        public bool includeExportDate = true;
        public string customHeader = "";

        public string scriptExtensionsRaw = ".cs, .shader, .cginc, .hlsl, .compute, .json, .xml, .yaml";
        public string excludedFoldersRaw = "Plugins, TextMesh Pro";

        [NonSerialized] public HashSet<string> scriptExtensions = new HashSet<string>();

        // FIX #4: cached excluded folders set (O(1) lookup)
        [NonSerialized]
        public HashSet<string> excludedFolders =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void ParseExtensions()
        {
            scriptExtensions = new HashSet<string>(
                scriptExtensionsRaw.Split(',')
                    .Select(s => s.Trim().ToLowerInvariant())
                    .Where(s => !string.IsNullOrEmpty(s)));
        }

        public void ParseExcludedFolders()
        {
            excludedFolders = new HashSet<string>(
                excludedFoldersRaw.Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s)),
                StringComparer.OrdinalIgnoreCase);
        }

        public ExportSettings()
        {
            ParseExtensions();
            ParseExcludedFolders();
        }
    }
}