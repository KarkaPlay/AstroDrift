#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Редакторная утилита (ТЗ v1.10): собирает префабы главного меню, чтобы владелец
/// правил меню в инспекторе без кода.
///   Assets/Prefabs/Menu/MenuLogo.prefab        — логотип-картинка (New UI/Logo.png)
///   Assets/Prefabs/Menu/LevelCard.prefab       — карточка уровня 610×128 + бар 364×24
///   Assets/Prefabs/Menu/MenuButton.prefab      — база нижней кнопки
///   Assets/Prefabs/Menu/MenuButton_{Settings,Upgrade,Shop}.prefab — варианты префаба
///   Assets/Prefabs/Menu/StartPanel.prefab      — меню целиком (инстанс в сцене)
/// Строит всё с нуля (не зависит от состояния сцены) и перезаписывает существующие
/// ассеты, поэтому повторный прогон даёт тот же результат.
/// Меню: AstroDrift → Build Menu Prefabs. После сборки — AstroDrift → Setup Scene UI.
/// </summary>
public static class MenuPrefabBuilder
{
    private const string Folder = "Assets/Prefabs/Menu";
    private const string LogoPath = Folder + "/MenuLogo.prefab";
    private const string LevelCardPath = Folder + "/LevelCard.prefab";
    private const string MenuButtonPath = Folder + "/MenuButton.prefab";
    private const string StartPanelPath = Folder + "/StartPanel.prefab";

    // Раскладка (координаты референса 1080×1920, якорь/пивот центра)
    private const float LogoW = 640f;                 // runtime-ширина; аспект New UI/Logo.png = 1288×512
    private const float CardW = 610f, CardH = 128f;   // по ТЗ владельца
    private const float BarW = 364f, BarH = 24f;      // прогресс-бар карточки
    private const float BtnW = 232f, BtnH = 120f;
    // Фон кнопки — спрайт 848×512 (аспект 1.656) с preserveAspect: при высоте 120
    // реальная ширина ≈199, а не 232. Шаг 186 < 199 давал наложение соседних кнопок.
    private const float BtnSpacing = 300f;

    [MenuItem("AstroDrift/Build Menu Prefabs")]
    public static void BuildAll()
    {
        var log = new System.Text.StringBuilder();
        EnsureFolders();

        var cfg = AssetDatabase.LoadAssetAtPath<TypographyConfig>("Assets/Resources/TypographyConfig.asset");
        if (cfg == null) { Debug.LogError("MenuPrefabBuilder: нет Assets/Resources/TypographyConfig.asset"); return; }
        if (cfg.bodyRegular == null) log.Append("ВНИМАНИЕ: TypographyConfig пуст (шрифт — fallback); ");

        var logoSprite = LoadSprite("Assets/New UI/Logo.png", "Logo");
        var levelIcon = LoadSprite("Assets/New UI/Sheet.png", "Level_Icon");
        var settingsIcon = LoadSprite("Assets/New UI/Sheet.png", "Settings_Icon");
        var shopIcon = LoadSprite("Assets/New UI/Sheet.png", "Shop_Icon");
        var btnBg = LoadSprite("Assets/New UI/MenuButtonBG.png", "MenuButtonBG_0");
        var barSprite = LoadSprite("Assets/New UI/Generated/RoundedBar.png", "RoundedBar");

        if (logoSprite == null || levelIcon == null || settingsIcon == null || shopIcon == null || btnBg == null || barSprite == null)
        {
            Debug.LogError("MenuPrefabBuilder: не найдены спрайты. logo=" + (logoSprite != null) + " levelIcon=" + (levelIcon != null)
                + " settings=" + (settingsIcon != null) + " shop=" + (shopIcon != null) + " bg=" + (btnBg != null) + " bar=" + (barSprite != null));
            return;
        }

        BuildMenuLogo(logoSprite);
        log.Append("MenuLogo OK; ");
        BuildLevelCard(cfg, levelIcon, barSprite);
        log.Append("LevelCard OK; ");
        BuildMenuButtonBase(cfg, btnBg, settingsIcon);
        log.Append("MenuButton OK; ");
        BuildVariant("MenuButton_Settings.prefab", settingsIcon, "menu_settings", "settings", cfg);
        BuildVariant("MenuButton_Upgrade.prefab", levelIcon, "menu_upgrade", "upgrade", cfg);
        BuildVariant("MenuButton_Shop.prefab", shopIcon, "menu_shop", "shop", cfg);
        log.Append("3 варианта OK; ");
        BuildStartPanel(cfg, logoSprite);
        log.Append("StartPanel OK");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("AstroDrift MenuPrefabs: " + log);
    }

