#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

/// <summary>
/// Редакторная утилита: добавляет ключи локализации Волны 1 (RU + EN) в таблицу
/// GameTexts (ТЗ §0.10: каждая новая строка — ключи RU+EN сразу; EN-перевод черновой,
/// вычитка в v1.40). Меню: AstroDrift → Setup Localization.
/// </summary>
public static class AstroDriftLocalizationSetup
{
    private const string CollectionPath = "Assets/Localizations/GameTexts.asset";

    /// <summary>Ключи, чей текст в таблице ПРИНУДИТЕЛЬНО приводится к Entries при каждом
    /// прогоне (переименование строк ТЗ). Остальные ключи правки владельца сохраняют.
    /// continue_cta/continue_caption — значения изменены редизайном смерти (GDD_DeathScreen_v3 §4).</summary>
    private static readonly string[] ForceKeys = { "reroll_cta", "reroll_caption", "continue_cta", "continue_caption" };

    /// <summary>Ключи, удалённые редизайном смерти (§4): потребителей не осталось.
    /// Вычищаются из таблицы (Shared Data + обе локали) — иначе валидатор видит их сиротами.</summary>
    private static readonly string[] RetiredKeys = { "score", "new_best", "level_line", "unlocked_title" };

    // (ключ, RU, EN)
    private static readonly (string key, string ru, string en)[] Entries =
    {
        // Перк-левелап оверлей (GDD §15.3)
        ("levelup_title", "УРОВЕНЬ ПОВЫШЕН!", "LEVEL UP!"),
        ("levelup_choose", "ВЫБЕРИ УЛУЧШЕНИЕ", "CHOOSE AN UPGRADE"),
        ("levelup_new", "НОВОЕ", "NEW"),
        ("reroll_cta", "ОБНОВИТЬ ВЫБОР", "REFRESH CHOICES"),
        ("reroll_caption", "ПОСМОТРЕТЬ РЕКЛАМУ", "WATCH AD"),
        // Первый реролл за забег бесплатный — подпись кнопки переключается кодом (GDD §15.3)
        ("reroll_caption_free", "БЕСПЛАТНО — ПЕРВЫЙ РАЗ ЗА ЗАБЕГ", "FREE — FIRST TIME THIS RUN"),

        // Стартовый экран (GDD §11)
        // Рекорд меню разбит на две ноды: подпись (best_label) и число (best_value).
        // Ключ best («РЕКОРД {0}» / «BEST {0}») редизайном смерти потерял единственного
        // потребителя (Death-экран теперь берёт два числа через best_value), но в список
        // удаляемых §4 не входит — ключ остаётся как есть.
        ("best_label", "РЕКОРД", "BEST"),
        ("best_value", "{0}", "{0}"),
        ("pilot_level", "УРОВЕНЬ ПИЛОТА {0}", "PILOT LEVEL {0}"),
        // v1.10: подпись карточки уровня — БЕЗ параметра (число уровня отдельным нодом)
        ("pilot_level_label", "УРОВЕНЬ ПИЛОТА", "PILOT LEVEL"),
        ("shield_cta", "СТАРТОВЫЙ ЩИТ", "START SHIELD"),
        ("shield_caption", "ЗА ПРОСМОТР РЕКЛАМЫ · 1/ДЕНЬ", "WATCH AD · 1/DAY"),
        ("shield_used_today", "УЖЕ ИСПОЛЬЗОВАНА СЕГОДНЯ", "ALREADY USED TODAY"),

        // Death-экран (GDD_DeathScreen_v3 §2/§4)
        ("death_title", "ВЫ ПОГИБЛИ", "YOU DIED"),
        ("death_subtitle", "ВАШ ПУТЬ ЗАКОНЧЕН", "YOUR JOURNEY IS OVER"),
        ("score_label", "ТЕКУЩИЙ РЕЗУЛЬТАТ", "RUN SCORE"),
        ("home_to_menu", "В МЕНЮ", "MAIN MENU"),
        // Значения изменены редизайном (§4): только экран смерти. Ключ home НЕ трогаем — он шарится с PausePanel.
        ("continue_cta", "ПРОДОЛЖИТЬ ЗА РЕКЛАМУ", "CONTINUE FOR AD"),
        ("continue_caption", "Вернитесь в игру и сохраните свой прогресс", "Return to the game and keep your progress"),
        // Переезд на главный экран (§7): «+Y XP» над LevelCard, капшн уровня в LevelCard.
        ("xp_gain", "+{0} XP", "+{0} XP"),
        ("level_up_line", "УРОВЕНЬ {0} → {1}", "LEVEL {0} → {1}"),

        // Названия перков (GDD §15.2)
        ("perk_bullet_speed_title", "СКОРОСТЬ ПУЛЬ+", "BULLET SPEED+"),
        ("perk_bullet_speed_desc", "Скорость снарядов +20%", "Bullet speed +20%"),
        ("perk_fire_rate_title", "ТЕМП СТРЕЛЬБЫ+", "FIRE RATE+"),
        ("perk_fire_rate_desc", "Интервал стрельбы −15%", "Fire interval −15%"),
        ("perk_turn_speed_title", "ПОВОРОТЛИВОСТЬ+", "TURN SPEED+"),
        ("perk_turn_speed_desc", "Скорость поворота +25%", "Turn speed +25%"),
        ("perk_combo_ext_title", "КОМБО+", "COMBO EXTENSION"),
        ("perk_combo_ext_desc", "Окно комбо +2 сек", "Combo window +2 sec"),
        ("perk_bigger_bullets_title", "КРУПНЫЕ ПУЛИ", "BIGGER BULLETS"),
        ("perk_bigger_bullets_desc", "Радиус пуль +50%", "Bullet radius +50%"),
        ("perk_score_mult_title", "ОЧКИ+", "SCORE MULTIPLIER+"),
        ("perk_score_mult_desc", "Очки за убийство +25%", "Kill score +25%"),
        ("perk_missile_jammer_title", "ГЛУШИТЕЛЬ РАКЕТ", "MISSILE JAMMER"),
        ("perk_missile_jammer_desc", "Скорость поворота ракет −40%", "Missile turn rate −40%"),
        ("perk_piercing_title", "ПРОБИВАЮЩИЕ ПУЛИ", "PIERCING SHOTS"),
        ("perk_piercing_desc", "Снаряд пробивает +1 врага", "Bullet pierces +1 enemy"),

        // Названия пикапов (GDD §4.6)
        ("pickup_rapid_fire", "УСКОРЕНИЕ ОГНЯ", "RAPID FIRE"),
        ("pickup_spread_shot", "ВЕЕРНЫЙ ВЫСТРЕЛ", "SPREAD SHOT"),
        ("pickup_shield", "ЩИТ", "SHIELD"),

        // Баннер «Разблокировано» (id из дерева §5bis.2, Wave1-only)
        ("unlock_BulletSpeed", "Перк «Скорость пуль+»", "Perk: Bullet Speed+"),
        ("unlock_FireRate", "Перк «Темп стрельбы+»", "Perk: Fire Rate+"),
        ("unlock_TurnSpeed", "Перк «Поворотливость+»", "Perk: Turn Speed+"),
        ("unlock_ComboExtension", "Перк «Комбо+»", "Perk: Combo Extension"),
        ("unlock_SpreadShot", "Пикап «Веерный выстрел»", "Pickup: Spread Shot"),
        ("unlock_BiggerBullets", "Перк «Крупные пули»", "Perk: Bigger Bullets"),
        ("unlock_ScoreMultiplier", "Перк «Очки+»", "Perk: Score Multiplier+"),
        ("unlock_MissileJammer", "Перк «Глушитель ракет»", "Perk: Missile Jammer"),
        ("unlock_Shield", "Пикап «Щит»", "Pickup: Shield"),
        ("unlock_Piercing", "Перк «Пробивающие пули»", "Perk: Piercing Shots"),
        ("unlock_RapidFire", "Пикап «Ускорение огня»", "Pickup: Rapid Fire"),

        // Дерево разблокировок — панель стартового экрана (GDD §5bis).
        // Ключ unlock_tree_open удалён вместе с кнопкой-заглушкой «ДЕРЕВО»:
        // вход в панель теперь кнопка «ПРОКАЧКА» (ключ menu_upgrade).
        ("unlock_tree_title", "РАЗБЛОКИРОВКИ ПИЛОТА", "PILOT UNLOCKS"),
        ("unlock_soon", "скоро", "soon"),

        // v1.10: нижние кнопки меню. «НАСТРОЙКИ» открывает экран настроек,
        // «ПРОКАЧКА» — дерево разблокировок; «МАГАЗИН» пока только логирует.
        ("menu_settings", "НАСТРОЙКИ", "SETTINGS"),
        ("menu_upgrade", "ПРОКАЧКА", "UPGRADE"),
        ("menu_shop", "МАГАЗИН", "SHOP"),

        // Экран настроек (SettingsPanel.prefab)
        ("settings_title", "НАСТРОЙКИ", "SETTINGS"),
        ("settings_sfx", "ЗВУКИ ИГРЫ", "GAME SOUND"),
        ("settings_music", "МУЗЫКА", "MUSIC"),
        ("settings_reset_progress", "СБРОСИТЬ ПРОГРЕСС", "RESET PROGRESS"),
        // Второй тап по красной кнопке (двухшаговое подтверждение в SettingsScreen)
        ("settings_reset_confirm", "ТОЧНО СБРОСИТЬ?", "TAP AGAIN TO RESET"),
        ("settings_back", "НАЗАД", "BACK"),

        // Нереализованные в Волне 1 награды дерева (§5bis.2) — для списка уровней 0–20
        ("unlock_Skin_Ship_Diamond", "Скин корабля «Ромб»", "Ship skin: Diamond"),
        ("unlock_Skin_Trail_Blue", "Синий след", "Blue trail"),
        ("unlock_Drones", "Дроны", "Drones"),
        ("unlock_Skin_Ship_Arrow", "Скин корабля «Стрела»", "Ship skin: Arrow"),
        ("unlock_Magnet", "Магнит", "Magnet"),
        ("unlock_SlowField", "Поле замедления", "Slow field"),
        ("unlock_Turrets", "Турели", "Turrets"),
        ("unlock_Skin_Trail_Red", "Красный след", "Red trail"),
        ("unlock_ExplosionOnKill", "Взрыв при убийстве", "Explosion on kill"),
        ("unlock_SideGuns", "Боковые пушки", "Side guns"),
        ("unlock_Skin_Ship_Cross", "Скин корабля «Крест»", "Ship skin: Cross"),
        ("unlock_Loadout", "Выбор лоадаута", "Loadout select"),
    };

