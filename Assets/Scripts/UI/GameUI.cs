using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI «Menu & Transitions v2» (ArtDirection §1–§7). Панели — сценовые объекты;
/// GameUI навешивает поведение и играет переходы:
/// • Стартовый экран: только текст + линия-индикатор (никаких плашек), пульс §3.
/// • Переходы — по двум каноническим кривым (UiAnim), каскады 60–90 мс, unscaled time.
/// • HUD геймплея не тронут (только fade-появление §5 и притушивание в паузе §4.4).
/// • Шрифты — только через Typography (TypographyConfig); пустой конфиг = LiberationSans.
/// Ни одного мгновенного SetActive(true) на видимой панели: панели всегда активны,
/// видимость управляется CanvasGroup (alpha + blocksRaycasts).
/// </summary>
public class GameUI : MonoBehaviour
{
    public enum Screen { Start, Hud, Death, Pause }

    [Header("Панели (объекты сцены)")]
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject deathPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject pauseBtn;

    [Header("Стартовый экран (§2)")]
    [SerializeField] private TextMeshProUGUI title1;   // устарело: заменён логотипом (может быть null)
    [SerializeField] private TextMeshProUGUI title2;   // устарело: заменён логотипом (может быть null)
    [SerializeField] private TextMeshProUGUI startBest;
    [SerializeField] private TextMeshProUGUI ctaText;
    [SerializeField] private Button tapToPlayBtn;     // полноэкранная невидимая зона тапа (ux4-5)
    [SerializeField] private Image logoImage;         // «Astro Drift» — картинка вместо текста (любая локаль)

    [Header("UI v3: мета-прогрессия (GDD §11, Волна 1)")]
    [SerializeField] private TextMeshProUGUI pilotLevelText; // «Pilot Level N» — зелёный #66FF66
    [SerializeField] private Image xpBarFill;                // XP bar — зелёный, заливка анкорами (UiProgressBar)
    [SerializeField] private Image perkProgressBarFill;      // HUD: прогресс до следующего перка (GDD §15.3)
    [SerializeField] private Button startShieldBtn;          // «Стартовый щит за рекламу»
    [SerializeField] private TextMeshProUGUI startShieldText;
    [SerializeField] private TextMeshProUGUI startShieldCaption;

    [Header("UI v3: Death-экран (GDD §7)")]
    [SerializeField] private TextMeshProUGUI deathXp;        // «+Y XP»
    [SerializeField] private TextMeshProUGUI deathLevel;     // «Level N → N+1» / «Level N»
    [SerializeField] private Image deathXpBarFill;           // прогресс нового уровня
    [SerializeField] private TextMeshProUGUI deathUnlocked;  // «Разблокировано: …» (только lvl-up, Wave1)

