using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// Оверлей перк-левелапа (GDD §15.3): затемнение 50% чёрного, Star, «УРОВЕНЬ ПОВЫШЕН»,
/// до 3 карт (название + описание из PerkDefinition) и кнопка реролла (первый за забег
/// бесплатный, далее за рекламу — подпись переключается кодом по ключу).
/// Внешний вид живёт в префабах (AstroDrift → Build LevelUp Prefabs), здесь только
/// данные: иконка/ключи текстов карт, выбор префаба _New для первого стака перка.
/// Вся анимация — unscaled time (фриз timeScale = 0). Тап по карте: перк применён,
/// разморозка мгновенная. Лимит рероллов за забег: freeRerollsPerRun + rerollPerRun.
/// </summary>
public class PerkChoiceUI : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private RectTransform _cardsRoot;
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private Button _rerollBtn;
    [SerializeField] private TextMeshProUGUI _rerollText;
    [SerializeField] private TextMeshProUGUI _rerollCaption;
    [SerializeField] private GameObject _cardPrefab;
    [SerializeField] private GameObject _cardNewPrefab;

    private PerkDefinition[] _currentOffers;
    private readonly System.Collections.Generic.List<GameObject> _cards = new System.Collections.Generic.List<GameObject>();
    private Coroutine _showRoutine;
    private Coroutine _rerollRoutine;
    private LocalizedString _captionAd, _captionFree;
    private bool _rerollBound;
    private Coroutine _rerollBtnRoutine;
    private bool _rerollCardsSettled;

    private const float CardW = 300f, CardH = 420f, CardGap = 30f;
    private const string CaptionAdKey = "reroll_caption";
    private const string CaptionFreeKey = "reroll_caption_free";

    private void Awake()
    {
        EnsureSceneRefs();
        ResetCanvasGroupOnly();
    }

    /// <summary>
    /// Самоподключение к сценовым объектам (fallback, если ссылки не выставлены
    /// билдером). Панель — инстанс LevelUpPanel.prefab с именем PerkPanel.
    /// </summary>
    private void EnsureSceneRefs()
    {
        if (_panel == null)
        {
            // §8: скрытая панель неактивна → GameObject.Find её не видит (ищет только активные).
            var panelGo = GameObject.Find("PerkPanel") ?? FindInactive("PerkPanel");
            if (panelGo == null)
            {
                Debug.LogError("PerkChoiceUI: PerkPanel не найден в сцене (AstroDrift → Setup Scene UI).");
                return;
            }
            _panel = panelGo;
        }
        if (_cardsRoot == null)
        {
            var cardsGo = _panel.transform.Find("CardsRoot");
            if (cardsGo != null) _cardsRoot = (RectTransform)cardsGo;
        }
        if (_title == null)
        {
            var titleGo = _panel.transform.Find("LevelUpTitle");
            if (titleGo != null) _title = titleGo.GetComponent<TextMeshProUGUI>();
        }
        if (_rerollBtn == null)
        {
            var rerollGo = _panel.transform.Find("Btn_Reroll");
            if (rerollGo != null)
            {
                _rerollBtn = rerollGo.GetComponent<Button>();
                if (_rerollText == null) _rerollText = FindTmp(rerollGo, "RerollText");
                if (_rerollCaption == null) _rerollCaption = FindTmp(rerollGo, "RerollCaption");
            }
        }
        if (_cardPrefab == null) _cardPrefab = LoadPrefab("UpgradeCard");
        if (_cardNewPrefab == null) _cardNewPrefab = LoadPrefab("UpgradeCard_New");

        BindReroll();
    }

    /// <summary>Панель уже неактивна (префаб/сцена, §8) — Awake лишь обнуляет CanvasGroup.
    /// Вызывать HideImmediate отсюда нельзя: Awake приходит из PerkChoiceUI.Show
    /// (первая активация скрытой панели) и SetActive(false) убил бы корутину показа.</summary>
    private void ResetCanvasGroupOnly()
    {
        if (_panel == null) return;
        var cg = Cg(_panel);
        cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
    }

    /// <summary>Подключение ссылок из сцены (вызывает AstroDrift → Setup Scene UI).</summary>
    public void Init(GameObject panel, RectTransform cardsRoot, TextMeshProUGUI title,
                     Button rerollBtn, TextMeshProUGUI rerollText, TextMeshProUGUI rerollCaption,
                     GameObject cardPrefab, GameObject cardNewPrefab)
    {
        _panel = panel;
        _cardsRoot = cardsRoot;
        _title = title;
        _rerollBtn = rerollBtn;
        _rerollText = rerollText;
        _rerollCaption = rerollCaption;
        _cardPrefab = cardPrefab;
        _cardNewPrefab = cardNewPrefab;

        BindReroll();
        HideImmediate();
    }

    private void BindReroll()
    {
        if (_rerollBtn == null || _rerollBound) return;
        _rerollBtn.onClick.AddListener(OnRerollTapped);
        _rerollBound = true;
    }

    private void OnRerollTapped()
    {
        var pm = PerkManager.Instance;
        if (pm == null || _rerollRoutine != null || !pm.RerollAvailable) return;

        // Первый реролл за забег — бесплатно, без рекламы (GDD §15.3)
        if (pm.RerollIsFree)
        {
            Analytics.Log("perk_reroll_free_used");
            _rerollRoutine = StartCoroutine(RerollSwapRoutine(true));
            return;
        }

        var ads = AdsFlow.Instance;
        if (ads == null) return;
        Analytics.Log("perk_reroll_ad_started");
        ads.ShowRewarded(ok =>
        {
            if (!ok)
            {
                Analytics.Log("perk_reroll_ad_aborted");
                return; // фриз сохраняется, карты не меняются
            }
            if (_rerollRoutine == null) _rerollRoutine = StartCoroutine(RerollSwapRoutine(false));
        });
    }

    /// <summary>Смена карт после реролла (unscaled): каскадный уход карт И кнопки одним тактом
    /// → перегенерация из того же пула (§15.3) → въезд новых (CardSlideIn). Тап заблокирован
    /// до завершения свопа кнопки.</summary>
    private IEnumerator RerollSwapRoutine(bool free)
    {
        if (_rerollBtn != null) _rerollBtn.interactable = false;

        // (b) уход: scale 1→0.85 + fade 1→0, 0.12 s, stagger 0.04 s; кнопка уходит тем же тактом
        const float ExitDur = 0.12f, ExitStagger = 0.04f;
        for (int i = 0; i < _cards.Count; i++)
        {
            if (_cards[i] == null) continue;
            StartCoroutine(CardExit(_cards[i], i * ExitStagger));
        }
        _rerollCardsSettled = false;
        _rerollBtnRoutine = StartCoroutine(RerollButtonSwapRoutine());

        yield return new WaitForSecondsRealtime(ExitDur + Mathf.Max(0, _cards.Count - 1) * ExitStagger);

        // (c) перегенерация ПОСЛЕ ухода; пул не расширяется
        var pm = PerkManager.Instance;
        var offers = pm != null ? pm.Reroll() : null;
        if (offers == null || offers.Length == 0)
        {
            _rerollRoutine = null;
            Hide();
            yield break;
        }
        _currentOffers = offers;
        RebuildCards();

        // (d) карты легли — разрешаем вход новой кнопки (она сама решает: вход или гашение)
        _rerollCardsSettled = true;
        if (_rerollBtnRoutine != null) yield return _rerollBtnRoutine;
        _rerollBtnRoutine = null;
        _rerollRoutine = null;
    }

    /// <summary>Уход одной карты: сужение до 0.85 и fade в 0 (EaseInQuick). Карту может
    /// уничтожить RebuildCards, пока корутина ещё крутится — защита от MissingReference.</summary>
    private IEnumerator CardExit(GameObject card, float delay)
    {
        if (card == null) yield break;
        var cg = Cg(card);
        var rt = card.transform as RectTransform;
        if (rt == null) yield break;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        const float dur = 0.12f;
        float t = 0f;
        while (t < dur)
        {
            if (card == null) yield break;
            t += Time.unscaledDeltaTime;
            float k = UiAnim.EaseInQuick.Evaluate(Mathf.Clamp01(t / dur));
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.85f, k);
            if (cg != null) cg.alpha = 1f - k;
            yield return null;
        }
        if (card == null) yield break;
        rt.localScale = Vector3.one * 0.85f;
        if (cg != null) cg.alpha = 0f;
    }

    /// <summary>Мгновенный слой видимости кнопки (без анимации): Show() и восстановление
    /// после прерывания. Анимированная смена — RerollButtonSwapRoutine.</summary>
    private void ApplyRerollVisibilityImmediate()
    {
        if (_rerollBtn == null) return;
        ResetRerollButtonPose();
        bool show = ShouldShowReroll(out bool free);
        SetVisible(_rerollBtn.gameObject, show);
        if (show) SetRerollCaption(free ? CaptionFreeKey : CaptionAdKey);
    }

    /// <summary>true — кнопку показываем; free — подпись бесплатного реролла
    /// (бесплатный реролл не требует готовности рекламы).</summary>
    private static bool ShouldShowReroll(out bool free)
    {
        var pm = PerkManager.Instance;
        var ads = AdsFlow.Instance;
        free = pm != null && pm.RerollIsFree;
        return pm != null && pm.RerollAvailable && (free || (ads != null && ads.IsRewardedReady));
    }

    /// <summary>Возврат кнопки в исходную позу (scale 1, alpha 1, тап разрешён), чтобы
    /// прерванный на середине своп не оставил её в промежуточном состоянии.</summary>
    private void ResetRerollButtonPose()
    {
        if (_rerollBtn == null) return;
        _rerollBtn.interactable = true;
        var rt = _rerollBtn.transform as RectTransform;
        if (rt != null) rt.localScale = Vector3.one;
        var cg = Cg(_rerollBtn);
        if (cg != null) cg.alpha = 1f;
    }

    /// <summary>Анимированная смена кнопки реролла (unscaled): выход 1→0 / 1→0.9 (EaseInQuick)
    /// → подмена капшена в невидимой фазе → вход 0→1 / 0.9→1 (EaseOutSoft) после того, как
    /// карты легли. Рероллов не осталось — выход доигрывается, нода гаснет целиком.</summary>
    private IEnumerator RerollButtonSwapRoutine()
    {
        var cg = _rerollBtn != null ? Cg(_rerollBtn) : null;
        var rt = _rerollBtn != null ? _rerollBtn.transform as RectTransform : null;
        if (cg == null || rt == null) yield break;

        cg.blocksRaycasts = false;
        const float exitDur = 0.12f;
        float t = 0f;
        while (t < exitDur)
        {
            t += Time.unscaledDeltaTime;
            float k = UiAnim.EaseInQuick.Evaluate(Mathf.Clamp01(t / exitDur));
            cg.alpha = 1f - k;
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, k);
            yield return null;
        }
        cg.alpha = 0f;
        rt.localScale = Vector3.one * 0.9f;

        // Ждём, пока карты лягут: только тогда счётчик рероллов финален. Подмена ключа —
        // строго при alpha 0, поэтому старая формулировка не мигает.
        while (!_rerollCardsSettled) yield return null;

        if (!ShouldShowReroll(out bool free))
        {
            SetVisible(_rerollBtn.gameObject, false); // выход доигран — гасим ноду, не оставляя alpha 0 живой
            yield break;
        }
        SetRerollCaption(free ? CaptionFreeKey : CaptionAdKey);

        yield return new WaitForSecondsRealtime(0.15f); // вход — отдельный бит после карт
        const float inDur = 0.18f;
        t = 0f;
        while (t < inDur)
        {
            t += Time.unscaledDeltaTime;
            float k = UiAnim.EaseOutSoft.Evaluate(Mathf.Clamp01(t / inDur));
            cg.alpha = k;
            rt.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, k);
            yield return null;
        }
        cg.alpha = 1f;
        rt.localScale = Vector3.one;
        cg.blocksRaycasts = true;
        _rerollBtn.interactable = true;
    }

    /// <summary>Переключение подписи по ключу: присваивание StringReference —
    /// принятый в проекте способ перезапустить загрузку (см. FillCard).</summary>
    private void SetRerollCaption(string key)
    {
        if (_rerollCaption == null) return;
        var lse = _rerollCaption.GetComponent<LocalizeStringEvent>();
        if (lse == null) return;
        var ls = key == CaptionFreeKey ? _captionFree : _captionAd;
        if (ls == null)
        {
            ls = new LocalizedString();
            ls.SetReference("GameTexts", key);
            if (key == CaptionFreeKey) _captionFree = ls; else _captionAd = ls;
        }
        lse.StringReference = ls;
    }

    /// <summary>Показ оверлея с предложениями (фриз уже включён PerkManager). Анимация — unscaled.</summary>
    public void Show(PerkDefinition[] offers)
    {
        _currentOffers = offers;
        if (_panel == null) EnsureSceneRefs();
        if (_panel == null) return;
        // §8: панель скрыта целиком (SetActive(false)) — сначала оживляем, потом анимируем
        var cg = Cg(_panel);
        UiAnim.EnsureActive(cg);
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        RebuildCards();          // карты создаются сразу, stagger — внутри CardSlideIn (unscaled)
        ApplyRerollVisibilityImmediate();

        if (_showRoutine != null) StopCoroutine(_showRoutine);
        _showRoutine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        var cg = Cg(_panel);
        // Затемнение 50% уже в префабе — гасим/поднимаем только CanvasGroup, unscaled
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / 0.2f);
            yield return null;
        }
        cg.alpha = 1f;
        cg.blocksRaycasts = true;
        cg.interactable = true;
        _showRoutine = null;
    }

    private void RebuildCards()
    {
        foreach (var c in _cards) if (c != null) Destroy(c);
        _cards.Clear();
        if (_currentOffers == null || _cardsRoot == null) return;

        int n = _currentOffers.Length;
        float totalW = n * CardW + (n - 1) * CardGap;
        float startX = -totalW * 0.5f + CardW * 0.5f;

        for (int i = 0; i < n; i++)
        {
            var def = _currentOffers[i];
            // Первый раз за забег (стаков 0) — карта с бейджем «НОВОЕ»
            bool isNew = PerkManager.Instance != null && PerkManager.Instance.GetStacks(def.id) == 0;
            var prefab = isNew && _cardNewPrefab != null ? _cardNewPrefab : _cardPrefab;
            if (prefab == null)
            {
                Debug.LogError("PerkChoiceUI: префаб карты не назначен (AstroDrift → Build LevelUp Prefabs → Setup Scene UI).");
                return;
            }

            var card = Instantiate(prefab, _cardsRoot);
            card.name = (isNew ? "UpgradeCard_New_" : "UpgradeCard_") + i;
            var rt = (RectTransform)card.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(startX + i * (CardW + CardGap), 0f);
            rt.sizeDelta = new Vector2(CardW, CardH);

            FillCard(card, def);
            _cards.Add(card);
            // Карты выезжают снизу со stagger 0.1 сек (unscaled, §15.3)
            StartCoroutine(CardSlideIn(card, i * 0.1f));
        }
    }

    // Ключи — из данных перка. LSE на нодах — пустой (§8.1): владелец ноды (билдер карт)
    // создаёт компонент, а ссылку/ключ присваивает рантайм (§3.2). Владеет подпиской LSE
    // (StringChanged напрямую на LocalizedString — запрещён, §3.6).
    // Перезапуск загрузки — присваивание StringReference, простой RefreshString()
    // на только что созданной карте молча ничего не делает (операции ещё нет).
    private void FillCard(GameObject card, PerkDefinition def)
    {
        var title = FindTmp(card.transform, "Title");
        if (title != null)
        {
            var lse = title.GetComponent<LocalizeStringEvent>();
            if (lse != null) lse.StringReference = def.title;
        }

        var desc = FindTmp(card.transform, "Description");
        if (desc != null)
        {
            var lse = desc.GetComponent<LocalizeStringEvent>();
            if (lse != null) lse.StringReference = def.desc;
        }

        var iconTr = card.transform.Find("Icon");
        if (iconTr != null)
        {
            var icon = iconTr.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = def.icon;
                icon.color = def.icon != null ? Color.white : Palette.UiPanelFrame;
            }
        }

        var btn = card.GetComponent<Button>();
        if (btn != null)
        {
            var captured = def;
            btn.onClick.AddListener(() => OnCardTapped(captured));
        }
    }

    private IEnumerator CardSlideIn(GameObject card, float delay)
    {
        // Карту мог уничтожить следующий RebuildCards (повторный Show), пока
        // корутина ещё крутится, — иначе MissingReferenceException на RectTransform.
        if (card == null) yield break;
        var cg = Cg(card);
        var rt = card.transform as RectTransform;
        if (rt == null) yield break;
        Vector2 rest = rt.anchoredPosition;
        Vector2 from = rest - new Vector2(0f, 220f);
        rt.anchoredPosition = from;
        if (cg != null) cg.alpha = 0f;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        float t = 0f;
        while (t < 0.3f)
        {
            if (card == null) yield break;
            t += Time.unscaledDeltaTime;
            float k = UiAnim.EaseOutSoft.Evaluate(Mathf.Clamp01(t / 0.3f));
            rt.anchoredPosition = Vector2.LerpUnclamped(from, rest, k);
            if (cg != null) cg.alpha = k;
            yield return null;
        }
        if (card == null) yield break;
        rt.anchoredPosition = rest;
        if (cg != null) cg.alpha = 1f;
    }

    private void OnCardTapped(PerkDefinition def)
    {
        Analytics.Log("perk_chosen", new System.Collections.Generic.Dictionary<string, object>
        {
            { "perk", def.id.ToString() },
            { "stacks", PerkManager.Instance != null ? PerkManager.Instance.GetStacks(def.id) + 1 : 1 },
        });

        Hide();
        PerkManager.Instance?.Choose(def);
        FloatingTextPool.Instance?.Spawn(ShipController.Instance != null
                ? ShipController.Instance.transform.position + Vector3.up * 1.2f
                : Vector3.zero,
            PerkTitle(def), Palette.PerkRare, 4.2f, 1.0f);
        AudioManager.Instance?.PlayPerkLevelUp();
    }

    /// <summary>Синхронное чтение для флоатера (§3.3): пустая ссылка/неготовая таблица
    /// дают "" (N3), поэтому проверка — IsNullOrEmpty.</summary>
    private static string PerkTitle(PerkDefinition def)
    {
        string s = def.title.GetLocalizedString();
        return string.IsNullOrEmpty(s) ? def.id.ToString() : s;
    }

    public void Hide()
    {
        if (_showRoutine != null) { StopCoroutine(_showRoutine); _showRoutine = null; }
        // Прерванный своп не должен оставить кнопку навсегда неживой или на середине scale/alpha
        if (_rerollRoutine != null) { StopCoroutine(_rerollRoutine); _rerollRoutine = null; }
        if (_rerollBtnRoutine != null) { StopCoroutine(_rerollBtnRoutine); _rerollBtnRoutine = null; }
        _rerollCardsSettled = false;
        ResetRerollButtonPose();
        HideImmediate();
    }

    /// <summary>§8: скрытая панель неактивна (SetActive(false) + alpha 0 + raycasts off).</summary>
    private void HideImmediate()
    {
        if (_panel == null) return;
        SetVisible(_panel, false);
        foreach (var c in _cards) if (c != null) Destroy(c);
        _cards.Clear();
    }

    private static TextMeshProUGUI FindTmp(Transform parent, string name)
    {
        var t = parent.Find(name);
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    /// <summary>Поиск по имени в иерархии сцены, включая неактивные узлы (§8: скрытое = неактивное).</summary>
    private static GameObject FindInactive(string name)
    {
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var all = roots[i].GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
                if (t.name == name) return t.gameObject;
        }
        return null;
    }

    /// <summary>Fallback-загрузка префаба карты в редакторе: в билде ссылки всегда
    /// выставлены билдером (Setup Scene UI), поэтому AssetDatabase тут не нужен.</summary>
    private static GameObject LoadPrefab(string name)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LevelUp/" + name + ".prefab");
#else
        return null;
#endif
    }

    private static CanvasGroup Cg(Object c)
    {
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

    /// <summary>§8: скрытие гасит объект целиком (alpha 0 + raycasts/interactable off +
    /// SetActive(false)); показ — активация + alpha 1. Звать только когда анимации уже нет.</summary>
    private static void SetVisible(Object c, bool visible) => UiAnim.SetVisible(Cg(c), visible);
}
