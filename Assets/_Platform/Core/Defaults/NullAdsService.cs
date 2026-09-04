using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Заглушка рекламы (редактор / платформы без своей реализации) — точный порт
/// editor-режима бывшего YandexAdsManager:
/// • IsRewardedReady — всегда true (fake-режим);
/// • ShowRewarded — fake-показ: 2.0 с ожидания → награда;
/// • ShowInterstitial — «показан» мгновенно, InterstitialClosed — в следующем кадре
///   (порт FireInterstitialClosedNextFrame);
/// • баннер — Debug.Log.
/// Mute звука здесь НЕ трогается — это игровая логика AdsFlow.
/// </summary>
public class NullAdsService : MonoBehaviour, IAdsService
{
    private static NullAdsService _instance;

    /// <summary>
    /// Ленивое создание единственного экземпляра: скрытый DontDestroyOnLoad-объект
    /// (классу нужна корутина для fake-показов). Вызывается из PlatformServices.
    /// </summary>
    public static NullAdsService Create()
    {
        if (_instance != null) return _instance;

        var go = new GameObject("[Platform] AdsStub");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        _instance = go.AddComponent<NullAdsService>();
        return _instance;
    }

    public event Action InterstitialClosed;

    public bool IsRewardedReady => true;

    public bool ShowInterstitial()
    {
        Debug.Log("[Platform] EDITOR fake-показ interstitial — InterstitialClosed выстрелит в следующем кадре.");
        StartCoroutine(FireInterstitialClosedNextFrame());
        return true;
    }

    public void ShowRewarded(Action<bool> onResult)
    {
        if (onResult == null) return;
        Debug.Log("[Platform] EDITOR fake-показ rewarded: 2.0 с ожидания → onResult(true).");
        StartCoroutine(FakeRewardedRoutine(onResult));
    }

    public void ShowBanner() => Debug.Log("[Platform] Editor-заглушка: ShowBanner()");

    public void HideBanner() => Debug.Log("[Platform] Editor-заглушка: HideBanner()");

    private IEnumerator FireInterstitialClosedNextFrame()
    {
        yield return null;
        InterstitialClosed?.Invoke();
    }

    private IEnumerator FakeRewardedRoutine(Action<bool> onResult)
    {
        yield return new WaitForSecondsRealtime(2.0f);
        // Mute/unmute и запись метки тихого окна — в AdsFlow (игровая логика).
        Debug.Log("[Platform] EDITOR fake-rewarded завершён → onResult(true) (перезагрузка симулируется).");
        onResult(true);
    }
}
