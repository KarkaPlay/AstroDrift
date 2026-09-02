using UnityEngine;

/// <summary>Маркер для объектов, живущих в пуле. Release() возвращает объект в пул.</summary>
public class Poolable : MonoBehaviour
{
    public ObjectPool Pool { get; set; }

    public void Release() => Pool?.Release(this);

    /// <summary>
    /// Хук для массового гашения (ObjectPool.ReleaseAll при рестарте): потомки сбрасывают
    /// своё служебное состояние (например, MissileWarning гасит своё предупреждение).
    /// Вызывается ПЕРЕД деактивацией, без GameEvents (очки/juice при рестарте не нужны).
    /// </summary>
    public virtual void OnPoolReleaseAll() { }
}
