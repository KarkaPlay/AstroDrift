namespace YG
{
    using UnityEngine;

    public class YMA_MainThread : MonoBehaviour
    {
#if InterstitialAdv_yg
        public YMA_MainThread_InterAdv interAdv;
#endif
#if RewardedAdv_yg
        public YMA_RewardAdv_MainThread rewardAdv;
#endif
#if BannerAdv_yg
        public YMA_BannerAdv_MainThread bannerAdv;
#endif
        public static YMA_MainThread Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("UnityMainThread [YandexMobileAds integration YG2]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<YMA_MainThread>();
        }

        private void Awake()
        {
#if !InterstitialAdv_yg && !RewardedAdv_yg && !BannerAdv_yg
            YG2.SyncInitialization();
            return;
#endif
#if UNITY_EDITOR
            YG2.SyncInitialization();
            return;
#else
            try { RuntimeInitSDK(); }
            catch (System.Exception e) { Debug.LogError($"YandexMobileAds SDK initialization error: {e.Message}"); }

            YG2.SyncInitialization();
#endif
        }

        private void RuntimeInitSDK()
        {
#if InterstitialAdv_yg
            interAdv = gameObject.AddComponent<YMA_MainThread_InterAdv>();
            interAdv.Setup();
#endif
#if RewardedAdv_yg
            rewardAdv = gameObject.AddComponent<YMA_RewardAdv_MainThread>();
            rewardAdv.Setup();
#endif
#if BannerAdv_yg
            bannerAdv = gameObject.AddComponent<YMA_BannerAdv_MainThread>();
#endif
        }
    }
}