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
    [SerializeField] private TextMeshProUGUI title1;
    [SerializeField] private TextMeshProUGUI title2;
    [SerializeField] private TextMeshProUGUI startBest;
    [SerializeField] private TextMeshProUGUI ctaText;
    [SerializeField] private Button tapToPlayBtn;     // полноэкранная невидимая зона тапа (ux4-5)

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

        // Поведение кнопок (структура — в сцене, обработчики — здесь)
        if (tapToPlayBtn != null) tapToPlayBtn.onClick.AddListener(() => GameManager.Instance.BeginRun());
        if (continueBtn != null) continueBtn.onClick.AddListener(OnContinueTapped);
        if (homeBtn != null) homeBtn.onClick.AddListener(HomeWithInterstitial);
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
        ShowStartImmediate();
        RefreshHud();
        StartCtaPulse();
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
        var ads = YandexAdsManager.Instance;
        if (ads != null && ads.TryShowInterstitial())
        {
            bool done = false;
            void OnClosed()
            {
                if (done) return;
                done = true;
                ads.InterstitialClosed -= OnClosed;
                if (offerWasAlive)
                    Analytics.Log("continue_declined", new Dictionary<string, object> { { "score", _score != null ? _score.Score : 0 } });
                GameManager.Instance.GoHome();
            }
            ads.InterstitialClosed += OnClosed;
        }
        else
        {
            if (offerWasAlive)
                Analytics.Log("continue_declined", new Dictionary<string, object> { { "score", _score != null ? _score.Score : 0 } });
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
        var ads = YandexAdsManager.Instance;
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

    /// <summary>Мгновенный вход в стартовый экран (только при инициализации сцены).</summary>
    public void ShowStartImmediate()
    {
        _screen = Screen.Start;
        StopTransitions();
        SetVisible(startPanel, true);
        SetVisible(deathPanel, false);
        SetVisible(pausePanel, false);
        SetVisible(hudRoot, false);
        if (pauseBtn != null) pauseBtn.SetActive(false);
        // Элементы — в позиции покоя
        ResetRest(title1); ResetRest(title2); ResetRest(startBest);
        ResetRest(deathScore); ResetRest(deathBest);
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

    private void StopTransitions()
    {
        foreach (var c in _transitions) if (c != null) StopCoroutine(c);
        _transitions.Clear();
    }

    // ——— §4.1 Старт (тап по CTA): UI-часть (камера — в GameManager/CameraDirector) ———

    public void PlayStartToGame()
    {
        _screen = Screen.Hud;
        StopCtaPulse();
        StopTransitions();
        // Фикс ux4-3: гасим ВСЮ стартовую панель сразу — её полноэкранное невидимое
        // Image оставался raycast-таргетом в gameplay и ловил тапы (повторный BeginRun).
        SetVisible(startPanel, false);
        SetVisible(hudRoot, false);
        // CTA: fade + slide −40 px, 0.25 s EaseInQuick, 0 мс
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(ctaText), ctaText.rectTransform, SlideCta, false, 0.25f, 0f, UiAnim.EaseInQuick)));
        // Заголовок + Best: fade + slide −60 px, 0.30 s EaseInQuick, 70 мс
        foreach (var t in new[] { title1, title2, startBest })
            _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(t), t.rectTransform, SlideTitle, false, 0.30f, 0.07f, UiAnim.EaseInQuick)));
    }

    // ——— §4.2 Смерть → Death panel (каскад, вход EaseOutSoft) ———

    public void PlayDeathIn(int score, int best, bool newBest)
    {
        _screen = Screen.Death;
        StopTransitions();
        StopOfferTimer();
        _offerActive = false;

        deathScore.text = "SCORE " + Format(score);
        deathBest.text = "BEST " + Format(best);
        deathNewBest.gameObject.SetActive(newBest);
        if (newBest && AudioManager.Instance != null) AudioManager.Instance.PlayRecord();

        SetVisible(deathPanel, true);
        var panelCg = Cg(deathPanel);
        panelCg.alpha = 1f;
        panelCg.blocksRaycasts = true;
        panelCg.interactable = true;

        // Реклама не готова → предложение скрыто целиком (§9: не disabled-серое),
        // каскад без него, таймер не запускается. 1 continue за забег (§3).
        bool adReady = YandexAdsManager.Instance != null && YandexAdsManager.Instance.IsRewardedReady;
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
    }

    /// <summary>Каскад стартового UI: заголовок 150 мс → Best 220 мс → CTA 300 мс, по 0.35 s EaseOutSoft.</summary>
    public void ShowStartCascade()
    {
        _screen = Screen.Start;
        StopTransitions();
        SetVisible(startPanel, true);
        var startCg = Cg(startPanel);
        startCg.alpha = 1f;
        startCg.blocksRaycasts = true;
        if (pauseBtn != null) pauseBtn.SetActive(false);
        foreach (var t in new[] { title1, title2, startBest, ctaText }) SetVisible(t, false);

        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(title1), title1.rectTransform, SlideTitle, true, 0.35f, 0.15f, UiAnim.EaseOutSoft)));
        _transitions.Add(StartCoroutine(UiAnim.SlideFade(Cg(title2), title2.rectTransform, SlideTitle, true, 0.35f, 0.15f, UiAnim.EaseOutSoft)));
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
        if (scoreText != null) scoreText.text = Format(_score.Score);

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

        if (startBest != null) startBest.text = "BEST " + Format(_score.Best);
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
        if (Time.timeScale > 0f)
        {
            Time.timeScale = 0f;
            PauseIn();
        }
        else
        {
            Time.timeScale = 1f;
            PauseOut();
        }
    }

    private void GoHomeFromPause()
    {
        Time.timeScale = 1f;
        GameManager.Instance.GoHome();
    }

    private static string Format(int v) => v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
}
