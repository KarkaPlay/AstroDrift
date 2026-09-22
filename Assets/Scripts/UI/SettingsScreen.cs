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

    private readonly List<MenuLayer> _menuLayers = new List<MenuLayer>();
    private readonly List<Coroutine> _menuFades = new List<Coroutine>();
    private Coroutine _menuAnim;
    private Transform _menuAnimRoot;

    private const string MenuSceneName = "Menu";
    private const string ResetKey = "settings_reset_progress";
    private const string ResetConfirmKey = "settings_reset_confirm";

    private static readonly Vector2 SlideDown = new Vector2(0f, -60f);
    private const float Dur = 0.30f;

    private bool _open;
    private bool _resetArmed;              // первый тап по red-кнопке сделан — ждём второй
    private Coroutine _cascade;
    private readonly List<Coroutine> _layers = new List<Coroutine>();
    private bool _localeSubscribed;

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
        // _menuAnimRoot — объект СЦЕНЫ (StartPanel): префаб не может держать такую ссылку,
        // поэтому резолвим в рантайме. Список слоёв собирается на уходе (rest снимается в покое).
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

    /// <summary>Открыть экран каскадом (§4: иконка → заголовок → бокс, шаг 70 мс).</summary>
    public void Show()
    {
        if (_open) return;
        _open = true;
        _resetArmed = false;
        RefreshResetLabel();

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        StopCascade();
        RestoreLayersIdle(); // слои могли остаться погашенными уходом (§8) — возвращаем в покой
        UiAnim.SetVisible(canvasGroup, true); // оживление до анимаций (§8)
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        BlockMenu(true);
        SyncSlidersFromAudio();

        AnimateMenu(visible: false, leadIn: 0f); // меню уезжает, логотип остаётся на месте
        PlayCascade(comingIn: true);
        Analytics.Log("settings_opened");
    }

    /// <summary>Закрыть экран и вернуть меню в исходное состояние (BACK).</summary>
    public void Close()
    {
        if (!_open) return;
        _open = false;
        _resetArmed = false;

        // Финальную деактивацию экрана делает MenuRoutine: корутины этого компонента
        // умирают вместе с его SetActive(false), а до тех пор экран обязан быть жив.
        PlayCascade(comingIn: false);
        // Меню возвращается с задержкой — экран настроек успевает уйти; raycast-блок
        // снимается по завершении возврата, а не сразу (иначе тап ловит Btn_TapToPlay).
        AnimateMenu(visible: true, leadIn: menuReturnDelay);
        Analytics.Log("settings_closed");
    }

    private void PlayCascade(bool comingIn)
    {
        StopCascade();
        if (!gameObject.activeInHierarchy) { HideLayersImmediate(); UiAnim.SetVisible(canvasGroup, false); return; }
        _cascade = StartCoroutine(CascadeRoutine(comingIn));
    }

    /// <summary>Гасит каскад вместе со ВСЕМИ слоями. Быстрый BACK или повторный вход иначе
    /// оставляет висеть старые SlideFade — они дерутся за alpha и anchoredPosition новых.</summary>
    private void StopCascade()
    {
        if (_cascade != null) { StopCoroutine(_cascade); _cascade = null; }
        for (int i = 0; i < _layers.Count; i++)
            if (_layers[i] != null) StopCoroutine(_layers[i]);
        _layers.Clear();
    }

    private void HideLayersImmediate()
    {
        var rts = new[] { cascadeIcon, cascadeTitle, cascadeBox, cascadeBack };
        for (int i = 0; i < rts.Length; i++)
            if (rts[i] != null) UiAnim.SetVisible(Cg(rts[i]), false);
    }

    /// <summary>Слои каскада возвращаются в покой (включены, alpha 1). Мид-анимация не мешает:
    /// вход всегда идёт из one place — rest-позиции фиксированы в префабе.</summary>
    private void RestoreLayersIdle()
    {
        var rts = new[] { cascadeIcon, cascadeTitle, cascadeBox, cascadeBack };
        for (int i = 0; i < rts.Length; i++)
            if (rts[i] != null) UiAnim.SetVisible(Cg(rts[i]), true);
    }

    private IEnumerator CascadeRoutine(bool comingIn)
    {
        var curve = comingIn ? UiAnim.EaseOutSoft : UiAnim.EaseInQuick;
        var rts = new[] { cascadeIcon, cascadeTitle, cascadeBox, cascadeBack };
        for (int i = 0; i < rts.Length; i++)
        {
            var rt = rts[i];
            if (rt == null) continue;
            var cg = Cg(rt);
            if (comingIn) UiAnim.SetVisible(cg, true);
            _layers.Add(StartCoroutine(UiAnim.SlideFade(cg, rt, SlideDown, comingIn, Dur, 0f, curve,
                deactivateWhenHidden: !comingIn)));
            // каскад 70 мс между слоями — unscaled, не зависит от FPS
            yield return WaitUnscaled(UiAnim.CascadeStep);
        }
        // ждём последний слой, затем при уходе гасим экран целиком
        yield return WaitUnscaled(Dur);
        _layers.Clear();
        _cascade = null;
        if (!comingIn) UiAnim.SetVisible(canvasGroup, false);
    }

    /// <summary>Пауза на unscaled-времени: WaitForSecondsRealtime при редких кадрах
    /// (свёрнутый Editor) откладывает шаг каскада на неопределённый срок.</summary>
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

    // ——— Переход «меню → экран»: дети меню уезжают, логотип остаётся ———

    /// <summary>Собирает детей меню, кроме логотипа. Позиция покоя снимается ТОЛЬКО здесь —
    /// в момент ухода меню стоит в покое (адаптивная раскладка GameUI уже отработала).</summary>
    private void CollectMenuLayers()
    {
        _menuLayers.Clear();
        if (_menuAnimRoot == null) return;
        for (int i = 0; i < _menuAnimRoot.childCount; i++)
        {
            var child = _menuAnimRoot.GetChild(i) as RectTransform;
            if (child == null) continue;
            if (child.name == keepVisibleName) continue; // логотип не убирается (§ТЗ)
            // Только то, что реально на экране в покое. Иначе чужие спрятанные панели
            // (UnlockTreePanel/StartXpGain — их гасит и включает GameUI) вернулись бы
            // ВИДИМЫМИ на выходе из настроек: EnsureActive разбудил бы их заново.
            if (!child.gameObject.activeSelf) continue;
            var cg = child.GetComponent<CanvasGroup>();
            if (cg == null) cg = child.gameObject.AddComponent<CanvasGroup>();
            _menuLayers.Add(new MenuLayer { cg = cg, rt = child, rest = child.anchoredPosition });
        }
    }

    private void AnimateMenu(bool visible, float leadIn)
    {
        StopMenuAnim();
        if (_menuAnimRoot == null) _menuAnimRoot = menuRoot != null ? menuRoot.transform : null;
        if (!visible || _menuLayers.Count == 0) CollectMenuLayers();
        if (!gameObject.activeInHierarchy) { ApplyMenuImmediate(visible); return; }
        _menuAnim = StartCoroutine(MenuRoutine(visible, leadIn));
    }

    private IEnumerator MenuRoutine(bool visible, float leadIn)
    {
        var curve = visible ? UiAnim.EaseOutSoft : UiAnim.EaseInQuick;
        float dur = Mathf.Max(0.01f, menuDur);
        float stagger = Mathf.Max(0f, menuStagger);
        if (leadIn > 0f) yield return WaitUnscaled(leadIn);

        for (int i = 0; i < _menuLayers.Count; i++)
        {
            var layer = _menuLayers[i];
            if (layer.cg == null || layer.rt == null) continue;
            if (visible) UiAnim.EnsureActive(layer.cg);
            layer.rt.anchoredPosition = layer.rest; // покой до старта: прерванный уход не оставляет съезда
            // наружу — от нижних к верхним, назад — в обратном порядке: у логотипа чисто
            float delay = (visible ? _menuLayers.Count - 1 - i : i) * stagger;
            _menuFades.Add(StartCoroutine(UiAnim.SlideFade(layer.cg, layer.rt, menuSlide, visible,
                dur, delay, curve, deactivateWhenHidden: !visible)));
        }

        yield return WaitUnscaled(dur + stagger * Mathf.Max(0, _menuLayers.Count - 1) + 0.02f);
        _menuFades.Clear();
        _menuAnim = null;

        if (!visible) yield break;
        BlockMenu(false); // вернувшееся меню снова ловит тапы
        // Экран гасим последним (§8: скрытое — неактивно). Здесь это безопасно: деактивация
        // объекта = смерть его корутин, поэтому только в самом конце.
        UiAnim.SetVisible(canvasGroup, false);
    }

    /// <summary>Гасит переход меню и возвращает все слои в покой. Быстрый BACK/повторный вход
    /// иначе оставил бы висеть старые SlideFade — они дерутся за alpha и anchoredPosition.</summary>
    private void StopMenuAnim()
    {
        if (_menuAnim != null) { StopCoroutine(_menuAnim); _menuAnim = null; }
        for (int i = 0; i < _menuFades.Count; i++)
            if (_menuFades[i] != null) StopCoroutine(_menuFades[i]);
        _menuFades.Clear();
        for (int i = 0; i < _menuLayers.Count; i++)
            if (_menuLayers[i].rt != null) _menuLayers[i].rt.anchoredPosition = _menuLayers[i].rest;
    }

    /// <summary>Синхронный вариант на случай вызова вне иерархии (корутины не пойдут).</summary>
    private void ApplyMenuImmediate(bool visible)
    {
        for (int i = 0; i < _menuLayers.Count; i++)
        {
            var layer = _menuLayers[i];
            if (layer.cg == null || layer.rt == null) continue;
            layer.rt.anchoredPosition = layer.rest;
            UiAnim.SetVisible(layer.cg, visible);
        }
        if (visible) BlockMenu(false);
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

    // ——— Двухшаговый сброс прогресса (без нового попапа) ———

    private void OnResetTapped()
    {
        if (!_resetArmed)
        {
            _resetArmed = true;
            ApplyResetLabelKey(ResetConfirmKey); // «ТОЧНО СБРОСИТЬ?» — второй тап выполняет
            return;
        }

        PilotProgressManager.Instance?.ResetProgress();
        ScoreManager.Instance?.ResetProgress();
        _resetArmed = false;
        RefreshResetLabel();
        Analytics.Log("progress_reset");
    }

    private void RefreshResetLabel() => ApplyResetLabelKey(_resetArmed ? ResetConfirmKey : ResetKey);

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
