using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Оверлей перк-левелапа (GDD §15.3): затемнение 60% чёрного, «LEVEL UP!», 3 карты
/// (название + описание 1 строка + рамка по редкости), без таймера. Вся анимация —
/// unscaled time (фриз timeScale = 0). Тап по карте: перк применён, разморозка мгновенная.
/// Кнопка «Реролл за рекламу» ниже карт, discreet; лимит rerollPerRun = 1 за забег.
/// Оверлей строится кодом из AstroDriftSceneSetup (панель PerkPanel + PerkChoiceUI).
/// </summary>
public class PerkChoiceUI : MonoBehaviour
{
    private GameObject _panel;
    private RectTransform _cardsRoot;
    private TextMeshProUGUI _title;
    private Button _rerollBtn;
    private TextMeshProUGUI _rerollText;
    private TextMeshProUGUI _rerollCaption;

    private PerkDef[] _currentOffers;
    private readonly System.Collections.Generic.List<GameObject> _cards = new System.Collections.Generic.List<GameObject>();
    private Coroutine _showRoutine;
    private bool _rerollBound;

    private void Awake() => EnsureSceneRefs();

    /// <summary>
    /// Самоподключение к сценовым объектам. Init() вызывается только в редакторе
    /// (AstroDrift → Setup Scene UI), приватные поля не сериализуются — в Play Mode
    /// без этого метода _panel == null и Show() молча выходит (alpha остаётся 0).
    /// </summary>
    private void EnsureSceneRefs()
    {
        if (_panel != null) return;
        var panelGo = GameObject.Find("PerkPanel");
        if (panelGo == null)
        {
            Debug.LogError("PerkChoiceUI: PerkPanel не найден в сцене (AstroDrift → Setup Scene UI).");
            return;
        }
        _panel = panelGo;
        var cardsGo = panelGo.transform.Find("PerkCards");
        if (cardsGo != null) _cardsRoot = (RectTransform)cardsGo;
        var titleGo = panelGo.transform.Find("PerkTitle");
        if (titleGo != null) _title = titleGo.GetComponent<TextMeshProUGUI>();
        var rerollGo = panelGo.transform.Find("Btn_Reroll");
        if (rerollGo != null)
        {
            _rerollBtn = rerollGo.GetComponent<Button>();
            var rtGo = rerollGo.Find("RerollText");
            if (rtGo != null) _rerollText = rtGo.GetComponent<TextMeshProUGUI>();
            var rcGo = rerollGo.Find("RerollCaption");
            if (rcGo != null) _rerollCaption = rcGo.GetComponent<TextMeshProUGUI>();
        }
        if (_rerollBtn != null && !_rerollBound)
        {
            _rerollBtn.onClick.AddListener(OnRerollTapped);
            _rerollBound = true;
        }
        HideImmediate();
    }

    public void Init(GameObject panel, RectTransform cardsRoot, TextMeshProUGUI title,
                     Button rerollBtn, TextMeshProUGUI rerollText, TextMeshProUGUI rerollCaption)
    {
        _panel = panel;
        _cardsRoot = cardsRoot;
        _title = title;
        _rerollBtn = rerollBtn;
        _rerollText = rerollText;
        _rerollCaption = rerollCaption;

        if (_rerollBtn != null && !_rerollBound)
        {
            _rerollBtn.onClick.AddListener(OnRerollTapped);
            _rerollBound = true;
        }
        if (_title != null) L10n.Bind(_title, "levelup_title");
        if (_rerollText != null) L10n.Bind(_rerollText, "reroll_cta");
        if (_rerollCaption != null) L10n.Bind(_rerollCaption, "reroll_caption");
        HideImmediate();
    }

    private void OnRerollTapped()
    {
        var pm = PerkManager.Instance;
        var ads = AdsFlow.Instance;
        if (pm == null || ads == null || !pm.RerollAvailable) return;

        Analytics.Log("perk_reroll_ad_started");
        ads.ShowRewarded(ok =>
        {
            if (!ok)
            {
                Analytics.Log("perk_reroll_ad_aborted");
                return; // фриз сохраняется, карты не меняются
            }
            var offers = pm.Reroll();
            if (offers == null || offers.Length == 0) { Hide(); return; }
            _currentOffers = offers;
            RebuildCards();
            UpdateRerollVisibility();
        });
    }

    private void UpdateRerollVisibility()
    {
        bool show = PerkManager.Instance != null && PerkManager.Instance.RerollAvailable
                    && AdsFlow.Instance != null && AdsFlow.Instance.IsRewardedReady;
        SetVisible(_rerollBtn, show);
        SetVisible(_rerollText, show);
        SetVisible(_rerollCaption, show);
    }