    [Header("Тексты")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI comboChip;

    [Header("Death (§4.2, GDD_DeathScreen_Continue)")]
    [SerializeField] private TextMeshProUGUI deathScore;
    [SerializeField] private TextMeshProUGUI deathBest;
    [SerializeField] private TextMeshProUGUI deathNewBest; // единственный золотой элемент (§7)
    [SerializeField] private Button continueBtn;            // невидимая тап-зона ≥ 720×160
    [SerializeField] private TextMeshProUGUI continueText;  // «ПРОДОЛЖИТЬ» (CTA 40 px)
    [SerializeField] private TextMeshProUGUI continueCaption; // «ЗА ПРОСМОТР РЕКЛАМЫ» (24 px)
    [SerializeField] private RectTransform continueTimerLine; // линия-таймер 2 px, убывает 5 с
    [SerializeField] private Button homeBtn;

    [Header("Кнопки")]
    [SerializeField] private Button pauseToggleBtn;
    [SerializeField] private Button resumeBtn;
    [SerializeField] private Button quitBtn;

    private ScoreManager _score;
    private int _lastMultiplier = 1;
    private Screen _screen;
    private RectTransform _canvasRt;   // для адаптивного лэйаута (высота кадра в юнитах)
    private float _lastLayoutH = -1f;  // кэш высоты: ре-лейаут только при смене разрешения
    private readonly List<Coroutine> _transitions = new List<Coroutine>();
    private Coroutine _ctaPulse;
    private Coroutine _offerTimer;         // таймер предложения (§10.2.3)
    private bool _offerActive;             // предложение видно и таймер идёт (OfferRunning)
    private bool _offerPausedByFocus;      // приложение ушло в фон при OfferRunning (§9)
    private float _offerRemaining;         // остаток таймера для продолжения после фокуса
    private float _offerFullDuration;
    private float _offerLineFullWidth;
    private float _offerLineRestWidth;     // ux5: исходная ширина линии-таймера (сцена), восстанавливается при каждом показе
    private bool _deathContinueAvailable;  // на этой смерти continue ещё доступен (1 за забег)
    private bool _continueUsedThisRun;     // флаг «continue уже был» для GameManager
    private bool _continueDeclinedLogged;  // ТЗ §2.5: continue_declined уже отправлен в этой смерти (антидубль exit_to_home)

    // Сдвиги слоёв (px @1080×1920) — §4.1–§4.4
    private static readonly Vector2 SlideCta = new Vector2(0f, -40f);
    private static readonly Vector2 SlideTitle = new Vector2(0f, -60f);
    private static readonly Vector2 SlideScore = new Vector2(0f, 48f);
    private static readonly Vector2 SlideBest = new Vector2(0f, 32f);
    private static readonly Vector2 SlideButton = new Vector2(0f, 24f);
    private static readonly Vector2 SlidePause = new Vector2(0f, 40f);

    public void Init(ScoreManager score)
    {
        _score = score;
        if (startPanel != null) _canvasRt = startPanel.transform.parent as RectTransform;
        ApplyAdaptiveStartLayout();

        // Поведение кнопок (структура — в сцене, обработчики — здесь)
        if (tapToPlayBtn != null) tapToPlayBtn.onClick.AddListener(() => GameManager.Instance.BeginRun());

        // Локализация статичных текстов (перечитываются при смене локали;
        // динамические — в PlayDeathIn/RefreshHud). Заголовок — картинка, локали не требует.
        if (title1 != null) L10n.Bind(title1, "title_main");
        if (title2 != null) L10n.Bind(title2, "title_sub");
        L10n.Bind(ctaText, "tap_to_play");
        L10n.Bind(continueText, "continue_cta");
        L10n.Bind(continueCaption, "continue_caption");
        L10n.Bind(deathNewBest, "new_best");
        L10n.Bind(homeBtn != null ? homeBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null, "home");
        BindPauseTexts();

        // Фикс «тап по TAP TO PLAY не стартует игру»: TMP-тексты стартового экрана
        // (перекрывающие полноэкранную невидимую зону тапа) перехватывали raycast.
        // Тексты — не интерактивные элементы: выключаем их raycastTarget, тап всегда
        // доходит до tapToPlayBtn в любой точке экрана (включая сам текст).
        foreach (var t in new[] { title1, title2, startBest, ctaText })
            if (t != null) t.raycastTarget = false;
        if (continueBtn != null) continueBtn.onClick.AddListener(OnContinueTapped);
        if (homeBtn != null) homeBtn.onClick.AddListener(HomeWithInterstitial);
        if (startShieldBtn != null) startShieldBtn.onClick.AddListener(OnStartShieldTapped);
        RefreshPilotBlock();
        if (pauseToggleBtn != null) pauseToggleBtn.onClick.AddListener(TogglePause);
        if (resumeBtn != null) resumeBtn.onClick.AddListener(TogglePause);
        if (quitBtn != null) quitBtn.onClick.AddListener(GoHomeFromPause);

        _score.OnScoreChanged += OnScoreChanged;
        _score.OnComboReset += ShrinkCombo;

        // ux5: запоминаем ширину линии-таймера из сцены ДО первого показа —
        // после истечения таймера sizeDelta.x уезжает в ~0 и без этого
        // не восстанавливается при повторной смерти (после Домой → новый забег).
        if (continueTimerLine != null)
            _offerLineRestWidth = continueTimerLine.sizeDelta.x;

        ApplyTypography();
        RefreshPilotBlock();
        ShowStartImmediate();
        RefreshHud();
        StartCtaPulse();
        BuildUnlockTreeStub();
    }

    // ——— Заглушка дерева разблокировок (плейтест Волны 1): кнопка + сворачиваемая панель ———

    private GameObject _treePanel;
    private TMPro.TextMeshProUGUI _treeText;
    private Button _treeBtn;

    /// <summary>
    /// Заглушка (НЕ фича): маленькая кнопка «ДЕРЕВО» на стартовом экране + сворачиваемая
    /// панель со списком уровней 0–20 из PilotProgressConfig.unlocks. Чистый текст,
    /// разблокированные — зелёные, нереализованные (implementedInWave1=false) — с «скоро».
    /// Строится программно поверх текущего UI (тот же Canvas), без сцены и скинов.
    /// </summary>
    private void BuildUnlockTreeStub()
    {
        if (_treePanel != null) return; // повторный Init
        var canvas = startPanel != null ? startPanel.transform.parent : null;
        if (canvas == null || PilotProgressManager.Instance == null) return;

        // Кнопка-заглушка: низко-левый угол, мелкая, не мешает CTA
        var btnGo = new GameObject("Btn_UnlockTree", typeof(RectTransform), typeof(CanvasGroup));
        btnGo.transform.SetParent(canvas, false);
        var btnRt = (RectTransform)btnGo.transform;
        btnRt.anchorMin = new Vector2(0f, 0f); btnRt.anchorMax = new Vector2(0f, 0f);
        btnRt.pivot = new Vector2(0f, 0f);
        btnRt.anchoredPosition = new Vector2(20f, 20f);
        btnRt.sizeDelta = new Vector2(180f, 60f);
        var btnImg = btnGo.AddComponent<UnityEngine.UI.Image>();
        btnImg.color = new Color(1f, 1f, 1f, 0.08f);
        _treeBtn = btnGo.AddComponent<UnityEngine.UI.Button>();
        _treeBtn.targetGraphic = btnImg;
        var btnCg = btnGo.GetComponent<CanvasGroup>();
        btnCg.blocksRaycasts = true;

        var btnTextGo = new GameObject("Text", typeof(RectTransform));
        btnTextGo.transform.SetParent(btnGo.transform, false);
        var btnTextRt = (RectTransform)btnTextGo.transform;
        btnTextRt.anchorMin = Vector2.zero; btnTextRt.anchorMax = Vector2.one;
        btnTextRt.offsetMin = Vector2.zero; btnTextRt.offsetMax = Vector2.zero;
        var btnTmp = btnTextGo.AddComponent<TMPro.TextMeshProUGUI>();
        btnTmp.fontSize = 24; btnTmp.alignment = TMPro.TextAlignmentOptions.Center;
        btnTmp.color = Palette.SecondaryText;
        btnTmp.raycastTarget = false;
        L10n.Bind(btnTmp, "unlock_tree_open");

        // Панель: по центру, скрыта (CanvasGroup alpha=0), поверх стартового экрана
        _treePanel = new GameObject("UnlockTreePanel", typeof(RectTransform), typeof(CanvasGroup));
        _treePanel.transform.SetParent(canvas, false);
        var panelRt = (RectTransform)_treePanel.transform;
        panelRt.anchorMin = Vector2.zero; panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = new Vector2(60f, 120f); panelRt.offsetMax = new Vector2(-60f, -120f);
        var panelImg = _treePanel.AddComponent<UnityEngine.UI.Image>();
        panelImg.color = Palette.UiPanel;
        var panelCg = _treePanel.GetComponent<CanvasGroup>();
        panelCg.alpha = 0f; panelCg.blocksRaycasts = false; panelCg.interactable = false;

        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(_treePanel.transform, false);
        var titleRt = (RectTransform)titleGo.transform;
        titleRt.anchorMin = new Vector2(0f, 1f); titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -16f);
        titleRt.sizeDelta = new Vector2(0f, 50f);
        var titleTmp = titleGo.AddComponent<TMPro.TextMeshProUGUI>();
        titleTmp.fontSize = 32; titleTmp.alignment = TMPro.TextAlignmentOptions.Center;
        titleTmp.color = Palette.XpBar; titleTmp.raycastTarget = false;
        L10n.Bind(titleTmp, "unlock_tree_title");

        var listGo = new GameObject("List", typeof(RectTransform));
        listGo.transform.SetParent(_treePanel.transform, false);
        var listRt = (RectTransform)listGo.transform;
        listRt.anchorMin = Vector2.zero; listRt.anchorMax = Vector2.one;
        listRt.offsetMin = new Vector2(24f, 12f); listRt.offsetMax = new Vector2(-24f, -80f);
        _treeText = listGo.AddComponent<TMPro.TextMeshProUGUI>();
        _treeText.fontSize = 24; _treeText.alignment = TMPro.TextAlignmentOptions.TopLeft;
        _treeText.raycastTarget = false;
        _treeText.enableWordWrapping = true;

        _treeBtn.onClick.AddListener(ToggleTree);
    }

    private void ToggleTree()
    {
        if (_treePanel == null) return;
        var cg = _treePanel.GetComponent<CanvasGroup>();
        bool show = cg.alpha < 0.5f;
        if (show) FillUnlockTree();
        cg.alpha = show ? 1f : 0f;
        cg.blocksRaycasts = show;
        cg.interactable = show;
    }

