#if STORE_YANDEX
using YG;

/// <summary>
/// GameReady / GameplayAPI Яндекса. Дедуп: GameReady — один раз; Start/Stop — только при смене
/// состояния (площадка просит не слать одинаковые вызовы подряд; GoHome из Death иначе задвоил бы Stop).
/// </summary>
public sealed class YandexGamesLifecycle : IPlatformLifecycle
{
    private bool _ready, _playing;

    public void GameReady()
    {
        if (_ready) return;
        _ready = true;
        YG2.GameReadyAPI();
    }

    public void GameplayStart()
    {
        if (_playing) return;
        _playing = true;
        YG2.GameplayStart();
    }

    public void GameplayStop()
    {
        if (!_playing) return;
        _playing = false;
        YG2.GameplayStop();
    }
}
#endif
