#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Редакторная утилита: строит UI сцены Game по макету «Menu & Transitions v2» (ArtDirection §2–§3):
/// • StartPanel: заголовок ASTRO DRIFT (96 px Light, трекинг +12 %) в зоне 0.14–0.26,
///   Best (34 px, SecondaryText, капс) под ним, CTA в зоне 0.72–0.82 — текст + тонкая
///   линия-индикатор с бегущим золотым сегментом (§7). НИ ОДНОЙ плашки/рамки (§8).
/// • DeathPanel: Score (88 px Light) / BEST / NEW BEST (золото) + текстовые кнопки
///   с разделителями UiLine — без рамок и плашек.
/// • PausePanel: оверлей UiOverlay + текстовый список (каскад §4.4).
/// • Все панели всегда активны, видимость — через CanvasGroup (никаких мгновенных
///   SetActive(true) на видимых панелях — §8).
/// Меню: AstroDrift → Setup Scene UI.
/// </summary>
public static class AstroDriftSceneSetup
{
    [MenuItem("AstroDrift/Setup Scene UI")]
    public static void SetupSceneUI()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var log = new System.Text.StringBuilder();

        // ——— Canvas ———
        var canvasGo = GameObject.Find("Canvas");
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
        var esGo = GameObject.Find("EventSystem");
        if (esGo == null)
        {
            esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }

        Color scoreTextCol = Palette.ScoreText;
        Color secondaryCol = Palette.SecondaryText;

        // ——— HUD (геймплейный — не трогаем, только ссылка) ———
        var hudGo = GameObject.Find("Hud");
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
        var perkBarBgTr = hudGo.transform.Find("PerkProgressBarBg");
        if (perkBarBgTr == null)
        {
            var perkBarBg = NewPanel(hudGo.transform, "PerkProgressBarBg", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(360f, 8f));
            perkBarBg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
            perkBarBgTr = perkBarBg.transform;
        }
        var perkFillImg = perkBarBgTr.Find("PerkProgressBarFill")?.GetComponent<Image>();
        if (perkFillImg == null) perkFillImg = MakeFill(perkBarBgTr, "PerkProgressBarFill", Palette.XpBar);

        var scoreT = hudGo.transform.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
        var comboChipT = hudGo.transform.Find("ComboChip")?.GetComponent<TextMeshProUGUI>();

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