    /// <summary>Список «N — награда»: разблокированные зелёные, «скоро» — для нереализованных.</summary>
    private void FillUnlockTree()
    {
        if (_treeText == null) return;
        var pilot = PilotProgressManager.Instance;
        if (pilot == null) return;

        string soon = L10n.Get("unlock_soon");
        if (string.IsNullOrEmpty(soon)) soon = "скоро";
        var sb = new System.Text.StringBuilder();
        var entries = pilot.AllUnlocks;
        var byLevel = new System.Collections.Generic.SortedDictionary<int, System.Collections.Generic.List<UnlockEntry>>();
        foreach (var u in entries)
        {
            if (!byLevel.TryGetValue(u.pilotLevel, out var list))
                byLevel[u.pilotLevel] = list = new System.Collections.Generic.List<UnlockEntry>();
            list.Add(u);
        }
        foreach (var kv in byLevel)
        {
            bool unlocked = pilot.PilotLevel >= kv.Key;
            string color = unlocked ? "#66FF66" : "#8A8A8A";
            foreach (var u in kv.Value)
            {
                string name = L10n.Get("unlock_" + u.id);
                if (string.IsNullOrEmpty(name)) name = u.id;
                sb.Append("<color=").Append(color).Append('>')
                  .Append(kv.Key).Append(" — ").Append(name);
                if (!u.implementedInWave1) sb.Append(" (").Append(soon).Append(')');
                sb.Append("</color>\n");
            }
        }
        _treeText.text = sb.ToString();
    }

    /// <summary>Пауза-тексты живут на панели сцены (не сериализованы) — биндинг по Find.</summary>
    private void BindPauseTexts()
    {
        if (pausePanel == null) return;
        L10n.Bind(pausePanel.transform.Find("PauseTitle")?.GetComponent<TMPro.TextMeshProUGUI>(), "pause_title");
        L10n.Bind(pausePanel.transform.Find("Btn_Resume")?.GetComponentInChildren<TMPro.TextMeshProUGUI>(), "resume");
        L10n.Bind(pausePanel.transform.Find("Btn_Home")?.GetComponentInChildren<TMPro.TextMeshProUGUI>(), "home");
    }

    private void OnDestroy()
    {
        if (_score != null)
        {
            _score.OnScoreChanged -= OnScoreChanged;
            _score.OnComboReset -= ShrinkCombo;
        }
    }

    private void OnScoreChanged(int s, int m) { RefreshHud(); PulseScore(); }

    /// <summary>
    /// Тап «ДОМОЙ» на Death-экране (единственная точка показа interstitial,
    /// GDD_DeathScreen_Continue §7: было Retry — стало Home; формула не меняется,
    /// добавлено тихое окно 60 с после reward внутри менеджера).
    /// Реклама не готова / формула не выполнена → GoHome мгновенно.
    /// Реклама показана → GoHome строго по закрытию (InterstitialClosed), fallback при ошибке.
    /// </summary>
    private void HomeWithInterstitial()
    {
        bool offerWasAlive = _offerActive;
        StopOfferTimer();
        var ads = AdsFlow.Instance;
        if (ads != null && ads.TryShowInterstitial())
        {
            bool done = false;
            void OnClosed()
            {
                if (done) return;
                done = true;
                ads.InterstitialClosed -= OnClosed;
                if (offerWasAlive)
                {
                    _continueDeclinedLogged = true; // ТЗ §2.5: блокируем дубль в exit_to_home
                    Analytics.Log("continue_declined", new Dictionary<string, object> { { "score", _score != null ? _score.Score : 0 } });
                }
                GameManager.Instance.GoHome();
            }
            ads.InterstitialClosed += OnClosed;
        }
        else
        {
            if (offerWasAlive)
            {
                _continueDeclinedLogged = true; // ТЗ §2.5: блокируем дубль в exit_to_home
                Analytics.Log("continue_declined", new Dictionary<string, object> { { "score", _score != null ? _score.Score : 0 } });
            }
            GameManager.Instance.GoHome();
        }
    }

    // ——— Continue: тап по предложению (§10.2.4) ———

    private void OnContinueTapped()
    {
        if (!_offerActive) return; // таймер истёк / предложения нет — кнопка мертва
        StopOfferTimer();
        _offerActive = false;

        var gm = GameManager.Instance;
        var ads = AdsFlow.Instance;
        if (gm == null || ads == null) return;

        Analytics.Log("continue_ad_started", new Dictionary<string, object>
        {
            { "score", _score != null ? _score.Score : 0 },
            { "multiplier", _score != null ? _score.Multiplier : 1 },
        });

        // Панель: fade-out 0.25 s, blocksRaycasts=false (§5.3: панель скрыта целиком и не интерактивна)
        StopTransitions();
        SetVisible(continueBtn, false);
        SetVisible(homeBtn, false);
        _transitions.Add(StartCoroutine(FadeOut(deathPanel, 0.25f, 0f)));

        ads.ShowRewarded(onResult =>
        {
            if (onResult)
            {
                Analytics.Log("continue_ad_completed", new Dictionary<string, object>
                {
                    { "score", _score != null ? _score.Score : 0 },
                    { "multiplier", _score != null ? _score.Multiplier : 1 },
                });
                _continueUsedThisRun = true;
                // ТЗ §3: первый в жизни успешный continue — флаг «попробовал главную монетизационную фичу»
                Analytics.ProfileSetString("used_continue_once", "yes");
                gm.ContinueRun();
            }
            else
            {
                // Aborted / ошибка показа (§5.3): возврат на Death-экран без предложения,
                // без рестарта мира, без повторного каскада и таймера
                Analytics.Log("continue_ad_aborted");
                ShowDeathPanelNoOffer();
            }
        });
    }

    /// <summary>
    /// Возврат на Death-экран после aborted/failed-рекламы (§5.3): Score/BEST/Домой
    /// мгновенно alpha=1 — каскад НЕ переигрывается, таймер НЕ перезапускается.
    /// </summary>
    private void ShowDeathPanelNoOffer()
    {
        SetVisible(deathPanel, true);
        var panelCg = Cg(deathPanel);
        panelCg.alpha = 1f;
        panelCg.blocksRaycasts = true;
        panelCg.interactable = true;

        SetVisible(deathScore, true);
        SetVisible(deathBest, true);
        if (deathNewBest.gameObject.activeSelf) SetVisible(deathNewBest, true);
        SetVisible(continueBtn, false);
        SetVisible(continueText, false);
        SetVisible(continueCaption, false);
        if (continueTimerLine != null) SetVisible(continueTimerLine, false);
        SetVisible(homeBtn, true);
        ResetRest(deathScore); ResetRest(deathBest);
        if (deathNewBest.gameObject.activeSelf) ResetRest(deathNewBest);
        ResetRest(homeBtn);
    }

    // ——— Таймер предложения (§10.2.3): unscaled, пауза при потере фокуса ———

    private void StartOfferTimer()
    {
        StopOfferTimer();
        float dur = GameManager.Instance != null && GameManager.Instance.Config != null
            ? GameManager.Instance.Config.continueOfferDuration : 5f;
        _offerFullDuration = dur;
        _offerRemaining = dur;
        _offerLineFullWidth = continueTimerLine != null ? continueTimerLine.sizeDelta.x : 0f;
        _offerActive = true;
        _offerTimer = StartCoroutine(OfferTimerRoutine());
    }

    private void StopOfferTimer()
    {
        if (_offerTimer != null) { StopCoroutine(_offerTimer); _offerTimer = null; }
    }

