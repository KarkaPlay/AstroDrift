using System.Collections;
using UnityEngine;

/// <summary>
/// Канонические кривые и хелперы переходов «Menu & Transitions v2» (ArtDirection §4).
/// Всё движение UI — только по двум кривым; выход всегда быстрее входа;
/// всё в unscaled time (пауза не замораживает UI-переходы).
/// Техника — свои корутины (лёгкие, без внешних tween-библиотек: задача не тянет DOTween).
/// </summary>
public static class UiAnim
{
    /// <summary>
    /// EaseOutSoft = cubic-bezier(0.22, 0.61, 0.36, 1) — всё входящее:
    /// fade, slide, camera zoom. Ключи (0,0),(0.22,1),(1,1), плавный длинный хвост.
    /// </summary>
    public static readonly AnimationCurve EaseOutSoft = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2.77f),
        new Keyframe(0.22f, 1f, 2.77f, 0f),
        new Keyframe(1f, 1f, 0f, 0f));

    /// <summary>
    /// EaseInQuick ≈ cubic-bezier(0.55, 0, 1, 0.45) — всё исходящее:
    /// fade-out, slide-out. Крутой вход в конце, быстрый и короткий.
    /// </summary>
    public static readonly AnimationCurve EaseInQuick = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 1f, 3.5f, 0f));

    /// <summary>Каскад внутри панели (§4): сдвиг слоёв 60–90 мс.</summary>
    public const float CascadeStep = 0.07f;

    /// <summary>Период пульса CTA (текст + линия-индикатор), §3.</summary>
    public const float CtaPulsePeriod = 1.8f;

    /// <summary>Чистый fade CanvasGroup (unscaled). HUD появляется/уходит только так (§6.4).</summary>
    public static IEnumerator Fade(CanvasGroup cg, float from, float to, float dur,
                                   AnimationCurve curve, float delay = 0f)
    {
        if (cg == null) yield break;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        cg.blocksRaycasts = to > 0.5f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = curve.Evaluate(Mathf.Clamp01(t / dur));
            cg.alpha = Mathf.LerpUnclamped(from, to, k);
            yield return null;
        }
        cg.alpha = to;
        cg.blocksRaycasts = to > 0.5f;
    }

    /// <summary>
    /// Fade + вертикальный слайд элемента (§4). Приход: элемент стартует на slideOffset НИЖЕ
    /// места покоя и доезжает до него. Уход: уезжает на slideOffset вниз и гаснет,
    /// позиция возвращается в покой (следующий вход стартует корректно).
    /// </summary>
    public static IEnumerator SlideFade(CanvasGroup cg, RectTransform rt, Vector2 slideOffset,
                                        bool comingIn, float dur, float delay, AnimationCurve curve)
    {
        if (cg == null || rt == null) yield break;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        Vector2 rest = rt.anchoredPosition;
        Vector2 from = comingIn ? rest - slideOffset : rest;
        Vector2 to = comingIn ? rest : rest - slideOffset;
        float aFrom = comingIn ? 0f : 1f;
        float aTo = comingIn ? 1f : 0f;

        cg.blocksRaycasts = false;
        cg.interactable = false; // ux4-2: SetVisible(false) перед слайдом гасил interactable навсегда
        if (comingIn) cg.alpha = 0f;

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = curve.Evaluate(Mathf.Clamp01(t / dur));
            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
            cg.alpha = Mathf.LerpUnclamped(aFrom, aTo, k);
            yield return null;
        }
        rt.anchoredPosition = rest; // всегда возвращаем в покой
        cg.alpha = aTo;
        cg.blocksRaycasts = comingIn;
        cg.interactable = comingIn;
    }
}
