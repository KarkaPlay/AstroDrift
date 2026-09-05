#if STORE_YANDEX
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Скрытый runner веб-слоя: корутины (watchdog рекламы, debounce сейвов) и фокус вкладки.
/// В WebGL OnApplicationPause ненадёжен, OnApplicationFocus приходит стабильно.
/// </summary>
public class YandexGamesRuntime : MonoBehaviour
{
    public static YandexGamesRuntime Instance { get; private set; }
    public event Action<bool> FocusChanged;

    public static YandexGamesRuntime Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("[Platform] YandexGames");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<YandexGamesRuntime>();
        return Instance;
    }

    public Coroutine RunAfter(float seconds, Action action) => StartCoroutine(After(seconds, action));

    private IEnumerator After(float seconds, Action action)
    {
        yield return new WaitForSecondsRealtime(seconds);
        action?.Invoke();
    }

    private void OnApplicationFocus(bool hasFocus) => FocusChanged?.Invoke(hasFocus);
}
#endif
