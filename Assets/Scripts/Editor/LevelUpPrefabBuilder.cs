#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// Редакторная утилита: собирает префабы оверлея перк-левелапа (GDD §15.3), чтобы
/// владелец правил оверлей в инспекторе без кода.
/// Внешние спрайты берутся ТОЛЬКО из контейнера Assets/Resources/PrefabBuilderSprites.asset
/// (поля cardBg/cardNewBg/star/rerollBg + panelBg при usePanelBg). Поиска по имени слайса
/// здесь нет: пустое обязательное поле — ошибка со списком всех пустых полей до записи.
///   Assets/Prefabs/LevelUp/UpgradeCard.prefab      — базовая карта улучшения (фон — cardBg)
///   Assets/Prefabs/LevelUp/UpgradeCard_New.prefab  — префаб-вариант базовой: фон cardNewBg + бейдж «НОВОЕ»
///   Assets/Prefabs/LevelUp/LevelUpPanel.prefab     — оверлей целиком (инстанс в сцене)
/// Вариант отличается от базы ровно двумя вещами (спрайт фона и бейдж), поэтому собирается
/// как Prefab Variant: правки базы доезжают до него без пересборки.
/// Строит всё с нуля (не зависит от состояния сцены) и перезаписывает существующие
/// ассеты, поэтому повторный прогон даёт тот же результат.
/// Меню: AstroDrift → Build LevelUp Prefabs. После сборки — AstroDrift → Setup Scene UI.
/// </summary>
public static class LevelUpPrefabBuilder
{
    private const string Folder = "Assets/Prefabs/LevelUp";
    private const string CardPath = Folder + "/UpgradeCard.prefab";
    private const string CardNewPath = Folder + "/UpgradeCard_New.prefab";
    private const string PanelPath = Folder + "/LevelUpPanel.prefab";

    // Раскладка (координаты референса 1080×1920, якорь/пивот центра)
    private const float CardW = 300f, CardH = 420f, CardGap = 30f;
    private const float PanelW = 1080f, PanelH = 1920f;

    [MenuItem("AstroDrift/Build LevelUp Prefabs")]
    public static void BuildAll()
    {
        var log = new System.Text.StringBuilder();

        var cfg = PrefabBuilderAssets.LoadContainer<TypographyConfig>("Assets/Resources/TypographyConfig.asset", "LevelUpPrefabBuilder");
        if (cfg == null) return;
        if (cfg.bodyRegular == null) log.Append("ВНИМАНИЕ: TypographyConfig пуст (шрифт — fallback); ");
        if (cfg.titleBold == null) log.Append("ВНИМАНИЕ: TypographyConfig.titleBold пуст (заголовок возьмёт headingLight); ");

        var src = PrefabBuilderAssets.LoadContainer<PrefabBuilderSprites>(PrefabBuilderAssets.SpritesContainerPath, "LevelUpPrefabBuilder",
            "Создайте: ПКМ в Project → Create → AstroDrift → Prefab Builder Sprites (см. поля контейнера).");
        if (src == null) return;

        // Внешние ассеты приходят ТОЛЬКО отсюда; проверка до первой записи префаба.
        // panelBg обязателен ровно тогда, когда включён usePanelBg: «панель без спрайта» —
        // осознанный выбор флагом, а не молчаливая деградация из-за незаполненного поля.
        if (!PrefabBuilderAssets.RequireAll(src, "LevelUpPrefabBuilder",
                PrefabBuilderAssets.Field(src.cardBg, nameof(src.cardBg)),
                PrefabBuilderAssets.Field(src.cardNewBg, nameof(src.cardNewBg)),
                PrefabBuilderAssets.Field(src.star, nameof(src.star)),
                PrefabBuilderAssets.Field(src.rerollBg, nameof(src.rerollBg)),
                PrefabBuilderAssets.FieldIf(src.usePanelBg, src.panelBg, nameof(src.panelBg)))) return;
        if (!src.usePanelBg) log.Append("usePanelBg=false — панель без спрайта (как на HEAD); ");

        EnsureFolders();

        BuildBaseCard(cfg, src.cardBg);
        log.Append("UpgradeCard (base) OK; ");
        BuildNewVariant(cfg, src.cardNewBg);
        log.Append("UpgradeCard_New (variant) OK; ");
        BuildPanel(cfg, src.star, src.rerollBg, src.panelBg);
        log.Append("LevelUpPanel OK");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("AstroDrift LevelUpPrefabs: " + log);
    }