    // ————————————— MenuLogo —————————————

    private static void BuildMenuLogo(Sprite sprite)
    {
        var go = new GameObject("MenuLogo", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f); // GameUI привязывает к верхней кромке
        rt.sizeDelta = new Vector2(LogoW, LogoW * 512f / 1288f);
        // Авторская позиция = источник истины: GameUI масштабирует группу целиком (k = h/1920),
        // поэтому прежний расчёт «gapTop = h * 0.11» здесь не нужен и ломал позицию (−211 вместо −178.6).
        rt.anchoredPosition = new Vector2(0f, -178.6f);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        PrefabUtility.SaveAsPrefabAsset(go, LogoPath);
        Object.DestroyImmediate(go);
    }

    // ————————————— LevelCard (610×128) —————————————

    private static void BuildLevelCard(TypographyConfig cfg, Sprite levelIcon, Sprite barSprite)
    {
        Color cardBg = Palette.Hex("#252C34");

        var root = new GameObject("LevelCard", typeof(RectTransform), typeof(Image));
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(CardW, CardH);
        var bg = root.GetComponent<Image>();
        bg.color = cardBg;
        bg.raycastTarget = false;

        // Иконка: 120×169 вписана по высоте блока, пропорции сохранены
        var icon = NewRect(root.transform, "Icon");
        SetRect(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 6f), new Vector2(92f, 119f));
        var iconImg = icon.gameObject.AddComponent<Image>();
        iconImg.sprite = levelIcon;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // Подпись «УРОВЕНЬ ПИЛОТА» — переводимая (ключ pilot_level_label)
        var label = NewTmp(root.transform, "LevelLabel", "УРОВЕНЬ ПИЛОТА", cfg.bodyRegular, 27f, Palette.SecondaryText, TextAlignmentOptions.Left);
        SetRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(130f, 22f), new Vector2(300f, 38f));
        var loc = label.gameObject.AddComponent<LocalizedTextUI>();
        var locSo = new SerializedObject(loc);
        locSo.FindProperty("key").stringValue = "pilot_level_label";
        locSo.FindProperty("role").enumValueIndex = (int)TypeRole.Button;
        locSo.ApplyModifiedPropertiesWithoutUndo();

        // Крупное число уровня — выравнивание по правому краю
        var value = NewTmp(root.transform, "LevelValue", "0", cfg.ctaSemiBold, 52f, Palette.XpBar, TextAlignmentOptions.Right);
        SetRect(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 20f), new Vector2(200f, 62f));

        // Нижняя полоса: бар 364×24 на 14 px от низа карточки
        var bottom = NewRect(root.transform, "Bottom");
        bottom.anchorMin = new Vector2(0f, 0f);
        bottom.anchorMax = new Vector2(1f, 0f);
        bottom.pivot = new Vector2(0.5f, 0f);
        bottom.anchoredPosition = new Vector2(0f, 14f);
        bottom.sizeDelta = new Vector2(0f, BarH);

        // ProgressRoot (фон #252C34) → Mask → Fill (скругление из RoundedBar.png)
        var prog = NewRect(bottom, "ProgressRoot");
        prog.anchorMin = prog.anchorMax = prog.pivot = new Vector2(0.5f, 0.5f);
        prog.anchoredPosition = Vector2.zero;
        prog.sizeDelta = new Vector2(BarW, BarH);
        var progImg = prog.gameObject.AddComponent<Image>();
        progImg.sprite = barSprite;
        progImg.type = Image.Type.Simple;
        progImg.color = cardBg;
        progImg.raycastTarget = false;
        var mask = prog.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true; // фон бара виден, скругление режет заливку

        // ОДИН механизм заливки: Filled / Horizontal / Left, якоря растянуты, значение — fillAmount.
        // Прежняя анкорная заливка (anchorMax.x = t) конфликтовала с Debug-fillAmount и «врала».
        var fill = NewRect(prog, "Fill");
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        var fillImg = fill.gameObject.AddComponent<Image>();
        fillImg.sprite = barSprite;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 0f; // значение задаёт LevelCardUI.Refresh через UiProgressBar.Set
        fillImg.color = Palette.XpBar; // решение Team Lead: цвет выпущенного XP-бара (#66FF66) → на ревью
        fillImg.raycastTarget = false;

        var cardUi = root.AddComponent<LevelCardUI>();
        var cardSo = new SerializedObject(cardUi);
        cardSo.FindProperty("levelValue").objectReferenceValue = value;
        cardSo.FindProperty("barFill").objectReferenceValue = fill;
        cardSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, LevelCardPath);
        Object.DestroyImmediate(root);
    }

    // ————————————— MenuButton + варианты —————————————

    private static void BuildMenuButtonBase(TypographyConfig cfg, Sprite bg, Sprite icon)
    {
        var root = new GameObject("MenuButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(BtnW, BtnH);
        var bgImg = root.GetComponent<Image>();
        bgImg.sprite = bg;
        bgImg.type = Image.Type.Simple;   // spriteBorder = 0 → 9-slice не настраиваем, растяжение пропорциональное
        bgImg.preserveAspect = true;
        bgImg.raycastTarget = true;
        var btn = root.GetComponent<Button>();
        btn.targetGraphic = bgImg;

        var iconRt = NewRect(root.transform, "Icon");
        SetRect(iconRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 6f), new Vector2(48f, 48f));
        var iconImg = iconRt.gameObject.AddComponent<Image>();
        iconImg.sprite = icon;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        var label = NewTmp(root.transform, "Label", "", cfg.ctaSemiBold, 26f, Palette.ScoreText, TextAlignmentOptions.Left);
        SetRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(84f, 0f), new Vector2(140f, 40f));

        var mb = root.AddComponent<MenuButtonUI>();
        var mbSo = new SerializedObject(mb);
        mbSo.FindProperty("id").stringValue = "settings";
        mbSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, MenuButtonPath);
        Object.DestroyImmediate(root);
    }

    private static void BuildVariant(string fileName, Sprite icon, string locKey, string id, TypographyConfig cfg)
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuButtonPath);
        if (basePrefab == null) { Debug.LogError("MenuPrefabBuilder: нет базы " + MenuButtonPath); return; }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        inst.name = "MenuButton_" + id;

        var iconTr = inst.transform.Find("Icon");
        if (iconTr != null) iconTr.GetComponent<Image>().sprite = icon;

        var labelTr = inst.transform.Find("Label");
        if (labelTr != null)
        {
            var tmp = labelTr.GetComponent<TextMeshProUGUI>();
            tmp.text = "";
            tmp.font = cfg.ctaSemiBold;
            var loc = labelTr.gameObject.AddComponent<LocalizedTextUI>();
            var so = new SerializedObject(loc);
            so.FindProperty("key").stringValue = locKey;
            so.FindProperty("role").enumValueIndex = (int)TypeRole.Cta;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var mb = inst.GetComponent<MenuButtonUI>();
        if (mb != null)
        {
            var so = new SerializedObject(mb);
            so.FindProperty("id").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Сохранение инстанса как ассета → prefab variant (правки базы разлетаются по вариантам)
        PrefabUtility.SaveAsPrefabAsset(inst, Folder + "/" + fileName);
        Object.DestroyImmediate(inst);
    }

    // ————————————— StartPanel (меню целиком) —————————————

    private static void BuildStartPanel(TypographyConfig cfg, Sprite logoSprite)
    {
        var panel = new GameObject("StartPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(1080f, 1920f);
        var pImg = panel.GetComponent<Image>();
        pImg.color = new Color(0f, 0f, 0f, 0f);
        pImg.raycastTarget = false;
        var pcg = panel.GetComponent<CanvasGroup>();
        pcg.alpha = 1f; pcg.blocksRaycasts = true; pcg.interactable = true;

        // 1. Полноэкранная невидимая тап-зона — САМЫЙ НИЖНИЙ узел по raycast.
        //    Кнопки меню стоят выше → перехватывают тап: кнопка НЕ стартует игру.
        var tap = NewRect(panel.transform, "Btn_TapToPlay");
        tap.anchorMin = tap.anchorMax = tap.pivot = new Vector2(0.5f, 0.5f);
        tap.sizeDelta = new Vector2(1680f, 2520f);
        tap.anchoredPosition = Vector2.zero;
        var tapImg = tap.gameObject.AddComponent<Image>();
        tapImg.color = new Color(0f, 0f, 0f, 0f);
        tapImg.raycastTarget = true;
        var tapBtn = tap.gameObject.AddComponent<Button>();
        tapBtn.targetGraphic = tapImg;

        // 2. Логотип-картинка (позицию задаёт GameUI.ApplyAdaptiveStartLayout)
        var logo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LogoPath), panel.transform);
        logo.name = "MenuLogo";

        // 3. Рекорд: ДВЕ ноды — подпись (ключ best_label) + число (ключ best_value).
        // Один текст «РЕКОРД 24 500» перезаписывался бы биндом ключа best (для Death-экрана).
        var best = NewTmp(panel.transform, "StartBest", "РЕКОРД", cfg.bodyRegular, 34f, Palette.SecondaryText, TextAlignmentOptions.Center);
        SetRect(best.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 438f), new Vector2(600f, 80f));
        best.characterSpacing = 8f;
        best.raycastTarget = false;

        var bestValue = NewTmp(panel.transform, "StartBestValue", "0", cfg.bodyRegular, 34f, Palette.SecondaryText, TextAlignmentOptions.Center);
        SetRect(bestValue.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 378.1f), new Vector2(600f, 64.3f));
        bestValue.raycastTarget = false;

        // 4. Карточка уровня
        var card = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LevelCardPath), panel.transform);
        card.name = "LevelCard";
        SetRect(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 295f), new Vector2(CardW, CardH));

        // 5. Три нижние кнопки: Настройки / Прокачка / Магазин — в ноде-ряду «Menu Buttons».
        // Ряд нужен, чтобы GameUI уводил/возвращал весь блок одним SlideFade: сами кнопки
        // расставляет HorizontalLayoutGroup, и двигать их по отдельности нельзя.
        var row = NewRect(panel.transform, "Menu Buttons");
        row.anchorMin = Vector2.zero;
        row.anchorMax = new Vector2(1f, 0f);
        row.pivot = new Vector2(0.5f, 0f);
        row.anchoredPosition = new Vector2(0f, 120f);
        row.sizeDelta = new Vector2(-56f, 208.26f);
        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing = 0f;

        PlaceBtn(row, "Btn_MenuSettings", "MenuButton_Settings.prefab", new Vector2(-BtnSpacing, 0f));
        PlaceBtn(row, "Btn_MenuUpgrade", "MenuButton_Upgrade.prefab", new Vector2(0f, 0f));
        PlaceBtn(row, "Btn_MenuShop", "MenuButton_Shop.prefab", new Vector2(BtnSpacing, 0f));

        // 6. Кнопка стартового щита (логика — GameUI.RefreshPilotBlock, гейт уровня 8)
        var shield = NewRect(panel.transform, "Btn_StartShield");
        shield.anchorMin = shield.anchorMax = shield.pivot = new Vector2(0.5f, 0.5f);
        shield.anchoredPosition = new Vector2(0f, -34f);
        shield.sizeDelta = new Vector2(560f, 110f);
        var shieldImg = shield.gameObject.AddComponent<Image>();
        shieldImg.color = new Color(0f, 0f, 0f, 0f);
        shieldImg.raycastTarget = true;
        var shieldBtn = shield.gameObject.AddComponent<Button>();
        shieldBtn.targetGraphic = shieldImg;
        var shieldTxt = NewTmp(shield, "ShieldText", "СТАРТОВЫЙ ЩИТ", cfg.ctaSemiBold, 28f, Palette.PickupShield, TextAlignmentOptions.Center);
        SetRect(shieldTxt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 40f));
        var shieldCap = NewTmp(shield, "ShieldCaption", "ЗА ПРОСМОТР РЕКЛАМЫ · 1/ДЕНЬ", cfg.bodyRegular, 20f, Palette.SecondaryText, TextAlignmentOptions.Center);
        SetRect(shieldCap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(560f, 30f));
        // Скрыта по умолчанию — включается GameUI.RefreshPilotBlock при выполнении гейта
        shield.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
        shield.GetComponent<CanvasGroup>().blocksRaycasts = false;
        foreach (var t in shield.GetComponentsInChildren<TextMeshProUGUI>(true))
            t.gameObject.AddComponent<CanvasGroup>().alpha = 0f;

        // 7. CTA (самый верхний узел: текст поверх остальных)
        var cta = NewTmp(panel.transform, "CtaText", "TAP TO PLAY", cfg.ctaSemiBold, 44f, Palette.ScoreText, TextAlignmentOptions.Center);
        SetRect(cta.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -480f), new Vector2(600f, 80f));
        cta.characterSpacing = 16f;

        PrefabUtility.SaveAsPrefabAsset(panel, StartPanelPath);
        Object.DestroyImmediate(panel);
    }

    private static void PlaceBtn(Transform parent, string name, string prefabFile, Vector2 pos)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + prefabFile);
        if (prefab == null) { Debug.LogError("MenuPrefabBuilder: нет " + Folder + "/" + prefabFile); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        // Позицию в ряду задаёт HorizontalLayoutGroup; pos здесь — только для сцены без группы.
        rt.anchoredPosition = pos;
    }

    // ————————————— хелперы —————————————

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Menu");
    }

    private static Sprite LoadSprite(string assetPath, string spriteName)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (var o in all)
        {
            var s = o as Sprite;
            if (s != null && s.name == spriteName) return s;
        }
        return null;
    }

    private static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static TextMeshProUGUI NewTmp(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        if (font != null) tmp.font = font;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }
}
#endif
