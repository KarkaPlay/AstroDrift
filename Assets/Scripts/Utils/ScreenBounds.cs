using UnityEngine;

/// <summary>
/// Единая математика видимой области для ЛЮБОГО соотношения сторон
/// (портрет 9:16, квадрат 1:1, альбом 16:9, ТВ 16:9/21:9).
///
/// Проблема, которую решает: старые спавнеры считали дистанцию «за экраном»
/// как ortho + margin по ВСЕМ направлениям. Для ортографической камеры полуширина
/// кадра = ortho · aspect, поэтому на альбомных экранах (aspect > 1) точка
/// «ortho + margin» по горизонтали оказывалась ВНУТРИ кадра — астероиды, ракеты
/// и звёзды появлялись на глазах у игрока. StarField, кроме того, кэшировал
/// aspect один раз в Start() — после смены ориентации/разрешения звёзды
/// респавнились по старым границам.
///
/// Все величины считаются от текущего кадра камеры — вызовы дешёвые
/// (orthographicSize/aspect — кэшированные свойства), можно звать каждый кадр.
/// </summary>
public static class ScreenBounds
{
    /// <summary>Полувысота кадра ортокамеры в мировых юнитах (= orthographicSize).</summary>
    public static float HalfHeight(Camera cam)
    {
        return cam != null ? cam.orthographicSize : 12f;
    }

    /// <summary>Полуширина кадра ортокамеры = ortho · aspect (главный фикс: aspect учитывается каждый вызов).</summary>
    public static float HalfWidth(Camera cam)
    {
        return cam != null ? cam.orthographicSize * cam.aspect : 12f * 0.56f;
    }

    /// <summary>Скаляр, покрывающий кадр по ЛЮБОМУ направлению из центра (радиус описанной окружности кадра).</summary>
    public static float FrameRadius(Camera cam)
    {
        if (cam == null) return 12f;
        float h = cam.orthographicSize, w = cam.orthographicSize * cam.aspect;
        return Mathf.Sqrt(h * h + w * w);
    }

    /// <summary>
    /// Точка, гарантированно ЗА видимой рамкой в направлении dir от центра (или от origin).
    /// dir нормализуется; если dir нулевой — берётся вверх.
    /// Позиция = origin + d · (расстояние до рамки вдоль d) + d · margin, где расстояние
    /// до рамки считается ТОЧНО для прямоугольного кадра: min(hw/|dx|, hh/|dy|).
    /// (Оценка полуосью эллипса занижала расстояние на промежуточных углах при
    /// экстремальных аспектах 21:9 — точки попадали в кадр; проверено статтестом.)
    /// </summary>
    public static Vector2 PointOutside(Camera cam, Vector2 origin, Vector2 dir, float margin)
    {
        if (cam == null) return origin + dir.normalized * margin;
        Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.up;
        float hw = HalfWidth(cam), hh = HalfHeight(cam);
        float ax = Mathf.Abs(d.x) > 1e-6f ? hw / Mathf.Abs(d.x) : float.MaxValue;
        float ay = Mathf.Abs(d.y) > 1e-6f ? hh / Mathf.Abs(d.y) : float.MaxValue;
        float dist = Mathf.Min(ax, ay) + margin;
        return origin + d * dist;
    }

    /// <summary>Точка ВНЕ видимого прямоугольника с запасом margin хотя бы по одной оси (деспавн угроз).</summary>
    public static bool IsOutsideFrame(Camera cam, Vector2 pos, float margin)
    {
        if (cam == null) return false;
        float hw = HalfWidth(cam) + margin, hh = HalfHeight(cam) + margin;
        Vector2 c = cam.transform.position;
        return Mathf.Abs(pos.x - c.x) > hw || Mathf.Abs(pos.y - c.y) > hh;
    }

    /// <summary>Кламп точки внутрь видимого прямоугольника с отступом pad (индикаторы не уходят за кадр).</summary>
    public static Vector2 ClampToFrame(Camera cam, Vector2 pos, float pad)
    {
        if (cam == null) return pos;
        float hw = Mathf.Max(0f, HalfWidth(cam) - pad), hh = Mathf.Max(0f, HalfHeight(cam) - pad);
        Vector2 c = cam.transform.position;
        return new Vector2(Mathf.Clamp(pos.x, c.x - hw, c.x + hw), Mathf.Clamp(pos.y, c.y - hh, c.y + hh));
    }
}
