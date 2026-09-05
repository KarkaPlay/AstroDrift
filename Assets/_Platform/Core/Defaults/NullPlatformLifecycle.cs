/// <summary>Заглушка жизненного цикла (RuStore / редактор): площадка ничего не требует.</summary>
public class NullPlatformLifecycle : IPlatformLifecycle
{
    public void GameReady() { }
    public void GameplayStart() { }
    public void GameplayStop() { }
}
