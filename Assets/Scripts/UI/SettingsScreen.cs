using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Экран настроек главного меню (открывается кнопкой «НАСТРОЙКИ»).
/// Владелец всей логики экрана: слайдеры каналов, каскад открытия/закрытия (UiAnim),
/// двухшаговое подтверждение сброса прогресса, обратный переход в меню.
///
/// Это ОТДЕЛЬНЫЙ экран, а не оверлей над меню: при входе все дети меню, КРОМЕ логотипа,
/// анимированно уезжают вниз и гаснут, при BACK — возвращаются. Логотип остаётся на месте
/// (см. keepVisibleName). Раскладка экрана целиком живёт в префабе SettingsPanel.prefab.
///
/// Громкости: слайдеры пишут ТОЛЬКО в рантайм-каналы AudioManager (SetSfxVolume/SetMusicVolume).
/// AudioConfig (masterVolume и per-sound volume) не переписывается никогда.
/// Скрытый экран — неактивен, alpha 0, raycasts off (§8 проекта).
/// </summary>
public class SettingsScreen : MonoBehaviour
{
    [Header("Корень экрана")]
    [Tooltip("GameObject экрана (он же — этот компонент). Гасится целиком при закрытии (§8).")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Слайдер звуков игры")]
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMPro.TextMeshProUGUI sfxValueText;

    [Header("Слайдер музыки")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private TMPro.TextMeshProUGUI musicValueText;

    [Header("Кнопки")]
    [SerializeField] private Button backBtn;
    [SerializeField] private Button resetBtn;
    [SerializeField] private TMPro.TextMeshProUGUI resetLabel; // нода с LSE ключа settings_reset_progress
    [SerializeField] private ResetConfirmation resetConfirmation;

    [Header("Каскад")]
    [Tooltip("Иконка настроек — первый слой каскада (SlideFade).")]
    [SerializeField] private RectTransform cascadeIcon;
    [SerializeField] private RectTransform cascadeTitle;
    [SerializeField] private RectTransform cascadeBox;
    [SerializeField] private RectTransform cascadeBack;

    /// <summary>Корень меню, возвращаемый по BACK (StartPanel). Ссылка на объект СЦЕНЫ —
    /// в префабе пуста, резолвится в Awake (FindMenuRoot) либо из GameUI.</summary>
    [SerializeField] private GameObject menuRoot;

    [Header("Переход «меню → экран»")]
    [Tooltip("Имя ребёнка меню, который НЕ уезжает. По ТЗ это логотип — он остаётся на месте.")]
    [SerializeField] private string keepVisibleName = "MenuLogo";
    [Tooltip("Куда уезжают элементы меню при входе в настройки.")]
    [SerializeField] private Vector2 menuSlide = new Vector2(0f, -140f);
    [Tooltip("Длительность ухода и возврата меню, сек (unscaled).")]
    [SerializeField] private float menuDur = 0.22f;
    [Tooltip("Каскадная задержка между элементами меню, сек.")]
    [SerializeField] private float menuStagger = 0.03f;
    [Tooltip("Пауза перед возвратом меню после BACK — экран настроек успевает уйти.")]
    [SerializeField] private float menuReturnDelay = 0.18f;

    /// <summary>Слой меню: CanvasGroup + RectTransform + позиция покоя.
    /// rest снимается ОДИН раз, на уходе, когда меню в покое — иначе адаптивная
    /// раскладка GameUI (идёт после Awake) сделала бы точку возврата протухшей.</summary>
    private sealed class MenuLayer
    {
        public CanvasGroup cg;
        public RectTransform rt;
        public Vector2 rest;
    }

    private sealed class TransitionLayer
    {
        public CanvasGroup group;
        public RectTransform rect;
        public Vector2 rest;
        public int order;
    }

    private sealed class Tween
    {
        public TransitionLayer layer;
        public bool panelLayer;
        public Vector2 fromPosition;
        public Vector2 toPosition;
        public float fromAlpha;
        public float toAlpha;
        public float delay;
        public float duration;
        public AnimationCurve curve;
    }

    private readonly List<MenuLayer> _menuLayers = new List<MenuLayer>();
    private Transform _menuAnimRoot;

    private const string MenuSceneName = "Menu";
    private const string ResetKey = "settings_reset_progress";

    private static readonly Vector2 SlideDown = new Vector2(0f, -60f);
    private const float Dur = 0.30f;

    private bool _open;
    private bool _localeSubscribed;
    private Coroutine _transition;
    private readonly List<TransitionLayer> _panelLayers = new List<TransitionLayer>();
    private bool _panelLayersCaptured;

    public bool IsOpen => _open;

    // ——— Настройка ссылок (вызывает билдер префаба и AstroDriftSceneSetup) ———

    public void Configure(CanvasGroup cg, Slider sfx, TMPro.TextMeshProUGUI sfxValue,
                          Slider music, TMPro.TextMeshProUGUI musicValue,
                          Button back, Button reset, TMPro.TextMeshProUGUI resetLabelText,
                          RectTransform icon, RectTransform title, RectTransform box, RectTransform backRt,
                          GameObject menu)
    {
        canvasGroup = cg != null ? cg : GetComponent<CanvasGroup>();
        sfxSlider = sfx; sfxValueText = sfxValue;
        musicSlider = music; musicValueText = musicValue;
        backBtn = back; resetBtn = reset; resetLabel = resetLabelText;
        cascadeIcon = icon; cascadeTitle = title; cascadeBox = box; cascadeBack = backRt;
        menuRoot = menu;
    }

    private void Awake()
    {
        // Идемпотентно: ссылки приходят сериализованными (инстанс префаба в сцене),
        // Configure остаётся для сборочных утилит.
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (menuRoot == null) menuRoot = FindMenuRoot();
        if (transform.parent != null && transform.parent.gameObject.name != "StartPanel")
            menuRoot = FindMenuRoot();
        _menuAnimRoot = menuRoot != null ? menuRoot.transform : null;
    }

    private void OnEnable()
    {
        if (backBtn != null) { backBtn.onClick.RemoveListener(Close); backBtn.onClick.AddListener(Close); }
        if (resetBtn != null) { resetBtn.onClick.RemoveListener(OnResetTapped); resetBtn.onClick.AddListener(OnResetTapped); }

        // dynamic-подписка: префаб сохраняет persistent-listener только у своих нод,
        // а Slider.onValueChanged в YAML префаба неудобен — вешаем в рантайме.
        if (sfxSlider != null) { sfxSlider.onValueChanged.RemoveListener(OnSfxChanged); sfxSlider.onValueChanged.AddListener(OnSfxChanged); }
        if (musicSlider != null) { musicSlider.onValueChanged.RemoveListener(OnMusicChanged); musicSlider.onValueChanged.AddListener(OnMusicChanged); }

        SubscribeLocale(true);
    }

    private void OnDisable()
    {
        if (backBtn != null) backBtn.onClick.RemoveListener(Close);
        if (resetBtn != null) resetBtn.onClick.RemoveListener(OnResetTapped);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
        SubscribeLocale(false);
    }

    private void SubscribeLocale(bool on)
    {
        if (on == _localeSubscribed) return;
        _localeSubscribed = on;
        if (on) LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        else LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(UnityEngine.Localization.Locale _) => RefreshResetLabel();

    // ——— Открытие / закрытие ———

    public void Show()
    {
        if (_open) return;
        _open = true;
        RefreshResetLabel();

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        CapturePanelLayers();
        UiAnim.EnsureActive(canvasGroup);
        BlockMenu(true);
        SyncSlidersFromAudio();
        StartTransition(true);
        Analytics.Log("settings_opened");
    }

    public void Close()
    {
        if (!_open) return;
        _open = false;
        StartTransition(false);
        Analytics.Log("settings_closed");
    }

    private void StartTransition(bool opening)
    {
        if (_transition != null)
        {
            StopCoroutine(_transition);
            _transition = null;
        }

        if (_menuAnimRoot == null)
        {
            if (menuRoot == null) menuRoot = FindMenuRoot();
            _menuAnimRoot = menuRoot != null ? menuRoot.transform : null;
        }

        if (opening)
        {
            CollectMenuLayers();
            CapturePanelLayers();
            PrepareLayersForOpening(_panelLayers);
            PrepareMenuForTransition();
        }
        else
        {
            CapturePanelLayers();
            CollectMenuLayers();
            for (int i = 0; i < _panelLayers.Count; i++)
            {
                var group = _panelLayers[i].group;
                if (group == null) continue;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        _transition = StartCoroutine(TransitionRoutine(opening));
    }

    private void CapturePanelLayers()
    {
        if (_panelLayersCaptured) return;
        _panelLayers.Clear();
        var rts = new[] { cascadeIcon, cascadeTitle, cascadeBox, cascadeBack };
        for (int i = 0; i < rts.Length; i++)
        {
            var rt = rts[i];
            if (rt == null) continue;
            var cg = Cg(rt);
            _panelLayers.Add(new TransitionLayer { group = cg, rect = rt, rest = rt.anchoredPosition, order = i });
        }
        _panelLayersCaptured = true;
    }

    private void PrepareLayersForOpening(List<TransitionLayer> layers)
    {
        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            if (layer.group == null || layer.rect == null) continue;
            bool wasInactive = !layer.group.gameObject.activeSelf;
            UiAnim.EnsureActive(layer.group);
            if (wasInactive)
            {
                layer.rect.anchoredPosition = layer.rest - SlideDown;
                layer.group.alpha = 0f;
            }
            layer.group.blocksRaycasts = false;
            layer.group.interactable = false;
        }
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
    }

    private void PrepareMenuForTransition()
    {
        for (int i = 0; i < _menuLayers.Count; i++)
        {
            var layer = _menuLayers[i];
            if (layer.cg == null || layer.rt == null) continue;
            UiAnim.EnsureActive(layer.cg);
            layer.cg.blocksRaycasts = false;
            layer.cg.interactable = false;
        }
    }

    private IEnumerator TransitionRoutine(bool opening)
    {
        var tweens = new List<Tween>();
        BuildPanelTweens(tweens, opening);
        BuildMenuTweens(tweens, opening);

        float elapsed = 0f;
        float totalDuration = 0f;
        for (int i = 0; i < tweens.Count; i++)
            totalDuration = Mathf.Max(totalDuration, tweens[i].delay + tweens[i].duration);

        while (elapsed < totalDuration)
        {
            ApplyTweens(tweens, elapsed);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        ApplyTweens(tweens, totalDuration);
        FinishTransition(opening);
        _transition = null;
    }

    private void BuildPanelTweens(List<Tween> tweens, bool opening)
    {
        var curve = opening ? UiAnim.EaseOutSoft : UiAnim.EaseInQuick;
        for (int i = 0; i < _panelLayers.Count; i++)
        {
            var layer = _panelLayers[i];
            if (layer.group == null || layer.rect == null) continue;
            Vector2 current = layer.rect.anchoredPosition;
            float alpha = layer.group.alpha;
            Vector2 targetPosition = opening ? layer.rest : layer.rest - SlideDown;
            float targetAlpha = opening ? 1f : 0f;
            Vector2 fromPosition = opening && Mathf.Approximately(alpha, 0f) ? layer.rest - SlideDown : current;
            float delay = opening ? layer.order * UiAnim.CascadeStep : (3 - layer.order) * UiAnim.CascadeStep;
            tweens.Add(new Tween
            {
                layer = layer,
                panelLayer = true,
                fromPosition = fromPosition,
                toPosition = targetPosition,
                fromAlpha = alpha,
                toAlpha = targetAlpha,
                delay = delay,
                duration = Dur,
                curve = curve
            });
        }
    }

    private void BuildMenuTweens(List<Tween> tweens, bool opening)
    {
        var curve = opening ? UiAnim.EaseInQuick : UiAnim.EaseOutSoft;
        float stagger = Mathf.Max(0f, menuStagger);
        for (int i = 0; i < _menuLayers.Count; i++)
        {
            var layer = _menuLayers[i];
            if (layer.cg == null || layer.rt == null) continue;
            if (!opening) UiAnim.EnsureActive(layer.cg);
            Vector2 current = layer.rt.anchoredPosition;
            float alpha = layer.cg.alpha;
            Vector2 targetPosition = opening ? layer.rest - menuSlide : layer.rest;
            Vector2 fromPosition = !opening && Mathf.Approximately(alpha, 0f) ? layer.rest - menuSlide : current;
            float targetAlpha = opening ? 0f : 1f;
            float delay = opening ? i * stagger : menuReturnDelay + (_menuLayers.Count - 1 - i) * stagger;
            tweens.Add(new Tween
            {
                layer = new TransitionLayer { group = layer.cg, rect = layer.rt, rest = layer.rest },
                panelLayer = false,
                fromPosition = fromPosition,
                toPosition = targetPosition,
                fromAlpha = alpha,
                toAlpha = targetAlpha,
                delay = delay,
                duration = Mathf.Max(0.01f, menuDur),
                curve = curve
            });
        }
    }

    private static void ApplyTweens(List<Tween> tweens, float elapsed)
    {
        for (int i = 0; i < tweens.Count; i++)
        {
            var tween = tweens[i];
            if (tween.layer.group == null || tween.layer.rect == null) continue;
            float t = elapsed - tween.delay;
            if (t <= 0f) continue;
            float k = tween.duration <= 0f ? 1f : Mathf.Clamp01(t / tween.duration);
            float eased = tween.curve != null ? tween.curve.Evaluate(k) : k;
            tween.layer.rect.anchoredPosition = Vector2.LerpUnclamped(tween.fromPosition, tween.toPosition, eased);
            tween.layer.group.alpha = Mathf.LerpUnclamped(tween.fromAlpha, tween.toAlpha, eased);
            if (tween.panelLayer && tween.toAlpha > 0f && k >= 1f)
            {
                tween.layer.group.blocksRaycasts = true;
                tween.layer.group.interactable = true;
            }
        }
    }

    private void FinishTransition(bool opening)
    {
        FinishLayers(_panelLayers, opening);
        for (int i = 0; i < _menuLayers.Count; i++)
        {
            var layer = _menuLayers[i];
            if (layer.cg == null || layer.rt == null) continue;
            layer.rt.anchoredPosition = layer.rest;
            UiAnim.SetVisible(layer.cg, !opening);
        }

        if (opening)
        {
            BlockMenu(true);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }
        }
        else
        {
            BlockMenu(false);
            UiAnim.SetVisible(canvasGroup, false);
        }
    }

    private static void FinishLayers(List<TransitionLayer> layers, bool visible)
    {
        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            if (layer.group == null || layer.rect == null) continue;
            layer.rect.anchoredPosition = layer.rest;
            UiAnim.SetVisible(layer.group, visible);
        }
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
    }

    private static CanvasGroup Cg(Component c)
    {
        if (c == null) return null;
        if (!c.gameObject.TryGetComponent<CanvasGroup>(out var cg)) cg = c.gameObject.AddComponent<CanvasGroup>();
        return cg;
    }

    /// <summary>
    /// Меню под экраном не должно ловить тапы (Btn_TapToPlay не стартует забег).
    ///
    /// ВАЖНО: гасить НАДО только raycast-приём, а не interactable. Родительский CanvasGroup
    /// с interactable = false Unity распространяет на ВСЕ вложенные Selectable — вместе с меню
    /// умирают и кнопки самого экрана настроек (проверено: Btn_Back / Btn_Reset / оба слайдера
    /// отдают IsInteractable() == false). Экран настроек стоит ВЫШЕ меню в иерархии, поэтому
    /// достаточно заблокировать приём рейкастов: тап не дойдёт до Btn_TapToPlay, а собственные
    /// контролы экрана остаются живыми.
    /// </summary>
    private void BlockMenu(bool blocked)
    {
        if (menuRoot == null) menuRoot = FindMenuRoot();
        if (menuRoot == null) return;
        var cg = menuRoot.GetComponent<CanvasGroup>();
        if (cg == null) cg = menuRoot.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = !blocked;
        // interactable НЕ трогаем — см. комментарий выше.
    }

    private GameObject FindMenuRoot()
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = FindInHierarchy(root.transform, "StartPanel");
            if (found != null) return found;
        }
        return null;
    }

    private static GameObject FindInHierarchy(Transform parent, string name)
    {
        if (parent == null) return null;
        foreach (var t in parent.GetComponentsInChildren<Transform>(true))
            if (t != parent && t.name == name) return t.gameObject;
        return null;
    }


    private void CollectMenuLayers()
    {
        if (_menuLayers.Count > 0) return;
        if (_menuAnimRoot == null) return;
        for (int i = 0; i < _menuAnimRoot.childCount; i++)
        {
            var child = _menuAnimRoot.GetChild(i) as RectTransform;
            if (child == null || child.name == keepVisibleName || !child.gameObject.activeSelf) continue;
            var cg = child.GetComponent<CanvasGroup>();
            if (cg == null) cg = child.gameObject.AddComponent<CanvasGroup>();
            _menuLayers.Add(new MenuLayer { cg = cg, rt = child, rest = child.anchoredPosition });
        }
    }

    // ——— Слайдеры ———

    private void SyncSlidersFromAudio()
    {
        var am = AudioManager.Instance;
        float sfx = am != null ? am.SfxVolume : 1f;
        float music = am != null ? am.MusicVolume : 1f;

        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f; sfxSlider.maxValue = 1f;
            sfxSlider.SetValueWithoutNotify(sfx);
        }
        if (musicSlider != null)
        {
            musicSlider.minValue = 0f; musicSlider.maxValue = 1f;
            musicSlider.SetValueWithoutNotify(music);
        }
        UpdateReadouts(sfx, music);
    }

