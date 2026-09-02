using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Весь UI — теперь СЦЕНОВЫЕ объекты (Canvas/Hud/StartPanel/DeathPanel/PausePanel
/// видны и редактируются в иерархии; иерархия построена один раз, не в рантайме).
/// GameUI только навешивает поведение: обработчики кнопок, обновление текстов,
/// пульс чипа множителя, анимация панелей. Цвета/размеры — по VisualStyle §4.2/§7
/// (заданы на объектах сцены). Числа очков и Best — из ScoreManager (конфиги).
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

    [Header("Тексты")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI comboChip;
    [SerializeField] private TextMeshProUGUI startBest;
    [SerializeField] private TextMeshProUGUI deathScore;
    [SerializeField] private TextMeshProUGUI deathBest;
    [SerializeField] private TextMeshProUGUI tapToPlayText; // для пульса

    [Header("Кнопки")]
    [SerializeField] private Button tapToPlayBtn;
    [SerializeField] private Button retryBtn;
    [SerializeField] private Button homeBtn;
    [SerializeField] private Button pauseToggleBtn;
    [SerializeField] private Button resumeBtn;
    [SerializeField] private Button quitBtn;

    private ScoreManager _score;
    private int _lastMultiplier = 1; // для детекта РОСТА множителя (пульс чипа)
    private Screen _screen; // текущий экран (для видимости чипа множителя)

    public void Init(ScoreManager score)
    {
        _score = score;

        // Поведение кнопок (структура — в сцене, обработчики — здесь)
        if (tapToPlayBtn != null) tapToPlayBtn.onClick.AddListener(() => GameManager.Instance.BeginRun());
        if (retryBtn != null) retryBtn.onClick.AddListener(() => GameManager.Instance.Retry());
        if (homeBtn != null) homeBtn.onClick.AddListener(() => GameManager.Instance.GoHome());
        if (pauseToggleBtn != null) pauseToggleBtn.onClick.AddListener(TogglePause);
        if (resumeBtn != null) resumeBtn.onClick.AddListener(TogglePause);
        if (quitBtn != null) quitBtn.onClick.AddListener(GoHomeFromPause);

        _score.OnScoreChanged += (s, m) => { RefreshHud(); PulseScore(); };
        _score.OnComboReset += () => ShrinkCombo();

        SetScreen(Screen.Start);
        RefreshHud();
        StartCoroutine(Pulse(tapToPlayText, 1f, 1.05f, 0.8f));
    }

    private void OnDestroy()
    {
        if (_score != null)
        {
            _score.OnScoreChanged -= (s, m) => RefreshHud();
            _score.OnComboReset -= () => ShrinkCombo();
        }
    }

    // ——— Отображение ———

    public void SetScreen(Screen s)
    {
        _screen = s;
        bool hud = s == Screen.Hud || s == Screen.Pause;
        if (startPanel != null) startPanel.SetActive(s == Screen.Start);
        if (deathPanel != null) deathPanel.SetActive(s == Screen.Death);
        if (pausePanel != null) pausePanel.SetActive(s == Screen.Pause);
        if (pauseBtn != null) pauseBtn.SetActive(hud);
        if (hudRoot != null) hudRoot.SetActive(hud);
        // Множитель ТОЛЬКО возле очков (Доработка 5): чип виден при x2–x5 в HUD/паузе
        if (comboChip != null)
            comboChip.gameObject.SetActive(hud && _score != null && _score.Multiplier > 1);
    }

    public void ShowDeathScreen(int score, int best, bool newBest)
    {
        deathScore.text = "Score: " + Format(score);
        deathBest.text = "Best: " + Format(best) + (newBest ? "  NEW BEST!" : "");
        if (newBest && AudioManager.Instance != null) AudioManager.Instance.PlayRecord();
        SetScreen(Screen.Death);
        StartCoroutine(PanelIn(deathPanel));
    }

    public void RefreshHud()
    {
        if (_score == null) return;
        if (scoreText != null) scoreText.text = Format(_score.Score);

        int m = _score.Multiplier;
        if (comboChip != null)
        {
            // Множитель ТОЛЬКО возле очков (Доработка 5): чип при x2–x5, x1 скрыт (GDD §5).
            bool show = m > 1 && (_screen == Screen.Hud || _screen == Screen.Pause);
            comboChip.gameObject.SetActive(show);
            if (show)
            {
                comboChip.text = "x" + m;
                comboChip.color = Palette.ComboColor(m);
                if (m > _lastMultiplier) PulseCombo(); // пульс при росте множителя
            }
        }
        _lastMultiplier = m;

        // Best в стартовом меню читается из ScoreManager.Best (PlayerPrefs) — Доработка 4
        if (startBest != null) startBest.text = "Best: " + Format(_score.Best);
    }

    /// <summary>Пульс чипа множителя возле очков: scale 1→1.4→1 за 0.2 с (GDD §8).</summary>
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
        Vector3 from = Vector3.one;
        Vector3 to = Vector3.one * 1.4f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float ease = Mathf.Sin(k * Mathf.PI);
            rt.localScale = Vector3.Lerp(from, to, ease);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    /// <summary>Scale pop счёта при приросте: 1→1.12→1 за 0.15 с (синус-изинг, unscaled — работает в death-slowmo).</summary>
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
        Vector3 from = Vector3.one;
        Vector3 to = Vector3.one * 1.12f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float ease = Mathf.Sin(k * Mathf.PI);
            rt.localScale = Vector3.Lerp(from, to, ease);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    /// <summary>«Сдув» чипа при сбросе комбо (GDD §5): сжатие до 0.8, затем скрытие (m=1).</summary>
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
        Vector3 to = Vector3.one * 0.8f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            rt.localScale = Vector3.Lerp(from, to, k);
            yield return null;
        }
        rt.localScale = Vector3.one;
        RefreshHud(); // m=1 → чип скрывается, надпись сбрасывается на x1
    }

    private void TogglePause()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
        Time.timeScale = Time.timeScale > 0f ? 0f : 1f;
        SetScreen(Time.timeScale == 0f ? Screen.Pause : Screen.Hud);
    }

    private void GoHomeFromPause()
    {
        Time.timeScale = 1f;
        GameManager.Instance.GoHome();
    }

    // ——— Утилиты ———

    private static string Format(int v) => v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    private static IEnumerator Pulse(TextMeshProUGUI tmp, float from, float to, float duration)
    {
        if (tmp == null) yield break;
        while (true)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.PingPong(t / duration, 1f);
                float scale = Mathf.Lerp(from, to, k);
                tmp.transform.localScale = Vector3.one * scale;
                yield return null;
            }
        }
    }

    private static IEnumerator PanelIn(GameObject panel)
    {
        if (panel == null) yield break;
        var rt = panel.GetComponent<RectTransform>();
        rt.localScale = Vector3.one * 0.8f;
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.25f);
            rt.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, k);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }
}
