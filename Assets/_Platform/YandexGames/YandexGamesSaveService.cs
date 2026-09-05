#if STORE_YANDEX
using UnityEngine;
using YG;

/// <summary>
/// ISaveService поверх облачных сейвов YG2. Ключи те же, что на RuStore (AstroDrift.Best,
/// analytics_first_launch) — игровой код не меняется.
/// Debounce: ScoreManager дёргает Flush() на КАЖДОМ килле рекордного забега; на Android это
/// дешёвый PlayerPrefs.Save(), а setData Яндекса лимитирован по частоте. Пишем не чаще раза
/// в 3 с; немедленно — при потере фокуса вкладки (свернули / закрыли).
/// </summary>
public sealed class YandexGamesSaveService : ISaveService
{
    private const float FlushDebounce = 3f;
    private bool _dirty, _scheduled;
    private float _lastFlushRealtime = -999f;

    public YandexGamesSaveService()
    {
        YandexGamesRuntime.Ensure().FocusChanged += hasFocus => { if (!hasFocus) FlushNow(); };
    }

    public bool HasKey(string key) => Find(key) != null;

    public int GetInt(string key, int defaultValue = 0)
    {
        var e = Find(key);
        return e != null && !e.isString ? e.intValue : defaultValue;
    }

    public void SetInt(string key, int value)
    {
        var e = FindOrCreate(key);
        if (e == null) return;
        e.isString = false;
        e.intValue = value;
        _dirty = true;
    }

    public string GetString(string key, string defaultValue = "")
    {
        var e = Find(key);
        return e != null && e.isString ? e.strValue : defaultValue;
    }

    public void SetString(string key, string value)
    {
        var e = FindOrCreate(key);
        if (e == null) return;
        e.isString = true;
        e.strValue = value;
        _dirty = true;
    }

    public void Flush()
    {
        if (!_dirty) return;
        float since = Time.realtimeSinceStartup - _lastFlushRealtime;
        if (since >= FlushDebounce) { FlushNow(); return; }
        if (_scheduled) return;
        _scheduled = true;
        YandexGamesRuntime.Ensure().RunAfter(FlushDebounce - since, () => { _scheduled = false; FlushNow(); });
    }

    private void FlushNow()
    {
        if (!_dirty || YG2.saves == null) return;
        _dirty = false;
        _lastFlushRealtime = Time.realtimeSinceStartup;
        YG2.SaveProgress();
    }

    private static AstroSaveEntry Find(string key)
    {
        var list = YG2.saves?.astro;
        if (list == null) return null;
        for (int i = 0; i < list.Count; i++)
            if (list[i].key == key) return list[i];
        return null;
    }

    private static AstroSaveEntry FindOrCreate(string key)
    {
        var e = Find(key);
        if (e != null) return e;
        if (YG2.saves == null)
        {
            Debug.LogError("[YandexGames] Save до готовности SDK — Bootstrap должен ждать PlatformBoot.IsReady.");
            return null;
        }
        e = new AstroSaveEntry { key = key };
        YG2.saves.astro.Add(e);
        return e;
    }
}
#endif
