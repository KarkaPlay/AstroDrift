#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// Редакторная утилита: строит UI сцены Game по макету «Menu & Transitions v2» (ArtDirection §2–§3):
/// • StartPanel: заголовок ASTRO DRIFT (96 px Light, трекинг +12 %) в зоне 0.14–0.26,
///   Best (34 px, SecondaryText, капс) под ним, CTA в зоне 0.72–0.82 — текст + тонкая
///   линия-индикатор с бегущим золотым сегментом (§7). НИ ОДНОЙ плашки/рамки (§8).
/// • DeathPanel: Score (88 px Light) / BEST / NEW BEST (золото) + текстовые кнопки
///   с разделителями UiLine — без рамок и плашек.
/// • PausePanel: оверлей UiOverlay + текстовый список (каскад §4.4).
/// • §8 (новая редакция): скрытая панель — НЕАКТИВНА. Сцена сохраняется с выключенными
///   Death/Pause/HUD/Perk/щитом — невидимое не виснет в сцене и не ловит клики.
/// Меню: AstroDrift → Setup Scene UI.
/// </summary>
public static class AstroDriftSceneSetup
{
    /// <summary>ТЗ v1.10: папка префабов меню (MenuLogo (child StartPanel), LevelCard, MenuButton*, StartPanel).</summary>
    private const string MenuPrefabFolder = "Assets/Prefabs/Menu";

    /// <summary>Префабы оверлея перк-левелапа (AstroDrift → Build LevelUp Prefabs).</summary>
    private const string LevelUpPrefabFolder = "Assets/Prefabs/LevelUp";

    /// <summary>Префаб экрана смерти (AstroDrift → Build Death Prefab, GDD_DeathScreen_v3 §2).</summary>
    private const string DeathPrefabFolder = "Assets/Prefabs/Death";

    // ОТКЛЮЧЕНО (инцидент 2026-09-22): перестраивает иерархию Canvas по кодовому шаблону и в конце
    // вызывает EditorSceneManager.SaveScene — молча затирает ручную раскладку меню владельца.
    // Метод оставлен для вызова из кода (карта локализационных ключей §8.2 ссылается на него).
    // [MenuItem("AstroDrift/Setup Scene UI")]
    public static void SetupSceneUI()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var log = new System.Text.StringBuilder();

        // ——— Canvas ———
        var canvasGo = FindSceneObject("Canvas");
        if (canvasGo == null) canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        if (canvasGo.GetComponent<GraphicRaycaster>() == null) canvasGo.AddComponent<GraphicRaycaster>();

        // ——— EventSystem ———
        var esGo = FindSceneObject("EventSystem");
        if (esGo == null)
        {
            esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }

        Color scoreTextCol = Palette.ScoreText;
        Color secondaryCol = Palette.SecondaryText;