    // ОТКЛЮЧЕНО (инцидент 2026-09-22): помимо добавления ключей делает AssetDatabase.SaveAssets(),
    // удаляет строки ключей из RetiredKeys из ВСЕХ локалей и перезаписывает значения из ForceKeys —
    // то есть может затереть ручные правки переводов. Все ключи уже засеяны (проверено).
    // [MenuItem("AstroDrift/Setup Localization")]
    public static void Setup()
    {
        var collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(CollectionPath);
        if (collection == null)
        {
            Debug.LogError($"AstroDrift Localization: коллекция не найдена по пути {CollectionPath}");
            return;
        }

        var shared = collection.SharedData;
        // Признак «ключ уже есть» — GetId(key) в Shared Data. GetEntry(key) у per-locale
        // таблицы для этого не годится: ключ мог быть добавлен раньше без строки,
        // и тогда AddEntry(key, …) падал бы/дублировал ключ на каждом прогоне.
        // §4: вычистка ключей, потерявших потребителей (id — из Shared Data, строки — из всех локалей).
        int removedKeys = 0;
        foreach (var key in RetiredKeys)
        {
            long id = shared.GetId(key);
            if (id == 0) continue;
            foreach (var st in collection.StringTables) st?.RemoveEntry(id);
            shared.RemoveKey(key);
            removedKeys++;
        }

        int addedKeys = 0;
        foreach (var e in Entries)
        {
            if (shared.GetId(e.key) == 0) { shared.AddKey(e.key); addedKeys++; }
        }

        int addedValues = 0;
        foreach (var st in collection.StringTables)
        {
            if (st == null) continue;
            string locale = st.LocaleIdentifier.Code; // "ru" / "en"
            foreach (var (key, ru, en) in Entries)
            {
                long id = shared.GetId(key);
                if (id == 0) continue; // ключ не зарегистрирован — строку некуда класть
                string value = locale == "ru" ? ru : en;
                var entry = st.GetEntry(id);
                if (entry == null) { st.AddEntry(id, value); addedValues++; }
                else if (string.IsNullOrEmpty(entry.LocalizedValue)) { entry.Value = value; addedValues++; }
                else if (System.Array.IndexOf(ForceKeys, key) >= 0 && entry.LocalizedValue != value)
                {
                    entry.Value = value; // переименование строки из ТЗ — приводим к канону
                    addedValues++;
                }
            }
            EditorUtility.SetDirty(st);
        }
        EditorUtility.SetDirty(shared);
        AssetDatabase.SaveAssets();
        Debug.Log($"AstroDrift Localization: ключей добавлено {addedKeys}, строк добавлено/заполнено {addedValues} (RU+EN), " +
                  $"устаревших ключей удалено {removedKeys}. Повторный запуск должен показать 0/0/0.");
    }

