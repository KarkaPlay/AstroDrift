#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Единственный источник внешних ассетов для билдеров префабов (меню и левел-ап).
/// Билдеры НЕ ищут спрайты по имени/слайсу: пустое поле = ошибка с перечислением
/// всех незаполненных полей ДО первой записи префаба. Заполняется владельцем в инспекторе.
/// Пустой <see cref="panelBg"/> не бывает молчаливым: панель без спрайта включается
/// явным флагом <see cref="usePanelBg"/> = false.
/// Создание: ПКМ в Project → Create → AstroDrift → Prefab Builder Sprites.
/// </summary>
[CreateAssetMenu(fileName = "PrefabBuilderSprites", menuName = "AstroDrift/Prefab Builder Sprites")]
public class PrefabBuilderSprites : ScriptableObject
{
    [Header("Меню — Build Menu Prefabs (7 обязательных)")]
    [Tooltip("Логотип меню: Assets/New UI/Logo.png (одиночный спрайт)")]
    public Sprite logo;

    [Tooltip("Иконка уровня в LevelCard и иконка MenuButton_Upgrade: Sheet.png → Sheet_2 (391×589)")]
    public Sprite levelIcon;

    [Tooltip("Иконка настроек MenuButton_Settings: Sheet.png → Player (181×288)")]
    public Sprite settingsIcon;

    [Tooltip("Иконка магазина MenuButton_Shop: Sheet.png → Sheet_3 (449×648)")]
    public Sprite shopIcon;

    [Tooltip("Иконка прокачки MenuButton_Upgrade (контракт §8.3 п.1). На HEAD иконки не было: слайса Level_Icon в Sheet.png не существует. Пусто → билдер падает, перечислив поле")]
    public Sprite upgradeIcon;

    [Tooltip("Фон нижних кнопок: Assets/New UI/MenuButtonBG.png (848×512, одиночный спрайт)")]
    public Sprite buttonBg;

    [Tooltip("Скругление прогресс-бара LevelCard: Assets/New UI/Generated/RoundedBar.png (364×24)")]
    public Sprite barSprite;

    [Header("Левел-ап — Build LevelUp Prefabs (5 обязательных)")]
    [Tooltip("Фон карты перка (база). На HEAD — Sheet.png → Sheet_2 «Upgrade» (391×589)")]
    public Sprite cardBg;

    [Tooltip("Фон карты перка «НОВОЕ» (вариант). На HEAD — Sheet.png → Sheet_3 «Upgrade_new» (449×648)")]
    public Sprite cardNewBg;

    [Tooltip("Ставить ли спрайт на фон панели левел-апа. false — панель без спрайта (как на HEAD: полупрозрачный чёрный); panelBg тогда не обязателен")]
    public bool usePanelBg;

    [Tooltip("Фон панели левел-апа (Sheet.png → Экран_0, 1081×1921). Обязателен только при включённом usePanelBg")]
    public Sprite panelBg;

    [Tooltip("Звезда над заголовком: Sheet.png → Star (188×188)")]
    public Sprite star;

    [Tooltip("Фон кнопки реролла: Sheet.png → Sheet_4 «Update Perks» (576×206)")]
    public Sprite rerollBg;

    [Header("Настройки — Build Settings Prefab (необязательные: пусто → берётся settingsIcon)")]
    [Tooltip("Иконка строки «ЗВУКИ ИГРЫ». Пусто — используется settingsIcon (билдер НЕ падает)")]
    public Sprite sfxIcon;

    [Tooltip("Иконка строки «МУЗЫКА». Пусто — используется settingsIcon (билдер НЕ падает)")]
    public Sprite musicIcon;

    [Tooltip("Иконка на красной кнопке сброса прогресса. Пусто — используется settingsIcon")]
    public Sprite resetIcon;
}

/// <summary>
/// Общие проверки контейнеров для билдеров: отсутствие контейнера и список
/// незаполненных полей — обе ошибки ДО первой записи префаба (§8.3 п.1).
/// </summary>
internal static class PrefabBuilderAssets
{
    // НЕ в Resources: ассет editor-only и не должен попадать в билд плеера.
    internal const string SpritesContainerPath = "Assets/Scripts/Editor/PrefabBuilderSprites.asset";

    internal static T LoadContainer<T>(string path, string owner, string hint = null) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            Debug.LogError(owner + ": нет ассета " + path + " — префабы не записаны."
                + (string.IsNullOrEmpty(hint) ? "" : " " + hint));
            return null;
        }
        return asset;
    }

    /// <summary>null, если поле заполнено; иначе — имя поля (для списка ошибки).</summary>
    internal static string Field(Sprite sprite, string fieldName)
    {
        return sprite == null ? fieldName : null;
    }

    /// <summary>Имя поля — только если спрайт обязателен по флагу (иначе null).</summary>
    internal static string FieldIf(bool required, Sprite sprite, string fieldName)
    {
        return required ? Field(sprite, fieldName) : null;
    }

    /// <summary>true — все поля заполнены. Иначе одна ошибка со ВСЕМ списком пустых полей.</summary>
    internal static bool RequireAll(PrefabBuilderSprites src, string owner, params string[] fieldNames)
    {
        var missing = new System.Text.StringBuilder();
        int count = 0;
        for (int i = 0; i < fieldNames.Length; i++)
        {
            if (fieldNames[i] == null) continue;
            if (count > 0) missing.Append(", ");
            missing.Append(fieldNames[i]);
            count++;
        }
        if (count == 0) return true;

        Debug.LogError(owner + ": не заполнено " + count + " поле(й) в " + AssetDatabase.GetAssetPath(src)
            + " — префабы не записаны. Незаполненные поля: " + missing);
        return false;
    }
}
#endif
