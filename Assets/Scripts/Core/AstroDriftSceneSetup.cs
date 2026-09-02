#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Редакторная утилита: строит инспектируемый UI сцены Game (Canvas + Hud +
/// StartPanel/DeathPanel/PausePanel + EventSystem + GameUI с ссылками).
/// Меню: AstroDrift → Setup Scene UI. Структура сохраняется в Game.unity —
/// дальше её можно править в иерархии, GameUI только навешивает поведение.
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
        scaler.referenceResolution = new Vector2(1080, 1920); // portrait (VisualStyle §4.2)
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

        // ——— HUD ———
        var hudGo = GameObject.Find("Hud");
        if (hudGo == null)
        {
            hudGo = NewPanel(canvas.transform, "Hud", new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(60f, -40f), new Vector2(1080, 200));
            var rt = hudGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            hudGo.GetComponent<Image>().enabled = false;
        }
        var scoreT = NewText(hudGo.transform, "ScoreText", "0", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), 56, scoreTextCol, TextAlignmentOptions.Center);
        scoreT.rectTransform.sizeDelta = new Vector2(600, 90);
        var comboT = NewText(hudGo.transform, "ComboChip", "x2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(420f, -68f), 28, Palette.ComboColor(2), TextAlignmentOptions.Left);
        comboT.gameObject.SetActive(false);

        var pauseBtnGo = GameObject.Find("Btn_Pause");
        if (pauseBtnGo == null)
        {
            pauseBtnGo = NewPanel(hudGo.transform, "Btn_Pause", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-70f, -70f), new Vector2(64f, 64f));
            pauseBtnGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
            var b = pauseBtnGo.AddComponent<Button>();
            b.targetGraphic = pauseBtnGo.GetComponent<Image>();
            var ptxt = NewText(pauseBtnGo.transform, "Text", "| |", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 30, Color.white, TextAlignmentOptions.Center);
            ptxt.rectTransform.sizeDelta = new Vector2(60, 60);
        }

        // ——— StartPanel ———
        var startGo = GameObject.Find("StartPanel");
        if (startGo == null)
        {
            startGo = NewPanel(canvas.transform, "StartPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 1920));
            NewText(startGo.transform, "Title1", "ASTRO", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 320), 88, scoreTextCol, TextAlignmentOptions.Center);
            NewText(startGo.transform, "Title2", "DRIFT", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 220), 88, scoreTextCol, TextAlignmentOptions.Center);
            NewText(startGo.transform, "StartBest", "Best: 0", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), 34, secondaryCol, TextAlignmentOptions.Center);
            var btnGo = NewPanel(startGo.transform, "Btn_TapToPlay", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(420, 96));
            btnGo.GetComponent<Image>().color = Color.white;
            var tb = btnGo.AddComponent<Button>();
            tb.targetGraphic = btnGo.GetComponent<Image>();
            var tt = NewText(btnGo.transform, "Text", "TAP TO PLAY", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 44, Palette.Hex("#0A0A0A"), TextAlignmentOptions.Center);
            tt.rectTransform.sizeDelta = new Vector2(420, 96);
        }
        var startBestT = startGo.transform.Find("StartBest").GetComponent<TextMeshProUGUI>();
        var tapBtn = startGo.transform.Find("Btn_TapToPlay").GetComponent<Button>();
        var tapText = startGo.transform.Find("Btn_TapToPlay/Text").GetComponent<TextMeshProUGUI>();

        // ——— DeathPanel ———
        var deathGo = GameObject.Find("DeathPanel");
        if (deathGo == null)
        {
            deathGo = NewPanel(canvas.transform, "DeathPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 900));
            deathGo.GetComponent<Image>().color = Palette.UiPanel;
            var outline = deathGo.AddComponent<Outline>();
            outline.effectColor = Palette.UiPanelFrame;
            outline.effectDistance = new Vector2(2, -2);
            NewText(deathGo.transform, "Title", "GAME OVER", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -80), 64, scoreTextCol, TextAlignmentOptions.Center);
            NewText(deathGo.transform, "DeathScore", "Score: 0", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -220), 36, scoreTextCol, TextAlignmentOptions.Center);
            NewText(deathGo.transform, "DeathBest", "Best: 0", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -300), 32, secondaryCol, TextAlignmentOptions.Center);
            var retry = NewPanel(deathGo.transform, "Btn_Retry", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -440), new Vector2(420, 96));
            retry.GetComponent<Image>().color = Color.white;
            retry.AddComponent<Button>().targetGraphic = retry.GetComponent<Image>();
            var rt1 = NewText(retry.transform, "Text", "RETRY", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 36, Palette.Hex("#0A0A0A"), TextAlignmentOptions.Center);
            rt1.rectTransform.sizeDelta = new Vector2(420, 96);
            var home = NewPanel(deathGo.transform, "Btn_Home", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -580), new Vector2(260, 64));
            home.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            home.AddComponent<Button>().targetGraphic = home.GetComponent<Image>();
            var ol = home.AddComponent<Outline>();
            ol.effectColor = Color.white;
            ol.effectDistance = new Vector2(1.5f, -1.5f);
            var rt2 = NewText(home.transform, "Text", "HOME", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 36, Color.white, TextAlignmentOptions.Center);
            rt2.rectTransform.sizeDelta = new Vector2(260, 64);
        }
        var deathScoreT = deathGo.transform.Find("DeathScore").GetComponent<TextMeshProUGUI>();
        var deathBestT = deathGo.transform.Find("DeathBest").GetComponent<TextMeshProUGUI>();
        var retryBtn = deathGo.transform.Find("Btn_Retry").GetComponent<Button>();
        var homeBtn = deathGo.transform.Find("Btn_Home").GetComponent<Button>();

        // ——— PausePanel ———
        var pauseGo = GameObject.Find("PausePanel");
        if (pauseGo == null)
        {
            pauseGo = NewPanel(canvas.transform, "PausePanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 1920));
            pauseGo.GetComponent<Image>().color = Palette.UiOverlay;
            var box = NewPanel(pauseGo.transform, "PauseBox", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 400));
            box.GetComponent<Image>().color = Palette.UiPanel;
            NewText(box.transform, "Title", "PAUSE", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -60), 48, scoreTextCol, TextAlignmentOptions.Center);
            var resume = NewPanel(box.transform, "Btn_Resume", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -180), new Vector2(360, 96));
            resume.GetComponent<Image>().color = Color.white;
            resume.AddComponent<Button>().targetGraphic = resume.GetComponent<Image>();
            var rt3 = NewText(resume.transform, "Text", "Продолжить", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 36, Palette.Hex("#0A0A0A"), TextAlignmentOptions.Center);
            rt3.rectTransform.sizeDelta = new Vector2(360, 96);
            var quit = NewPanel(box.transform, "Btn_Quit", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -300), new Vector2(260, 64));
            quit.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            quit.AddComponent<Button>().targetGraphic = quit.GetComponent<Image>();
            var ol2 = quit.AddComponent<Outline>();
            ol2.effectColor = Color.white;
            ol2.effectDistance = new Vector2(1.5f, -1.5f);
            var rt4 = NewText(quit.transform, "Text", "Выйти", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 36, Color.white, TextAlignmentOptions.Center);
            rt4.rectTransform.sizeDelta = new Vector2(260, 64);
        }
        var resumeBtn = pauseGo.transform.Find("PauseBox/Btn_Resume").GetComponent<Button>();
        var quitBtn = pauseGo.transform.Find("PauseBox/Btn_Quit").GetComponent<Button>();

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
        SetRef(so, "scoreText", scoreT, log);
        SetRef(so, "comboChip", comboT, log);
        SetRef(so, "startBest", startBestT, log);
        SetRef(so, "deathScore", deathScoreT, log);
        SetRef(so, "deathBest", deathBestT, log);
        SetRef(so, "tapToPlayText", tapText, log);
        SetRef(so, "tapToPlayBtn", tapBtn, log);
        SetRef(so, "retryBtn", retryBtn, log);
        SetRef(so, "homeBtn", homeBtn, log);
        SetRef(so, "pauseToggleBtn", pauseBtnGo.GetComponent<Button>(), log);
        SetRef(so, "resumeBtn", resumeBtn, log);
        SetRef(so, "quitBtn", quitBtn, log);
        so.ApplyModifiedPropertiesWithoutUndo();

        // Начальные состояния экранов (Start виден)
        startGo.SetActive(true);
        deathGo.SetActive(false);
        pauseGo.SetActive(false);
        hudGo.SetActive(false);
        pauseBtnGo.SetActive(false);

        EditorUtility.SetDirty(ui);
        MarkSceneDirty(scene);
        Debug.Log("AstroDrift SceneSetup: UI построен. " + log);
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
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }
}
#endif
