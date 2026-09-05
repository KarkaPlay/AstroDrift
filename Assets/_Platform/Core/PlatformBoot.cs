using System;

/// <summary>
/// Готовность платформенного слоя. По умолчанию — готов сразу (RuStore / редактор без define).
/// Инсталлер с асинхронной инициализацией (Яндекс Игры: SDK → сейвы → язык) вызывает
/// BeginAsyncInit() в BeforeSceneLoad и MarkReady() по готовности.
/// Bootstrap собирает сцену только после IsReady: ScoreManager читает Best из уже
/// загруженных сейвов, app_first_launch уходит в живую аналитику, локаль применена.
/// </summary>
public static class PlatformBoot
{
    public static bool IsReady { get; private set; } = true;
    public static event Action Ready;

    public static void BeginAsyncInit() => IsReady = false;

    public static void MarkReady()
    {
        if (IsReady) return;
        IsReady = true;
        Ready?.Invoke();
        Ready = null;
    }
}