    // ————————————— Карта улучшения (300×420) —————————————

    private static void BuildBaseCard(TypographyConfig cfg, Sprite bg)
    {
        var root = new GameObject("UpgradeCard", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(CardW, CardH);

        var img = root.GetComponent<Image>();
        img.sprite = bg;
        img.type = Image.Type.Simple;
        img.color = Color.white;
        img.raycastTarget = true;

        var btn = root.GetComponent<Button>();
        btn.targetGraphic = img;

        var cg = root.GetComponent<CanvasGroup>();
        cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true;

        // Иконка перка — ключ ей не нужен: спрайт ставит код из PerkDefinition
        var icon = NewRect(root.transform, "Icon");
        SetRect(icon, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(140f, 140f));
        var iconImg = icon.gameObject.AddComponent<Image>();
        iconImg.raycastTarget = false;

        // Название и описание — текст подставляет PerkChoiceUI из ключей перка
        var title = NewTmp(root.transform, "Title", "", cfg.ctaSemiBold, 34f, Palette.ScoreText, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(CardW - 24f, 90f));
        title.textWrappingMode = TextWrappingModes.Normal;

        var desc = NewTmp(root.transform, "Description", "", cfg.bodyRegular, 24f, Palette.CardDescText, TextAlignmentOptions.Top);
        SetRect(desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -286f), new Vector2(CardW - 24f, 110f));
        desc.textWrappingMode = TextWrappingModes.Normal;

        // §8.1: пустой LSE — ссылку присвоит PerkChoiceUI.FillCard (вариант А, задача 6).
        // Теги ролей по запечённым шрифтам (Cta=ctaSemiBold, Body=bodyRegular) → нулевой
        // дифф (§8.3 п.4); применит OnEnable тега при Instantiate (N2).
        AddLocalizeEmpty(title);
        AddRole(title, TypeRole.Cta);
        AddLocalizeEmpty(desc);
        AddRole(desc, TypeRole.Body);

        PrefabUtility.SaveAsPrefabAsset(root, CardPath);
        Object.DestroyImmediate(root);
    }

