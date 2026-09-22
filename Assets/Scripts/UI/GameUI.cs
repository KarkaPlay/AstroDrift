using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// UI «Menu & Transitions v2» (ArtDirection §1–§7). Панели — сценовые объекты;
/// GameUI навешивает поведение и играет переходы:
/// • Стартовый экран: только текст + линия-индикатор (никаких плашек), пульс §3.
/// • Переходы — по двум каноническим кривым (UiAnim), каскады 60–90 мс, unscaled time.
/// • HUD геймплея не тронут (только fade-появление §5 и притушивание в паузе §4.4).
/// • Шрифты — только через Typography (TypographyConfig); пустой конфиг = LiberationSans.
/// §8 (новая редакция): скрытая панель — НЕАКТИВНА (SetActive(false)) + alpha 0 + raycasts/
/// interactable off. Показ всегда начинается с активации (SetVisible / PrepareForShow).
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
    [SerializeField] private TextMeshProUGUI startBest;       // подпись рекорда (ключ best_label)
    [SerializeField] private TextMeshProUGUI startBestValue;  // число рекорда (ключ best_value)
    [SerializeField] private TextMeshProUGUI ctaText;
    [SerializeField] private Button tapToPlayBtn;     // полноэкранная невидимая зона тапа (ux4-5)
    [SerializeField] private Image logoImage;         // «Astro Drift» — картинка вместо текста (любая локаль)

    [Header("UI v3: мета-прогрессия (GDD §11, Волна 1)")]
    [SerializeField] private TextMeshProUGUI pilotLevelText; // капшн карточки: pilot_level_label ↔ level_up_line (GDD_v3 §7)
    [SerializeField] private LevelCardUI levelCard;          // карточка уровня: число + бар 364×24 (UiProgressBar)
    [SerializeField] private RectTransform xpBarFill;        // заливка бара карточки (Filled, UiProgressBar)
    [SerializeField] private TextMeshProUGUI startXpGain;    // «+N XP» над карточкой при возврате с XP (GDD_v3 §8)
    [SerializeField] private Image perkProgressBarFill;      // HUD: прогресс до следующего перка (GDD §15.3)
    [SerializeField] private RectTransform menuButtonsRow;   // ряд нижних кнопок меню (SlideFade выхода/входа)
    [SerializeField] private Button menuUpgradeBtn;          // «ПРОКАЧКА» в ряду меню — открывает дерево разблокировок
    [SerializeField] private SettingsScreen settingsPanel;   // экран настроек (закрывается при смене Screen)
    [SerializeField] private Button startShieldBtn;          // «Стартовый щит за рекламу»
    [SerializeField] private TextMeshProUGUI startShieldText;
    [SerializeField] private TextMeshProUGUI startShieldCaption;

    // LSE-ссылки (необязательны): нода с LocalizeStringEvent обычно та же, что TMP-ссылка,
    // поэтому GetLse берёт компонент с неё. Поля — для случая, когда владелец разнёс ноды.
    [SerializeField] private LocalizeStringEvent startBestValueLse;
    [SerializeField] private LocalizeStringEvent shieldCaptionLse;

    // §3.4: горячий путь (RefreshHud на каждую смену счёта) — гард по значению +
    // кэшированный массив. Сброс — LanguageService → ResetLanguageGuards (подписка одна).
    private readonly object[] _bestArgs = new object[1];
    private int _lastBestShown = -1;
    private string _shieldCaptionKey; // текущее состояние двухсостоятельной подписи щита
    private static readonly List<GameUI> _instances = new List<GameUI>();

    [Header("Тексты")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI comboChip;

    [Header("Death (GDD_DeathScreen_v3 §2/§7)")]
    [SerializeField] private TextMeshProUGUI deathScore;      // только ЧИСЛО результата (ключ best_value)
    [SerializeField] private TextMeshProUGUI deathBest;       // только ЧИСЛО рекорда (ключ best_value)
    [SerializeField] private Button continueBtn;              // корень спрайт-кнопки: CanvasGroup + дети (бар/подписи)
    [SerializeField] private TextMeshProUGUI continueText;    // CTA 40 px (continue_cta)
    [SerializeField] private TextMeshProUGUI continueCaption; // подпись 22 px (continue_caption)
    [SerializeField] private RectTransform continueTimerFill; // заливка бара таймера оффера (UiProgressBar)
    [SerializeField] private RectTransform deathSkull;        // каскад §3
    [SerializeField] private RectTransform deathTitle;
    [SerializeField] private RectTransform deathSubtitle;
    [SerializeField] private RectTransform deathScorePanel;
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
    private const float RefHeight = 1920f; // референс CanvasScaler — база пропорции стартовой группы

    // Авторская раскладка стартовой группы (источник истины): кэш позиций/размеров из сцены.
    private struct RestEntry { public RectTransform rt; public Vector2 basePos; public Vector2 baseSize; public Vector2 restPos; }
    private readonly List<RestEntry> _restEntries = new List<RestEntry>();
    private bool _restCaptured;
    private readonly List<Coroutine> _transitions = new List<Coroutine>();
    private Coroutine _ctaPulse;
    private Coroutine _offerTimer;         // таймер предложения (§10.2.3)
    private bool _offerActive;             // предложение видно и таймер идёт (OfferRunning)
    private bool _offerPausedByFocus;      // приложение ушло в фон при OfferRunning (§9)
    private float _offerRemaining;         // остаток таймера для продолжения после фокуса
    private float _offerFullDuration;
    // §8: XP, полученный за последний забег (флаг для анимации возврата в меню). 0 = анимации нет.
    private int _pendingXpGain;
    private Coroutine _menuProgressAnim;   // единая корутина «бар + +XP + пульс уровня» (GDD_v3 §8)
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

        // Поведение кнопок (структура — в сцене, обработчики — здесь).
        // Открытый экран настроек блокирует старт забега — и гардом здесь, и блокировкой
        // raycast на самом StartPanel (SettingsScreen.BlockMenu): одного гарда мало,
        // тап по пустому месту ловил бы невидимую зону Btn_TapToPlay.
        if (tapToPlayBtn != null) tapToPlayBtn.onClick.AddListener(() =>
        {
            if (settingsPanel != null && settingsPanel.IsOpen) return;
            GameManager.Instance.BeginRun();
        });

        // Локализация статичных текстов — компоненты LocalizeStringEvent на нодах
        // (владельцы: билдеры префабов §8.1 и Setup Scene §8.2 — привязка идёт в момент
        // создания ноды). GameUI владеет только динамикой: Arguments и рантайм-смена
        // entry (§3.1). Заголовок-картинка локали не требует. Рекорд меню — две ноды:
        // подпись best_label и число best_value (число обновляет RefreshHud, гард §3.4).
        if (!_instances.Contains(this)) _instances.Add(this);

        // Фикс «тап по TAP TO PLAY не стартует игру»: TMP-тексты стартового экрана
        // (перекрывающие полноэкранную невидимую зону тапа) перехватывали raycast.
        // Тексты — не интерактивные элементы: выключаем их raycastTarget, тап всегда
        // доходит до tapToPlayBtn в любой точке экрана (включая сам текст).
        foreach (var t in new[] { startBest, startBestValue, ctaText })
            if (t != null) t.raycastTarget = false;
        if (continueBtn != null) continueBtn.onClick.AddListener(OnContinueTapped);
        if (homeBtn != null) homeBtn.onClick.AddListener(HomeWithInterstitial);
        if (startShieldBtn != null) startShieldBtn.onClick.AddListener(OnStartShieldTapped);
        // §8: XP приходит событием на каждой смерти (GrantRunXp) — подписка живёт весь рантайм,
        // флаг читает ShowStartCascade при возврате в меню.
        if (PilotProgressManager.Instance != null)
            PilotProgressManager.Instance.OnRunXpGranted += OnRunXpGranted;
        RefreshPilotBlock();
        if (pauseToggleBtn != null) pauseToggleBtn.onClick.AddListener(TogglePause);
        if (resumeBtn != null) resumeBtn.onClick.AddListener(TogglePause);
        if (quitBtn != null) quitBtn.onClick.AddListener(GoHomeFromPause);

        _score.OnScoreChanged += OnScoreChanged;
        _score.OnComboReset += ShrinkCombo;

        RefreshPilotBlock();
        ShowStartImmediate();
        RefreshHud();
        StartCtaPulse();
        BuildTreePanel();
        if (menuUpgradeBtn != null) menuUpgradeBtn.onClick.AddListener(ToggleTree);
        ValidateMandatoryRefs();
    }

    /// <summary>Обязательные ссылки: молчаливый ранний выход («if (scoreText != null)») —
    /// именно то, из-за чего «счёт всегда 0» дожил до ревью. Ошибка вместо тишины.</summary>
    private void ValidateMandatoryRefs()
    {
        if (hudRoot == null) Debug.LogError("GameUI: hudRoot не назначен.", this);
        if (scoreText == null) Debug.LogError("GameUI: scoreText не назначен — HUD-счёт не обновится (Hud/Score/ScoreText).", this);
        if (perkProgressBarFill == null) Debug.LogError("GameUI: perkProgressBarFill не назначен — бар перка не обновится (Hud/Score/PerkProgressBarBg/PerkProgressBarFill).", this);
        if (startBestValue == null) Debug.LogError("GameUI: startBestValue не назначен — число рекорда не обновится (StartPanel/StartBestValue).", this);
        if (menuUpgradeBtn == null) Debug.LogError("GameUI: menuUpgradeBtn не назначен — кнопка «ПРОКАЧКА» не откроет дерево разблокировок (StartPanel/Menu Buttons/MenuButton_Upgrade).", this);
        if (comboChip == null) Debug.LogWarning("GameUI: comboChip не назначен — чип комбо не покажется.", this);
        if (settingsPanel == null) Debug.LogWarning("GameUI: settingsPanel не назначен — «НАСТРОЙКИ» не откроют экран (StartPanel/SettingsPanel).", this);
    }

    /// <summary>Дашборд/старт забега при открытом экране настроек: экран обязан вернуться
    /// в меню сам, иначе Screen уехал бы в Hud, а поверх висел бы экран настроек.</summary>
    private void CloseSettingsIfOpen()
    {
        if (settingsPanel != null && settingsPanel.IsOpen) settingsPanel.Close();
    }

    // ——— Панель дерева разблокировок (плейтест Волны 1) ———

    private GameObject _treePanel;
    private CanvasGroup _treeCg;
    private TMPro.TextMeshProUGUI _treeText;
    private bool _treeLocaleSubscribed;

    /// <summary>
    /// Панель со списком уровней 0–20 из PilotProgressConfig.unlocks. Чистый текст,
    /// разблокированные — зелёные, нереализованные (implementedInWave1=false) — с «скоро».
    /// Строится программно поверх текущего UI (тот же Canvas), без сцены и скинов.
    /// Кнопки-заглушки «ДЕРЕВО» больше нет: панель открывает «ПРОКАЧКА» в ряду меню
    /// (menuUpgradeBtn, ссылка из AstroDriftSceneSetup; аналитика — MenuButtonUI на кнопке).
    /// </summary>
    private void BuildTreePanel()
    {
        if (_treePanel != null) return; // повторный Init
        if (startPanel == null || PilotProgressManager.Instance == null) return;

        // Панель: по центру, скрыта (CanvasGroup alpha=0).
        // Родитель — StartPanel, сосед НИЖЕ ряда меню: панель перекрывает полноэкранную
        // зону тапа (тап по панели не стартует забег), а «ПРОКАЧКА» остаётся выше панели —
        // иначе открытую панель нечем было бы закрыть.
        _treePanel = new GameObject("UnlockTreePanel", typeof(RectTransform), typeof(CanvasGroup));
        _treePanel.transform.SetParent(startPanel.transform, false);
        _treePanel.transform.SetSiblingIndex(menuButtonsRow != null ? menuButtonsRow.GetSiblingIndex() : _treePanel.transform.parent.childCount - 1);
        var panelRt = (RectTransform)_treePanel.transform;
        panelRt.anchorMin = Vector2.zero; panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = new Vector2(60f, 120f); panelRt.offsetMax = new Vector2(-60f, -120f);
        var panelImg = _treePanel.AddComponent<UnityEngine.UI.Image>();
        panelImg.color = Palette.UiPanel;
        _treeCg = _treePanel.GetComponent<CanvasGroup>();
        _treeCg.alpha = 0f; _treeCg.blocksRaycasts = false; _treeCg.interactable = false;
        // (целиком панель гасится в конце BuildTreePanel — уже после создания нод с LSE)

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
        // Нода создаётся здесь → владелец LSE здесь же (§8.0); роли у неё не было (§8.4).
        AddLocalized(titleTmp, "unlock_tree_title");

        var listGo = new GameObject("List", typeof(RectTransform));
        listGo.transform.SetParent(_treePanel.transform, false);
        var listRt = (RectTransform)listGo.transform;
        listRt.anchorMin = Vector2.zero; listRt.anchorMax = Vector2.one;
        listRt.offsetMin = new Vector2(24f, 12f); listRt.offsetMax = new Vector2(-24f, -80f);
        _treeText = listGo.AddComponent<TMPro.TextMeshProUGUI>();
        _treeText.fontSize = 24; _treeText.alignment = TMPro.TextAlignmentOptions.TopLeft;
        _treeText.raycastTarget = false;
        _treeText.textWrappingMode = TMPro.TextWrappingModes.Normal;

        // §8: скрытая панель дерева неактивна (ноды с LSE уже созданы и один раз активировались)
        UiAnim.SetVisible(_treeCg, false);
    }

    private void ToggleTree() => SetTreeVisible(_treePanel != null && _treeCg.alpha < 0.5f);

    private void SetTreeVisible(bool show)
    {
        if (_treePanel == null) return;
        if (show) FillUnlockTree();
        UiAnim.SetVisible(_treeCg, show); // §8: скрытая панель — неактивна
        SubscribeTreeLocale(show);
    }

    /// <summary>§10.1 п.4: дерево перерисовывается на смену локали, пока панель видима.
    /// Подписка снимается при скрытии и в OnDestroy — статическая подписка без отписки
    /// недопустима (образец — LanguageService.OnSelectedLocaleChanged).</summary>
    private void SubscribeTreeLocale(bool on)
    {
        if (on == _treeLocaleSubscribed) return;
        _treeLocaleSubscribed = on;
        if (on) LocalizationSettings.SelectedLocaleChanged += OnTreeLocaleChanged;
        else LocalizationSettings.SelectedLocaleChanged -= OnTreeLocaleChanged;
    }

    private void OnTreeLocaleChanged(UnityEngine.Localization.Locale _)
    {
        if (_treeCg == null || _treeCg.alpha < 0.5f) return;
        FillUnlockTree();
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

    private void OnDestroy()
    {
        _instances.Remove(this);
        SubscribeTreeLocale(false);
        if (PilotProgressManager.Instance != null)
            PilotProgressManager.Instance.OnRunXpGranted -= OnRunXpGranted;
        if (_score != null)
        {
            _score.OnScoreChanged -= OnScoreChanged;
            _score.OnComboReset -= ShrinkCombo;
        }
    }

    /// <summary>§8: запоминаем XP последнего забега — анимацию проиграет ShowStartCascade.
    /// Ноль — валидное «гранта не было»: тогда ни бара, ни лейбла, ни пульса.</summary>
    private void OnRunXpGranted(int delta) => _pendingXpGain = delta;

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
        // Aborted-реклама может закрыться РАНЬШЕ, чем доиграет fade-out панели (§5.3):
        // его корутина в конце погасила бы заново показанную панель.
        StopTransitions();
        SetVisible(deathPanel, true);
        var panelCg = Cg(deathPanel);
        panelCg.alpha = 1f;
        panelCg.blocksRaycasts = true;
        panelCg.interactable = true;

        SetVisible(deathSkull, true);
        SetVisible(deathTitle, true);
        SetVisible(deathSubtitle, true);
        SetVisible(deathScorePanel, true);
        SetVisible(deathScore, true);
        SetVisible(deathBest, true);
        // §5: Continue-блок скрыт ЦЕЛИКОМ (одним fade гасился — оживляем корень), бар сброшен в 1.
        SetVisible(continueBtn, false);
        UiProgressBar.Set(continueTimerFill, 1f);
        SetVisible(homeBtn, true);
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
        UiProgressBar.Set(continueTimerFill, 1f); // §5: таймер всегда стартует с полного бара
        _offerActive = true;
        _offerTimer = StartCoroutine(OfferTimerRoutine());
    }

    private void StopOfferTimer()
    {
        if (_offerTimer != null) { StopCoroutine(_offerTimer); _offerTimer = null; }
    }

    private IEnumerator OfferTimerRoutine()
    {
        // §5: бар Continue убывает 1 → 0 линейно, unscaled (мир заморожен), пауза при потере фокуса.
        while (_offerRemaining > 0f)
        {
            if (!_offerPausedByFocus)
            {
                _offerRemaining -= Time.unscaledDeltaTime;
                UiProgressBar.Set(continueTimerFill, _offerRemaining / _offerFullDuration);
            }
            yield return null;
        }
        // OfferExpired (§5): interactable = false НЕМЕДЛЕННО, затем ОДИН fade CanvasGroup
        // корня Btn_Continue — гаснет весь блок (подпись + бар), Btn_Home остаётся.
        _offerActive = false;
        if (continueBtn != null) continueBtn.interactable = false;
        Analytics.Log("continue_timer_expired", new Dictionary<string, object>
        {
            { "score", _score != null ? _score.Score : 0 },
        });
        if (continueBtn != null)
        {
            var contCg = Cg(continueBtn);
            _transitions.Add(StartCoroutine(UiAnim.Fade(contCg, contCg.alpha, 0f, 0.25f, UiAnim.EaseInQuick, 0f, deactivateWhenHidden: true)));
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // §9: предложение живёт, пока игрок на экране; таймер — на паузе в фоне
        if (Screen_DeathVisible)
            _offerPausedByFocus = !hasFocus;
    }

    private bool Screen_DeathVisible => _screen == Screen.Death && deathPanel != null && Cg(deathPanel).alpha > 0.5f;

    // ——— Типографика: роли носит TypeRoleTag (§3.5, задача 4); ручных применений в GameUI нет ———

    // ——— Экраны: показ/скрытие. §8: скрытое — НЕАКТИВНО (SetActive(false)) ———

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

    /// <summary>Мгновенный показ/скрытие (§8: скрытое неактивно). Скрытие = alpha 0 +
    /// raycasts/interactable off + SetActive(false). НЕ звать посреди анимации: деактивация
    /// оборвала бы fade — для уходов есть FadeOut / Fade(..., deactivateWhenHidden: true).</summary>
    private static void SetVisible(Object c, bool visible) => UiAnim.SetVisible(Cg(c), visible);

    /// <summary>Подготовка узла к анимации входа: живой, но alpha 0 и не ловит raycast.
    /// Отличается от SetVisible: тот гасит объект целиком и убил бы следующий за ним SlideFade.</summary>
    private static void PrepareForShow(Object c)
    {
        var cg = Cg(c);
        if (cg == null) return;
        UiAnim.EnsureActive(cg);
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
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

        // ТЗ v1.10: карточка уровня — префаб LevelCard.prefab. Подпись «УРОВЕНЬ ПИЛОТА»
        // переводимая и статична (LocalizeStringEvent, ключ pilot_level_label), число
        // уровня и бар живут в LevelCardUI (тот же UiProgressBar, с анкорной заливкой).
        if (levelCard != null) levelCard.Refresh();
        else UiProgressBar.Set(Rt(xpBarFill), pilot.ProgressToNextLevel());

        // Кнопка стартового щита (§10.1)
        var gmCfg = GameManager.Instance != null ? GameManager.Instance.Config : null;
        bool pointEnabled = gmCfg != null && gmCfg.startShieldDailyRewarded;
        bool levelOk = gmCfg != null && pilot.PilotLevel >= gmCfg.startShieldUnlockPilotLevel;
        bool adReady = AdsFlow.Instance != null && AdsFlow.Instance.IsRewardedReady;
        bool show = pointEnabled && levelOk;
        bool usedToday = pilot.StartShieldUsedToday;

        // §8: прячем ДО возможной активации — кнопка, показанная прошлым кадром, при
        // снятом гейте должна уйти целиком (не остаться невидимой, но кликабельной).
        if (!show)
        {
            SetVisible(startShieldBtn, false);
            SetVisible(startShieldText, false);
            SetVisible(startShieldCaption, false);
        }
        else
        {
            SetVisible(startShieldBtn, true);
            SetVisible(startShieldText, true);
            SetVisible(startShieldCaption, true);
            startShieldBtn.interactable = !usedToday && adReady;
            if (usedToday)
            {
                SetShieldCaption("shield_used_today");
                if (startShieldText != null) startShieldText.color = Palette.SecondaryText;
            }
            else
            {
                SetShieldCaption("shield_caption");
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
        ResetRest(startBest);
        ResetRest(logoImage);
        // Фикс плейтеста: XP/уровень пилота перечитываются при каждом показе меню
        // (после забега бар и текст показывали значения с момента Init).
        RefreshPilotBlock();
        // §8.4: мгновенный показ меню анимацию НЕ переигрывает — флаг сбрасывается.
        _pendingXpGain = 0;
        if (_menuProgressAnim != null) { StopCoroutine(_menuProgressAnim); _menuProgressAnim = null; }
        SetVisible(startXpGain, false); // нода «+N XP» не должна пережить прерванную анимацию
        _lastBestShown = -1; // §8: ноды меню могли быть неактивны — число рекорда пишем заново
        StartCtaPulse();
    }

    private void ResetRest(Object c)
    {
        var rt = Rt(c);
        if (rt == null) return;
        // Элементы стартовой группы — на rest текущего кадра (после ApplyAdaptiveStartLayout).
        for (int i = 0; i < _restEntries.Count; i++)
            if (_restEntries[i].rt == rt) { rt.anchoredPosition = _restEntries[i].restPos; return; }
        // Остальные (Death-экран) позиционирует сцена — не трогаем.
    }

    // ——— Адаптивный лэйаут стартового экрана (любое соотношение сторон) ———

    /// <summary>
    /// Авторская раскладка (сцена/префаб) — ИСТОЧНИК ИСТИНЫ: она кэшируется один раз и
    /// затем масштабируется ЦЕЛИКОМ одним коэффициентом k = h/1920 (референс CanvasScaler).
    /// На 1080×1920 k = 1 → кадр пиксель-в-пиксель авторский; на 1080×2340 / 2400×1080
    /// группа пропорционально растёт/сжимается и остаётся в кадре.
    /// Прежние по-элементные формулы (gapTop = h*0.11, «logoH + 50f», AnchorTop/Bottom)
    /// переставляли только лого/BEST/CTA и ломали авторские позиции: лого −178.6 → −211,
    /// BEST 438 → 421, а карточка, ряд кнопок и щит вообще не двигались.
    /// Панель стартового экрана растянута на кадр (якоря 0..1) — отсчёт «от кромки» верен
    /// на любом аспекте. Вызывается при Init, перед каждым каскадом и при смене разрешения.
    /// </summary>
    private void ApplyAdaptiveStartLayout(bool force = false)
    {
        if (_canvasRt == null) return;
        float h = _canvasRt.rect.height;
        if (h <= 0f) return;
        if (!force && Mathf.Approximately(h, _lastLayoutH)) return;
        CaptureRestLayout();
        _lastLayoutH = h;

        float k = h / RefHeight;
        for (int i = 0; i < _restEntries.Count; i++)
        {
            var e = _restEntries[i];
            if (e.rt == null) continue;
            e.restPos = new Vector2(e.basePos.x * k, e.basePos.y * k);
            e.rt.anchoredPosition = e.restPos;
            // Растянутая по оси нода (ряд кнопок: anchors 0..1) меряется от родителя,
            // который уже масштабируется канвасом — её sizeDelta по этой оси не трогаем.
            bool stretchX = !Mathf.Approximately(e.rt.anchorMin.x, e.rt.anchorMax.x);
            bool stretchY = !Mathf.Approximately(e.rt.anchorMin.y, e.rt.anchorMax.y);
            e.rt.sizeDelta = new Vector2(stretchX ? e.baseSize.x : e.baseSize.x * k,
                                         stretchY ? e.baseSize.y : e.baseSize.y * k);
            _restEntries[i] = e;
        }
    }

    /// <summary>Кэш авторской раскладки стартовой группы (один раз, до первой правки позиций).</summary>
    private void CaptureRestLayout()
    {
        if (_restCaptured) return;
        _restCaptured = true;
        AddRest(Rt(logoImage));
        AddRest(Rt(startBest));
        AddRest(Rt(startBestValue));
        AddRest(Rt(levelCard));
        AddRest(menuButtonsRow);
        AddRest(Rt(startShieldBtn));
        AddRest(Rt(ctaText));
    }

    private void AddRest(RectTransform rt)
    {
        if (rt == null) return;
        _restEntries.Add(new RestEntry { rt = rt, basePos = rt.anchoredPosition, baseSize = rt.sizeDelta });
    }

    /// <summary>Снимок авторской раскладки для тестов/отладки: «имя = pos|size».</summary>
    public string DumpRestLayout()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < _restEntries.Count; i++)
        {
            var e = _restEntries[i];
            if (e.rt == null) continue;
            sb.Append(e.rt.name).Append(" base=").Append(e.basePos).Append(" size=").Append(e.baseSize)
              .Append(" rest=").Append(e.restPos).Append(" now=").Append(e.rt.anchoredPosition).Append(" | ");
        }
        return sb.ToString();
    }

    /// <summary>Вернуть все элементы стартовой группы на rest-позицию текущего кадра.</summary>
    private void ResetRestAll()
    {
        for (int i = 0; i < _restEntries.Count; i++)
        {
            var e = _restEntries[i];
            if (e.rt != null) e.rt.anchoredPosition = e.restPos;
        }
    }


    private void StopTransitions()
    {
        foreach (var c in _transitions) if (c != null) StopCoroutine(c);
        _transitions.Clear();
    }

    // ——— §4.1 Старт (тап по CTA): UI-часть (камера — в GameManager/CameraDirector) ———

    /// <summary>Вектора ухода ВВЕРХ за экран. UiAnim.SlideFade считает цель ухода как
    /// rest − offset, поэтому отрицательный Y-сдвиг = уход вверх в anchored-координатах.</summary>
    private static readonly Vector2 ExitUpTitle = new Vector2(0f, -480f);
    private static readonly Vector2 ExitUpBest = new Vector2(0f, -420f);
    private static readonly Vector2 ExitUpCard = new Vector2(0f, -300f);
    /// <summary>Нижний ряд кнопок уходит ВНИЗ за экран (положительный Y → rest + (0,−300)).
    /// Раньше ряд уезжал вверх вместе с карточкой — будучи самым нижним элементом меню.</summary>
    private static readonly Vector2 ExitDownRow = new Vector2(0f, 300f);

    /// <summary>
    /// Старт (решение владельца): UI уходит плавно и РАЗНОНАПРАВЛЕННО —
    /// логотип и рекорд улетают ВВЕРХ за экран (slide-out, EaseInQuick), «TAP TO PLAY» —
    /// чистый fade-out. Панель НЕ гасится мгновенно (резкий уход), но сразу перестаёт
    /// ловить raycast (повторный BeginRun), а после завершения анимаций скрывается целиком.
    /// Каскад 70 мс: лого → рекорд (подпись+число) → карточка уровня + ряд кнопок → щит.
    /// Раньше уходили только лого и BEST — карточка, ряд кнопок и щит оставались висеть.
    /// </summary>
    public void PlayStartToGame()
    {
        CloseSettingsIfOpen(); // забег вперёд экрана настроек — экран уходит сам
        _screen = Screen.Hud;
        StopCtaPulse();
        StopTransitions();
        SetTreeVisible(false); // панель дерева не должна всплыть при возврате в меню
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
        if (ctaText != null)
            _transitions.Add(StartCoroutine(UiAnim.Fade(Cg(ctaText), Cg(ctaText).alpha, 0f, 0.25f, UiAnim.EaseInQuick, 0f, deactivateWhenHidden: true)));
        // Логотип ASTRO DRIFT: улетает вверх за экран, 0.30 s, 0 мс
        SlideOut(logoImage, ExitUpTitle, 0.30f, 0f);
        // Рекорд: подпись и число — вместе, 0.30 s, 70 мс
        SlideOut(startBest, ExitUpBest, 0.30f, 0.07f);
        SlideOut(startBestValue, ExitUpBest, 0.30f, 0.07f);
        // Карточка уровня + ряд кнопок + щит, 0.30 s, 140 мс.
        // Ряд кнопок — единственный, кто уходит ВНИЗ (он и стоит внизу): карточка и щит — вверх.
        SlideOut(levelCard, ExitUpCard, 0.30f, 0.14f);
        SlideOut(menuButtonsRow, ExitDownRow, 0.30f, 0.14f);
        // Щит анимируем только когда он реально показан (гейт уровня), иначе
        // SlideFade вернул бы его видимым на выходе из меню.
        if (IsShieldVisible) SlideOut(startShieldBtn, ExitUpCard, 0.30f, 0.14f);
        // Страховка: после завершения ухода панель скрыта целиком (alpha=0)
        _transitions.Add(StartCoroutine(HideStartPanelAfter(0.55f)));
    }

    /// <summary>Уход элемента стартовой группы (SlideFade out) в общий список переходов.
    /// §8: по завершении узел гасится целиком — невидимое в меню не остаётся живым.</summary>
    private void SlideOut(Object c, Vector2 offset, float dur, float delay)
    {
        var rt = Rt(c);
        if (rt == null) return;
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(c), rt, offset, false, dur, delay, UiAnim.EaseInQuick, deactivateWhenHidden: true)));
    }

    /// <summary>Щит меню сейчас виден (гейт уровня 8 выполнен — решает RefreshPilotBlock).</summary>
    private bool IsShieldVisible
    {
        get
        {
            var cg = startShieldBtn != null ? Cg(startShieldBtn) : null;
            return cg != null && cg.alpha > 0.5f;
        }
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

        // §8: панель могла быть погашена предыдущим уходом (PlayDeathOut/PlayContinueOut/
        // PlayPanelOut) — оживляем ДО текстов: LSE на неактивной ноде обновит строку
        // только после OnEnable, иначе Death-экран показал бы прошлые значения.
        SetVisible(deathPanel, true);
        var panelCg = Cg(deathPanel);
        panelCg.alpha = 1f;
        panelCg.blocksRaycasts = true;
        panelCg.interactable = true;

        // §2/§7: DeathScore и DeathBest — только ЧИСЛА (ключ best_value), подписи — статичные LSE префаба.
        // §10: при пустых Arguments TMP показал бы литерал {0} — поэтому Arguments ставим здесь же.
        SetLocalized(deathScore, "best_value", Format(score));
        SetLocalized(deathBest, "best_value", Format(best));
        if (newBest && AudioManager.Instance != null) AudioManager.Instance.PlayRecord();

        // Реклама не готова → блок скрыт целиком (§5: не disabled-серое),
        // каскад без него, таймер не запускается. 1 continue за забег (§3).
        bool adReady = AdsFlow.Instance != null && AdsFlow.Instance.IsRewardedReady;
        bool offerVisible = adReady && !_continueUsedThisRun;

        // §1: «одно золото в кадре». NEW BEST нет → золото получает CTA Continue;
        // есть → CTA белый (золотым становится число рекорда DeathBest).
        if (continueText != null)
            continueText.color = newBest ? Color.white : Palette.UiAccent;
        if (deathBest != null)
            deathBest.color = newBest ? Palette.Gold : Palette.ScoreText;

        // Каскад §3: бар ВСЕГДА стартует полным (страховка к StartOfferTimer).
        UiProgressBar.Set(continueTimerFill, 1f);

        // Узлы-участники держим ЖИВЫМИ с alpha 0 (PrepareForShow), а не SetVisible —
        // тот погасил бы их вместе с будущей анимацией.
        PrepareForShow(deathSkull);
        PrepareForShow(deathTitle);
        PrepareForShow(deathSubtitle);
        PrepareForShow(deathScorePanel);
        // Заголовок входит с задержкой 0.07 с, поэтому его узел (как и скулл) стартует заранее
        // и остаётся живым до конца каскада.
        deathSkull.gameObject.SetActive(true);
        if (offerVisible)
        {
            // §2: гасится/показывается весь блок ОДНИМ CanvasGroup корня Btn_Continue
            PrepareForShow(continueBtn);
            if (continueBtn != null) continueBtn.interactable = true;
        }
        else
        {
            SetVisible(continueBtn, false);
        }
        PrepareForShow(homeBtn);

        // Каскад §3: Skull 0.00 → Title 0.07 → Subtitle 0.12 → ScorePanel 0.19 → Continue 0.26 → Home 0.33
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(deathSkull.gameObject), deathSkull, SlideTitle, true, 0.35f, 0.00f, UiAnim.EaseOutSoft)));
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(deathTitle.gameObject), deathTitle, SlideTitle, true, 0.35f, 0.07f, UiAnim.EaseOutSoft)));
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(deathSubtitle.gameObject), deathSubtitle, SlideBest, true, 0.35f, 0.12f, UiAnim.EaseOutSoft)));
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(deathScorePanel.gameObject), deathScorePanel, SlideScore, true, 0.40f, 0.19f, UiAnim.EaseOutSoft)));
        if (offerVisible)
        {
            _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(continueBtn), Rt(continueBtn), SlideButton, true, 0.30f, 0.26f, UiAnim.EaseOutSoft)));
            _transitions.Add(StartCoroutine(StartOfferAfterCascade()));
        }
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(homeBtn), Rt(homeBtn), SlideButton, true, 0.30f, 0.33f, UiAnim.EaseOutSoft)));

        Analytics.Log("death_screen_shown", new Dictionary<string, object>
        {
            { "score", score },
            { "new_best", newBest },
            { "ad_ready", adReady },
        });
        _deathContinueAvailable = offerVisible;
    }

    /// <summary>Таймер стартует через 0.56 s после входа панели — после каскада §3
    /// (последний слайд: Home 0.33 + 0.30 длительность).</summary>
    private IEnumerator StartOfferAfterCascade()
    {
        yield return new WaitForSecondsRealtime(0.56f);
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

    /// <summary>HUD выключается сразу при смерти (§4.2). §8: скрытое — неактивно.
    /// Прежний FadeOut здесь анимировал уже нулевую альфу (SetVisible гасил её первым).</summary>
    public void HudOut()
    {
        SetVisible(hudRoot, false);
        if (pauseBtn != null) pauseBtn.SetActive(false);
    }

    // ——— §4.3 Retry: Death panel fade-out 0.25 s EaseInQuick, HUD fade-in 0.3 s на 0.45 s ———

    public void PlayDeathOut()
    {
        StopTransitions();
        _transitions.Add(StartCoroutine(FadeOut(deathPanel, 0.25f, 0f)));
    }

    /// <summary>HUD fade-in (§5: delay = startUnlockTime; §6.1: delay 0.45, dur 0.3).
    /// §8: вход начинается с активации — HUD гасится целиком на Death/в меню.</summary>
    public void HudIn(float delay, float dur)
    {
        CloseSettingsIfOpen(); // EnterMenu/новый забег: экран настроек не переживает смену состояния
        var cg = Cg(hudRoot);
        SetVisible(hudRoot, true); // SetActive(true): HUD мог быть погашен уходом
        cg.alpha = 0f;
        // Кнопка паузы живёт вместе с HUD (v2-регрессия: после SetActive(false)
        // в Setup она больше нигде не включалась — в геймплее паузы не было).
        if (pauseBtn != null) pauseBtn.SetActive(true);
        _transitions.Add(StartCoroutine(UiAnim.Fade(cg, 0f, 1f, dur, UiAnim.EaseOutSoft, delay)));
    }

    // ——— §4.4 Pause / Resume (unscaled; выход быстрее входа) ———

    public void PauseIn()
    {
        CloseSettingsIfOpen();
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
        // §8: уход доводится до неактивной панели (SetActive(false) ПОСЛЕ fade, не в середине)
        _transitions.Add(StartCoroutine(UiAnim.Fade(Cg(pausePanel), Cg(pausePanel).alpha, 0f, 0.22f, UiAnim.EaseInQuick, 0f, deactivateWhenHidden: true)));
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
        PrepareForShow(rt); // §8: узел оживает здесь — уход гасил его целиком
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
            _transitions.Add(StartCoroutine(UiAnim.Fade(hudCg, hudCg.alpha, 0f, 0.20f, UiAnim.EaseInQuick, 0f, deactivateWhenHidden: true)));
        }
    }

    /// <summary>
    /// Каскад стартового UI (возврат в меню): лого 150 мс → рекорд (подпись + число) 220 мс →
    /// карточка уровня + ряд кнопок 290 мс → щит + CTA 360 мс, по 0.35 s EaseOutSoft —
    /// ровно те же элементы и в том же порядке, что и уход в PlayStartToGame.
    /// Раньше возвращались только лого/BEST/CTA: карточка, ряд кнопок и щит оставались
    /// «уже пришедшими», и меню после старта выглядело иначе, чем до него.
    /// </summary>
    public void ShowStartCascade()
    {
        CloseSettingsIfOpen();
        _screen = Screen.Start;
        StopTransitions();
        ApplyAdaptiveStartLayout(force: true); // rest-позиции под текущий кадр ДО старта каскада
        SetVisible(startPanel, true); // §8: оживление ДО каскада (панель могла быть погашена)
        var startCg = Cg(startPanel);
        startCg.alpha = 1f;
        startCg.blocksRaycasts = true;
        SetTreeVisible(false); // панель «Дерево» всегда закрыта при входе в меню
        if (pauseBtn != null) pauseBtn.SetActive(false);
        // Фикс плейтеста: перечитываем XP/уровень пилота при возврате в меню (Home).
        // ВАЖНО: до каскада — RefreshPilotBlock решает, виден ли щит; иначе SlideFade
        // показал бы щит всегда, даже когда гейт уровня не выполнен.
        RefreshPilotBlock();
        _lastBestShown = -1; // §8: ноды меню были неактивны — число рекорда пишем заново
        ResetRestAll(); // страховка от прерванных на середине уходов (StopTransitions)

        SlideInEl(Rt(logoImage), SlideTitle, 0.35f, 0.15f);
        SlideInEl(Rt(startBest), SlideBest, 0.35f, 0.22f);
        SlideInEl(Rt(startBestValue), SlideBest, 0.35f, 0.22f);
        SlideInEl(Rt(levelCard), SlideButton, 0.35f, 0.29f);
        // Ряд кнопок возвращается ОТРАЖЕНИЕМ ухода: снизу вверх (SlideFade: from = rest − offset).
        // Так он приходит ровно с той стороны, куда ушёл, и садится точно на rest.
        SlideInEl(menuButtonsRow, ExitDownRow, 0.35f, 0.29f);
        if (IsShieldVisible) SlideInEl(Rt(startShieldBtn), SlideButton, 0.35f, 0.36f);
        SlideInEl(Rt(ctaText), SlideCta, 0.35f, 0.36f);
        StartCtaPulse();

        // §8: анимация мета-прогрессии — только если в забеге реально был грант XP.
        if (_menuProgressAnim != null) { StopCoroutine(_menuProgressAnim); _menuProgressAnim = null; }
        if (_pendingXpGain > 0) _menuProgressAnim = StartCoroutine(MenuProgressGainRoutine());
    }

    private IEnumerator FadeIn(GameObject go, float dur, float delay)
    {
        SetVisible(go, true);
        yield return UiAnim.Fade(Cg(go), 0f, 1f, dur, UiAnim.EaseOutSoft, delay);
    }

    /// <summary>Уход панели/HUD: по завершении объект гасится целиком (§8: скрытое — неактивно).</summary>
    private IEnumerator FadeOut(GameObject go, float dur, float delay)
    {
        yield return UiAnim.Fade(Cg(go), Cg(go).alpha, 0f, dur, UiAnim.EaseInQuick, delay, deactivateWhenHidden: true);
    }

    // ——— §8 (GDD_DeathScreen_v3): заглушечная мета-прогрессия на главном экране ———

    // Карточка уровня приходит на 0.29 s за 0.35 s → бар стартует на 0.64 s (§8a.2).
    private const float XpBarAnimStart = 0.64f;
    private const float XpBarAnimDur = 0.80f;
    private const float LevelCaptionHold = 1.6f;   // §8b.1: капшн level_up_line держится ~1.6 s
    private const float LevelPulseDur = 0.45f;
    private const float XpGainFadeIn = 0.15f;
    private const float XpGainHold = 0.60f;
    private const float XpGainFadeOut = 0.25f;

    /// <summary>
    /// §8: бар LevelCard принудительно встаёт на СТАРОЕ значение (RefreshPilotBlock уже показал
    /// новое) и доезжает old → new за 0.80 s EaseOutSoft; одновременно «+N XP» (fade in 0.15,
    /// hold 0.60, fade out 0.25 EaseInQuick, затем нода гасится). При level-up — после прохода
    /// бара капшн level_up_line с диапазоном уровней и пульс числа. Флаг гасится в начале:
    /// повторный показ меню анимацию не переигрывает.
    /// </summary>
    private IEnumerator MenuProgressGainRoutine()
    {
        var pilot = PilotProgressManager.Instance;
        int gain = _pendingXpGain;
        _pendingXpGain = 0;
        if (pilot == null || gain <= 0) yield break;

        bool leveled = pilot.PilotLevel > pilot.LevelBeforeLastRun;
        RectTransform fill = levelCard != null ? levelCard.BarFill : Rt(xpBarFill);
        float oldT = pilot.ProgressToNextLevelBeforeLastRun();
        float newT = pilot.ProgressToNextLevel();
        UiProgressBar.Set(fill, oldT);

        var gainCg = Cg(startXpGain);
        if (gainCg != null)
        {
            SetLocalized(startXpGain, "xp_gain", gain);
            UiAnim.SetVisible(gainCg, true);
            gainCg.alpha = 0f;
            _transitions.Add(StartCoroutine(UiAnim.Fade(gainCg, 0f, 1f, XpGainFadeIn, UiAnim.EaseOutSoft, XpBarAnimStart)));
            _transitions.Add(StartCoroutine(FadeXpGainOut()));
        }

        yield return new WaitForSecondsRealtime(XpBarAnimStart);
        float t = 0f;
        while (t < XpBarAnimDur)
        {
            t += Time.unscaledDeltaTime;
            float k = UiAnim.EaseOutSoft.Evaluate(Mathf.Clamp01(t / XpBarAnimDur));
            UiProgressBar.Set(fill, Mathf.Lerp(oldT, newT, k));
            yield return null;
        }
        UiProgressBar.Set(fill, newT);

        if (!leveled)
        {
            _menuProgressAnim = null;
            yield break;
        }

        // §8b.3: капшн и пульс стартуют ПОСЛЕ заливки бара (0.64 + 0.80 = 1.44 s от начала),
        // FIFO — после завершения твина. Отдельной константы нет: бар физически заканчивается
        // позже 0.85 s из §8b.2, поэтому отсчёт ведём от конца бара.
        SetLocalized(pilotLevelText, "level_up_line", pilot.LevelBeforeLastRun, pilot.PilotLevel);
        if (levelCard != null) levelCard.PulseLevelNumber(LevelPulseDur);
        yield return new WaitForSecondsRealtime(LevelCaptionHold);
        SetLocalized(pilotLevelText, "pilot_level_label");
        _menuProgressAnim = null;
    }

    /// <summary>§8a.3: «+N XP» — fade in уже запущен, здесь hold 0.60 s и уход 0.25 s с гашением ноды.</summary>
    private IEnumerator FadeXpGainOut()
    {
        yield return new WaitForSecondsRealtime(XpBarAnimStart + XpGainFadeIn + XpGainHold);
        var cg = Cg(startXpGain);
        if (cg != null) yield return UiAnim.Fade(cg, 1f, 0f, XpGainFadeOut, UiAnim.EaseInQuick, 0f, deactivateWhenHidden: true);
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
            UiAnim.SetVisible(Cg(comboChip.gameObject), show); // §8: скрытый чип неактивен
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
            UiAnim.SetVisible(Cg(perkBarRoot.gameObject), perkBarShow); // §8: скрытый бар неактивен
            if (perkBarShow) UiProgressBar.Set(Rt(perkProgressBarFill), perks.PerkProgress(score));
        }

        // Число рекорда — отдельная нода от подписи. Ключ best («РЕКОРД {0}») остался
        // за Death-экраном: на подписи меню он перезаписывал бы обе строки.
        RefreshBestValue(_score.Best);
    }

    /// <summary>best_value: Arguments + RefreshString. Гард режет и аллокацию массива,
    /// и пересборку строки на каждую смену счёта (счёт меняется чаще, чем рекорд).
    /// Смена локали сбрасывает гард — иначе значение внутри сессии не перерисовалось бы.</summary>
    private void RefreshBestValue(int best)
    {
        if (best == _lastBestShown) return;
        var lse = GetLse(startBestValueLse, startBestValue);
        if (lse == null) return;
        _lastBestShown = best;
        _bestArgs[0] = Format(best);
        lse.StringReference.Arguments = _bestArgs;
        lse.RefreshString();
    }

    /// <summary>Сброс гардов §3.4 при смене локали. Подписка на SelectedLocaleChanged
    /// ровно одна и живёт в LanguageService (§3.5) — здесь только вызываемый хук.</summary>
    public static void ResetLanguageGuards()
    {
        for (int i = 0; i < _instances.Count; i++)
        {
            var ui = _instances[i];
            if (ui == null) continue;
            ui._lastBestShown = -1;
            if (ui._score != null) ui.RefreshBestValue(ui._score.Best);
        }
    }

    /// <summary>Двухсостоятельная подпись щита: смена TableEntryReference (§8.1),
    /// а не прямая запись .text (§3.6).</summary>
    private void SetShieldCaption(string key)
    {
        if (_shieldCaptionKey == key) return; // уже это состояние — загрузку не дёргаем
        _shieldCaptionKey = key;
        ApplyEntry(GetLse(shieldCaptionLse, startShieldCaption), key, null);
    }

    /// <summary>Холодный путь: entry + Arguments (динамика §8.2).</summary>
    private void SetLocalized(TextMeshProUGUI tmp, string key, params object[] args)
        => ApplyEntry(GetLse(null, tmp), key, args);

    /// <summary>Компонентный текст на ноде, которую создаёт сам GameUI (§8.0).
    /// Слушатель OnUpdateString → tmp.text вешаем в рантайме: билдеры префабов делают
    /// это persistent-listener'ом, а нода, созданная кодом, обязана получить его сама —
    /// иначе строка загружается, но в TMP не пишется (наблюдение задачи 6 по Title).</summary>
    private static void AddLocalized(TextMeshProUGUI tmp, string key)
    {
        if (tmp == null) return;
        var lse = tmp.gameObject.AddComponent<LocalizeStringEvent>();
        var captured = tmp;
        lse.OnUpdateString.AddListener(v => captured.text = v);
        ApplyEntry(lse, key, null);
    }

    /// <summary>
    /// Присваивает таблицу/entry/аргументы и ПЕРЕЗАПУСКАЕТ загрузку.
    /// Простой RefreshString() здесь не годится: он молча выходит, если операции загрузки
    /// ещё нет (нода создана кодом → OnEnable отработал на пустой ссылке; либо entry
    /// сменился после загрузки). Присваивание StringReference даёт ClearChangeHandler +
    /// RegisterChangeHandler, а первый подписчик запускает ForceUpdate → HandleLocaleChange.
    /// Таблица уже в памяти → строка приходит в том же кадре.
    /// </summary>
    private static void ApplyEntry(LocalizeStringEvent lse, string key, object[] args)
    {
        if (lse == null || string.IsNullOrEmpty(key)) return;
        var ls = lse.StringReference;
        if (ls == null) return;
        ls.TableReference = "GameTexts";
        ls.TableEntryReference = key;
        ls.Arguments = args;
        lse.StringReference = ls;
    }

    /// <summary>LSE с ноды: сериализованная ссылка (если задана), иначе компонент рядом
    /// с существующей TMP-ссылкой.</summary>
    private static LocalizeStringEvent GetLse(LocalizeStringEvent cached, TextMeshProUGUI tmp)
    {
        if (cached != null) return cached;
        return tmp != null ? tmp.GetComponent<LocalizeStringEvent>() : null;
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
            GameManager.Instance.SetPausedBanner(true); // §4.5: пауза — не геймплей, баннер скрыт
            // ТЗ §2.4: один агрегат на оба ветвления — «какой % забегов прерывается паузой?»
            Analytics.Log("pause_toggled", new Dictionary<string, object> { { "action", "open" } });
        }
        else
        {
            TimeFreeze.Unfreeze();
            PauseOut();
            PlatformServices.Lifecycle.GameplayStart();
            GameManager.Instance.SetPausedBanner(false); // §4.5: баннер вернётся, если State == Playing
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