    private void OnSfxChanged(float v)
    {
        AudioManager.Instance?.SetSfxVolume(v);
        UpdateReadouts(v, AudioManager.Instance != null ? AudioManager.Instance.MusicVolume : musicSlider != null ? musicSlider.value : 1f);
    }

    private void OnMusicChanged(float v)
    {
        AudioManager.Instance?.SetMusicVolume(v);
        UpdateReadouts(AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : sfxSlider != null ? sfxSlider.value : 1f, v);
    }

    private void UpdateReadouts(float sfx, float music)
    {
        if (sfxValueText != null) sfxValueText.text = Mathf.RoundToInt(sfx * 100f) + "%";
        if (musicValueText != null) musicValueText.text = Mathf.RoundToInt(music * 100f) + "%";
    }

    // ——— Подтверждение сброса прогресса ———

    private void OnResetTapped()
    {
        RefreshResetLabel();
        if (resetConfirmation != null) resetConfirmation.Show();
        else Debug.LogError("SettingsScreen: ResetConfirmation reference is missing.", this);
    }

    private void RefreshResetLabel() => ApplyResetLabelKey(ResetKey);

    /// <summary>Смена ключа на существующем LSE (образец GameUI.SetShieldCaption) —
    /// прямую запись .text не используем: строка должна следовать за локалью.</summary>
    private void ApplyResetLabelKey(string key)
    {
        if (resetLabel == null) return;
        var lse = resetLabel.GetComponent<LocalizeStringEvent>();
        if (lse == null) return;
        var ls = lse.StringReference;
        if (ls == null) return;
        ls.TableReference = "GameTexts";
        ls.TableEntryReference = key;
        ls.Arguments = null;
        lse.StringReference = ls;
    }
}