    /// <summary>
    /// Одноразовый ремонт (ТЗ: fix pilot_level/level_line). Строки Волны 1 лежали в
    /// таблицах ДВАЖДЫ и без m_Key (наборы id 1543249381… и 1584525502…), поэтому
    /// L10n.GetFormatted не находил ключ и оба текста падали в хардкод-фолбэк.
    /// Ремонт: проставляет в Shared Data ключи по совпадению контента (канонический
    /// id — минимальный из совпавших), удаляет дубли и осиротевшие строки.
    /// Идемпотентен — повторный прогон не меняет файлы.
    /// </summary>
    // ОТКЛЮЧЕНО (инцидент 2026-09-22): удаляет дубли и осиротевшие строки локализации.
    // [MenuItem("AstroDrift/Repair Localization (one-shot)")]
    public static void Repair()
    {
        var collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(CollectionPath);
        if (collection == null)
        {
            Debug.LogError($"AstroDrift Localization: коллекция не найдена по пути {CollectionPath}");
            return;
        }

        var shared = collection.SharedData;
        int mapped = 0, removedDup = 0, removedOrphan = 0, ambiguous = 0;
        foreach (var st in collection.StringTables)
        {
            if (st == null) continue;
            bool ruLocale = st.LocaleIdentifier.Code == "ru";
            var ids = new List<long>();
            foreach (var kv in st) ids.Add(kv.Key);
            ids.Sort(); // канонический id — минимальный из совпавших по контенту
            foreach (var id in ids)
            {
                if (!string.IsNullOrEmpty(shared.GetKey(id))) continue; // строка уже с ключом
                var entry = st.GetEntry(id);
                if (entry == null) continue;

                bool isAmbiguous;
                string key = MatchKey(entry.LocalizedValue, ruLocale, out isAmbiguous);
                if (isAmbiguous) { ambiguous++; continue; } // не трогаем — решает владелец
                if (key == null) { st.RemoveEntry(id); removedOrphan++; continue; }
                if (shared.GetId(key) == 0) { shared.AddKey(key, id); mapped++; }
                else { st.RemoveEntry(id); removedDup++; } // дубль того же ключа
            }
            EditorUtility.SetDirty(st);
        }
        EditorUtility.SetDirty(shared);
        AssetDatabase.SaveAssets();
        Debug.Log($"AstroDrift Localization (repair): привязано ключей {mapped}, удалено дублей {removedDup}, " +
                  $"удалено строк без ключа {removedOrphan}, неоднозначных пропущено {ambiguous}.");
    }

    /// <summary>Ключ из Entries, чей текст локали совпадает со строкой таблицы.
    /// isAmbiguous = такому тексту соответствует больше одного ключа (строку не трогаем).</summary>
    private static string MatchKey(string value, bool ruLocale, out bool isAmbiguous)
    {
        isAmbiguous = false;
        if (string.IsNullOrEmpty(value)) return null;
        string found = null;
        foreach (var e in Entries)
        {
            if ((ruLocale ? e.ru : e.en) != value) continue;
            if (found != null) { isAmbiguous = true; return null; }
            found = e.key;
        }
        return found;
    }
}
#endif
