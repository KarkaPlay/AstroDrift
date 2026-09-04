using UnityEngine;

/// <summary>
/// Реализация ISaveService по умолчанию (редактор и RuStore): прямые обёртки PlayerPrefs.
/// Ключи остаются прежними — совместимость со старыми инсталлами.
/// </summary>
public class PlayerPrefsSaveService : ISaveService
{
    public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);

    public int GetInt(string key, int defaultValue = 0) => PlayerPrefs.GetInt(key, defaultValue);

    public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);

    public string GetString(string key, string defaultValue = "") => PlayerPrefs.GetString(key, defaultValue);

    public bool HasKey(string key) => PlayerPrefs.HasKey(key);

    public void Flush() => PlayerPrefs.Save();
}