        // ——— HUD (геймплейный — не трогаем, только ссылка) ———
        var hudGo = FindSceneObject("Hud");
        if (hudGo == null)
        {
            hudGo = NewPanel(canvas.transform, "Hud", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1080, 200));
            hudGo.GetComponent<Image>().enabled = false;
            NewText(hudGo.transform, "ScoreText", "0", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), 56, scoreTextCol, TextAlignmentOptions.Center);
            var comboT = NewText(hudGo.transform, "ComboChip", "x2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(420f, -68f), 28, Palette.ComboColor(2), TextAlignmentOptions.Left);
            comboT.gameObject.SetActive(false);
        }
        // Кнопка паузы живёт в HUD и выключается в конце сетапа → GameObject.Find её не находит,
        // и каждый повторный прогон плодил копии. Ищем по трансформу (находит и неактивные) + чистим старые дубли.
        PruneDuplicates(hudGo.transform, "Btn_Pause");

        // ——— HUD: прогресс до следующего перка (GDD §15.3) ———
        // HUD НЕ проходит ClearChildren (геймплейный) → только find-or-create, иначе дубли при повторном прогоне.
        // РЕГРЕССИЯ, которую чиним: раньше здесь создавался второй, legacy-бар
        // Hud/PerkProgressBarBg на y = −172, а канонический бар живёт в группе Hud/Score.
        // В сцене оказывались ДВА бара перка, и GameUI.perkProgressBarFill указывал на мёртвый.
        // Нода удаляется из сцены и БОЛЬШЕ НЕ СОЗДАЁТСЯ здесь — иначе вернулась бы на следующем прогоне.
        var legacyPerkBar = hudGo.transform.Find("PerkProgressBarBg");
        if (legacyPerkBar != null)
        {
            Object.DestroyImmediate(legacyPerkBar.gameObject);
            log.Append("legacy Hud/PerkProgressBarBg удалён; ");
        }

        var perkBarBgGo = FindInHierarchy(hudGo.transform, "PerkProgressBarBg");
        if (perkBarBgGo == null)
        {
            var perkBarBg = NewPanel(hudGo.transform, "PerkProgressBarBg", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(360f, 8f));
            perkBarBg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
            perkBarBgGo = perkBarBg;
        }
        // Фон бара перка — только визуал: тапы должен ловить Btn_TapToPlay под ним.
        perkBarBgGo.GetComponent<Image>().raycastTarget = false;
        var perkFillImg = FindInHierarchy(perkBarBgGo.transform, "PerkProgressBarFill")?.GetComponent<Image>();
        if (perkFillImg == null) perkFillImg = MakeFill(perkBarBgGo.transform, "PerkProgressBarFill", Palette.XpBar);
        perkFillImg.raycastTarget = false; // заливка тоже не ловит raycast

        // Счёт и чип комбо лежат под Hud/Score (VerticalLayoutGroup), а не в корне HUD:
        // transform.Find("ScoreText") от корня их НЕ находит — счёт молча оставался нулём.
        var scoreT = FindInHierarchy(hudGo.transform, "ScoreText")?.GetComponent<TextMeshProUGUI>();
        var comboChipT = FindInHierarchy(hudGo.transform, "ComboChip")?.GetComponent<TextMeshProUGUI>();
        if (scoreT == null) Debug.LogError("AstroDrift SceneSetup: не найден HUD-счёт (Hud/Score/ScoreText) — scoreText останется пустым.");
        if (perkFillImg == null) Debug.LogError("AstroDrift SceneSetup: не найдена заливка бара перка (PerkProgressBarFill).");

        var pauseBtnGo = hudGo.transform.Find("Btn_Pause")?.gameObject;
        if (pauseBtnGo == null)
        {
            pauseBtnGo = NewPanel(hudGo.transform, "Btn_Pause", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-70f, -70f), new Vector2(64f, 64f));
            pauseBtnGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
            var b = pauseBtnGo.AddComponent<Button>();
            b.targetGraphic = pauseBtnGo.GetComponent<Image>();
            var ptxt = NewText(pauseBtnGo.transform, "Text", "| |", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 30, Color.white, TextAlignmentOptions.Center);
            ptxt.rectTransform.sizeDelta = new Vector2(60, 60);
        }

        // ——— StartPanel (ТЗ v1.10) ———
        // Панель = связанный инстанс StartPanel.prefab: меню правится в префабе инспектором.
        // Утилита НЕ пересобирает содержимое по кускам (иначе повторный прогон воскрешал
        // текстовый логотип Title1/Title2 и сносил префаб-инстанс).
        var startPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPrefabFolder + "/StartPanel.prefab");
        var startGo = FindSceneObject("StartPanel");
        if (startPrefab == null)
        {
            Debug.LogError("AstroDrift SceneSetup: не найден " + MenuPrefabFolder + "/StartPanel.prefab — меню не собрано.");
            log.Append("StartPanel.prefab MISSING; ");
        }
        else if (startGo == null || PrefabUtility.GetPrefabInstanceHandle(startGo) == null)
        {
            if (startGo != null) Object.DestroyImmediate(startGo); // распакованная/старая панель — заменяем инстансом
            startGo = (GameObject)PrefabUtility.InstantiatePrefab(startPrefab, canvas.transform);
            startGo.name = "StartPanel";
            log.Append("StartPanel: инстанс префаба создан; ");
        }
        else
        {
            log.Append("StartPanel: инстанс уже есть; ");
        }
        if (startGo == null)
        {
            startGo = new GameObject("StartPanel");
            startGo.transform.SetParent(canvas.transform, false);
            var srtFallback = startGo.AddComponent<RectTransform>();
            srtFallback.anchorMin = srtFallback.anchorMax = srtFallback.pivot = new Vector2(0.5f, 0.5f);
            srtFallback.sizeDelta = new Vector2(1080, 1920);
        }

        // Логотип-картинка (ТЗ v1.10): спрайт New UI/Logo.png. Текстовых Title1/Title2 больше нет.
        var logoGo = FindInHierarchy(startGo.transform, "MenuLogo");
        var logoImg = logoGo != null ? logoGo.GetComponent<Image>() : null;
        // Best: под заголовком, центр ~0.30 высоты (зазор ≥ 80 px до носа корабля — §2).
        // Рекорд меню разбит на ДВЕ ноды: подпись (ключ best_label) + число (best_value).
        // find-or-create: ноды уже есть в StartPanel.prefab — вторые создавать нельзя.
        var bestT = FindInHierarchy(startGo.transform, "StartBest")?.GetComponent<TextMeshProUGUI>();
        if (bestT == null)
            bestT = NewText(startGo.transform, "StartBest", "РЕКОРД", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 438), 34, secondaryCol, TextAlignmentOptions.Center);

        // Число рекорда: старое имя «StartBest (1)» — случайный дубль, суффикс «(1)»
        // делал нод неразрешимым FindInHierarchy (число никто не обновлял). Переименовываем.
        var bestValueGo = FindInHierarchy(startGo.transform, "StartBestValue")
                          ?? FindInHierarchy(startGo.transform, "StartBest (1)");
        if (bestValueGo != null) bestValueGo.name = "StartBestValue";
        var bestValueT = bestValueGo != null ? bestValueGo.GetComponent<TextMeshProUGUI>() : null;
        if (bestValueT == null)
            bestValueT = NewText(startGo.transform, "StartBestValue", "0", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 378f), 34, secondaryCol, TextAlignmentOptions.Center);
        // Обе ноды — не интерактивные: тап обязан доходить до полноэкранной зоны Btn_TapToPlay.
        bestT.raycastTarget = false;
        bestValueT.raycastTarget = false;

        // CTA: зона 0.72–0.82 → центр 0.77 высоты (y = −520 от центра). Только текст (пульс §3).
        // ux4-5: кликабельная зона — весь экран: тап в любой точке стартует игру.
        // Невидимый Image под текстами (первый ребёнок → нижний порядок raycast).
        // Запас ±300/±300 px за края панели — покрытие при нестандартных аспектах
        // (CanvasScaler match 0.5 в dev-окнах растягивает Canvas выше панели).
        var ctaGo = FindInHierarchy(startGo.transform, "Btn_TapToPlay");
        if (ctaGo == null)
        {
            ctaGo = NewPanel(startGo.transform, "Btn_TapToPlay", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1680, 2520));
            ctaGo.GetComponent<Image>().color = new Color(0, 0, 0, 0); // невидимая кликабельная зона
            var tapBtnNew = ctaGo.AddComponent<Button>();
            tapBtnNew.targetGraphic = ctaGo.GetComponent<Image>();
        }
        ctaGo.transform.SetSiblingIndex(0); // нижний порядок raycast — кнопки меню перехватывают тап раньше
        var tapBtn = ctaGo.GetComponent<Button>();
        // ux4-6: линия-индикатор с бегущим золотым сегментом удалена (второе золото в кадре);
        // остаётся текст CTA в исходной позиции с пульсом прозрачности.
        var ctaT = FindInHierarchy(startGo.transform, "CtaText")?.GetComponent<TextMeshProUGUI>();
        if (ctaT == null)
        {
            ctaT = NewText(startGo.transform, "CtaText", "TAP TO PLAY", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -480), 44, scoreTextCol, TextAlignmentOptions.Center);
            ctaT.rectTransform.sizeDelta = new Vector2(600, 80);
        }

        // ——— UI v3 / v1.10: мета-прогрессия (GDD §11) — карточка уровня + 3 нижние кнопки + щит ———
        // Карточка — инстанс LevelCard.prefab (610×128, ТЗ владельца). Старые PilotLevelText/
        // XpBarBg/XpBarFill удалены: их заменили карточка и её бар 364×24 (#252C34 + Mask).
        // В инстансе перезаписываем только раскладку (anchoredPosition/sizeDelta) — структура
        // и содержимое остаются префабными, связь с префабом сохраняется.
        var levelCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPrefabFolder + "/LevelCard.prefab");
        GameObject levelCardGo = null;
        LevelCardUI levelCardUi = null;
        TextMeshProUGUI pilotT = null;
        RectTransform xpFillRt = null;
        if (levelCardPrefab != null)
        {
            levelCardGo = FindInHierarchy(startGo.transform, "LevelCard");
            if (levelCardGo == null)
                levelCardGo = (GameObject)PrefabUtility.InstantiatePrefab(levelCardPrefab, startGo.transform);
            levelCardGo.name = "LevelCard";
            var cardRt = levelCardGo.GetComponent<RectTransform>();
            cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(610f, 128f);
            // 210 (не 295): авторская раскладка префаба — источник истины, а GameUI
            // масштабирует стартовую группу целиком от неё. 295 сдвигал бы карточку при каждом прогоне.
            cardRt.anchoredPosition = new Vector2(0f, 210f); // ряд карточки — выше ряда кнопок
            levelCardUi = levelCardGo.GetComponent<LevelCardUI>();
            var labelTr = levelCardGo.transform.Find("LevelLabel");
            pilotT = labelTr != null ? labelTr.GetComponent<TextMeshProUGUI>() : null;
            var fillTr = levelCardGo.transform.Find("Bottom/ProgressRoot/Fill");
            xpFillRt = fillTr as RectTransform;
            log.Append("LevelCard OK; ");
        }
        else log.Append("LevelCard.prefab MISSING; ");

        // Три нижние кнопки (префаб + варианты): фон/иконка/подпись — в префабе, клик — MenuButtonUI.
        // Btn_TapToPlay стоит SetSiblingIndex(0), поэтому кнопки перехватывают тап раньше:
        // тап по кнопке НЕ стартует игру, тап по пустому месту — стартует.
        // Кнопки живут в ноде-ряду «Menu Buttons» (HorizontalLayoutGroup): только так
        // GameUI может увести/вернуть весь ряд ОДНИМ SlideFade. Сами кнопки стоят
        // на anchoredPosition 0,0, а раскладку им задаёт группа — двигать их по отдельности
        // нельзя (позиции перезапишет лэйаут-группа при следующем ребилде).
        var menuRowGo = FindInHierarchy(startGo.transform, "Menu Buttons");
        if (menuRowGo == null && !Application.isPlaying)
        {
            var rowGo = new GameObject("Menu Buttons", typeof(RectTransform));
            rowGo.transform.SetParent(startGo.transform, false);
            var rrt = (RectTransform)rowGo.transform;
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = new Vector2(1f, 0f);
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.anchoredPosition = new Vector2(0f, 120f);
            rrt.sizeDelta = new Vector2(-56f, 208.26f);
            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.spacing = 0f;
            menuRowGo = rowGo;
            log.Append("Menu Buttons (ряд) создан; ");
        }
        if (menuRowGo != null)
        {
            // Миграция: кнопки могли остаться прямыми детьми StartPanel (имена — как у ассетов).
            foreach (var n in new[] { "MenuButton_Settings", "MenuButton_Upgrade", "MenuButton_Shop" })
            {
                var btnGo = FindInHierarchy(startGo.transform, n);
                if (btnGo != null && btnGo.transform.parent != menuRowGo.transform)
                    btnGo.transform.SetParent(menuRowGo.transform, false);
            }
        }
        // Шаг 300: фон кнопки — спрайт 848×512 с preserveAspect, при 232×120 реально
        // занимает ≈199 px по ширине. Прежний шаг 186 давал наложение кнопок друг на друга.
        var rowParent = menuRowGo != null ? menuRowGo.transform : startGo.transform;
        PlaceMenuButton(rowParent, "Btn_MenuSettings", "MenuButton_Settings.prefab", new Vector2(-300f, 118f), log);
        PlaceMenuButton(rowParent, "Btn_MenuUpgrade", "MenuButton_Upgrade.prefab", new Vector2(0f, 118f), log);
        PlaceMenuButton(rowParent, "Btn_MenuShop", "MenuButton_Shop.prefab", new Vector2(300f, 118f), log);
        var menuRowRt = menuRowGo != null ? (RectTransform)menuRowGo.transform : null;
        // «ПРОКАЧКА» — единственный вход в панель дерева разблокировок (кнопка-заглушка
        // «ДЕРЕВО» в углу удалена). Аналитика остаётся на MenuButtonUI этой кнопки.
        // Имя ноды в сцене — Btn_MenuUpgrade, имя ассета — MenuButton_Upgrade: ищем оба,
        // иначе ссылка молча оставалась пустой и дерево разблокировок не открывалось.
        var menuUpgradeBtn = (FindInHierarchy(rowParent, "MenuButton_Upgrade")
                              ?? FindInHierarchy(rowParent, "Btn_MenuUpgrade"))?.GetComponent<Button>();
        if (menuUpgradeBtn == null) Debug.LogError("AstroDrift SceneSetup: не найдена кнопка «ПРОКАЧКА» (MenuButton_Upgrade) — дерево разблокировок не открыть.");

        // Кнопка стартового щита: под нижним рядом, discreet (текст 28 + caption 20)
        var shieldGo = FindInHierarchy(startGo.transform, "Btn_StartShield");
        if (shieldGo == null)
        {
            shieldGo = NewPanel(startGo.transform, "Btn_StartShield", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 44), new Vector2(560, 110));
            shieldGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            var shieldBtnNew = shieldGo.AddComponent<Button>();
            shieldBtnNew.targetGraphic = shieldGo.GetComponent<Image>();
            NewText(shieldGo.transform, "ShieldText", "СТАРТОВЫЙ ЩИТ", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 28, Palette.PickupShield, TextAlignmentOptions.Center);
            var cap = NewText(shieldGo.transform, "ShieldCaption", "ЗА ПРОСМОТР РЕКЛАМЫ · 1/ДЕНЬ", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -36), 20, secondaryCol, TextAlignmentOptions.Center);
            cap.rectTransform.sizeDelta = new Vector2(560, 30);
        }
        // §8 (GDD_DeathScreen_v3): «+N XP» главного экрана — над LevelCard (0, 372), не в таблице смерти.
        // Нода живёт в СЦЕНЕ (find-or-create), а не в StartPanel.prefab: префаб меню не трогаем,
        // а повторный прогон сетапа восстановит ноду после пересборки меню.
        var xpGainGo = FindInHierarchy(startGo.transform, "StartXpGain");
        if (xpGainGo == null)
        {
            var xpGainT = NewText(startGo.transform, "StartXpGain", "+0 XP", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 372), 34, Palette.XpBar, TextAlignmentOptions.Center);
            xpGainT.rectTransform.sizeDelta = new Vector2(400, 50);
            xpGainT.raycastTarget = false; // тап обязан доходить до Btn_TapToPlay
            AddLocalize(xpGainT, "xp_gain");
            AddRole(xpGainT, TypographyStyles.Secondary);
            xpGainGo = xpGainT.gameObject;
        }
        // Стартовое состояние §8: скрытое — неактивно (GameUI показывает его тиком анимации §8.3).
        EnsureCanvasGroup(xpGainGo, visible: false);

        // ——— SettingsPanel (экран настроек): инстанс SettingsPanel.prefab внутри StartPanel ———
        // Внутри StartPanel, а не в корне Canvas: логотип и меню остаются видны ПОД экраном
        // (затемнение — Image цвета UiOverlay), логотип никуда не переезжает.
        // Последний sibling → выше «Menu Buttons» в порядке raycast (кнопки меню недоступны,
        // пока экран открыт) и выше Btn_TapToPlay.
        var settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPrefabFolder + "/SettingsPanel.prefab");
        GameObject settingsGo = FindInHierarchy(startGo.transform, "SettingsPanel");
        if (settingsPrefab == null)
        {
            Debug.LogError("AstroDrift SceneSetup: не найден " + MenuPrefabFolder + "/SettingsPanel.prefab — экран настроек не собран (AstroDrift → Build Settings Prefab).");
            log.Append("SettingsPanel.prefab MISSING; ");
        }
        else if (settingsGo == null || PrefabUtility.GetPrefabInstanceHandle(settingsGo) == null)
        {
            if (settingsGo != null) Object.DestroyImmediate(settingsGo); // распакованная/старая — заменяем инстансом
            settingsGo = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab, startGo.transform);
            settingsGo.name = "SettingsPanel";
            log.Append("SettingsPanel: инстанс префаба создан; ");
        }
        else log.Append("SettingsPanel: инстанс уже есть; ");

        SettingsScreen settingsScreen = null;
        if (settingsGo != null)
        {
            var srt = settingsGo.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = Vector2.zero;
            srt.sizeDelta = new Vector2(1080f, 1920f);
            settingsGo.transform.SetAsLastSibling(); // topmost среди детей StartPanel
            settingsScreen = settingsGo.GetComponent<SettingsScreen>();
            if (settingsScreen == null) settingsScreen = settingsGo.AddComponent<SettingsScreen>();
            // menuRoot — сценовая ссылка (в префабе её быть не может)
            var settingsSo = new SerializedObject(settingsScreen);
            var menuRootProp = settingsSo.FindProperty("menuRoot");
            if (menuRootProp != null) menuRootProp.objectReferenceValue = startGo;
            settingsSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settingsScreen);
            // §8: сохранённый экран настроек неактивен (GameUI/SettingsScreen покажут его каскадом)
            EnsureCanvasGroup(settingsGo, visible: false);
        }

        // Кнопка «НАСТРОЙКИ» должна знать, ЧТО открывать: typed action + ссылка на экран.
        var settingsBtnUi = (FindInHierarchy(rowParent, "MenuButton_Settings")
                             ?? FindInHierarchy(rowParent, "Btn_MenuSettings"))?.GetComponent<MenuButtonUI>();
        if (settingsBtnUi != null && settingsScreen != null)
        {
            var mbSo = new SerializedObject(settingsBtnUi);
            var actProp = mbSo.FindProperty("action");
            if (actProp != null) actProp.enumValueIndex = 1; // MenuAction.OpenSettings
            var tgtProp = mbSo.FindProperty("targetScreen");
            if (tgtProp != null) tgtProp.objectReferenceValue = settingsScreen;
            mbSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settingsBtnUi);
        }
        else if (settingsBtnUi == null)
            Debug.LogWarning("AstroDrift SceneSetup: не найдена кнопка «НАСТРОЙКИ» (MenuButton_Settings) — экран настроек не откроется.");

        var shieldRt = shieldGo.GetComponent<RectTransform>();
        shieldRt.anchoredPosition = new Vector2(0f, -34f); // ниже ряда кнопок (низ меню)
        var shieldBtn = shieldGo.GetComponent<Button>();
        var shieldText = FindInHierarchy(shieldGo.transform, "ShieldText")?.GetComponent<TextMeshProUGUI>();
        var shieldCap = FindInHierarchy(shieldGo.transform, "ShieldCaption")?.GetComponent<TextMeshProUGUI>();
        // Кнопка скрыта по умолчанию (гейт уровня 8 решает GameUI.RefreshPilotBlock).
        // §8: в сохранённой сцене кнопка выключена целиком — невидимая зона 560×110
        // больше не ловит тапы по меню.
        EnsureCanvasGroup(shieldGo, visible: false);
        var shieldCg = shieldGo.GetComponent<CanvasGroup>();
        shieldCg.alpha = 0f; shieldCg.blocksRaycasts = false;
        foreach (var t in shieldGo.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            var g = t.gameObject;
            if (g.GetComponent<CanvasGroup>() == null) g.AddComponent<CanvasGroup>();
            g.GetComponent<CanvasGroup>().alpha = 0f;
        }

        // ——— DeathPanel (GDD_DeathScreen_v3 §2): связанный инстанс префаба ———
        // Дом-паттерн StartPanel выше: содержимое правится в Assets/Prefabs/Death/DeathPanel.prefab
        // (AstroDrift → Build Death Prefab). Утилита НЕ собирает узлы руками — иначе повторный
        // прогон воскрешал бы удалённые §3 ноды (DeathNewBest/DeathXp/DeathLevel/DeathUnlocked/
        // SepLine/ContinueTimerLine) и сносил префаб-инстанс.
        var deathPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeathPrefabFolder + "/DeathPanel.prefab");
        var deathGo = FindSceneObject("DeathPanel");
        if (deathPrefab == null)
        {
            Debug.LogError("AstroDrift SceneSetup: не найден " + DeathPrefabFolder + "/DeathPanel.prefab — Death-экран не собран (AstroDrift → Build Death Prefab).");
            log.Append("DeathPanel.prefab MISSING; ");
        }
        else if (deathGo == null || PrefabUtility.GetPrefabInstanceHandle(deathGo) == null)
        {
            if (deathGo != null) Object.DestroyImmediate(deathGo); // распакованная/старая панель — заменяем инстансом
            deathGo = (GameObject)PrefabUtility.InstantiatePrefab(deathPrefab, canvas.transform);
            deathGo.name = "DeathPanel";
            log.Append("DeathPanel: инстанс префаба создан; ");
        }
        else
        {
            log.Append("DeathPanel: инстанс уже есть; ");
        }
        if (deathGo == null)
        {
            deathGo = new GameObject("DeathPanel");
            deathGo.transform.SetParent(canvas.transform, false);
            var drtFallback = deathGo.AddComponent<RectTransform>();
            drtFallback.anchorMin = drtFallback.anchorMax = drtFallback.pivot = new Vector2(0.5f, 0.5f);
            drtFallback.sizeDelta = new Vector2(1080, 1920);
        }
        // Раскладку и содержимое держит префаб; сцена только ставит панель по центру 1080×1920.
        var deathRt = deathGo.GetComponent<RectTransform>();
        deathRt.anchorMin = deathRt.anchorMax = deathRt.pivot = new Vector2(0.5f, 0.5f);
        deathRt.anchoredPosition = Vector2.zero;
        deathRt.sizeDelta = new Vector2(1080, 1920);

        // Ссылки GameUI — из инстанса по именам нод префаба (§2). Динамика (числа рекорда/
        // счёта) подаётся Arguments'ами из GameUI.PlayDeathIn; подписи — статичные LSE префаба.
        var deathSkullRt = FindInHierarchy(deathGo.transform, "DeathSkull")?.GetComponent<RectTransform>();
        var deathTitleRt = FindInHierarchy(deathGo.transform, "DeathTitle")?.GetComponent<RectTransform>();
        var deathSubtitleRt = FindInHierarchy(deathGo.transform, "DeathSubtitle")?.GetComponent<RectTransform>();
        var deathScorePanelRt = FindInHierarchy(deathGo.transform, "DeathScorePanel")?.GetComponent<RectTransform>();
        var deathScoreT = FindInHierarchy(deathGo.transform, "DeathScore")?.GetComponent<TextMeshProUGUI>();
        var deathBestT = FindInHierarchy(deathGo.transform, "DeathBest")?.GetComponent<TextMeshProUGUI>();
        var continueBtn = FindInHierarchy(deathGo.transform, "Btn_Continue")?.GetComponent<Button>();
        var continueT = FindInHierarchy(deathGo.transform, "ContinueText")?.GetComponent<TextMeshProUGUI>();
        var continueCapT = FindInHierarchy(deathGo.transform, "ContinueCaption")?.GetComponent<TextMeshProUGUI>();
        var timerFillRt = FindInHierarchy(deathGo.transform, "ContinueTimerFill")?.GetComponent<RectTransform>();
        var homeBtn = FindInHierarchy(deathGo.transform, "Btn_Home")?.GetComponent<Button>();
        if (deathSkullRt == null || deathTitleRt == null || deathSubtitleRt == null || deathScorePanelRt == null
            || deathScoreT == null || deathBestT == null || continueBtn == null || homeBtn == null || timerFillRt == null)
            Debug.LogError("AstroDrift SceneSetup: в DeathPanel.prefab не хватает нод/компонентов (§2) — часть ссылок GameUI останется пустой. Пересоберите префаб: AstroDrift → Build Death Prefab.");

        // ——— PausePanel (§4.4: оверлей + текстовый список, каскад) ———
        var pauseGo = FindSceneObject("PausePanel");
        if (pauseGo == null) pauseGo = NewPanel(canvas.transform, "PausePanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 1920));
        ClearChildren(pauseGo.transform);
        pauseGo.GetComponent<Image>().color = Palette.UiOverlay;

        var pauseTitle = NewText(pauseGo.transform, "PauseTitle", "PAUSE", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 160), 48, scoreTextCol, TextAlignmentOptions.Center);
        AddLocalize(pauseTitle, "pause_title");
        var resumeBtn = NewTextButton(pauseGo.transform, "Btn_Resume", "RESUME", new Vector2(0, 20), 420, "resume");
        var quitBtn = NewTextButton(pauseGo.transform, "Btn_Home", "HOME", new Vector2(0, -100), 420, "home");
        var sep2Go = NewPanel(pauseGo.transform, "SepLine", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(420, 2));
        sep2Go.GetComponent<Image>().color = Palette.UiLine;

        // ——— PerkPanel (GDD §15.3): инстанс LevelUpPanel.prefab, правится инспектором ———
        var levelUpPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelUpPrefabFolder + "/LevelUpPanel.prefab");
        if (levelUpPrefab == null)
        {
            Debug.LogError("AstroDrift SceneSetup: не найден " + LevelUpPrefabFolder + "/LevelUpPanel.prefab — оверлей перка не собран (AstroDrift → Build LevelUp Prefabs).");
            log.Append("LevelUpPanel.prefab MISSING; ");
        }
        var perkGo = FindSceneObject("PerkPanel");
        bool perkIsPrefabInstance = perkGo != null && PrefabUtility.IsPartOfPrefabInstance(perkGo);
        if (levelUpPrefab != null && !perkIsPrefabInstance)
        {
            if (perkGo != null) Object.DestroyImmediate(perkGo); // распакованная/старая панель — заменяем инстансом
            perkGo = (GameObject)PrefabUtility.InstantiatePrefab(levelUpPrefab, canvas.transform);
            perkGo.name = "PerkPanel";
        }
        if (perkGo != null)
        {
            var perkRt = perkGo.GetComponent<RectTransform>();
            perkRt.anchorMin = perkRt.anchorMax = perkRt.pivot = new Vector2(0.5f, 0.5f);
            perkRt.anchoredPosition = Vector2.zero;
            perkRt.sizeDelta = new Vector2(1080, 1920);
            perkGo.transform.SetAsLastSibling(); // поверх всех панелей
            EnsureCanvasGroup(perkGo, visible: false);
        }

        // Компонент живёт на самой панели (её и показывает/скрывает), а не на отдельном объекте.
        var strayUi = FindSceneObject("PerkChoiceUI");
        if (strayUi != null && (perkGo == null || strayUi != perkGo)) Object.DestroyImmediate(strayUi);
        var perkUi = perkGo != null ? perkGo.GetComponent<PerkChoiceUI>() : null;
        if (perkGo != null && perkUi == null) perkUi = perkGo.AddComponent<PerkChoiceUI>();

        var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelUpPrefabFolder + "/UpgradeCard.prefab");
        var cardNewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelUpPrefabFolder + "/UpgradeCard_New.prefab");
        if (cardPrefab == null || cardNewPrefab == null) log.Append("UpgradeCard*.prefab MISSING; ");
        if (perkGo != null)
        {
            var cardsRt = perkGo.transform.Find("CardsRoot") as RectTransform;
            var perkTitle = perkGo.transform.Find("LevelUpTitle")?.GetComponent<TextMeshProUGUI>();
            var rerollGo = perkGo.transform.Find("Btn_Reroll");
            perkUi.Init(perkGo, cardsRt, perkTitle,
                rerollGo != null ? rerollGo.GetComponent<Button>() : null,
                rerollGo != null ? rerollGo.Find("RerollText")?.GetComponent<TextMeshProUGUI>() : null,
                rerollGo != null ? rerollGo.Find("RerollCaption")?.GetComponent<TextMeshProUGUI>() : null,
                cardPrefab, cardNewPrefab);
        }

        // ——— GameUI с сериализованными ссылками ———
        var uiGo = FindSceneObject("GameUI");
        if (uiGo == null) uiGo = new GameObject("GameUI");
        var ui = uiGo.GetComponent<GameUI>();
        if (ui == null) ui = uiGo.AddComponent<GameUI>();

        var so = new SerializedObject(ui);
        SetRef(so, "hudRoot", hudGo, log);
        SetRef(so, "startPanel", startGo, log);
        SetRef(so, "deathPanel", deathGo, log);
        SetRef(so, "pausePanel", pauseGo, log);
        SetRef(so, "pauseBtn", pauseBtnGo, log);
        SetRef(so, "logoImage", logoImg, log);
        SetRef(so, "startBest", bestT, log);
        SetRef(so, "startBestValue", bestValueT, log);
        SetRef(so, "menuButtonsRow", menuRowRt, log);
        SetRef(so, "menuUpgradeBtn", menuUpgradeBtn, log);
        SetRef(so, "settingsPanel", settingsScreen, log);
        SetRef(so, "ctaText", ctaT, log);
        SetRef(so, "tapToPlayBtn", tapBtn, log);
        SetRef(so, "scoreText", scoreT, log);
        SetRef(so, "comboChip", comboChipT, log);
        SetRef(so, "deathScore", deathScoreT, log);
        SetRef(so, "deathBest", deathBestT, log);
        SetRef(so, "continueBtn", continueBtn, log);
        SetRef(so, "continueText", continueT, log);
        SetRef(so, "continueCaption", continueCapT, log);
        SetRef(so, "continueTimerFill", timerFillRt, log);
        SetRef(so, "deathSkull", deathSkullRt, log);
        SetRef(so, "deathTitle", deathTitleRt, log);
        SetRef(so, "deathSubtitle", deathSubtitleRt, log);
        SetRef(so, "deathScorePanel", deathScorePanelRt, log);
        SetRef(so, "homeBtn", homeBtn, log);
        SetRef(so, "pauseToggleBtn", pauseBtnGo.GetComponent<Button>(), log);
        SetRef(so, "resumeBtn", resumeBtn, log);
        SetRef(so, "quitBtn", quitBtn, log);
        SetRef(so, "pilotLevelText", pilotT, log);
        SetRef(so, "levelCard", levelCardUi, log);
        SetRef(so, "xpBarFill", xpFillRt, log);
        SetRef(so, "startShieldBtn", shieldBtn, log);
        SetRef(so, "startShieldText", shieldText, log);
        SetRef(so, "startShieldCaption", shieldCap, log);
        SetRef(so, "perkProgressBarFill", perkFillImg, log);
        SetRef(so, "startXpGain", FindInHierarchy(startGo.transform, "StartXpGain")?.GetComponent<TextMeshProUGUI>(), log);
        so.ApplyModifiedPropertiesWithoutUndo();

        // Начальные состояния (§8): скрытые панели в сохранённой сцене — НЕАКТИВНЫ.
        EnsureCanvasGroup(startGo, visible: true);
        EnsureCanvasGroup(deathGo, visible: false);
        EnsureCanvasGroup(pauseGo, visible: false);
        EnsureCanvasGroup(hudGo, visible: false);
        pauseBtnGo.SetActive(false);

        EditorUtility.SetDirty(ui);
        EditorUtility.SetDirty(perkUi);
        MarkSceneDirty(scene);
        // Без явного SaveScene §8-состояние (скрытое = неактивно) оставалось только в памяти
        // редактора: в файле сцены панели по-прежнему активны.
        EditorSceneManager.SaveScene(scene);
        Debug.Log("AstroDrift SceneSetup v2: UI построен по макету §2–§3. " + log);
    }

    /// <summary>Оставить первого ребёнка с именем name, остальных одноимённых удалить
    /// (для объектов вне ClearChildren, например HUD).</summary>
    private static void PruneDuplicates(Transform parent, string name)
    {
        bool kept = false;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var c = parent.GetChild(i);
            if (c.name != name) continue;
            if (!kept) { kept = true; continue; }
            Object.DestroyImmediate(c.gameObject);
        }
    }

    /// <summary>Поиск сценового объекта по имени, ВКЛЮЧАЯ неактивные (§8: скрытые панели
    /// выключены → GameObject.Find их не находит и утилита плодила бы дубли при повторном прогоне).</summary>
    private static GameObject FindSceneObject(string name)
    {
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var r in roots)
        {
            if (r.name == name) return r;
            var found = FindInHierarchy(r.transform, name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>Поиск по имени в иерархии, включая неактивные узлы (GameObject.Find их не видит).</summary>
    private static GameObject FindInHierarchy(Transform parent, string name)
    {
        if (parent == null) return null;
        var all = parent.GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
            if (t != parent && t.name == name) return t.gameObject;
        return null;
    }

    /// <summary>Кнопка меню = связанный инстанс варианта MenuButton (фон/иконка/подпись в префабе).
    /// Ищем по ИМЕНИ АССЕТА (MenuButton_Settings и т.п.) И по старому имени ноды (Btn_MenuSettings):
    /// кнопки в сцене уже названы по ассету, и find-or-create по одному имени плодил бы дубли.</summary>
    private static void PlaceMenuButton(Transform parent, string goName, string prefabFile, Vector2 pos, System.Text.StringBuilder log)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPrefabFolder + "/" + prefabFile);
        if (prefab == null) { log.Append(prefabFile + " MISSING; "); return; }

        string assetName = prefabFile.EndsWith(".prefab")
            ? prefabFile.Substring(0, prefabFile.Length - ".prefab".Length) : prefabFile;
        var go = FindInHierarchy(parent, assetName) ?? FindInHierarchy(parent, goName);
        if (go == null) go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        // В ряду «Menu Buttons» раскладку задаёт HorizontalLayoutGroup — позицию не трогаем.
        if (parent.GetComponent<HorizontalLayoutGroup>() == null) rt.anchoredPosition = pos;
        log.Append(assetName + " OK; ");
    }

    /// <summary>Полная очистка детей (обязательно с конца — иначе при удалении в foreach
    /// индексы сдвигаются и остаются дубли-«призраки»).</summary>
    private static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(t.GetChild(i).gameObject);
    }

    /// <summary>Кнопка = текст + невидимая кликабельная зона (≥ 88 pt) — §3.
    /// key — LSE на ноде Text (без тега роли: шрифт остаётся текущим дефолтом, §8.4).</summary>
    private static Button NewTextButton(Transform parent, string name, string label, Vector2 pos, float width, string key)
    {
        var go = NewPanel(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(width, 88));
        go.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        var txt = NewText(go.transform, "Text", label, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 40, Color.white, TextAlignmentOptions.Center);
        txt.rectTransform.sizeDelta = new Vector2(width, 80);
        AddLocalize(txt, key);
        return btn;
    }

    // ————————————— локализация (§8.2): LSE + тег роли —————————————
    // Владелец ноды вешает компоненты сам (§8.0).

    /// <summary>LocalizeStringEvent на ноде + persistent-listener TMP.text
    /// (та же схема, что LocalizeComponent_TMP.SetupForLocalization).</summary>
    private static LocalizeStringEvent AddLocalize(TextMeshProUGUI tmp, string key)
    {
        var lse = tmp.gameObject.AddComponent<LocalizeStringEvent>();
        lse.StringReference.TableReference = "GameTexts";
        lse.StringReference.TableEntryReference = key;
        BindTmpText(lse, tmp);
        return lse;
    }

    private static void BindTmpText(LocalizeStringEvent lse, TextMeshProUGUI tmp)
    {
        var setter = tmp.GetType().GetProperty("text").GetSetMethod();
        var handler = System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction<string>), tmp, setter)
            as UnityEngine.Events.UnityAction<string>;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(lse.OnUpdateString, handler);
        lse.OnUpdateString.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
    }

    /// <summary>Тег шрифтового стиля. Пустой id = тег бездействует (шрифт ноды сохраняется).</summary>
    private static void AddRole(TextMeshProUGUI tmp, string styleId)
    {
        var tag = tmp.gameObject.AddComponent<TypeRoleTag>();
        var so = new SerializedObject(tag);
        so.FindProperty("styleId").stringValue = styleId;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Начальное состояние панели (§8, новая редакция): CanvasGroup + активность.
    /// Скpытая панель — alpha 0, raycasts/interactable off и SetActive(false): так она и в
    /// сохранённую сцену попадёт. Рантайм показывает её через UiAnim.SetVisible.</summary>
    private static void EnsureCanvasGroup(GameObject go, bool visible)
    {
        if (go == null) return;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            // В Play-режиме (сцена уже проинициализирована) компоненты не добавляем:
            // утилита рассчитана на Edit-режим, а префабы несут CanvasGroup заранее.
            if (Application.isPlaying) return;
            cg = go.AddComponent<CanvasGroup>();
        }
        cg.alpha = visible ? 1f : 0f;
        cg.blocksRaycasts = visible;
        cg.interactable = visible;
        go.SetActive(visible);
    }

    private static void SetRef(SerializedObject so, string prop, Object val, System.Text.StringBuilder log)
    {
        var p = so.FindProperty(prop);
        if (p == null) { log.Append("MISSING " + prop + "; "); return; }
        p.objectReferenceValue = val;
        log.Append(prop + " OK; ");
    }

    private static void MarkSceneDirty(UnityEngine.SceneManagement.Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
    }

    /// <summary>Спрайт заливки/фона прогресс-бара. БЕЗ него Image.OnPopulateMesh идёт
    /// по пути «полный квад» и игнорирует Type.Filled — бар рисуется всегда полным.</summary>
    private const string BarSpritePath = "Assets/New UI/Generated/RoundedBar.png";

    /// <summary>Заливка прогресс-бара: ОДИН механизм — Image.Type.Filled, Horizontal/Left,
    /// якоря растянуты (0,0)-(1,1), sizeDelta 0, значение в fillAmount (UiProgressBar.Set).
    /// Прошлый анкорный вариант (Simple + anchorMax.x = t) конфликтовал с Debug-значением
    /// fillAmount = 0.4 в префабе: бар «врал» на любом значении.
    /// Спрайт обязателен: с sprite = null Filled не работает вообще.</summary>
    private static Image MakeFill(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BarSpritePath);
        if (img.sprite == null) Debug.LogError("AstroDrift SceneSetup: спрайт бара не найден — " + BarSpritePath);
        img.raycastTarget = false;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 0f;
        return img;
    }

    private static GameObject NewPanel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0);
        return go;
    }

    private static TextMeshProUGUI NewText(Transform parent, string name, string text, Vector2 anchor, Vector2 pivot, Vector2 pos, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(600, 80);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }
}
#endif
