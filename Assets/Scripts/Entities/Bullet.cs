using UnityEngine;

/// <summary>
/// Снаряд корабля (GDD §4.2). Кинематический, летит по прямой, деспавн по жизни.
/// </summary>
public class Bullet : Poolable
{
    private float _life;
    private Vector2 _dir;
    private float _speed;

    public void Spawn(Vector3 pos, Vector2 dir, float speed, float life)
    {
        transform.position = pos;
        // Пуля берётся из пула: без Clear() в трейле остаётся последняя точка
        // прошлой жизни, и при выстреле от позиции спавна тянется лишняя линия.
        // autodestruct обязан быть выключен: с ним TrailRenderer уничтожает ВСЁ
        // GameObject, как только очередь точек пустеет (а после Clear() она
        // пустеет мгновенно — пуля умирала на следующий кадр после выстрела).
        if (TryGetComponent<TrailRenderer>(out var trail))
        {
            trail.autodestruct = false;
            trail.Clear();
        }
        _dir = dir.normalized;
        _speed = speed;
        _life = life;
    }

    private void Update()
    {
        _life -= Time.deltaTime;
        if (_life <= 0f) { Release(); return; }
        transform.position += (Vector3)(_dir * _speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Компонентная проверка вместо тега (надёжнее: тег может потеряться).
        // «1 снаряд = 1 попадание»: пуля исчезает при ЛЮБОМ касании астероида или ракеты,
        // даже если цель не уничтожена (баг 5: пуля пролетала сквозь крупный астероид).
        var ast = other.GetComponentInParent<Asteroid>();
        if (ast != null)
        {
            ast.Hit(1, transform.position);
            Release();
            return;
        }
        var m = other.GetComponentInParent<Missile>();
        if (m != null)
        {
            m.Hit(1, transform.position);
            Release();
        }
    }
}
