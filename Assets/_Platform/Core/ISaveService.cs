/// <summary>
/// Игровой контракт сохранений (сейчас — PlayerPrefs; на Яндекс Играх — облако).
/// Через сервис мигрирует только best_score; внутренняя бухгалтерия подсистем
/// (счётчики рекламы AdsFlow, analytics_runs_total) остаётся на прямом PlayerPrefs.
/// </summary>
public interface ISaveService
{
    void SetInt(string key, int value);
    int GetInt(string key, int defaultValue = 0);
    void SetString(string key, string value);
    string GetString(string key, string defaultValue = "");
    bool HasKey(string key);
    /// <summary>Принудительная запись (для облачных сейвов Яндекс Игр).</summary>
    void Flush();
}
