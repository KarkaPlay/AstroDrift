// Единственный файл Core без единого #if: по умолчанию — безопасные заглушки.
// Платформенная реализация регистрируется инсталлером магазина (_Platform/RuStore/RuStoreInstaller,
// RuntimeInitializeOnLoadMethod BeforeSceneLoad) — к первому вызову PlatformServices.* всё зарегистрировано.
//
// Почему ленивые свойства, а не инициализаторы полей: NullAdsService — MonoBehaviour
// (нужна корутина для fake-показов), «new» ему нельзя — создаётся через GameObject
// лениво при первом обращении (ТЗ: «лениво или в статическом конструкторе»).
public static class PlatformServices
{
    private static IAdsService _ads;
    private static IAnalyticsService _analytics;
    private static ISaveService _save;

    public static IAdsService Ads => _ads ??= NullAdsService.Create();
    public static IAnalyticsService Analytics => _analytics ??= new NullAnalyticsService();
    public static ISaveService Save => _save ??= new PlayerPrefsSaveService();

    public static void Register(IAdsService service) => _ads = service;
    public static void Register(IAnalyticsService service) => _analytics = service;
    public static void Register(ISaveService service) => _save = service;
}