    /// <summary>
    /// Вариант базовой карты: тот же рут и ноды, оверрайды — только спрайт фона
    /// и добавленный бейдж «НОВОЕ» (added node внутри варианта, базу не мутирует).
    /// </summary>
    private static void BuildNewVariant(TypographyConfig cfg, Sprite bg)
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPath);
        if (basePrefab == null)
        {
            Debug.LogError("LevelUpPrefabBuilder: нет базовой карты " + CardPath + " — вариант не собран.");
            return;
        }

        var root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        // Инстанс живёт в текущей сцене; временно — чтобы SaveAsPrefabAsset связал его с базой.
        root.name = "UpgradeCard_New";

        var img = root.GetComponent<Image>();
        img.sprite = bg;

        var badge = NewTmp(root.transform, "NewBadge", "НОВОЕ", cfg.ctaSemiBold, 30f, Palette.Gold, TextAlignmentOptions.Center);
        SetRect(badge.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(220f, 46f));
        AddLocalize(badge, "levelup_new");
        AddRole(badge, TypeRole.Cta);

        PrefabUtility.SaveAsPrefabAsset(root, CardNewPath);
        Object.DestroyImmediate(root);
    }

    // ————————————— Оверлей целиком (1080×1920) —————————————

    private static void BuildPanel(TypographyConfig cfg, Sprite star, Sprite rerollBg, Sprite panelBg)
    {
        var root = new GameObject("LevelUpPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(PanelW, PanelH);

        // Фон панели: спрайт из контейнера (пусто = чистый затемняющий прямоугольник, как на HEAD).
        // Затемнение 50% чёрного — цвет живёт в префабе, PerkChoiceUI только гасит CanvasGroup
        var img = root.GetComponent<Image>();
        img.sprite = panelBg;
        img.type = Image.Type.Simple;
        img.color = new Color(0f, 0f, 0f, 0.5f);
        img.raycastTarget = true;

        // §8: скрытый по умолчанию оверлей неактивен (PerkChoiceUI.Show сначала активирует)
        var cg = root.GetComponent<CanvasGroup>();
        cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
        root.SetActive(false);

        // Звезда-акцент над заголовком
        var starRt = NewRect(root.transform, "Star");
        SetRect(starRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 640f), new Vector2(150f, 150f));
        var starImg = starRt.gameObject.AddComponent<Image>();
        starImg.sprite = star;
        starImg.preserveAspect = true;
        starImg.raycastTarget = false;

        // «УРОВЕНЬ ПОВЫШЕН» — 72 px Bold, ключ в инспекторе (levelup_title).
        // Роль LevelUpTitle резолвится в cfg.titleBold (Montserrat Bold): если бы
        // роль была Title/DeathScore, Bold утёк бы в HUD и на экран смерти.
        var titleFont = cfg.titleBold != null ? cfg.titleBold : cfg.headingLight;
        var title = NewTmp(root.transform, "LevelUpTitle", "УРОВЕНЬ ПОВЫШЕН", titleFont, 72f, Palette.ScoreText, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 470f), new Vector2(1000f, 110f));
        AddLocalized(title, "levelup_title", TypeRole.LevelUpTitle);

        // Подзаголовок «ВЫБЕРИ УЛУЧШЕНИЕ» (levelup_choose)
        var subtitle = NewTmp(root.transform, "LevelUpChoose", "ВЫБЕРИ УЛУЧШЕНИЕ", cfg.bodyRegular, 34f, Palette.SecondaryText, TextAlignmentOptions.Center);
        SetRect(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 370f), new Vector2(900f, 60f));
        AddLocalized(subtitle, "levelup_choose", TypeRole.Secondary);

        // Контейнер карт: раскладку задаёт PerkChoiceUI (cardW 300, cardH 420, gap 30)
        var cards = NewRect(root.transform, "CardsRoot");
        SetRect(cards, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelW - 80f, CardH + 20f));

        // Кнопка реролла: фон — слайс Update Perks (576×206, preserveAspect)
        var btnRt = NewRect(root.transform, "Btn_Reroll");
        SetRect(btnRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -420f), new Vector2(560f, 200f));
        var btnImg = btnRt.gameObject.AddComponent<Image>();
        btnImg.sprite = rerollBg;
        btnImg.type = Image.Type.Simple;
        btnImg.preserveAspect = true;
        btnImg.color = Color.white;
        btnImg.raycastTarget = true;
        var btn = btnRt.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        var btnCg = btnRt.gameObject.AddComponent<CanvasGroup>();
        btnCg.alpha = 1f; btnCg.blocksRaycasts = true; btnCg.interactable = true;

        var rerollText = NewTmp(btnRt, "RerollText", "РЕРОЛЛ", cfg.ctaSemiBold, 34f, Palette.ScoreText, TextAlignmentOptions.Center);
        SetRect(rerollText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(520f, 56f));
        AddLocalized(rerollText, "reroll_cta", TypeRole.Cta);

        var rerollCap = NewTmp(btnRt, "RerollCaption", "ЗА ПРОСМОТР РЕКЛАМЫ", cfg.bodyRegular, 22f, Palette.SecondaryText, TextAlignmentOptions.Center);
        SetRect(rerollCap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -44f), new Vector2(520f, 36f));
        AddLocalized(rerollCap, "reroll_caption", TypeRole.Secondary);

        PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
        Object.DestroyImmediate(root);
    }

    // ————————————— хелперы —————————————

    private static void AddLocalized(TextMeshProUGUI tmp, string key, TypeRole role)
    {
        AddLocalize(tmp, key);
        AddRole(tmp, role);
    }

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

    /// <summary>Пустой LSE: таблицу/ключ присвоит код в рантайме (§8.1, карты перков).</summary>
    private static LocalizeStringEvent AddLocalizeEmpty(TextMeshProUGUI tmp)
    {
        var lse = tmp.gameObject.AddComponent<LocalizeStringEvent>();
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

    /// <summary>Тег шрифтовой роли. Имя поля 'role' зафиксировано ТЗ §3.5.</summary>
    private static void AddRole(TextMeshProUGUI tmp, TypeRole role)
    {
        var tag = tmp.gameObject.AddComponent<TypeRoleTag>();
        var so = new SerializedObject(tag);
        so.FindProperty("role").enumValueIndex = (int)role;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "LevelUp");
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

    private static TextMeshProUGUI NewTmp(Transform parent, string name, string text, TMP_FontAsset font,
                                          float size, Color color, TextAlignmentOptions align)
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
