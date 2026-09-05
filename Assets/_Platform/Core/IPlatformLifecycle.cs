/// <summary>
/// Жизненный цикл, который требует площадка. Яндекс Игры: GameReady (лоадер площадки
/// скрывается, игра интерактивна), GameplayStart/Stop (границы реального геймплея —
/// обязательны для модерации). На RuStore — no-op.
/// </summary>
public interface IPlatformLifecycle
{
    void GameReady();
    void GameplayStart();
    void GameplayStop();
}
