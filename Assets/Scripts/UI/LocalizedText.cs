using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Минимальный хелпер локализации UI (таблица GameTexts).
/// • L10n.Get(key) — синхронное чтение (если локаль ещё не готова — пусто, придёт обновление).
/// • L10n.GetFormatted(key, args) — Smart String с уже отформатированными числами.
/// • L10n.Bind(tmp, key, args) — ставит текст и перечитывает при смене локали/догрузке таблиц.
/// Переводы правятся владельцем в Assets/Localizations/GameTexts_en/ru.asset.
/// </summary>
public static class L10n
{
    private const string TableName = "GameTexts";
    private static bool _subscribed;

    public static string Get(string key)
    {
        var op = LocalizationSettings.StringDatabase.GetTableEntryAsync(TableName, key);
        if (!op.IsDone || op.Status != AsyncOperationStatus.Succeeded || op.Result.Entry == null)
            return null;
        return op.Result.Entry.GetLocalizedString();
    }

    public static string GetFormatted(string key, params object[] args)
    {
        string s = Get(key);
        if (s == null) return null;
        return args != null && args.Length > 0 ? string.Format(s, args) : s;
    }

    /// <summary>Ставит локализованный текст (форматирование чисел — до вызова) и
    /// регистрирует биндинг: если таблица ещё не готова (Get вернул null), текст
    /// обновится из RefreshAll при догрузке локали или при смене локали.
    /// Повторный Bind того же tmp+key не создаёт дубликат. Фолбэк при недоступной
    /// таблице — оставляет текст как есть.</summary>
    public static void Bind(TMPro.TextMeshProUGUI tmp, string key, params object[] args)
    {
        if (tmp == null) return;
        if (_bindings.Count > 0) PurgeDestroyed();
        Subscribe();
        Apply(tmp, key, args);
        for (int i = 0; i < _bindings.Count; i++)
        {
            if (_bindings[i].tmp == tmp && _bindings[i].key == key)
            {
                _bindings[i] = new Binding { tmp = tmp, key = key, args = args };
                return;
            }
        }
        _bindings.Add(new Binding { tmp = tmp, key = key, args = args });
    }

    private static bool Apply(TMPro.TextMeshProUGUI tmp, string key, object[] args)
    {
        string s = GetFormatted(key, args);
        if (string.IsNullOrEmpty(s)) return false;
        tmp.text = s;
        return true;
    }

    /// <summary>Убирает биндинги уничтоженных текстов (рестарт сцены) —
    /// статический список иначе растёт вечно.</summary>
    private static void PurgeDestroyed()
    {
        _bindings.RemoveAll(b => b.tmp == null);
    }

    private static void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;
        // Единая подписка на весь список биндингов (вместо лямбды на каждый Bind):
        // локаль сменилась — перечитываем все зарегистрированные тексты.
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        // Стартовый экран: локаль инициализируется асинхронно — обновляем все
        // привязанные тексты, когда таблицы догрузились.
        Application.onBeforeRender += RefreshAll;
    }

    private static void OnLocaleChanged(Locale _)
    {
        // Таблица новой локали грузится асинхронно: после смены локали снова
        // включаем RefreshAll, пока все строки не перечитаются.
        Application.onBeforeRender -= RefreshAll;
        Application.onBeforeRender += RefreshAll;
        RefreshAll();
    }

    private struct Binding
    {
        public TMPro.TextMeshProUGUI tmp;
        public string key;
        public object[] args;
    }

    private static readonly System.Collections.Generic.List<Binding> _bindings = new System.Collections.Generic.List<Binding>();

    private static void RefreshAll()
    {
        if (_bindings.Count == 0) return;
        PurgeDestroyed();
        if (_bindings.Count == 0)
        {
            Application.onBeforeRender -= RefreshAll;
            return;
        }
        // Применяем ВСЕМ каждый проход: таблица могла перезагрузиться (смена
        // локали), а строки, не готовые в прошлый проход, — появиться.
        bool allApplied = true;
        foreach (var b in _bindings)
        {
            string s = GetFormatted(b.key, b.args);
            if (!string.IsNullOrEmpty(s))
            {
                b.tmp.text = s;
            }
            else
            {
                allApplied = false;
            }
        }
        // Отписываемся только когда ВСЕ биндинги успешно применились в одном
        // проходе — иначе частично готовые тексты навсегда останутся без перевода.
        if (allApplied)
        {
            Application.onBeforeRender -= RefreshAll;
        }
    }
}
