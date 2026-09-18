using UnityEngine.Localization.Settings;

/// <summary>
/// Ридер таблицы GameTexts для рантайм-компонуемых строк (§3.3 ТЗ): там, где ноды нет
/// или строка собирается из многих ключей.
/// • L10n.Get(key) — синхронное чтение; неготовность таблицы/ключа = null.
/// • L10n.GetFormatted(key, args) — то же + string.Format уже готовыми аргументами.
/// Единый контракт проверки у вызывающих — string.IsNullOrEmpty (N3).
/// Переводы правятся владельцем в Assets/Localizations/GameTexts_en/ru.asset.
/// </summary>
public static class L10n
{
    private const string TableName = "GameTexts";

    /// <summary>Синхронное чтение (таблица из кэша/Addressables). Асинхронный
    /// GetTableEntryAsync в Play Mode не завершается, пока таблицу никто не
    /// запросил синхронно, поэтому читаем синхронно; неготовность = null,
    /// фолбэк строки — на стороне вызывающего.</summary>
    public static string Get(string key)
    {
        try
        {
            var table = LocalizationSettings.StringDatabase.GetTable(TableName);
            if (table == null) return null;
            var entry = table.GetEntry(key);
            if (entry == null) return null;
            string s = entry.GetLocalizedString();
            return string.IsNullOrEmpty(s) ? null : s;
        }
        catch (System.Exception)
        {
            return null; // локаль/таблица ещё не готова
        }
    }

    public static string GetFormatted(string key, params object[] args)
    {
        string s = Get(key);
        if (s == null) return null;
        return args != null && args.Length > 0 ? string.Format(s, args) : s;
    }
}