        // ——— StartPanel (§2: макет стартового экрана) ———
        var startGo = GameObject.Find("StartPanel");
        if (startGo == null) startGo = NewPanel(canvas.transform, "StartPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 1920));
        ClearChildren(startGo.transform);

        // Заголовок: зона 0.14–0.26 → центры строк на 0.165/0.245 высоты (y = +640/+510 от центра, 1920)
        var t1 = NewText(startGo.transform, "Title1", "ASTRO", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 640), 96, scoreTextCol, TextAlignmentOptions.Center);
        var t2 = NewText(startGo.transform, "Title2", "DRIFT", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 510), 96, scoreTextCol, TextAlignmentOptions.Center);
        // Best: под заголовком, центр ~0.30 высоты (зазор ≥ 80 px до носа корабля — §2)
        var bestT = NewText(startGo.transform, "StartBest", "BEST 0", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 380), 34, secondaryCol, TextAlignmentOptions.Center);

        // CTA: зона 0.72–0.82 → центр 0.77 высоты (y = −520 от центра). Только текст (пульс §3).
        // ux4-5: кликабельная зона — весь экран: тап в любой точке стартует игру.
        // Невидимый Image под текстами (первый ребёнок → нижний порядок raycast).
        // Запас ±300/±300 px за края панели — покрытие при нестандартных аспектах
        // (CanvasScaler match 0.5 в dev-окнах растягивает Canvas выше панели).
        var ctaGo = NewPanel(startGo.transform, "Btn_TapToPlay", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1680, 2520));
        ctaGo.GetComponent<Image>().color = new Color(0, 0, 0, 0); // невидимая кликабельная зона
        ctaGo.transform.SetSiblingIndex(0);
        var tapBtn = ctaGo.AddComponent<Button>();
        tapBtn.targetGraphic = ctaGo.GetComponent<Image>();
        // ux4-6: линия-индикатор с бегущим золотым сегментом удалена (второе золото в кадре);
        // остаётся текст CTA в исходной позиции с пульсом прозрачности.
        var ctaT = NewText(startGo.transform, "CtaText", "TAP TO PLAY", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -480), 44, scoreTextCol, TextAlignmentOptions.Center);
        ctaT.rectTransform.sizeDelta = new Vector2(600, 80);
        ctaT.transform.SetAsLastSibling();

        // ——— UI v3: мета-прогрессия (GDD §11): Pilot Level + XP bar + кнопка щита ———
        // Разместим блок ПОД Best (Best на y=380): Pilot Level y=300, XP bar y=245.
        var pilotT = NewText(startGo.transform, "PilotLevelText", "PILOT LEVEL 0", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 300), 34, Palette.XpBar, TextAlignmentOptions.Center);
        // XP bar: фон тёмный + зелёная заливка анкорами (UiProgressBar), 360×10
        var xpBgGo = NewPanel(startGo.transform, "XpBarBg", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 245), new Vector2(360, 10));
        xpBgGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
        var xpFillImg = MakeFill(xpBgGo.transform, "XpBarFill", Palette.XpBar);
        // Кнопка стартового щита: под CTA, discreet (текст 28 + caption 20)
        var shieldGo = NewPanel(startGo.transform, "Btn_StartShield", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -620), new Vector2(560, 110));
        shieldGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        var shieldBtn = shieldGo.AddComponent<Button>();
        shieldBtn.targetGraphic = shieldGo.GetComponent<Image>();
        var shieldText = NewText(shieldGo.transform, "ShieldText", "СТАРТОВЫЙ ЩИТ", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 28, Palette.PickupShield, TextAlignmentOptions.Center);
        var shieldCap = NewText(shieldGo.transform, "ShieldCaption", "ЗА ПРОСМОТР РЕКЛАМЫ · 1/ДЕНЬ", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -36), 20, secondaryCol, TextAlignmentOptions.Center);
        shieldCap.rectTransform.sizeDelta = new Vector2(560, 30);
        // Кнопка скрыта по умолчанию (гейт уровня 8 решает GameUI.RefreshPilotBlock)
        EnsureCanvasGroup(shieldGo, visible: false);
        var shieldCg = shieldGo.GetComponent<CanvasGroup>();
        var shieldTextCg = shieldText.gameObject.AddComponent<CanvasGroup>();
        var shieldCapCg = shieldCap.gameObject.AddComponent<CanvasGroup>();
        shieldCg.alpha = 0f; shieldCg.blocksRaycasts = false;
        shieldTextCg.alpha = 0f; shieldCapCg.alpha = 0f;

        // ——— DeathPanel (§3: кнопка = текст; NEW BEST — единственное золото) ———
        var deathGo = GameObject.Find("DeathPanel");
        if (deathGo == null) deathGo = NewPanel(canvas.transform, "DeathPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 1920));
        ClearChildren(deathGo.transform);
        deathGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // прозрачный фон — плашку убрали

        // ux5-6 [ЧИТАЕМОСТЬ]: чёрная полупрозрачная подложка ЗА содержимым Death-панели.
        // Первый ребёнок → нижний слой рисования (под всем контентом панели, поверх мира).
        // Фейдится вместе с панелью через CanvasGroup родителя — отдельной анимации не нужно.
        // raycastTarget=false: подложка не ловит тапы (урок ux4-3 со стартовым экраном);
        // тапы на Death-экране обрабатывают только Btn_Continue/Btn_Home.
        // Запас ±300 px за края панели — как у Btn_TapToPlay (нестандартные аспекты).
        var deathScrimGo = NewPanel(deathGo.transform, "DeathScrim", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1680, 2520));
        var deathScrimImg = deathScrimGo.GetComponent<Image>();
        // ux5-6 r2: 0.55 → 0.7. Bloom «пробивал» alpha-подложку: яркие HDR-объекты
        // (красная ракета, вспышка взрыва, белые осколки) после пост-процесса дают
        // итоговую яркость выше исходной, и при 0.55 остаток ~0.45×(1+bloom) был
        // читаем прямо под текстом «ЗА ПРОСМОТР РЕКЛАМЫ». 0.7 глушит вспышку,
        // но мир/звёзды по краям всё ещё слегка видны (не «чёрный лист»).
        // Один scrim на весь экран → нет «двух чернот» разной силы.
        deathScrimImg.color = new Color(0f, 0f, 0f, 0.7f);
        deathScrimImg.raycastTarget = false;

        var deathScoreT = NewText(deathGo.transform, "DeathScore", "SCORE 0", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 300), 88, scoreTextCol, TextAlignmentOptions.Center);
        var deathBestT = NewText(deathGo.transform, "DeathBest", "BEST 0", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 180), 34, secondaryCol, TextAlignmentOptions.Center);
        var newBestT = NewText(deathGo.transform, "DeathNewBest", "NEW BEST!", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 110), 34, Palette.Gold, TextAlignmentOptions.Center);
        newBestT.gameObject.SetActive(false);

        // Death v2 (GDD_DeathScreen_Continue §4): предложение ПРОДОЛЖИТЬ (текст + подпись +
        // линия-таймер + невидимая тап-зона ≥720×160) ВЫШЕ «Домой»; RETRY удалён.
        var continueT = NewText(deathGo.transform, "ContinueText", "ПРОДОЛЖИТЬ", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), 40, scoreTextCol, TextAlignmentOptions.Center);
        continueT.rectTransform.sizeDelta = new Vector2(600, 60);
        var continueCapT = NewText(deathGo.transform, "ContinueCaption", "ЗА ПРОСМОТР РЕКЛАМЫ", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -120), 24, secondaryCol, TextAlignmentOptions.Center);
        continueCapT.rectTransform.sizeDelta = new Vector2(600, 40);
        // ux5-6 [ВИДИМОСТЬ]: линия-таймер утолщена 2→4 px (плохо читалась на фоне);
        // ширина 600 не трогается, OfferTimerRoutine меняет только sizeDelta.x,
        // _offerLineRestWidth хранит ширину — высота на фикс восстановления не влияет.
        var timerLineGo = NewPanel(deathGo.transform, "ContinueTimerLine", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -155), new Vector2(600, 4));
        timerLineGo.GetComponent<Image>().color = scoreTextCol;

        // Невидимая тап-зона ≥ 720×160 (правило §3 арт-дирекшна) вокруг предложения
        var continueGo = NewPanel(deathGo.transform, "Btn_Continue", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(760, 190));
        continueGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        var continueBtn = continueGo.AddComponent<Button>();
        continueBtn.targetGraphic = continueGo.GetComponent<Image>();

        var homeBtn = NewTextButton(deathGo.transform, "Btn_Home", "ДОМОЙ", new Vector2(0, -280), 420);
        // Разделитель между предложением и «Домой» — тонкая линия UiLine (§3)
        var sepGo = NewPanel(deathGo.transform, "SepLine", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -220), new Vector2(420, 2));
        sepGo.GetComponent<Image>().color = Palette.UiLine;

        // ——— UI v3: Death-экран мета-блок (GDD §7): +XP / Level / бар / Разблокировано ———
        var deathXpT = NewText(deathGo.transform, "DeathXp", "+0 XP", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), 34, Palette.XpBar, TextAlignmentOptions.Center);
        var deathLevelT = NewText(deathGo.transform, "DeathLevel", "LEVEL 0", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 430), 30, scoreTextCol, TextAlignmentOptions.Center);
        var deathXpBg = NewPanel(deathGo.transform, "DeathXpBarBg", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(360, 10));
        deathXpBg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
        var deathXpFillImg = MakeFill(deathXpBg.transform, "DeathXpBarFill", Palette.XpBar);
        var deathUnlockT = NewText(deathGo.transform, "DeathUnlocked", "РАЗБЛОКИРОВАНО", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 470), 26, Palette.XpBar, TextAlignmentOptions.Center);
        deathUnlockT.rectTransform.sizeDelta = new Vector2(900, 70);
        deathUnlockT.gameObject.SetActive(false);
        deathXpT.gameObject.SetActive(false);
        deathLevelT.gameObject.SetActive(false);

        // ——— PausePanel (§4.4: оверлей + текстовый список, каскад) ———
        var pauseGo = GameObject.Find("PausePanel");
        if (pauseGo == null) pauseGo = NewPanel(canvas.transform, "PausePanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 1920));
        ClearChildren(pauseGo.transform);
        pauseGo.GetComponent<Image>().color = Palette.UiOverlay;

        var pauseTitle = NewText(pauseGo.transform, "PauseTitle", "PAUSE", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 160), 48, scoreTextCol, TextAlignmentOptions.Center);
        var resumeBtn = NewTextButton(pauseGo.transform, "Btn_Resume", "RESUME", new Vector2(0, 20), 420);
        var quitBtn = NewTextButton(pauseGo.transform, "Btn_Home", "HOME", new Vector2(0, -100), 420);
        var sep2Go = NewPanel(pauseGo.transform, "SepLine", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(420, 2));
        sep2Go.GetComponent<Image>().color = Palette.UiLine;

        // ——— PerkPanel (Волна 1, GDD §15.3): оверлей выбора перка, строится кодом ———
        var perkGo = GameObject.Find("PerkPanel");
        if (perkGo == null) perkGo = NewPanel(canvas.transform, "PerkPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 1920));
        ClearChildren(perkGo.transform);
        perkGo.transform.SetAsLastSibling(); // поверх всех панелей
        perkGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // затемнение делает PerkChoiceUI (0.6)

        var perkTitle = NewText(perkGo.transform, "PerkTitle", "LEVEL UP!", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 640), 64, scoreTextCol, TextAlignmentOptions.Center);
        var perkCards = new GameObject("PerkCards");
        perkCards.transform.SetParent(perkGo.transform, false);
        var perkCardsRt = perkCards.AddComponent<RectTransform>();
        perkCardsRt.anchorMin = perkCardsRt.anchorMax = perkCardsRt.pivot = new Vector2(0.5f, 0.5f);
        perkCardsRt.anchoredPosition = Vector2.zero;
        perkCardsRt.sizeDelta = new Vector2(1080, 400);
        // Реролл — ниже карт, discreet (мелкий текст + невидимая зона ≥ 88 pt, §3)
        var rerollGo = NewPanel(perkGo.transform, "Btn_Reroll", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -420), new Vector2(500, 110));
        rerollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        var rerollBtn = rerollGo.AddComponent<Button>();
        rerollBtn.targetGraphic = rerollGo.GetComponent<Image>();
        var rerollText = NewText(rerollGo.transform, "RerollText", "РЕРОЛЛ", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 28, secondaryCol, TextAlignmentOptions.Center);
        var rerollCap = NewText(rerollGo.transform, "RerollCaption", "ЗА ПРОСМОТР РЕКЛАМЫ · 1/ЗАБЕГ", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), 20, secondaryCol, TextAlignmentOptions.Center);
        rerollCap.rectTransform.sizeDelta = new Vector2(500, 30);

        EnsureCanvasGroup(perkGo, visible: false);
        var perkUiGo = GameObject.Find("PerkChoiceUI");
        if (perkUiGo == null) perkUiGo = new GameObject("PerkChoiceUI");
        perkUiGo.transform.SetParent(canvas.transform, false);
        var perkUi = perkUiGo.GetComponent<PerkChoiceUI>();
        if (perkUi == null) perkUi = perkUiGo.AddComponent<PerkChoiceUI>();
        perkUi.Init(perkGo, perkCardsRt, perkTitle, rerollBtn, rerollText.GetComponent<TextMeshProUGUI>(), rerollCap);

        // ——— GameUI с сериализованными ссылками ———
        var uiGo = GameObject.Find("GameUI");
        if (uiGo == null) uiGo = new GameObject("GameUI");
        var ui = uiGo.GetComponent<GameUI>();
        if (ui == null) ui = uiGo.AddComponent<GameUI>();

        var so = new SerializedObject(ui);
        SetRef(so, "hudRoot", hudGo, log);
        SetRef(so, "startPanel", startGo, log);
        SetRef(so, "deathPanel", deathGo, log);
        SetRef(so, "pausePanel", pauseGo, log);
        SetRef(so, "pauseBtn", pauseBtnGo, log);
        SetRef(so, "title1", t1, log);
        SetRef(so, "title2", t2, log);
        SetRef(so, "startBest", bestT, log);
        SetRef(so, "ctaText", ctaT, log);
        SetRef(so, "tapToPlayBtn", tapBtn, log);
        SetRef(so, "scoreText", scoreT, log);
        SetRef(so, "comboChip", comboChipT, log);
        SetRef(so, "deathScore", deathScoreT, log);
        SetRef(so, "deathBest", deathBestT, log);
        SetRef(so, "deathNewBest", newBestT, log);
        SetRef(so, "continueBtn", continueBtn, log);
        SetRef(so, "continueText", continueT, log);
        SetRef(so, "continueCaption", continueCapT, log);
        SetRef(so, "continueTimerLine", timerLineGo.GetComponent<RectTransform>(), log);
        SetRef(so, "homeBtn", homeBtn, log);
        SetRef(so, "pauseToggleBtn", pauseBtnGo.GetComponent<Button>(), log);
        SetRef(so, "resumeBtn", resumeBtn, log);
        SetRef(so, "quitBtn", quitBtn, log);
        SetRef(so, "pilotLevelText", pilotT, log);
        SetRef(so, "xpBarFill", xpFillImg, log);
        SetRef(so, "startShieldBtn", shieldBtn, log);
        SetRef(so, "startShieldText", shieldText, log);
        SetRef(so, "startShieldCaption", shieldCap, log);
        SetRef(so, "deathXp", deathXpT, log);
        SetRef(so, "deathLevel", deathLevelT, log);
        SetRef(so, "deathXpBarFill", deathXpFillImg, log);
        SetRef(so, "perkProgressBarFill", perkFillImg, log);
        SetRef(so, "deathUnlocked", deathUnlockT, log);
        so.ApplyModifiedPropertiesWithoutUndo();

        // Начальные состояния: панели ВСЕГДА активны, видимость — CanvasGroup (§8).
        EnsureCanvasGroup(startGo, visible: true);
        EnsureCanvasGroup(deathGo, visible: false);
        EnsureCanvasGroup(pauseGo, visible: false);
        EnsureCanvasGroup(hudGo, visible: false);
        pauseBtnGo.SetActive(false);

        EditorUtility.SetDirty(ui);
        EditorUtility.SetDirty(perkUi);
        MarkSceneDirty(scene);
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

    /// <summary>Полная очистка детей (обязательно с конца — иначе при удалении в foreach
    /// индексы сдвигаются и остаются дубли-«призраки»).</summary>
    private static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(t.GetChild(i).gameObject);
    }

    /// <summary>Кнопка = текст + невидимая кликабельная зона (≥ 88 pt) — §3.</summary>
    private static Button NewTextButton(Transform parent, string name, string label, Vector2 pos, float width)
    {
        var go = NewPanel(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(width, 88));
        go.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        var txt = NewText(go.transform, "Text", label, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 40, Color.white, TextAlignmentOptions.Center);
        txt.rectTransform.sizeDelta = new Vector2(width, 80);
        return btn;
    }

    /// <summary>Панель всегда активна; видимость — CanvasGroup (никаких SetActive-миганий §8).</summary>
    private static void EnsureCanvasGroup(GameObject go, bool visible)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = visible ? 1f : 0f;
        cg.blocksRaycasts = visible;
        cg.interactable = visible;
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

    /// <summary>Заливка прогресс-бара: якоря (0,0)-(1,1), sizeDelta 0, Image.Type.Simple.
    /// Filled-тип не используется — при растянутой заливке он даёт пустой/полный бар.</summary>
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
        img.type = Image.Type.Simple;
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
