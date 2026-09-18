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

    /// <summary>Чистый fade CanvasGroup (unscaled). HUD появляется/уходит только так (§6.4).
    /// §8 (новая редакция): вход сам активирует объект, уход с deactivateWhenHidden доводится
    /// до «alpha=0 + raycasts/interactable off + SetActive(false)» — невидимое не остаётся живым.</summary>
    public static IEnumerator Fade(CanvasGroup cg, float from, float to, float dur,
                                   AnimationCurve curve, float delay = 0f, bool deactivateWhenHidden = false)
    {
        if (cg == null) yield break;
        if (to > 0.5f) EnsureActive(cg); // оживление ДО анимации: панель могла быть неактивна
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        if (cg == null) yield break;
        cg.blocksRaycasts = to > 0.5f;
        float t = 0f;
        while (t < dur)
        {
            if (cg == null) yield break;
            t += Time.unscaledDeltaTime;
            float k = curve.Evaluate(Mathf.Clamp01(t / dur));
            cg.alpha = Mathf.LerpUnclamped(from, to, k);
            yield return null;
        }
        cg.alpha = to;
        cg.blocksRaycasts = to > 0.5f;
        if (to > 0.5f) cg.interactable = true;                // вход: панель снова ловит тапы
        else if (deactivateWhenHidden) SetVisible(cg, false); // уход завершён — гасим целиком
    }

    /// <summary>
    /// Единственная точка видимости панели/элемента (§8 в новой редакции: скрытое — НЕАКТИВНО).
    /// Показ: SetActive(true) + alpha 1 + raycasts/interactable on.
    /// Скрытие: alpha 0 + raycasts/interactable off + SetActive(false).
    /// Звать только по завершении ухода: SetActive(false) внутри анимации убил бы корутину.
    /// </summary>
    public static void SetVisible(CanvasGroup cg, bool visible)
    {
        if (cg == null) return;
        if (visible)
        {
            EnsureActive(cg);
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;
        }
        else
        {
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
            cg.gameObject.SetActive(false);
        }
    }

    /// <summary>Активация перед входом: скрытая панель (§8) обязана ожить до старта анимации.</summary>
    public static void EnsureActive(CanvasGroup cg)
    {
        if (cg == null) return;
        if (!cg.gameObject.activeSelf) cg.gameObject.SetActive(true);
    }

    /// <summary>
    /// Fade + вертикальный слайд элемента (§4). Приход: элемент стартует на slideOffset НИЖЕ
    /// места покоя и доезжает до него. Уход: уезжает на slideOffset вниз и гаснет,
    /// позиция возвращается в покой (следующий вход стартует корректно).
    /// </summary>
    public static IEnumerator SlideFade(CanvasGroup cg, RectTransform rt, Vector2 slideOffset,
                                        bool comingIn, float dur, float delay, AnimationCurve curve,
                                        bool deactivateWhenHidden = false)
    {
        if (cg == null || rt == null) yield break;
        if (comingIn) EnsureActive(cg); // элемент мог быть погашен уходом (§8) — сначала оживляем
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        if (cg == null || rt == null) yield break;

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
            if (cg == null || rt == null) yield break;
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
        // Уход завершён → скрытое неактивно (§8). Внутри анимации деактивация запрещена.
        if (!comingIn && deactivateWhenHidden) SetVisible(cg, false);
    }
}