    private IEnumerator OfferTimerRoutine()
    {
        // Линия: sizeDelta.x от полной ширины до 0 линейно; unscaled (мир заморожен)
        while (_offerRemaining > 0f)
        {
            if (!_offerPausedByFocus)
            {
                _offerRemaining -= Time.unscaledDeltaTime;
                if (continueTimerLine != null)
                {
                    float k = Mathf.Clamp01(_offerRemaining / _offerFullDuration);
                    continueTimerLine.sizeDelta = new Vector2(_offerLineFullWidth * k, continueTimerLine.sizeDelta.y);
                }
            }
            yield return null;
        }
        // OfferExpired: fade-out предложения/подписи/линии 0.25 s EaseInQuick
        _offerActive = false;
        SetVisible(continueBtn, false);
        Analytics.Log("continue_timer_expired", new Dictionary<string, object>
        {
            { "score", _score != null ? _score.Score : 0 },
        });
        if (continueText != null)
            _transitions.Add(StartCoroutine(UiAnim.Fade(Cg(continueText), Cg(continueText).alpha, 0f, 0.25f, UiAnim.EaseInQuick)));
        if (continueCaption != null)
            _transitions.Add(StartCoroutine(UiAnim.Fade(Cg(continueCaption), Cg(continueCaption).alpha, 0f, 0.25f, UiAnim.EaseInQuick)));
        if (continueTimerLine != null)
            _transitions.Add(StartCoroutine(UiAnim.Fade(Cg(continueTimerLine.gameObject), Cg(continueTimerLine.gameObject).alpha, 0f, 0.25f, UiAnim.EaseInQuick)));
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // §9: предложение живёт, пока игрок на экране; таймер — на паузе в фоне
        if (Screen_DeathVisible)
            _offerPausedByFocus = !hasFocus;
    }

    private bool Screen_DeathVisible => _screen == Screen.Death && deathPanel != null && Cg(deathPanel).alpha > 0.5f;

    // ——— Типографика (§3, поправка владельца: только через TypographyConfig) ———

    private void ApplyTypography()
    {
        Typography.Apply(title1, TypeRole.Title);
        Typography.Apply(title2, TypeRole.Title);
        Typography.Apply(startBest, TypeRole.Secondary);
        Typography.Apply(ctaText, TypeRole.Cta);
        Typography.Apply(deathScore, TypeRole.DeathScore);
        Typography.Apply(deathBest, TypeRole.Secondary);
        Typography.Apply(deathNewBest, TypeRole.Secondary);
        // HUD-тексты (score/combo) не трогаем — геймплейный HUD вне скоупа.
    }

    // ——— Экраны: показ/скрытие через CanvasGroup (без SetActive) ———

