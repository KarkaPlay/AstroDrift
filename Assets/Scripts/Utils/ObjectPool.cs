using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Универсальный Object Pool (DevTask правило 2). Пул работает с Poolable-компонентами:
/// Get() активирует, Release() деактивирует и прячет под родителя.
/// ReleaseAll() гасит ВСЕ активные объекты разом (рестарт забега) — без событий,
/// чтобы не спамить очки/juice.
/// </summary>
public class ObjectPool
{
    private readonly Stack<Poolable> _free = new Stack<Poolable>();
    private readonly HashSet<Poolable> _active = new HashSet<Poolable>();
    private readonly Func<Poolable> _create;
    private readonly Transform _parent;

    public ObjectPool(Func<Poolable> create, Transform parent, int prewarm = 0)
    {
        _create = create;
        _parent = parent;
        for (int i = 0; i < prewarm; i++)
        {
            Poolable item = create();
            item.Pool = this;
            item.gameObject.SetActive(false);
            if (_parent != null) item.transform.SetParent(_parent);
            _free.Push(item);
        }
    }

    public Poolable Get()
    {
        Poolable item = _free.Count > 0 ? _free.Pop() : _create();
        item.Pool = this;
        item.transform.SetParent(null);
        item.gameObject.SetActive(true);
        _active.Add(item);
        return item;
    }

    public void Release(Poolable item)
    {
        if (!_active.Remove(item)) return; // защита от double-release
        item.gameObject.SetActive(false);
        if (_parent != null) item.transform.SetParent(_parent);
        _free.Push(item);
    }

    /// <summary>
    /// Гасит все активные объекты: деактивирует, возвращает в _free, reparent'ит под _parent.
    /// Вызывает только Poolable.OnPoolReleaseAll (без GameEvents — очки/juice не нужны при рестарте).
    /// </summary>
    public void ReleaseAll()
    {
        // Копия: Release() модифицирует _active
        var snapshot = new List<Poolable>(_active);
        _active.Clear();
        foreach (var item in snapshot)
        {
            item.OnPoolReleaseAll();
            item.gameObject.SetActive(false);
            if (_parent != null) item.transform.SetParent(_parent);
            _free.Push(item);
        }
    }
}
