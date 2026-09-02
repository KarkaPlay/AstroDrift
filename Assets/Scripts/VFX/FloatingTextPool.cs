using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Всплывающий текст (COMBO! +200, +50, NEW BEST!) — пулируемые TMP-объекты.
/// Поднимается вверх и fade out (VisualStyle §4.2).
/// </summary>
public class FloatingTextPool : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private int prewarm = 8;

    private void Awake()
    {
        // Прогрев пула
        for (int i = 0; i < prewarm; i++)
        {
            var tmp = Create();
            tmp.gameObject.SetActive(false);
            _free.Push(tmp);
        }
    }

    private readonly Stack<TextMeshPro> _free = new Stack<TextMeshPro>();
    private readonly List<TextMeshPro> _active = new List<TextMeshPro>();

    public void Spawn(Vector3 worldPos, string text, Color color, float fontSize = 3.2f, float life = 0.8f)
    {
        TextMeshPro tmp = _free.Count > 0 ? _free.Pop() : Create();
        tmp.gameObject.SetActive(true);
        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = fontSize;
        tmp.transform.position = worldPos;
        _active.Add(tmp);
        StartCoroutine(Animate(tmp, life));
    }

    private TextMeshPro Create()
    {
        var go = new GameObject("FloatingText");
        go.transform.SetParent(transform);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.font = font;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 3.2f;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.sortingOrder = 20;
        return tmp;
    }

    private System.Collections.IEnumerator Animate(TextMeshPro tmp, float life)
    {
        float t = 0f;
        Vector3 start = tmp.transform.position;
        while (t < life)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / life);
            tmp.transform.position = start + Vector3.up * (k * 1.2f);
            Color c = tmp.color;
            c.a = 1f - k * k;
            tmp.color = c;
            float s = 1f + Mathf.Sin(Mathf.Min(k * 4f, 1f) * Mathf.PI) * 0.25f;
            tmp.transform.localScale = Vector3.one * s;
            yield return null;
        }
        tmp.gameObject.SetActive(false);
        _active.Remove(tmp);
        _free.Push(tmp);
    }
}