    private static CanvasGroup Cg(Object c)
    {
        if (c == null) return null;
        var go = c switch
        {
            GameObject g => g,
            Component comp => comp.gameObject,
            _ => null,
        };
        if (go == null) return null;
        if (!go.TryGetComponent<CanvasGroup>(out var cg)) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    private static void SetVisible(Object c, bool visible)
    {
        var cg = Cg(c);
        if (cg == null) return;
        cg.alpha = visible ? 1f : 0f;
        cg.blocksRaycasts = visible;
        cg.interactable = visible;
    }

    private static RectTransform Rt(Object c)
    {
        return c switch
        {
            GameObject g => g.GetComponent<RectTransform>(),
            Component comp => comp.GetComponent<RectTransform>(),
            _ => null,
        };
    }

    // ——— UI v3: мета-прогрессия (GDD §11/§7, Волна 1) ———

    /// <summary>Обновление блока пилота (стартовый экран) + кнопки стартового щита.
    /// pilotLevel < 8 ИЛИ точка выключена → кнопки нет; использована сегодня → disabled; иначе активна.</summary>
    public void RefreshPilotBlock()
    {
        var pilot = PilotProgressManager.Instance;
        if (pilot == null) return;

        if (pilotLevelText != null)
        {
            // Фолбэк — сразу на русском (поздняя загрузка локали не мигает английским),
            // и через Bind: RefreshPilotBlock вызывается из Init ДО готовности таблицы,
            // одноразовый Get успевал вернуть null и текст оставался фолбэком до Home.
            pilotLevelText.text = $"УРОВЕНЬ ПИЛОТА {pilot.PilotLevel}";
            L10n.Bind(pilotLevelText, "pilot_level", pilot.PilotLevel);
            pilotLevelText.color = Palette.XpBar;
        }
        UiProgressBar.Set(Rt(xpBarFill), pilot.ProgressToNextLevel());

        // Кнопка стартового щита (§10.1)
        var gmCfg = GameManager.Instance != null ? GameManager.Instance.Config : null;
        bool pointEnabled = gmCfg != null && gmCfg.startShieldDailyRewarded;
        bool levelOk = gmCfg != null && pilot.PilotLevel >= gmCfg.startShieldUnlockPilotLevel;
        bool adReady = AdsFlow.Instance != null && AdsFlow.Instance.IsRewardedReady;
        bool show = pointEnabled && levelOk;
        bool usedToday = pilot.StartShieldUsedToday;

        SetVisible(startShieldBtn, show);
        SetVisible(startShieldText, show);
        SetVisible(startShieldCaption, show);
        if (show)
        {
            startShieldBtn.interactable = !usedToday && adReady;
            if (usedToday)
            {
                string cap = L10n.Get("shield_used_today");
                if (startShieldCaption != null)
                    startShieldCaption.text = string.IsNullOrEmpty(cap) ? "УЖЕ ИСПОЛЬЗОВАНА СЕГОДНЯ" : cap;
                if (startShieldText != null) startShieldText.color = Palette.SecondaryText;
            }
            else
            {
                L10n.Bind(startShieldText, "shield_cta");
                L10n.Bind(startShieldCaption, "shield_caption");
                if (startShieldText != null) startShieldText.color = Palette.PickupShield;
            }
        }
    }

    /// <summary>Клик «Стартовый щит за рекламу» (§10.1): ShowRewarded → MarkRewardedShown → следующий забег с 1 щитом.</summary>
    private void OnStartShieldTapped()
    {
        var pilot = PilotProgressManager.Instance;
        var ads = AdsFlow.Instance;
        if (pilot == null || ads == null || pilot.StartShieldUsedToday) return;

        Analytics.Log("start_shield_ad_started");
        ads.ShowRewarded(ok =>
        {
            if (!ok) { Analytics.Log("start_shield_ad_aborted"); return; }
            pilot.MarkStartShieldUsed();
            _startShieldPending = true; // BeginRun прочитает и выдаст щит
            Analytics.Log("start_shield_ad_completed");
            RefreshPilotBlock();
        });
    }

    /// <summary>Флаг «щит выдан на следующий забег» (читает/сбрасывает GameManager через UI).</summary>
    private bool _startShieldPending;
    public bool ConsumeStartShield()
    {
        bool v = _startShieldPending;
        _startShieldPending = false;
        return v;
    }

    /// <summary>
    /// Death-экран: блок мета-прогрессии («+Y XP», «Level N → N+1», прогресс-бар,
    /// «Разблокировано: …» — только при lvl-up и только Wave1-строки, GDD §7).
    /// </summary>
    private void FillDeathMeta()
    {
        var pilot = PilotProgressManager.Instance;
        if (pilot == null || deathXp == null) return;

        // Фикс плейтеста: в сцене DeathXp/DeathLevel создаются SetActive(false) и
        // нигде не включались — «+Y XP» и уровень не были видны на Death-экране.
        deathXp.gameObject.SetActive(true);
        if (deathLevel != null) deathLevel.gameObject.SetActive(true);

        L10n.Bind(deathXp, "xp_gain", Format(pilot.LastRunXp));
        if (pilot.PilotLevel > pilot.LevelBeforeLastRun)
        {
            string s = L10n.GetFormatted("level_up_line", pilot.LevelBeforeLastRun, pilot.PilotLevel);
            deathLevel.text = string.IsNullOrEmpty(s)
                ? $"УРОВЕНЬ {pilot.LevelBeforeLastRun} → {pilot.PilotLevel}" : s;
        }
        else
        {
            string s = L10n.GetFormatted("level_line", pilot.PilotLevel);
            deathLevel.text = string.IsNullOrEmpty(s) ? $"УРОВЕНЬ {pilot.PilotLevel}" : s;
        }
        UiProgressBar.Set(Rt(deathXpBarFill), pilot.ProgressToNextLevel());

        // «Разблокировано» — только при росте уровня и implementedInWave1 (GDD §7/§5bis.2)
        bool leveled = pilot.PilotLevel > pilot.LevelBeforeLastRun;
        if (leveled)
        {
            var unlocks = pilot.GetUnlocksForRange(pilot.LevelBeforeLastRun + 1, pilot.PilotLevel);
            var names = new List<string>();
            foreach (var u in unlocks)
            {
                if (!u.implementedInWave1) continue;
                string n = L10n.Get("unlock_" + u.id);
                names.Add(string.IsNullOrEmpty(n) ? u.id : n);
            }
            if (names.Count > 0)
            {
                string title = L10n.Get("unlocked_title");
                deathUnlocked.text = (string.IsNullOrEmpty(title) ? "РАЗБЛОКИРОВАНО: " : title + " ") + string.Join(", ", names);
                deathUnlocked.gameObject.SetActive(true);
            }
            else deathUnlocked.gameObject.SetActive(false);
        }
        else deathUnlocked.gameObject.SetActive(false);
    }

    /// <summary>Мгновенный вход в стартовый экран (только при инициализации сцены).</summary>
    public void ShowStartImmediate()
    {
        _screen = Screen.Start;
        StopTransitions();
        ApplyAdaptiveStartLayout();
        SetVisible(startPanel, true);
        SetVisible(deathPanel, false);
        SetVisible(pausePanel, false);
        SetVisible(hudRoot, false);
        if (pauseBtn != null) pauseBtn.SetActive(false);
        // Элементы — в позиции покоя
        ResetRest(title1); ResetRest(title2); ResetRest(startBest);
        ResetRest(logoImage);
        ResetRest(deathScore); ResetRest(deathBest);
        // Фикс плейтеста: XP/уровень пилота перечитываются при каждом показе меню
        // (после забега бар и текст показывали значения с момента Init).
        RefreshPilotBlock();
        StartCtaPulse();
    }

    private static void ResetRest(Object c)
    {
        var rt = Rt(c);
        if (rt != null) rt.anchoredPosition = RestPos(rt);
    }

    // Позиция покоя: SlideFade всегда возвращает элемент на место, здесь страховка.
    private static Vector2 RestPos(RectTransform rt)
    {
        // Позиции заданы сценой; SlideFade хранит rest на входе. После выхода
        // элемент уже на rest — просто не трогаем, если корутины не в полёте.
        return rt.anchoredPosition;
    }

    // ——— Адаптивный лэйаут стартового экрана (любое соотношение сторон) ———

    /// <summary>
    /// Адаптивный лэйаут стартового экрана. Элементы программно привязываются к
    /// сторонам экрана: лого и BEST — к верхней кромке, TAP TO PLAY — к нижней.
    /// Якоря и pivot выставляются кодом, поэтому раскладка не зависит от якорей
    /// сцены и не «уезжает» за кадр ни при одном соотношении сторон
    /// (CanvasScaler.Expand меняет высоту канваса на узких/альбомных экранах).
    /// Для низких кадров (h < 1500) применяется компактный пресет.
    /// Вызывается при Init, перед каждым каскадом и при смене разрешения (Update).
    /// </summary>
    private void ApplyAdaptiveStartLayout()
    {
        if (_canvasRt == null) return;
        float h = _canvasRt.rect.height;
        if (h <= 0f || Mathf.Approximately(h, _lastLayoutH)) return;
        _lastLayoutH = h;

        bool compact = h < 1500f;
        float logoW = compact ? 560f : 700f;
        float cw = _canvasRt.rect.width;
        if (cw > 0f) logoW = Mathf.Min(logoW, cw * 0.85f); // не шире 85% ширины кадра
        float logoAspect = 237f / 587f;                    // Assets/Logo.png (587×237)
        float logoH = logoW * logoAspect;
        float gapTop = compact ? 150f : h * 0.11f;         // отступ лого от верхней кромки
        float gapBottom = compact ? 150f : h * 0.22f;      // отступ CTA от нижней кромки

        if (logoImage != null)
        {
            var rt = logoImage.rectTransform;
            AnchorTop(rt);
            rt.sizeDelta = new Vector2(logoW, logoH);
            rt.anchoredPosition = new Vector2(0f, -gapTop);
        }
        if (startBest != null)
        {
            var rt = startBest.rectTransform;
            AnchorTop(rt);
            // BEST — под логотипом (позиция покоя для слайда SlideBest)
            rt.anchoredPosition = new Vector2(0f, -(gapTop + logoH + 50f));
        }
        if (ctaText != null)
        {
            var rt = ctaText.rectTransform;
            AnchorBottom(rt);
            rt.anchoredPosition = new Vector2(0f, gapBottom);
        }
    }

    /// <summary>Верхняя привязка: anchor (0.5,1), pivot (0.5,1); Y отсчитывается от верхней кромки вниз.</summary>
    private static void AnchorTop(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
    }

    /// <summary>Нижняя привязка: anchor (0.5,0), pivot (0.5,0); Y отсчитывается от нижней кромки вверх.</summary>
    private static void AnchorBottom(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
    }


    private void StopTransitions()
    {
        foreach (var c in _transitions) if (c != null) StopCoroutine(c);
        _transitions.Clear();
    }

    // ——— §4.1 Старт (тап по CTA): UI-часть (камера — в GameManager/CameraDirector) ———

    /// <summary>Вектора ухода ВВЕРХ за экран (отрицательный Y-сдвиг = вверх в anchored-координатах).</summary>
    private static readonly Vector2 ExitUpTitle = new Vector2(0f, -480f);
    private static readonly Vector2 ExitUpBest = new Vector2(0f, -420f);

    /// <summary>
    /// Старт (решение владельца): UI уходит плавно и РАЗНОНАПРАВЛЕННО —
    /// заголовок ASTRO DRIFT и BEST улетают ВВЕРХ за экран (slide-out, EaseInQuick),
    /// «TAP TO PLAY» — чистый fade-out. Панель НЕ гасится мгновенно (резкий уход),
    /// но сразу перестаёт ловить raycast (повторный BeginRun), а после завершения
    /// анимаций полностью скрывается.
    /// </summary>
    public void PlayStartToGame()
    {
        _screen = Screen.Hud;
        StopCtaPulse();
        StopTransitions();
        // Фикс ux4-3 (сохранён): панель сразу НЕ интерактивна — её полноэкранное
        // невидимое Image больше не ловит тапы; альфа остаётся 1 — элементы уходят плавно.
        var startCg = Cg(startPanel);
        if (startCg != null)
        {
            startCg.blocksRaycasts = false;
            startCg.interactable = false;
        }
        SetVisible(hudRoot, false);
        // CTA: чистый fade-out 0.25 s EaseInQuick, 0 мс (без слайда — решение владельца)
        _transitions.Add(StartCoroutine(UiAnim.Fade(Cg(ctaText), Cg(ctaText).alpha, 0f, 0.25f, UiAnim.EaseInQuick)));
        // Заголовок ASTRO DRIFT: улетает вверх за экран, 0.30 s EaseInQuick, каскад 70 мс
        // (текстовые заголовки заменены картинкой-логотипом — летит первым в каскаде)
        if (logoImage != null)
            _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(logoImage), logoImage.rectTransform, ExitUpTitle, false, 0.30f, 0f, UiAnim.EaseInQuick)));
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(title1), title1 != null ? title1.rectTransform : null, ExitUpTitle, false, 0.30f, 0f, UiAnim.EaseInQuick)));
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(title2), title2 != null ? title2.rectTransform : null, ExitUpTitle, false, 0.30f, 0.07f, UiAnim.EaseInQuick)));
        // BEST: вверх следом, 0.30 s, 140 мс
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(startBest), startBest.rectTransform, ExitUpBest, false, 0.30f, 0.14f, UiAnim.EaseInQuick)));
        // Страховка: после завершения ухода панель скрыта целиком (alpha=0)
        _transitions.Add(StartCoroutine(HideStartPanelAfter(0.45f)));
    }

    private IEnumerator HideStartPanelAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (_screen == Screen.Hud) SetVisible(startPanel, false);
    }

    // ——— §4.2 Смерть → Death panel (каскад, вход EaseOutSoft) ———

    public void PlayDeathIn(int score, int best, bool newBest)
    {
        _screen = Screen.Death;
        StopTransitions();
        StopOfferTimer();
        _offerActive = false;
        _continueDeclinedLogged = false; // новая смерть — правило §2.5 начинается заново

        L10n.Bind(deathScore, "score", Format(score));
        L10n.Bind(deathBest, "best", Format(best));
        L10n.Bind(deathNewBest, "new_best");
        deathNewBest.gameObject.SetActive(newBest);
        FillDeathMeta(); // UI v3: «+Y XP», уровень, прогресс-бар, «Разблокировано»
        if (newBest && AudioManager.Instance != null) AudioManager.Instance.PlayRecord();

        SetVisible(deathPanel, true);
        var panelCg = Cg(deathPanel);
        panelCg.alpha = 1f;
        panelCg.blocksRaycasts = true;
        panelCg.interactable = true;

        // Реклама не готова → предложение скрыто целиком (§9: не disabled-серое),
        // каскад без него, таймер не запускается. 1 continue за забег (§3).
        bool adReady = AdsFlow.Instance != null && AdsFlow.Instance.IsRewardedReady;
        bool offerVisible = adReady && !_continueUsedThisRun;

        // §7: не более одного золотого элемента — NEW BEST золото → предложение белое;
        // NEW BEST нет → золото получает предложение (Palette.UiAccent, §4 GDD)
        if (continueText != null)
            continueText.color = newBest ? Color.white : Palette.UiAccent;

        SetVisible(deathScore, false);
        SetVisible(deathBest, false);
        SetVisible(deathNewBest, false);
        SetVisible(continueBtn, false);
        SetVisible(continueText, false);
        SetVisible(continueCaption, false);
        if (continueTimerLine != null) SetVisible(continueTimerLine, false);
        SetVisible(homeBtn, false);

        // Каскад §5.1: Score 0 мс → Best/NEW BEST 70 мс → предложение (+подпись+линия) 140 мс → Домой 210 мс
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(deathScore), deathScore.rectTransform, SlideScore, true, 0.40f, 0f, UiAnim.EaseOutSoft)));
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(deathBest), deathBest.rectTransform, SlideBest, true, 0.35f, 0.07f, UiAnim.EaseOutSoft)));
        if (newBest)
            _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(deathNewBest), deathNewBest.rectTransform, SlideBest, true, 0.35f, 0.07f, UiAnim.EaseOutSoft)));
        if (offerVisible)
        {
            _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(continueBtn), Rt(continueBtn), SlideButton, true, 0.30f, 0.14f, UiAnim.EaseOutSoft)));
            _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(continueText), continueText.rectTransform, SlideButton, true, 0.30f, 0.14f, UiAnim.EaseOutSoft)));
            _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(continueCaption), continueCaption.rectTransform, SlideButton, true, 0.30f, 0.14f, UiAnim.EaseOutSoft)));
            // Линия-таймер входит одним слайдом с предложением (§5.1).
            // ux5: после истечения таймера линия осталась с sizeDelta.x→0 и alpha=0 —
            // восстанавливаем исходную ширину (альфу поднимет SlideFade входа).
            if (continueTimerLine != null)
            {
                if (_offerLineRestWidth > 0f)
                    continueTimerLine.sizeDelta = new Vector2(_offerLineRestWidth, continueTimerLine.sizeDelta.y);
                _offerLineFullWidth = continueTimerLine.sizeDelta.x;
                _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(continueTimerLine.gameObject), continueTimerLine, SlideButton, true, 0.30f, 0.14f, UiAnim.EaseOutSoft)));
            }
            _transitions.Add(StartCoroutine(StartOfferAfterCascade()));
        }
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(homeBtn), Rt(homeBtn), SlideButton, true, 0.30f, 0.21f, UiAnim.EaseOutSoft)));

        Analytics.Log("death_screen_shown", new Dictionary<string, object>
        {
            { "score", score },
            { "new_best", newBest },
            { "ad_ready", adReady },
        });
        _deathContinueAvailable = offerVisible;
    }

    /// <summary>Таймер стартует через 0.78 s после входа панели — после каскада (§10.2.3).</summary>
    private IEnumerator StartOfferAfterCascade()
    {
        yield return new WaitForSecondsRealtime(0.78f);
        StartOfferTimer();
    }

    /// <summary>
    /// Continue fade-out панели 0.25 s EaseInQuick (§5.2 GDD) — вызывается из
    /// GameManager.ContinueRun. Предложение/таймер гасятся, каскад не переигрывается.
    /// </summary>
    public void PlayContinueOut()
    {
        StopOfferTimer();
        _offerActive = false;
        _deathContinueAvailable = false;
        StopTransitions();
        _transitions.Add(StartCoroutine(FadeOut(deathPanel, 0.25f, 0f)));
    }

    /// <summary>Флаги для GameManager: continue на этой смерти доступен / уже использован в забеге.</summary>
    public bool DeathContinueAvailable => _deathContinueAvailable;
    public bool ContinueUsedThisRun => _continueUsedThisRun;

    /// <summary>ТЗ §2.5: continue_declined уже отправлен в этой смерти (антидубль exit_to_home).</summary>
    public bool ContinueDeclinedLogged => _continueDeclinedLogged;

    /// <summary>ТЗ §2.5: сейчас виден экран паузы (from = "pause" для exit_to_home).</summary>
    public bool IsPauseScreen => _screen == Screen.Pause;

    /// <summary>Сброс флага «continue использован» (GameManager.BeginRun — новый забег).</summary>
    public void ResetContinueFlag() => _continueUsedThisRun = false;

    /// <summary>HUD score fade-out 0.20 s EaseInQuick (§4.2, сразу при смерти).</summary>
    public void HudOut()
    {
        SetVisible(hudRoot, false);
        if (pauseBtn != null) pauseBtn.SetActive(false);
        _transitions.Add(StartCoroutine(FadeOut(hudRoot, 0.20f, 0f)));
    }

    // ——— §4.3 Retry: Death panel fade-out 0.25 s EaseInQuick, HUD fade-in 0.3 s на 0.45 s ———

    public void PlayDeathOut()
    {
        StopTransitions();
        _transitions.Add(StartCoroutine(FadeOut(deathPanel, 0.25f, 0f)));
    }

    /// <summary>HUD fade-in (§5: delay = startUnlockTime; §6.1: delay 0.45, dur 0.3).</summary>
    public void HudIn(float delay, float dur)
    {
        var cg = Cg(hudRoot);
        SetVisible(hudRoot, true);
        cg.alpha = 0f;
        // Кнопка паузы живёт вместе с HUD (v2-регрессия: после SetActive(false)
        // в Setup она больше нигде не включалась — в геймплее паузы не было).
        if (pauseBtn != null) pauseBtn.SetActive(true);
        _transitions.Add(StartCoroutine(UiAnim.Fade(cg, 0f, 1f, dur, UiAnim.EaseOutSoft, delay)));
    }

    // ——— §4.4 Pause / Resume (unscaled; выход быстрее входа) ———

    public void PauseIn()
    {
        _screen = Screen.Pause;
        StopTransitions();
        // ux4-R2 (фикс №2): на Pause-экране кнопка паузы гасится (за оверлеем
        // она иначе видна и «висит» без дела), resume возвращает её.
        if (pauseBtn != null) pauseBtn.SetActive(false);
        // Стартовая панель могла остаться в середине fade-out (StopTransitions
        // убил корутины) — форсируем её скрытие: в HUD/паузе её не видно.
        SetVisible(startPanel, false);
        SetVisible(deathPanel, false);
        SetVisible(pausePanel, true);
        var cg = Cg(pausePanel);
        cg.alpha = 0f;
        _transitions.Add(StartCoroutine(UiAnim.Fade(cg, 0f, 1f, 0.25f, UiAnim.EaseOutSoft)));
        // Каскад 70 мс: заголовок → RESUME → HOME (текст + разделители, без плашек — §3)
        var title = pausePanel.transform.Find("PauseTitle") as RectTransform;
        var resume = pausePanel.transform.Find("Btn_Resume") as RectTransform;
        var home = pausePanel.transform.Find("Btn_Home") as RectTransform;
        SlideInEl(title, SlidePause, 0.30f, 0f);
        SlideInEl(resume, SlidePause, 0.30f, 0.07f);
        SlideInEl(home, SlidePause, 0.30f, 0.14f);
        // HUD score: fade 1→0.25 (притушить, не спрятать)
        var scoreCg = Cg(scoreText);
        _transitions.Add(StartCoroutine(UiAnim.Fade(scoreCg, scoreCg.alpha, 0.25f, 0.25f, UiAnim.EaseOutSoft)));
    }

    public void PauseOut()
    {
        StopTransitions();
        if (pauseBtn != null) pauseBtn.SetActive(true);
        _transitions.Add(StartCoroutine(UiAnim.Fade(Cg(pausePanel), Cg(pausePanel).alpha, 0f, 0.22f, UiAnim.EaseInQuick)));
        // HUD возвращается полностью видимым (страховка от заниженной альфы,
        // если пауза случилась посреди HUD-fade старта)
        var hudCg = Cg(hudRoot);
        _transitions.Add(StartCoroutine(UiAnim.Fade(hudCg, hudCg.alpha, 1f, 0.22f, UiAnim.EaseInQuick)));
        var scoreCg = Cg(scoreText);
        _transitions.Add(StartCoroutine(UiAnim.Fade(scoreCg, scoreCg.alpha, 1f, 0.22f, UiAnim.EaseInQuick)));
        _screen = Screen.Hud;
    }

    private void SlideInEl(RectTransform rt, Vector2 offset, float dur, float delay)
    {
        if (rt == null) return;
        SetVisible(rt, false);
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(rt), rt, offset, true, dur, delay, UiAnim.EaseOutSoft)));
    }

    // ——— §4.5 Home: панели уже ушли (PlayPanelOut), стартовый UI каскадом ———

    public void PlayPanelOut()
    {
        StopTransitions();
        StopCtaPulse();
        // Панели fade-out 0.25 s EaseInQuick (какая видима — та и уходит)
        if (Cg(deathPanel).alpha > 0.5f)
            _transitions.Add(StartCoroutine(FadeOut(deathPanel, 0.25f, 0f)));
        if (Cg(pausePanel).alpha > 0.5f)
            _transitions.Add(StartCoroutine(FadeOut(pausePanel, 0.25f, 0f)));
        // Фикс адаптива (адаптация экранов): HUD (счёт/чип/пауза) при выходе в меню
        // оставался видимым — «25» висело над логотипом. Из Playing напрямую GoHome
        // возможен (клавиатура/пульт), из паузы score оставался притушенным (0.25).
        var hudCg = Cg(hudRoot);
        if (hudCg != null && hudCg.alpha > 0.05f)
        {
            if (pauseBtn != null) pauseBtn.SetActive(false);
            _transitions.Add(StartCoroutine(UiAnim.Fade(hudCg, hudCg.alpha, 0f, 0.20f, UiAnim.EaseInQuick)));
        }
    }

    /// <summary>Каскад стартового UI: заголовок 150 мс → Best 220 мс → CTA 300 мс, по 0.35 s EaseOutSoft.</summary>
    public void ShowStartCascade()
    {
        _screen = Screen.Start;
        StopTransitions();
        ApplyAdaptiveStartLayout(); // rest-позиции под текущий кадр ДО старта каскада
        SetVisible(startPanel, true);
        var startCg = Cg(startPanel);
        startCg.alpha = 1f;
        startCg.blocksRaycasts = true;
        if (pauseBtn != null) pauseBtn.SetActive(false);
        foreach (var t in new[] { title1, title2, startBest, ctaText }) SetVisible(t, false);
        // Фикс плейтеста: перечитываем XP/уровень пилота при возврате в меню (Home)
        RefreshPilotBlock();

        if (logoImage != null)
        {
            SetVisible(logoImage, false);
            _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(logoImage), logoImage.rectTransform, SlideTitle, true, 0.35f, 0.15f, UiAnim.EaseOutSoft)));
        }
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(title1), title1 != null ? title1.rectTransform : null, SlideTitle, true, 0.35f, 0.15f, UiAnim.EaseOutSoft)));
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(title2), title2 != null ? title2.rectTransform : null, SlideTitle, true, 0.35f, 0.15f, UiAnim.EaseOutSoft)));
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(startBest), startBest.rectTransform, SlideBest, true, 0.35f, 0.22f, UiAnim.EaseOutSoft)));
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(ctaText), ctaText.rectTransform, SlideCta, true, 0.35f, 0.30f, UiAnim.EaseOutSoft)));
        StartCtaPulse();
    }

    private IEnumerator FadeIn(GameObject go, float dur, float delay)
    {
        SetVisible(go, true);
        yield return UiAnim.Fade(Cg(go), 0f, 1f, dur, UiAnim.EaseOutSoft, delay);
    }

    private IEnumerator FadeOut(GameObject go, float dur, float delay)
    {
        yield return UiAnim.Fade(Cg(go), Cg(go).alpha, 0f, dur, UiAnim.EaseInQuick, delay);
    }

    // ——— Пульс CTA (§3): alpha текста 1→0.72→1, период 1.8 s, синхронно с линией ———

    private void StartCtaPulse()
    {
        StopCtaPulse();
        if (ctaText == null) return;
        _ctaPulse = StartCoroutine(CtaPulseRoutine());
    }

    private void StopCtaPulse()
    {
        if (_ctaPulse != null) { StopCoroutine(_ctaPulse); _ctaPulse = null; }
        if (ctaText != null) ctaText.alpha = 1f;
    }

    // Пульс прозрачности текста CTA (§3): 100% → 72% → 100%, период 1.8 s.
    // Линия-индикатор удалена (решение владельца ux4-6) — текст остался единственным якорем.
    private IEnumerator CtaPulseRoutine()
    {
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Repeat(t / UiAnim.CtaPulsePeriod, 1f);
            // Текст: 100% → 72% → 100% за период (масштаб НЕ трогаем — §3.3)
            ctaText.alpha = 1f - 0.28f * (1f - Mathf.Cos(k * 2f * Mathf.PI)) * 0.5f;
            yield return null;
        }
    }

    // ——— HUD (геймплейный — без изменений, только чип/пульс как было) ———

    public void RefreshHud()
    {
        if (_score == null) return;
        int score = _score.Score;
        if (scoreText != null) scoreText.text = Format(score);

        int m = _score.Multiplier;
        if (comboChip != null)
        {
            bool show = m > 1 && (_screen == Screen.Hud || _screen == Screen.Pause);
            comboChip.gameObject.SetActive(show);
            if (show)
            {
                comboChip.text = "x" + m;
                comboChip.color = Palette.ComboColor(m);
                if (m > _lastMultiplier) PulseCombo();
            }
        }
        _lastMultiplier = m;

        // Прогресс до следующего перка (§15.3). Пороги не заданы (levelUpScoreThresholds пуст) → бар скрыт целиком
        // (скрываем родителя: иначе пустая полупрозрачная дорожка 360×8 остаётся висеть под счётом).
        var perks = PerkManager.Instance;
        var perkBarRoot = perkProgressBarFill != null ? perkProgressBarFill.transform.parent as RectTransform : null;
        bool perkBarShow = perks != null && perks.PerkProgressAvailable && perkBarRoot != null;
        if (perkBarRoot != null)
        {
            perkBarRoot.gameObject.SetActive(perkBarShow);
            if (perkBarShow) UiProgressBar.Set(Rt(perkProgressBarFill), perks.PerkProgress(score));
        }

        if (startBest != null) L10n.Bind(startBest, "best", Format(_score.Best));
    }

    private void PulseCombo()
    {
        if (comboChip == null) return;
        StopCoroutine(nameof(PulseComboRoutine));
        StartCoroutine(PulseComboRoutine());
    }

    private IEnumerator PulseComboRoutine()
    {
        RectTransform rt = comboChip.rectTransform;
        float t = 0f;
        float dur = 0.2f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float ease = Mathf.Sin(k * Mathf.PI);
            rt.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.4f, ease);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private void PulseScore()
    {
        if (scoreText == null) return;
        StopCoroutine(nameof(PulseScoreRoutine));
        StartCoroutine(PulseScoreRoutine());
    }

    private IEnumerator PulseScoreRoutine()
    {
        RectTransform rt = scoreText.rectTransform;
        float t = 0f;
        float dur = 0.15f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float ease = Mathf.Sin(k * Mathf.PI);
            rt.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.12f, ease);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private void ShrinkCombo()
    {
        StopCoroutine(nameof(PulseComboRoutine));
        if (comboChip == null || !comboChip.gameObject.activeSelf) return;
        StartCoroutine(ShrinkComboRoutine());
    }

    private IEnumerator ShrinkComboRoutine()
    {
        RectTransform rt = comboChip.rectTransform;
        float t = 0f;
        float dur = 0.2f;
        Vector3 from = rt.localScale;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            rt.localScale = Vector3.Lerp(from, Vector3.one * 0.8f, k);
            yield return null;
        }
        rt.localScale = Vector3.one;
        RefreshHud(); // m=1 → чип скрывается
    }

    // ——— Пауза / Home ———

    private void TogglePause()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
        // §0.5: установка/снятие фриза только через TimeFreeze (один владелец timeScale)
        if (!TimeFreeze.Frozen)
        {
            TimeFreeze.Freeze();
            PauseIn();
            PlatformServices.Lifecycle.GameplayStop();
            // ТЗ §2.4: один агрегат на оба ветвления — «какой % забегов прерывается паузой?»
            Analytics.Log("pause_toggled", new Dictionary<string, object> { { "action", "open" } });
        }
        else
        {
            TimeFreeze.Unfreeze();
            PauseOut();
            PlatformServices.Lifecycle.GameplayStart();
            Analytics.Log("pause_toggled", new Dictionary<string, object> { { "action", "close" } });
        }
    }

    private void GoHomeFromPause()
    {
        TimeFreeze.Unfreeze(); // §0.5: EnterMenu() продублирует снятие фриза (идемпотентно)
        GameManager.Instance.GoHome();
    }

    private static string Format(int v) => v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
}