    /// <summary>Показ оверлея с предложениями (фриз уже включён PerkManager). Анимация — unscaled.</summary>
    public void Show(PerkDef[] offers)
    {
        _currentOffers = offers;
        if (_panel == null) EnsureSceneRefs();
        if (_panel == null) return;
        SetVisible(_panel, true);
        var cg = Cg(_panel);
        cg.alpha = 0f;

        if (_title != null) { L10n.Bind(_title, "levelup_title"); SetVisible(_title, true); }
        RebuildCards();          // карты создаются сразу, stagger — внутри CardSlideIn (unscaled)
        UpdateRerollVisibility();

        if (_showRoutine != null) StopCoroutine(_showRoutine);
        _showRoutine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        var cg = Cg(_panel);
        // Затемнение до 60% чёрного (Palette.UiOverlayPerk), unscaled
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / 0.2f) * 0.6f;
            yield return null;
        }
        cg.alpha = 0.6f;
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
        float cardW = 300f, cardH = 420f, gap = 30f;
        float totalW = n * cardW + (n - 1) * gap;
        float startX = -totalW * 0.5f + cardW * 0.5f;

        for (int i = 0; i < n; i++)
        {
            var def = _currentOffers[i];
            var card = CreateCard(def, new Vector2(startX + i * (cardW + gap), 0f), cardW, cardH, i);
            _cards.Add(card);
            // Карты выезжают снизу со stagger 0.1 сек (unscaled, §15.3)
            StartCoroutine(CardSlideIn(card, i * 0.1f));
        }
    }

    private GameObject CreateCard(PerkDef def, Vector2 pos, float w, float h, int index)
    {
        var go = new GameObject("PerkCard_" + index, typeof(RectTransform));
        go.transform.SetParent(_cardsRoot, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(w, h);

        var img = go.AddComponent<Image>();
        img.color = Palette.UiPanel;
        // Рамка по редкости: обычный #AAAAAA, редкий #66CCFF (GDD §15.3 / §9)
        var outline = go.AddComponent<Outline>();
        outline.effectColor = def.rarity == Rarity.Rare ? Palette.PerkRare : Palette.PerkCommon;
        outline.effectDistance = new Vector2(3f, -3f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        // Иконка (слот 128×128, отступ сверху 28): спрайт перка либо плашка-заглушка
        Color rarityCol = def.rarity == Rarity.Rare ? Palette.PerkRare : Palette.PerkCommon;
        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(go.transform, false);
        var irt = (RectTransform)iconGo.transform;
        irt.anchorMin = new Vector2(0.5f, 1f); irt.anchorMax = new Vector2(0.5f, 1f); irt.pivot = new Vector2(0.5f, 1f);
        irt.anchoredPosition = new Vector2(0f, -28f);
        irt.sizeDelta = new Vector2(128f, 128f);
        var icon = iconGo.AddComponent<Image>();
        icon.raycastTarget = false;
        if (def.icon != null)
        {
            icon.sprite = def.icon;
            icon.color = Color.white;
        }
        else
        {
            // Заглушка «пустой слот»: тёмная плашка + контур цвета редкости (как рамка карты)
            icon.color = Palette.UiPanelFrame;
            var iconOutline = iconGo.AddComponent<Outline>();
            iconOutline.effectColor = rarityCol;
            iconOutline.effectDistance = new Vector2(2f, -2f);
        }

        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(go.transform, false);
        var trt = (RectTransform)titleGo.transform;
        trt.anchorMin = new Vector2(0.5f, 1f); trt.anchorMax = new Vector2(0.5f, 1f); trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -170f);
        // Высота 76 = две строки fontSize 32: RU-заголовки вроде «ПРОБИВАЮЩИЕ ПУЛИ» меряются 72.6 px
        // и при 60 px вылезали в описание (зазор до desc всего 6 px).
        trt.sizeDelta = new Vector2(w - 24f, 76f);
        var title = titleGo.AddComponent<TextMeshProUGUI>();
        title.fontSize = 32; title.color = Color.white;
        title.alignment = TextAlignmentOptions.Center;
        title.textWrappingMode = TextWrappingModes.Normal;
        string t = L10n.Get(def.titleKey);
        title.text = string.IsNullOrEmpty(t) ? def.id.ToString() : t;

        var descGo = new GameObject("Desc", typeof(RectTransform));
        descGo.transform.SetParent(go.transform, false);
        var drt = (RectTransform)descGo.transform;
        drt.anchorMin = new Vector2(0.5f, 1f); drt.anchorMax = new Vector2(0.5f, 1f); drt.pivot = new Vector2(0.5f, 1f);
        drt.anchoredPosition = new Vector2(0f, -252f);
        drt.sizeDelta = new Vector2(w - 24f, h - 268f);
        var desc = descGo.AddComponent<TextMeshProUGUI>();
        desc.fontSize = 24; desc.color = Palette.CardDescText;
        desc.alignment = TextAlignmentOptions.TopGeoAligned;
        desc.textWrappingMode = TextWrappingModes.Normal;
        string d = L10n.Get(def.descKey);
        desc.text = string.IsNullOrEmpty(d) ? "" : d;

        var captured = def;
        btn.onClick.AddListener(() => OnCardTapped(captured));
        return go;
    }

    private IEnumerator CardSlideIn(GameObject card, float delay)
    {
        var cg = Cg(card);
        var rt = (RectTransform)card.transform;
        Vector2 rest = rt.anchoredPosition;
        rt.anchoredPosition = rest - new Vector2(0f, 220f);
        cg.alpha = 0f;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            float k = UiAnim.EaseOutSoft.Evaluate(Mathf.Clamp01(t / 0.3f));
            rt.anchoredPosition = Vector2.LerpUnclamped(rest - new Vector2(0f, 220f), rest, k);
            cg.alpha = k;
            yield return null;
        }
        rt.anchoredPosition = rest;
        cg.alpha = 1f;
    }

    private void OnCardTapped(PerkDef def)
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

    private static string PerkTitle(PerkDef def)
    {
        string s = L10n.Get(def.titleKey);
        return string.IsNullOrEmpty(s) ? def.id.ToString() : s;
    }

    public void Hide()
    {
        if (_showRoutine != null) { StopCoroutine(_showRoutine); _showRoutine = null; }
        HideImmediate();
    }

    private void HideImmediate()
    {
        if (_panel == null) return;
        var cg = Cg(_panel);
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
        foreach (var c in _cards) if (c != null) Destroy(c);
        _cards.Clear();
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

    private static void SetVisible(Object c, bool visible)
    {
        var cg = Cg(c);
        if (cg == null) return;
        cg.alpha = visible ? 1f : 0f;
        cg.blocksRaycasts = visible;
        cg.interactable = visible;
    }
}
