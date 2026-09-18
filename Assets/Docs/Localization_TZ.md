# ТЗ: Миграция системы локализации — v2.3

| Поле | Значение |
|---|---|
| **Статус** | Готово к запуску в работу (N1–N3 + F1–F4 закрыты; архитектурных изменений нет) |
| **Версия** | 2.4 (F5: приёмка — ин-движок на профиле RuStore, билды и скриншоты убраны) |
| **Дата** | 2026-09-18 |
| **Заменяет** | `Docs/Localization_TZ.md` v2.3 |
| **Окружение** | Unity 6000.3.9f1, Localization 1.5.13, TMP, WebGL (ЯИ) / RuStore / itch.io |
| **Исходное состояние** | Таблицы `GameTexts`: 2 локали (ru, en), 75 ключей, 0 пустых |

Журнал v1 → v2 (§16) проверен ревью, расхождений нет. Ревизия 2.1 закрыла 5 блокеров (B1–B5) и 10 пропусков ревью v2 — см. журнал §17. Ревизия 2.2 закрывает 3 новых дефекта (N1–N3) и 5 мелочей ревью v2.1 — см. журнал §18. Ревизия 2.3 закрывает противоречие в критериях задачи 3 (F1), очистку scripting defines (F2) и 2 уточнения — см. журнал §19. Ревизия 2.4 (F5) заменяет сборку билдов и скриншот-приёмку ин-движок проверкой на активном профиле **RuStore (Android)** — см. журнал §20.

---

## 0. Скоуп

### 0.1. В скоупе милстоуна

| # | Задача | Результат |
|---|---|---|
| 1 | Пересборка `Montserrat-SemiBold SDF` | Единицы МБ, Static, Padding 9, Point Size 60, полный чарсет таблиц |
| 2 | Удаление `AutoTranslateLangs` + гейт G1 источника языка + фикс `YandexGamesInstaller` | Ноль ссылок на модуль; источник языка зафиксирован и проверен; профиль YandexGames компилируется |
| 3 | `LanguageService` + удаление моста + контракт инициализации C1–C3 | Единственный владелец языка; EveryGameLaunch воспроизведён; вспышки языка нет |
| 4 | `TypographyConfig` v2 + `TypeRoleTag` + удаление цепочки `LanguageChanged` | Шрифты от `SelectedLocale`; роли переживают удаление `LocalizedTextUI` |
| 5 | `LocalizedTextUI` → `LocalizeStringEvent` (все билдеры, `AstroDriftSceneSetup`, `GameUI`) | Компонентный подход везде, где есть нода. **Приёмка после задачи 4** |
| 6 | `LocalizedString` в SO + правка `AstroDriftSetup` + разовая миграция + `PerkTitle` | Перки/пикапы локализуются штатно. Приёмка после задачи 5 (LSE на картах) |
| 7 | Ужатие `L10n` до синхронного ридера | `Get` + `GetFormatted` (~20 строк), без `Bind`/`RefreshAll` |
| 8 | Missing translations report + coverage чарсета + чистка сироты «ДЕРЕВО» | Отчёты зелёные на RU/EN |

### 0.2. Вне скоупа (отдельные фичи после миграции)

- **Экран Настроек + ручной выбор языка.** Экрана нет (`MenuButton_Settings` только логирует клик). Решение по приоритету зафиксировано в §6.3, реализация — позже.
- **RTL/арабский.** Отдельный этап (шрифт + RTL TextMeshPro + флаг RTL в конфиге языка).
- **Новые языки как контент.** Миграция строит инфраструктуру; добавление локали = перевод + шрифт, без кода.
- **CSV-workflow переводчика.** Встроенная фича редактора таблиц (Export/Import), отдельной задачей не ставится.
- **Назначение шрифтовых ролей нодам, у которых их не было.** Неролированные ноды остаются без тегов (§8.4) — это гарантия нулевого визуального диффа, а не долг.
- **Сборка билдов и скриншот-приёмка.** Заменены ин-движок проверкой (F5, §20): билды не собираются, скриншоты не снимаются; визуальная приёмка RU/EN идёт в Editor Play Mode на активном профиле RuStore. Билд-вес из §4.3/§14 выведен — вместо него метрики Font Asset'а.

### 0.3. Нерушимые ограничения

1. **RU/EN без регрессий на каждом шаге.** Каждая задача проверяется отдельно до перехода к следующей.
2. **Ключи таблиц не переименовывать** (аналог правила «ключи PlayerPrefs не трогать»).
3. **Поведение EveryGameLaunch сохраняется** в рамках миграции (требование ЯИ): язык платформы применяется при каждом запуске.
4. **YG2 остаётся обязательным рантаймом** — удаляется только владение языком у его модулей (§1.3).
5. **Нулевой визуальный дифф RU/EN.** В т.ч.: ноды без ролей остаются без тегов (§8.4), Padding атласа не меняется (§4.1), чарсет покрывает все символы таблиц (§4.2).

---

## 1. Диагноз текущего состояния

### 1.1. Три подсистемы — точная картина

| Подсистема | Что хранит | Проблема |
|---|---|---|
| Unity Localization (`GameTexts`) | 75 ключей, RU+EN | Хозяин по факту; всё остальное должно ей подчиняться |
| YG2 Localization (`Lang_yg`, мост) | Состояние «текущий язык» (`YG2.lang`, `onSwitchLang`) + `CorrectLang` переписывает `YG2.lang` | **Второй владелец языка** (первый — `SelectedLocale`) |
| YG2 AutoTranslateLangs | Переводы в полях префабов (`LanguageYG.AssignTranslate` пишет в компоненты, не в таблицы) | **Параллельное неуправляемое хранилище строк**: расходится с таблицами, не покрывается валидацией и CSV-экспортом |

### 1.2. Кастомный `L10n` — хрупкое ядро

`LocalizedText.cs`: `Bind` + единый список биндингов + `RefreshAll` через `Application.onBeforeRender` + переподписка на `SelectedLocaleChanged`. Ручная борьба с асинхронной загрузкой/перезагрузкой таблиц; штатный `LocalizeStringEvent` решает то же из коробки.

Полностью `L10n` не выводится: синхронного чтения требуют флоатеры из пула (`GameManager.cs:566`, `PickupManager.cs:168`), `_treeText` (`GameUI.cs:244`, StringBuilder + 24 ключа + rich text), склейка `DeathUnlocked`. Целевое состояние — тонкий ридер (задача 7).

### 1.3. YG2 — рантайм всех платформ

Факты: RuStore-реклама через YG2 (`YandexMobileAdsService.cs:38`), itch-аналитика через `YG2.MetricaSend` (`ItchAnalyticsService.cs:24`), `PlatformServices` с ленивыми заглушками в `_Platform`. Локализационная часть YG2 в UI — тонкая (`Typography.cs:47`, мост).

**Следствие:** цель — не «вынести YG2», а **забрать у YG2-модулей владение языком**. Сам плагин остаётся обязательным рантаймом.

### 1.4. Шрифт 34.7 МБ

Разборка `Montserrat-SemiBold SDF.asset` (подтверждена ревью v2):

- глифов — **99** (латиница + кириллица, вкл. Ё/Й/№);
- атлас `2048×2048` A8 = **8.0 МБ**;
- сериализованные таблицы кернинга/фич — **26.65 МБ**;
- `m_AtlasPopulationMode: 1` (**Dynamic**) + `m_IsMultiAtlasTexturesEnabled: 1` + `m_AtlasPadding: 9`.
- `m_PointSize: 90` (у Bold — 59); `m_AtlasRenderMode: 4165` = **SDFAA** (в обоих шрифтах).

Причины веса: кернинг + расточительный атлас. Риск помимо веса: Dynamic + multiAtlas **растит атлас в рантайме** (турецкие `ı ş ğ`, любые новые символы). Для сравнения: `Montserrat-Bold SDF` — 220 глифов, атлас 1024×512, Static, **1.17 МБ**. По `TypographyConfig.asset:20` SemiBold стоит в **3 слотах из 4**.

> ⚠️ Прямое следствие для задачи 1 (B1): спецсимволы таблиц «— → −» рендерятся сейчас
> **только благодаря Dynamic-атласу**. Static-пересборка с неполным чарсетом даст квадраты
> в `tap_to_play`, `level_up_line`, EN-описаниях перков. Поэтому §4.2 фиксирует полный чарсет,
> а §11.2 — проверку покрытия чарсета валидатором (рецидив B1 исключён процессом).

### 1.5. Коды языков

YG2 отдаёт ISO 639-1 (`ru`, `en`, `tr`, `Lang_yg.cs:57`), `LocaleIdentifier.Code` в проекте — те же `ru`/`en`. Расхождение только на региональных/скриптовых кодах (`zh-Hans`, `pt-BR`). Маппинг в `LanguageService` — задел на будущее. `languageOverrides` в текущем конфиге — пустой массив.

### 1.6. Зафиксированные долги (закрываются задачей 8)

- Массив `Entries` в `AstroDriftLocalizationSetup.cs` **неполон**: ~20 ключей (`title_main`, `title_sub`, `best`, `score`, `new_best`, `continue_*`, `pause_*`, `combo`, `pilot_level`, `shield_*`, `pickup_*`) созданы вне его. Валидатор не опирается на `Entries`.
- Осиротевшая строка id `1584525502046248` («ДЕРЕВО») в обеих таблицах без ключа — удалить.
- Рантайм-ключи `unlock_*` — тянуть из `PilotProgressManager.AllUnlocks`.
- Максимальный кодпоинт таблиц — `U+2212` (зафиксировано ревью v2) → чарсет §4.2 полон для текущих таблиц.

---

## 2. Целевая архитектура

```
┌─ Вход: код языка платформы (только чтение!) ──┐
│ ЯИ → источник гейта G1 · RuStore/itch → system │
└──────────────────────┬─────────────────────────┘
                       ▼
┌─ LanguageService (ЕДИНСТВЕННЫЙ владелец языка) ┐
│ маппинг кода → ближайшая доступная локаль      │
│ применение при каждом запуске (= EveryGameLaunch)│
│ триггер TypeRoleApplier + сброс гардов Arguments│
└──────────────────────┬─────────────────────────┘
                       ▼ SelectedLocale
┌─ Unity Localization ───────────────────────────┐
│ String Table "GameTexts" (все языки, вручную)   │
└──┬──────────────┬──────────────┬────────────────┘
   ▼              ▼              ▼
LocalizeString   LocalizedString Sync-ридер L10n
Event (ноды)     в SO (перки)    (пул, дерево)
```

Правила:

1. **Строки** живут только в `GameTexts`. Исключения: фолбэки на случай неготовности таблицы и технический текст без перевода.
2. **Язык** переключает только `LanguageService` через `LocalizationSettings.SelectedLocale`. Прямые записи из других мест — запрещены (grep-контроль, §15).
3. **YG2** даёт только входную строку «код языка платформы». Подписки UI на `onSwitchLang`/`onCorrectLang` — запрещены.
4. **Шрифты** резолвятся по `SelectedLocale.Identifier.Code`. Коды YG2 в типографике не используются.
5. **Роли шрифтов** носит только `TypeRoleTag` (§3.5). Таблиц «роль-по-имени» нет (хрупко при переименованиях).

---

## 3. Паттерны локализации (4 разрешённых + запрещённые)

### 3.1. Ноды префабов и сцены → `LocalizeStringEvent`

Статика и динамика-с-параметрами на существующих нодах. Компонент владеет подпиской, переживает смену локали и асинхронную догрузку таблиц.

```csharp
var lse = go.AddComponent<LocalizeStringEvent>();
lse.StringReference.TableReference = "GameTexts";
lse.StringReference.TableEntryReference = "menu_settings";
```

Динамика с числами — через `Arguments` + `RefreshString()` (с гардами на горячем пути, §3.4). Рантайм-смена ключа (двухсостоятельные ноды: `DeathLevel`, `ShieldCaption`) — через смену `TableEntryReference` + `RefreshString()`.

**Правило владения (B4):** LSE и `TypeRoleTag` вешаются **в источнике истины ноды** — в том билдере, который её создаёт. `AstroDriftSceneSetup` только находит ноды префаба; навешивание компонентов на найденные ноды префаба запрещено (сотрётся следующей пересборкой префаба / потерей инстанса).

### 3.2. SO-ассеты → `LocalizedString`-поля. ТОЛЬКО вариант А

Поля `LocalizedString` в `PerkDefinition` (и новое поле `PickupDef.name`). В рантайме UI **присваивает ссылку** компоненту карты:

```csharp
// ✅ ЕДИНСТВЕННЫЙ разрешённый способ (вариант А):
cardTitleLse.StringReference = def.title;
cardDescLse.StringReference  = def.desc;
```

> ⛔ **Подписка `StringChanged` на `LocalizedString` из SO-ассета — ЗАПРЕЩЕНА.**
> Подписка идёт на общий делегат ассета, а карты пересоздаются каждый левелап
> (`PerkChoiceUI.cs:187`) и уничтожаются — подписки накапливаются и пишут в уничтоженные TMP.
> При reroll возможны две карты на одну `LocalizedString`. `LocalizeStringEvent` владеет
> подпиской сам и снимает её на `OnDisable` — поэтому только он.

### 3.3. Рантайм-компонуемые строки → синхронное чтение

Разрешено ровно там, где ноды нет или строка собирается из многих ключей:

| Место | Ключи | Обновление при смене локали |
|---|---|---|
| `_treeText` (`FillUnlockTree`) | `L10n.Get("unlock_" + id)`, `unlock_soon`, `unlock_tree_title` | Перерисовать по `SelectedLocaleChanged`, если панель видима |
| Комбо-флоатер (`GameManager`) | `L10n.GetFormatted("combo", …)` | Не нужно (живёт <1 с) |
| Перк-флоатер (`PerkChoiceUI.PerkTitle`) | `def.title.GetLocalizedString()` с фолбэком на `id` (§9.1) | Не нужно (живёт <1 с) |
| Пикап-флоатер (`PickupManager`) | `def.name.GetLocalizedString()` с фолбэком на `type` (§9.3) | Не нужно (живёт <1 с) |
| `DeathUnlocked` (заголовок + склейка имён) | `unlocked_title`, `unlock_*` | Не нужно (перезаполняется на каждый показ) |

Все синхронные чтения обязаны быть **пустото-толерантными**: `LocalizedString.GetLocalizedString()` при незаполненной ссылке/неготовой таблице возвращает **пустую строку, а не `null`** — проверка только через `string.IsNullOrEmpty(...)`, иначе фолбэк на cold-start WebGL не сработает (N3). `L10n.Get` возвращает `null` — для него `IsNullOrEmpty` покрывает оба случая. Единый контракт проверки — `IsNullOrEmpty` везде.

### 3.4. Горячий путь: гарды от аллокаций

`StringReference.Arguments = new object[] { … }` аллоцирует массив. Правило:

- **Холодный путь** (показ Death-экрана, меню, оверлей перка) — как угодно, аллокации не нормируются.
- **Горячий путь** (`RefreshHud` на каждую смену счёта — туда переезжает `startBestValue`) — **гард по изменению значения + кэшированный массив + сброс гарда при смене локали** (иначе гард заблокирует обновление `best_value` внутри сессии):

```csharp
private readonly object[] _bestArgs = new object[1];
private int _lastBestShown = -1;

void RefreshBestValue(int best) {
    if (best == _lastBestShown) return;
    _lastBestShown = best;
    _bestArgs[0] = Format(best);
    _bestValueLse.StringReference.Arguments = _bestArgs;
    _bestValueLse.StringReference.RefreshString();
}

// сброс — по SelectedLocaleChanged (владелец подписки — LanguageService, §6.1):
void OnLocaleChanged() { _lastBestShown = -1; RefreshBestValue(_score.Best); }
```

### 3.5. Шрифты → `TypographyConfig` v2 + `TypeRoleTag`

Эволюция существующего конфига (идея 4 ролей сохраняется):

- `langCode` (код YG2) → `localeCode` (код `LocaleIdentifier.Code`: `ru`, `en`, `zh-Hans`, …);
- источник языка — `LocalizationSettings.SelectedLocale`;
- цепочка фолбэков без изменений: оверрайд локали → базовый слот → `TMP_Settings.defaultFontAsset`;
- CJK-стратегия (инфраструктура сейчас, контент позже): базовый шрифт + **TMP Font Asset Fallback** / Dynamic-ассет, а не один статический атлас на весь CJK.

**Носитель роли (B5).** `LocalizedTextUI.OnEnable` — сейчас единственное место, вызывающее `Typography.ApplyFontOnly(tmp, role)` (плюс 5 ручных применений в `GameUI.ApplyTypography`). После удаления компонента роли некому применять → задача 4 стала бы инертной. Решение — крошечный компонент-носитель (таблица «роль-по-имени» отклонена: хрупка при переименованиях):

```csharp
// Новый файл Assets/Scripts/UI/TypeRoleTag.cs
public class TypeRoleTag : MonoBehaviour {
    public TypeRole role;   // имя поля фиксировано: билдеры пишут через SerializedObject.FindProperty("role")
    public void Apply() => Typography.ApplyFontOnly(GetComponent<TextMeshProUGUI>(), role);
    // Self-apply (N2): карты перков создаются/уничтожаются в рантайме —
    // одноразовый свип при старте их не увидит. Шрифт не зависит от текста,
    // поэтому порядок OnEnable vs FillCard не важен.
    private void OnEnable() => Apply();
}
```

```csharp
// Свип — ТОЛЬКО для смены локали (стартовые и рантайм-ноды покрывает OnEnable).
// Opt-in: нетегированные ноды не трогаются (HUD и Ко).
public static class TypeRoleApplier {
    public static void ApplyAll() {
        foreach (var tag in Object.FindObjectsByType<TypeRoleTag>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            tag.Apply();
    }
}
```

Триггер свипа — `LanguageService` (единственный владелец реакций на язык): по `SelectedLocaleChanged` → `TypeRoleApplier.ApplyAll()` (+ сброс гардов §3.4). Стартовое применение идёт через `OnEnable` тегов сцены/префабов — отдельного вызова не нужно. Отдельная цепочка событий типографики не создаётся; существующая `Typography.LanguageChanged` **удаляется** как мёртвая (§7.1).

Роли переносятся **1:1 с текущим кодом**, без переосмысления (карты — §8.1/§8.2). Ноды, у которых роли не было, тегов не получают (§8.4).

> Вариант Asset Tables + `LocalizedTmpFont` убран из ТЗ как непроверенная опция.
> Выбранный вариант от него не зависит — проверка рефлексией не требуется.

### 3.6. Запрещённые паттерны (все — grep-контроль в §15)

1. Подписка `StringChanged` на `LocalizedString` из SO-ассета.
2. `L10n.Bind` (метод удаляется в задаче 7) и прямые записи `.text =` для переводимых строк вне §3.3.
3. Подписки UI на `YG2.onSwitchLang` / `onCorrectLang`; любые чтения `YG2.lang` вне `LanguageService` (после задачи 3 — ноль везде, источник фиксирует гейт G1).
4. Прямая запись `LocalizationSettings.SelectedLocale` вне `LanguageService`.
5. Компоненты `LanguageYG` на сценах/префабах (модуль удалён).
6. `string.Format` поверх ключей таблиц вне §3.3 (динамика — через `Arguments`).
7. `Typography.LanguageChanged` / `NotifyLanguageChanged` (цепочка удаляется в задаче 4).
8. Чтения `titleKey` / `descKey` после задачи 6 (поля удалены из класса).

---

## 4. Задача 1. Пересборка `Montserrat-SemiBold SDF` ⭐ начинать отсюда

**Почему первая:** не зависит ни от чего, риск нулевой (после B1/B2/N1-правок), результат измерим и влияет на вес билдов.

### 4.1. Шаги

1. Сделать бэкап текущего `Assets/Fonts/Montserrat-SemiBold SDF.asset` (рядом, суффикс `.backup`, удалить после приёмки).
2. Открыть **Window → TextMeshPro → Font Asset Creator**:
   - Source Font: `Montserrat-SemiBold.ttf`;
   - Atlas Resolution: **1024×1024** (512 под ~450 глифов с Padding 9 не влезет — B2);
   - Character Set: Custom / Unicode Range hex (§4.2);
   - Render Mode: **SDFAA** (фактическое `m_AtlasRenderMode: 4165` в обоих шрифтах; в Creator три разных пункта — выбрать явно);
   - **Sampling Point Size = 60** (N1; было 90). Опора: Bold при 59 держит 220 глифов в 1024×512 → 450 глифов ≈ 1024² при том же классе настроек; пара шрифтов становится консистентной;
   - Atlas Population Mode: **Static**; Multi Atlas Textures: **off**;
   - **Padding = 9** (как текущий `m_AtlasPadding` — толщина SDF-обводки; смена ломает критерий «визуально без изменений», B2);
   - Get Kerning Pairs: **off**.
3. Проверить отчёт Creator: **0 failed/missing glyphs**. Лестница фолбэков (строго по порядку, каждый следующий — с записью причины в §14):
   1. Primary: 1024² @60 — ожидается 0 failed (опора — Bold выше);
   2. Fallback: 2048×1024 @60 — допустим, цель «≤2 МБ» при этом не применяется. Фактический размер файла — **≈4.2 МБ**, а не 2–2.5 МБ: Unity-YAML хранит Alpha8-атлас как **~2 Б/тексел**, поэтому 2048×1024 даёт 4.00 МБ секцию текстуры (та же модель подтверждена на Bold: 1024×512 → 1.05 МБ при файле 1.11 МБ, и на baseline SemiBold: 2048×2048 → 8.0 МБ). ⚠️ Из той же модели следует, что **и primary 1024² не уложился бы в «≤2 МБ»** (~2.0 МБ только атлас + метаданные) — лимит недостижим без шага 3 (F6);
   3. Запрещено без решения продукта: дроп глифов (квадраты), point size > 72 (ломает посадку), 2048².
4. Перезаписать ассет **по тому же пути** (ссылки `TypographyConfig` сохраняются).
5. Проверить **ин-движком** на активном профиле **RuStore (Android)**: Play Mode, все экраны RU/EN + тестовая строка `— → − · «» ı ş ğ İ` (покрывает B1 и турецкие символы). Билды не собираются, скриншоты не снимаются — приёмка по F5/§20.

### 4.2. Чарсет (hex-диапазоны) — исправлено по B1

```
0020-007E   Basic Latin
00A0-00FF   Latin-1 Supplement (· « » Ç Ö Ü ç ö ü)
0100-017F   Latin Extended-A (Ğ ğ İ ı Ş ş)
0400-045F   Cyrillic (вкл. Ё Й ё й)
2010-2027   General Punctuation (вкл. — U+2014)            ← добавлено (B1)
2116        №
2190-2199   Arrows (вкл. → U+2192)                         ← добавлено (B1)
2212        − (минус U+2212)                               ← добавлено (B1)
```

> **F6 — эффективный чарсет (зафиксировано по факту задачи 1).** 8 кодпоинтов диапазона
> `2010-2027` — `U+2016 U+2017 U+201B U+201F U+2023 U+2024 U+2025 U+2027` — присутствуют в cmap
> `Montserrat-SemiBold.ttf` (`Font.HasCharacter` = true), но **не имеют глифовых аутлайнов**
> (`Font.GetCharacterInfo` = false), поэтому не пакуются ни при каком Atlas Resolution/Point Size.
> Ни один из них не используется таблицами (проверено ин-движком: 120 уникальных кодпоинтов
> в `GameTexts_ru`+`en`, максимум — `U+2212`). Эффективный чарсет:
> `0020-007E, 00A0-00FF, 0100-017F, 0400-045F, 2010-2015, 2018-201A, 201C-201E, 2020-2022, 2026, 2116, 2190-2199, 2212`
> → **443 глифа, 0 missing**. Критерий «0 failed/missing glyphs» трактуется как «0 среди требуемых
> кодпоинтов», а не «все кодпоинты сырых диапазонов» — иначе критерий валится при каждой пересборке.

Без добавленных диапазонов Static-пересборка даёт квадраты в `tap_to_play` («ТАП — СТАРТ»), `level_up_line` («УРОВЕНЬ {0} → {1}»), EN-описаниях перков («−15%»). Максимальный кодпоинт таблиц — `U+2212`, т.е. чарсет полон для текущих таблиц; покрытие новых символов идёт через валидатор (§11.2, п. 6). Итого ~450+ глифов (было 99) — посадка в 1024² при Point Size 60 опирается на Bold (220 глифов в 1024×512 при 59); лестница фолбэков — §4.1, п. 3.

### 4.3. Критерии приёмки

- [ ] **0 failed/missing glyphs среди требуемых кодпоинтов** (первый и главный критерий — N1): 443/443 по эффективному чарсету (F6). 8 кодпоинтов без аутлайнов в исходном TTF **не считаются провалом** — §4.2 F6;
- [ ] Размер файла: **≤2 МБ** (primary 1024²) или fallback 2048×1024 с записанной причиной. Применён **шаг 2**: фактический размер **4 431 136 Б (4.23 МБ)** — лимит 2–2.5 МБ недостижим по модели «2 Б/тексел» (F6), шаг 3 не применялся;
- [ ] Population Mode = Static, MultiAtlas = off, Padding = 9, Point Size = 60, SDFAA, Kerning off — проверено **ин-движком** (чтение `TMP_FontAsset` через редактор, не только YAML);
- [ ] **Ин-движок приёмка на активном профиле RuStore (Android), Play Mode:** все экраны RU/EN визуально без изменений, **вкл. строки из B1** — стартовый экран, Death с `level_up_line`, оверлей перка с EN-описаниями. Даунсемпл point size 90→60: ожидаемый дифф — нулевой (опора: Bold@59 уже рендерит крупные кегли); явные артефакты = провал приёмки;
- [ ] Тестовая строка `— → − · «» ı ş ğ İ` рендерится без квадратов (ин-движок, через `TMP_Text`);
- [ ] Метрики Font Asset'а до/после — в матрицу §14 (ин-движок, профиль RuStore): размер файла, Population Mode, MultiAtlas, атлас, Point Size, число глифов, Padding.

---

## 5. Задача 2. Удаление `AutoTranslateLangs` + гейт G1 + фикс `YandexGamesInstaller`

Предусловие задачи 3: `CorrectLang` (`CorrectLang.cs:10`, саморегистрация на `onCorrectLang`, перезапись `YG2.lang`) — второй владелец языка, несовместим с `LanguageService`.

### 5.1. Шаги

1. Grep по `Assets/`: `LanguageYG`, `CorrectLang`, `AutoTranslateLangs`, `fontsTMP`, `AssignTranslate`, `onCorrectLang` — зафиксировать все вхождения (сцены, префабы, код). **Проверено на момент v2.3: вхождений вне папки модуля — ноль** (`LanguageYG`/`fontsTMP`/`AutoTranslateLangs` не встречаются ни в `Assets/Scripts`, ни в сценах, ни в префабах; `onCorrectLang` читает только сам `Lang_yg.cs`). Шаг выполняется как подтверждение, а не как работа — искать нечего.
2. Снять компоненты `LanguageYG` со всех объектов — при нулевом результате шага 1 это no-op; результат фиксируется в коммите как «0 вхождений».
3. Отключить модули Localization/AutoTranslate в настройках YG2.
4. Физическое удаление папки `Modules/AutoTranslateLangs` — **только после проверки**, что ядро YG2 и остальные модули не ссылаются на неё (чистая компиляция + окно настроек YG2 открывается). Иначе — оставить отключённой, зафиксировать в журнале.
5. **Удалить define `AutoTranslateLangs_yg`** из `ProjectSettings/ProjectSettings.asset` (все строки платформ). Сейчас он проставлен для всех билд-профилей, а после удаления модуля не имеет ни одного читателя в проекте — мёртвый define вводит в заблуждение и маскирует ошибки компиляции при откате.
6. **Заменить `YG2.lang` в `YandexGamesInstaller.cs:35`** (внутри `#if STORE_YANDEX`, без гарда — B3: выключение модуля даёт ошибку компиляции профиля YandexGames) на источник `LanguageService` + проверить grep'ом остальные чтения `YG2.lang` в `_Platform`.
7. Проверить, что define `Localization_yg` после задачи 3 не имеет читателей (мост и `Typography` его теряют) — удаление вынесено в задачу 3, шаг 7.

### 5.2. Гейт G1: источник кода языка на ЯИ — ОБЯЗАТЕЛЬНЫЙ (не «верификация»)

Исходные данные ревью v2: `EnvirData.jslib` берёт `navigator.language` (язык **браузера**, не аккаунта); язык **аккаунта** ЯИ (`ysdk.environment.i18n.lang`) отдавал только `Language.jslib` выключаемого модуля; `InitEnvironmentData_js` возвращает JS-переменную `environmentData`, которой нет ни в одном `.jslib` проекта.

Ранжированные варианты (решает исполнитель гейта, решение записывается в Приложение A):

1. **Перенести существующий `LangRequest_js`** из `Modules/Localization/Plugins/Language.jslib` в `_Platform/YandexGames` (один файл, остальное удалить). Писать новый код не нужно — та же семантика (`ysdk.environment.i18n.lang`, язык аккаунта), ноль нового риска. Предпочтителен, если переносится и верифицируется тестовой сборкой ЯИ без затягивания.
2. **Язык браузера** (`navigator.language` через существующий EnvirData-путь / `Application.systemLanguage`). Отклонение от прежней семантики (браузер ≈ аккаунт на практике) — фиксируется как осознанное.

Дополнительно зафиксировано: `Application.systemLanguage` — **основной путь для RuStore/itch/Editor**, а не фолбэк.

### 5.3. Критерии приёмки

- [ ] Ноль вхождений `LanguageYG`/`CorrectLang` в сценах, префабах и `Assets/Scripts`;
- [ ] Проект компилируется **ин-движком при переключении активного профиля** (ItchIO → RuStore → YandexGames), вкл. YandexGames (B3) — компиляция, не сборка; окно настроек YG2 открывается;
- [ ] Источник кода языка на ЯИ зафиксирован (вариант 1 или 2 гейта G1). **Проверка на ЯИ отложена** (F5: билды не собираются) — фиксируется в Приложении A как «отложено», финальная проверка переносится на первую сборку ЯИ после милстоуна.

---

## 6. Задача 3. `LanguageService` + удаление моста

### 6.1. Поведение и контракт инициализации

- Вызывается **один раз при старте**. Резервирует хук `TryApplyPlayerOverride()` под будущий явный выбор (заглушка, реализация с экраном Настроек).
- Читает код языка платформы (источник из гейта G1 / `systemLanguage`) → маппит на ближайшую доступную локаль → выставляет `SelectedLocale`, если отличается.
- Таблица маппинга (код платформы → локаль; региональные — через базу):

```
ru → ru · en → en · tr → ru* · <unsupported> → ru (дефолт, решение D1 §6.3)
zh-Hans/zh-Hant → zh → ru* · pt-BR → pt → ru* · <пусто> → ru
* tr/zh/pt — нет локалей в проекте. Когда локаль добавится,
  правится одна строка таблицы, не код.
```

**Контракт инициализации C1–C3** (риск №1 миграции; требования унаследованы от документации удаляемого моста):

- **C1.** Не вызывать `AvailableLocales.GetLocale` / не выставлять `SelectedLocale` до `LocalizationSettings.InitializationOperation.Completed` (`IsDone` → сразу, иначе `Completed += …`). До этого `GetLocale` вернёт null.
- **C2.** `Bootstrap.Build` стартует по **И** (`PlatformBoot.Ready`, `LanguageService.StartupApplied`) — правка wiring'а в `Bootstrap`. Иначе UI построится в дефолтной локали и будет видимая вспышка языка при позднем применении (на WebGL `PlatformBoot.Ready` приходит позже `BeforeSceneLoad`).
- **C3.** Watchdog 5 с: если init не завершился — применить дефолт `ru`, выставить `StartupApplied`, warning в лог. Старт игры не должен висеть из-за локализации.

`[RuntimeInitializeOnLoadMethod]` сам по себе C1–C2 не даёт — контракт реализуется явным кодом (подписка + флаг + ожидание в `Bootstrap`).

### 6.2. Удаление моста

- Удалить `Assets/Scripts/Core/AstroDriftLanguageBridge.cs`;
- Обновить комментарий-ссылку в `YandexGamesInstaller.cs:10` («за это отвечает …» → `LanguageService`);
- **`Typography.CurrentLang`: источник меняется здесь же, в задаче 3** — с `#if Localization_yg → YG.YG2.lang` на `LocalizationSettings.SelectedLocale?.Identifier.Code` (F1). Причина: §6.4 требует отсутствия `YG2.lang` в `Assets/Scripts` уже после задачи 3, а `Typography.cs` находится в `Assets/Scripts` и содержит это чтение. Перестановка безопасна: `Identifier.Code` даёт те же `ru`/`en`, что и `YG2.lang`, поэтому `TypographyConfig.GetFonts(langCode)` работает без изменений, а задача 4 добавляет только переименование поля в `localeCode`.
- Удалить define `Localization_yg` из `ProjectSettings/ProjectSettings.asset` (после шагов выше у него не остаётся читателей — проверить grep'ом `Localization_yg` по `Assets/Scripts`). **Порядок (F7, санкция продюсера):** СНАЧАЛА [`SettingsYG2.asset:55`](Assets/PluginYourGames/Resources/SettingsYG2.asset:55) `Basic.autoDefineSymbols: 1 → 0`, ЗАТЕМ снять токен со всех 11 платформ. Причина: [`DefineSymbols.ModulesDefineSymbols()`](Assets/PluginYourGames/Scripts/EditorScr/DefineSymbols.cs:226) регенерирует `<Folder>_yg` из **папок** `Modules/*` (строка 253 — `Directory.GetDirectories`), а не из реестра выбранных модулей, и делает это при любом refresh; папка `Modules/Localization` при этом неудаляема — её [`Lang_yg.cs:10`](Assets/PluginYourGames/Modules/Localization/Scripts/Lang_yg.cs:10) объявляет `YG2.lang`/`onSwitchLang`, безусловно используемые вне модуля: [`GetPlayerYG.cs:43`](Assets/PluginYourGames/Modules/Authorization/Scripts/GetPlayerYG.cs:43) (`DrawName(YG2.lang)`, без `#if`), [`GetPlayerYG.cs:25`](Assets/PluginYourGames/Modules/Authorization/Scripts/GetPlayerYG.cs:25), [`GetPlayerYG.cs:35`](Assets/PluginYourGames/Modules/Authorization/Scripts/GetPlayerYG.cs:35), [`EventsYG2.cs:192`](Assets/PluginYourGames/Scripts/Other/EventsYG2.cs:192). Без `autoDefineSymbols = 0` define возвращается на каждом refresh и критерий §6.4 недостижим. Стойкость подтверждается refresh'ем + домен-релоадом: grep по `ProjectSettings` = 0.

### 6.3. Решения по языку — ЗАФИКСИРОВАНЫ

**EveryGameLaunch (статус-кво):** в рамках миграции поведение сохраняется — `LanguageService` воспроизводит его (читает язык платформы и применяет при каждом запуске). `setLanguageMod` при выключенном модуле инертен; отсутствие двойного применения проверяется.

**D1 — фолбэк `tr/zh/pt → ru` (решение продукта, не «статус-кво»).** Это осознанное отклонение от поведения удаляемого `CorrectLang`: неподдерживаемый код → дефолт `ru` (первичная аудитория). Триггер пересмотра — добавление локали `tr` (таблица §6.1 уже готова). Приёмка «без регрессий» трактуется с учётом D1.

**Ручной выбор (будущая фича):** явный выбор игрока отключит автоопределение навсегда (`DoNotChangeLanguageStartup`). Реализация — с экраном Настроек, вне милстоуна.

### 6.4. Критерии приёмки

- [ ] Холодный старт на ЯИ с языками ru/en → корректная локаль; с неподдерживаемым → ru (D1);
- [ ] RuStore/itch/editor — локаль от `systemLanguage` с тем же фолбэком;
- [ ] Видимой вспышки языка при старте нет (C2); watchdog срабатывает корректно (тест с искусственной задержкой init — опционально);
- [ ] Ноль ссылок на `AstroDriftLanguageBridge`, `YG2.onSwitchLang`, `YG2.lang` в `Assets/Scripts` и `_Platform` (единственное чтение кода языка — внутри `LanguageService` через источник G1);
- [ ] RU/EN визуально без изменений (ин-движок, Play Mode, активный профиль RuStore).

---

## 7. Задача 4. `TypographyConfig` v2 + роли

### 7.1. Шаги

1. В `TypographyConfig`: поле оверрайда `langCode` → `localeCode` (код `LocaleIdentifier.Code`); слоты и фолбэк-цепочка без изменений. Существующий ассет (оверрайды пусты) продолжает работать.
2. В `Typography`: источник языка — `LocalizationSettings.SelectedLocale.Identifier.Code` (**сделан в задаче 3, §6.2 — здесь только подтверждается и подписывается на `SelectedLocaleChanged`**).
3. **Удалить цепочку `LanguageChanged`:** событие `Typography.LanguageChanged` + `NotifyLanguageChanged()` (единственный инициатор — удаляемый мост) + подписчика в `GameUI` (`OnEnable`/`OnDisable`) + метод `GameUI.ApplyTypography` (5 ручных применений переезжают на теги, §8.1/§8.2).
4. Создать `TypeRoleTag` (с self-apply в `OnEnable`, N2) + `TypeRoleApplier` (§3.5, новый файл `Assets/Scripts/UI/TypeRoleTag.cs`).
5. `LanguageService`: по `SelectedLocaleChanged` → `TypeRoleApplier.ApplyAll()` (+ сброс гардов §3.4). Стартовое применение идёт через `OnEnable` тегов — отдельного вызова не нужно.
6. Настроить TMP Font Asset **Fallbacks** базового шрифта под будущий CJK (инфраструктура; сам CJK-шрифт — вне скоупа).

### 7.2. Критерии приёмки

- [x] Смена локали переключает шрифты тегированных нод (проверка: временный оверрайд на тестовом шрифте, затем откат);
- [x] Нетегированные ноды (HUD и др.) не меняются; пустые оверрайды → базовые слоты; пустые слоты → LiberationSans;
- [x] Ноль ссылок на YG2, `LanguageChanged`, `NotifyLanguageChanged` в UI-слое.

---

## 8. Задача 5. Компонентная миграция

### 8.0. Правило владения (B4)

LSE и `TypeRoleTag` вешаются **там, где нода создаётся** (источник истины). Карта владельцев — §8.1 (билдеры префабов) и §8.2 (код сцены). Навешивание на найденные ноды чужого владельца — запрещено.

### 8.1. Билдеры префабов (полная карта — B4: блок StartPanel добавлен)

| Билдер | Нода | LSE-ключ | Роль (`TypeRoleTag`) |
|---|---|---|---|
| MenuButton-варианты | `Label` | `menu_settings` / `menu_upgrade` / `menu_shop` | Cta (1:1 с текущим) |
| LevelCard | `LevelLabel` | `pilot_level_label` | Button (1:1) |
| StartPanel | `StartBest` | `best_label` | Secondary (1:1 с `GameUI.ApplyTypography`) |
| StartPanel | `StartBestValue` | `best_value` (+ `Arguments`, гард §3.4) | Secondary (тот же слот, что запечённый шрифт → нулевой дифф) |
| StartPanel | `CtaText` | `tap_to_play` | Cta (1:1) |
| StartPanel | `ShieldText` | `shield_cta` | Cta (тот же слот → нулевой дифф) |
| StartPanel | `ShieldCaption` | `shield_caption` ↔ `shield_used_today` (рантайм-смена entry, см. ниже) | Secondary (тот же слот → нулевой дифф) |
| LevelUp-панель | `LevelUpTitle` / `LevelUpChoose` / `RerollText` / `RerollCaption` / `NewBadge` | `levelup_title` / `levelup_choose` / `reroll_cta` / `reroll_caption` / `levelup_new` | LevelUpTitle / Secondary / Cta / Secondary / Cta (1:1) |
| Карты перков | `Title` / `Description` | Пустой LSE (ссылку присвоит `FillCard`, задача 6) | Cta / Body (по запечённым шрифтам → нулевой дифф; применит `OnEnable` тега при `Instantiate` — N2) |

Двухсостоятельный `ShieldCaption` (пропуск ревью): ветка `usedToday` в `GameUI.RefreshPilotBlock` (`GameUI.cs:542`), сегодня пишущая `.text =` напрямую (нарушение §3.6), переводится на рантайм-смену `TableEntryReference` (`shield_caption` ↔ `shield_used_today`) + `RefreshString()`. Логика цвета/интерактивности — без изменений.

После правок: `Build Menu Prefabs` → `Build LevelUp Prefabs` → `Setup Scene UI`, проверить связи. Сериализованные ссылки `GameUI` на LSE (или `GetComponent` рядом с существующими ссылками на TMP) — на усмотрение исполнителя, зафиксировать в коммите.

### 8.2. `AstroDriftSceneSetup` — только ноды, создаваемые кодом

Статика (LSE, **без тегов** — ролей у этих нод не было, шрифт остаётся текущим дефолтом TMP, §8.4):

| Нода | Ключ |
|---|---|
| `PauseTitle` | `pause_title` |
| `Btn_Resume/Text` | `resume` |
| `PausePanel/Btn_Home/Text` | `home` |
| `DeathPanel/Btn_Home/Text` | `home` |
| `ContinueText` / `ContinueCaption` | `continue_cta` / `continue_caption` |
| `DeathNewBest` | `new_best` (+ тег Secondary — роль есть в `GameUI.ApplyTypography`) |

> Обе `Btn_Home` — строго в скоупе своей панели (ссылки в `GameUI` берутся через
> `transform.Find` от панели, коллизии нет): LSE вешается на ноду внутри
> соответствующей панели, не глобальным поиском по имени.

Динамика через `Arguments` (холодный путь):

| Нода | Ключ | Тег | Обновление |
|---|---|---|---|
| `DeathScore` | `score` | DeathScore (1:1) | `PlayDeathIn` |
| `DeathBest` | `best` | Secondary (1:1) | `PlayDeathIn` |
| `DeathXp` | `xp_gain` | — (роли не было) | `FillDeathMeta` |
| `DeathLevel` | `level_up_line` ↔ `level_line` (рантайм-смена entry) | — (роли не было) | `FillDeathMeta` |

`DeathUnlocked` — склейка → синхронный `L10n` по §3.3. Метод `BindPauseTexts()` (`GameUI.cs:276`) удаляется — привязка переезжает в момент создания нод.

### 8.3. Критерии приёмки

- [x] Все экраны RU/EN визуально как было (ин-движок, Play Mode на активном профиле RuStore, вкл. паузу и оверлей перка); — **с оговоркой:** визуальная дельта RU↔EN снята «по построению» (плейсхолдер-конфиг, см. «Клоузаут задачи 5» → риск конфига). Механическая часть доказана пофайлово: шрифты префабов/сцены == родительские (см. §8.3 п.4);
- [x] Смена локали посреди сессии обновляет все компонентные тексты (включая паузу, оверлей перка, подпись щита в обоих состояниях); — перенесённый temp-override тест §7.2 (решение продюсера, п.2б) выполнен **на реальных тегах**: тегированные ноды переприменены, нетегированные (`§8.4`) сохранили `LiberationSans SDF` (см. «Клоузаут задачи 5» → перенесённый тест);
- [x] `RefreshHud` не аллоцирует на каждое изменение счёта (гард §3.4 + сброс при смене локали); — [`GameUI.cs:55`](Assets/Scripts/UI/GameUI.cs:55) `readonly object[] _bestArgs = new object[1]` (кэш создаётся один раз), [`GameUI.cs:1141`](Assets/Scripts/UI/GameUI.cs:1141) ранний выход по гарду, [`GameUI.cs:1158`](Assets/Scripts/UI/GameUI.cs:1158) сброс `_lastBestShown` из `ResetLanguageGuards()`, вызываемого из [`LanguageService.cs:78`](Assets/Scripts/Core/LanguageService.cs:78);
- [x] **Ноды, впервые получающие `TypeRoleTag` (`Title`/`Description` карт перков), не меняют начертание:** `Typography.ApplyFont` выставляет `fontStyle = FontStyles.Normal`, а карты раньше не проходили через апплаер вообще. Запечённые шрифты совпадают с ролевыми (`Cta` = `ctaSemiBold`, `Body` = `bodyRegular`), ожидаемый дифф — нулевой; отличие = провал приёмки; — **доказано диффом родитель↔коммит:** [`UpgradeCard.prefab`](Assets/Prefabs/LevelUp/UpgradeCard.prefab) `Title`/`Description` — `m_fontAsset` = `2ebd00df…`/`20fb9121…` и `m_fontStyle: 0` совпадают байт-в-байт с коммитом `693787e` (верификация Team Lead);
- [x] Ноль `L10n.Bind` в проекте; **`LocalizedTextUI` удалён в этой задаче** (единая отсечка — §15). — `grep L10n.Bind` = 1 (док-комментарий [`LocalizedText.cs:10`](Assets/Scripts/UI/LocalizedText.cs:10), вызовов 0); `LocalizedTextUI` = 0 вхождений в `Assets/` (`.cs`/`.prefab`/`.unity`), файл + `.meta` удалены в коммите.

### 8.4. Ноды без тегов — явный список (нулевой дифф)

Без `TypeRoleTag` остаются (шрифт — текущий дефолт TMP, как сейчас): тексты паузы, `ContinueText`/`ContinueCaption`, `Home`-подписи, `DeathXp`, `DeathLevel`, `DeathUnlocked`, HUD (`scoreText`, `comboChip`). Назначение им ролей — отдельное дизайн-решение **вне миграции**.

---

## 9. Задача 6. `LocalizedString` в SO

### 9.1. `PerkDefinition`

Заменить сырые `string titleKey/descKey` на поля:

```csharp
public LocalizedString title; // Table: GameTexts, Entry: perk_*_title
public LocalizedString desc;
```

Порядок (важен): **сначала** разовый editor-скрипт читает старые `titleKey/descKey` из 8 ассетов через `SerializedObject` и проставляет `TableEntryReference`, сохраняет; **потом** строковые поля удаляются из класса. `FillCard` — только вариант А (§3.2).

**`PerkTitle` (пропуск ревью):** `PerkChoiceUI.PerkTitle` (`PerkChoiceUI.cs:284`, вызов `:280` для `FloatingTextPool`) читает `def.titleKey` — после удаления поля это ошибка компиляции. Перевести на `def.title.GetLocalizedString()` с фолбэком на `def.id.ToString()` (проверка — `string.IsNullOrEmpty`, не `== null`: пустая ссылка даёт `""` — N3; синхронное чтение для флоатера — §3.3).

### 9.2. `AstroDriftSetup` — ОБЯЗАТЕЛЬНО вместе с §9.1

- Seed-таблица (`AstroDriftSetup.cs:160`) и присваивание (`:192`) переводятся на простановку `LocalizedString`-ссылок;
- Legacy-парсер (`:253`) маппит распарсенные ключи в новые поля;
- Повторный прогон `AstroDrift → Setup Assets` идемпотентен (не затирает ссылки).

### 9.3. `PickupDef` — новое поле

Сейчас строкового поля нет вообще (имя собирается `PickupNameKey(type)`, `PickupManager.cs:228`):

1. Добавить `public LocalizedString name;` в `PickupDef`;
2. Проставить ссылки в `AstroDriftSetup` (там же, где создаются дефы пикапов) — `pickup_rapid_fire` / `pickup_spread_shot` / `pickup_shield`;
3. `PickupManager`: флоатер — `def.name.GetLocalizedString()` с фолбэком на `type.ToString()` (проверка — `string.IsNullOrEmpty`, N3); метод `PickupNameKey` удалить.

### 9.4. Критерии приёмки

- [x] 8 ассетов перков + 3 дефа пикапов имеют заполненные ссылки (видно в инспекторе с превью); — пересчёт по факту: `m_TableCollectionName: GameTexts` = **2** в каждом из 8 перков (**16**) + **3** в [`PickupConfig.asset`](Assets/Resources/PickupConfig.asset) = 19 ссылок; ключи не переименованы (`perk_*_title`/`perk_*_desc`, `pickup_rapid_fire`/`_spread_shot`/`_shield`);
- [x] `Setup Assets` повторным прогоном ничего не меняет; — **перепроверено Team Lead независимым прогоном:** `AstroDrift Perks: ассетов создано 0 из 8`, `git status --porcelain` пусто, `git diff` по `Assets/Resources/Perks` + `PickupConfig.asset` пуст (байт-в-байт);
- [x] Карты перков (включая дубли при reroll) и флоатеры показывают корректные тексты RU/EN; — Play Mode (RuStore): 8 карт `ru` = `СКОРОСТЬ ПУЛЬ+`/`КРУПНЫЕ ПУЛИ`/…, `en` = `BULLET SPEED+`/`BIGGER BULLETS`/…, `CYRILLIC=0 LATIN=8`; бейдж `НОВОЕ`/`NEW`; reroll-дубли (3× `BulletSpeed`) — 3 корректные карты, одна общая ссылка (`ReferenceEquals=True`); флоатеры — 11 значений RU
- [x] Смена локали при открытом оверлее обновляет карты (владелец подписки — компонент); — `ru → en` при открытом оверлее: 8/8 карт перешли в латиницу (`CYRILLIC=0 LATIN=8`); владелец — `LocalizeStringEvent` (вариант А, §3.2), `StringChanged` вручную не подписывается
- [x] Ноль чтений `titleKey` / `descKey` в проекте. — grep по `Assets/**` (`.cs`/`.asset`/`.prefab`/`.unity`): **0**; 11 вхождений остались только в [`Localization_TZ.md`](Assets/Docs/Localization_TZ.md) (описание задачи, не код)

---

## 10. Задача 7. Ужатие `L10n` до ридера

### 10.1. Шаги

1. Оставить только `Get(key)` + `GetFormatted(key, args)` (~20 строк). Контракт: `Get` возвращает `null`, `GetFormatted` — `null`; все вызывающие проверяют через `string.IsNullOrEmpty` (единый контракт с `GetLocalizedString`, дающим `""` — N3).
2. Удалить `Bind`, список `_bindings`, `RefreshAll`, подписку на `onBeforeRender` и `SelectedLocaleChanged`.
3. Оставшиеся call sites (§3.3): `FillUnlockTree`, комбо-флоатер, `DeathUnlocked`. (Перк- и пикап-флоатеры уже переведены на `GetLocalizedString` в задаче 6.)
4. `FillUnlockTree()`: подписать перерисовку на `SelectedLocaleChanged`, если панель видима.

### 10.2. Критерии приёмки

- [ ] Ноль `L10n.Bind` и `Application.onBeforeRender` в `Assets/Scripts`;
- [ ] Дерево разблокировок корректно на RU/EN и перерисовывается при смене локали;
- [ ] **Заголовок дерева резолвится** — `unlock_tree_title` даёт текст на RU/EN; подтверждено **прямым вызовом `FillUnlockTree()`** (кнопка «ПРОКАЧКА» не подключена — пост-милстоун дефект P1) + Play Mode. **Закрывает наблюдение задачи 6** по `UnlockTreePanel/Title` (санкция продюсера, задача 6, п.1);
- [ ] Флоатеры корректны; поведение при неготовой таблице — фолбэк, не исключение;
- [ ] **Cleanup–7 выполнен:** мёртвое поле `shieldTextLse` удалено; док-комментарий `L10n.Bind` снят (`Assets/Scripts/UI/LocalizedText.cs:10`) — [распределение cleanup](Assets/Docs/Localization_TZ.md:933).

---

## 11. Задача 8. Валидатор + чистка

### 11.1. Чистка сироты

Удалить строку id `1584525502046248` («ДЕРЕВО») из `GameTexts_ru` и `GameTexts_en` (после бэкапа таблиц). Независимо от остального — можно делать в любой момент.

### 11.2. Missing translations report

Editor-меню, собирающее множество используемых ключей **не из `Entries`** (он неполон, §1.6), а из:

1. всех `LocalizeStringEvent` в сценах и префабах (`TableEntryReference`, вкл. рантайм-смены entry из §8.1/§8.2 — статическим списком);
2. `LocalizedString`-полей данных — **явным перечислением** (`PickupDef` — вложенный `[Serializable]`-класс, не SO): `Resources/Perks/*.asset` (`PerkDefinition.title/desc`) + `Resources/PickupConfig.asset` (`pickups[].name`) + `Resources/PerkConfig.asset`. Новые типы SO с `LocalizedString` регистрируются в валидаторе одной строкой;
3. `PilotProgressManager.AllUnlocks` → `unlock_<id>` + `unlock_soon` (рантайм-ключи, иначе ложные пропуски);
4. белого списка синхронных ключей (`combo`, `unlocked_title`, `level_up_line`, `level_line`, … — по §3.3);
5. ключей, проставляемых `AstroDriftSceneSetup` (статический список = карта §8.2).
6. **Coverage чарсета (от рецидива B1):** все кодпоинты всех строк таблиц обязаны существовать в назначенных Static-шрифтах (проверка через таблицу символов Font Asset'а). Новые символы без обновления чарсета — варн валидатора.

Отчёт: ключи без перевода в любой локали + ключи таблицы без использования (кандидаты в сироты) + непокрытые чарсетом символы.

### 11.3. Критерии приёмки

- [ ] Отчёт зелёный на RU/EN (0 missing, 0 сирот после чистки, 0 непокрытых символов);
- [ ] Валидатор варнит при ключе без перевода и при символе вне чарсета (проверка временными кейсами).

---

## 12. Порядок, зависимости и параллельность

```
1 (SDF) ──────────────────────────────────► anytime, первая
§11.1 (сирота) ───────────────────────────► anytime
2 (снос + G1 + фикс инсталлера) ──► 3 (Service + C1–C3) ──► 4 (конфиг + теги)
        ──► 5 (компоненты; приёмка после 4: теги/апплаер) ──► 6 (SO; приёмка после 5: LSE на картах)
                ──► 7 (L10n; после 5 и 6) ──► §11.2 (валидатор; последний)
```

**Правило приёмки милстоуна:** задачи закрываются строго по порядку 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8. Параллельная *работа* разрешена (подготовка 5/6 не ждёт 4), параллельная *приёмка* — нет. Исправление к v2: «5 независима от 2–4» было неверно (B5) — приёмка 5 требует готовых тегов и апплаера из 4.

---

## 13. Риски

| # | Риск | Митигация |
|---|---|---|
| R1 | Источник языка ЯИ недоступен/неясен (подтверждён жёстче: см. §5.2) | Обязательный гейт G1 до задачи 3; ранжированные варианты зафиксированы |
| R2 | Двойное применение языка (YG2 + `LanguageService`) | Модуль выключен → инертен; проверка отсутствия второго применения |
| R3 | Dynamic-атлас растёт в рантайме до задачи 1 | Задача 1 первая; до неё — известный риск, не регрессия |
| R4 | Синхронное чтение на холодном старте WebGL вернёт пустоту (таблица грузится: `L10n.Get` → `null`, `GetLocalizedString` → `""`) | Единый контракт `string.IsNullOrEmpty` + фолбэк (§3.3); компонентные тексты догрузятся сами |
| R5 | Потеря ссылок при пересборке префабов | Билдеры + `Setup Scene UI` в одном коммите; правило владения §8.0; ин-движок сверка до/после (Play Mode) |
| R6 | Неидемпотентный `Setup Assets` затрёт `LocalizedString` | Приёмка §9.2 требует повторного прогона без изменений |
| R7 | Подписки-утечки на картах перков | Вариант подписки запрещён (§3.2); приёмка с многократными левелапами и reroll |
| R8 | Init локализации висит → старт игры висит (C2) | Watchdog C3: 5 с → дефолт `ru` + warning, старт не блокируется |
| R9 | `Docs/` целиком в `.gitignore` (`:12`) — ТЗ не под версионным контролем: риск потери/расхождения | **ЗАКРЫТ** (2026-09-18): ТЗ перемещено в [`Assets/Docs/Localization_TZ.md`](Assets/Docs/Localization_TZ.md) и добавлено в git (коммит `384a718` + `.meta` `27ed699`, ветка `feature/localization-migration`). Эталон «до» — тег `baseline-loc-migration` |
| R10 | Переключение build-профилей в редакторе побочно **перезаписывает снапшот `m_ScriptingDefines`** в `*.asset` профиля: теряются собственные токены профиля и добавляются чужие. Сработало фактически в задаче 2: Developer поймал на [`ItchIO.asset`](Assets/Settings/Build%20Profiles/ItchIO.asset) — потерян `EmptyWebGLPlatform_yg`, добавлены `YandexMobileAdsPlatform_yg;Localization_yg`; откачено `git checkout --` | Правило для задач 3–8: **после каждого переключения профиля ин-движком** сверять `git status --porcelain` по `Assets/Settings/Build Profiles/` и откатывать нежелательные правки defines через `git checkout --`. Приёмка задачи не считается пройденной, если в коммит попали побочные изменения снапшотов профилей |
| R11 | **`autoDefineSymbols = 0` закрывает только автоматический путь.** [`DefineSymbols.ModulesDefineSymbols()`](Assets/PluginYourGames/Scripts/EditorScr/DefineSymbols.cs:226) **не проверяет** `AutoDefinesEnabled()` — прямой вызов возвращает `<Folder>_yg` для всех папок `Modules/*`. Вызовы есть в трёх местах окна YG2: [`VersionControlWindow.cs:93`](Assets/PluginYourGames/Scripts/Server/Editor/VersionControlWindow.cs:93), [`VersionControlWindow.cs:708`](Assets/PluginYourGames/Scripts/Server/Editor/VersionControlWindow.cs:708), [`ModuleQueue.cs:78`](Assets/PluginYourGames/Scripts/Server/Editor/Utils/ModuleQueue.cs:78) | Проверено фактом (прямой вызов вернул `Localization_yg` на 11 таргетов). Митигация: после любого ручного импорта/обновления модулей через окно YG2 — повторный grep `Localization_yg` по `ProjectSettings` и повторное снятие define. Define живёт в committed `ProjectSettings.asset` (влияет на редакторскую компиляцию и будущие профили), но **не** на собранные профили: их собственный `m_ScriptingDefines` токен не содержит |

---

## 14. Приёмочная матрица

| Проверка | RuStore (активный профиль) | Editor | ЯИ WebGL (отложено, F5) |
|---|---|---|---|
| **Финальный обход экранов RU/EN — явным списком** (старт / смерть / пауза / оверлей перка / дерево разблокировок × RU + EN) — решение продюсера по задаче 6, п.2 (закрывает долг 4д) | + | + | отложено |
| Холодный старт ru/en → верная локаль | + (systemLang) | + | отложено |
| Неподдерживаемый язык → ru (D1) | + | − | отложено |
| Все экраны RU/EN без визуальных отличий (вкл. B1-строки) | + | + | отложено |
| Смена локали mid-session (временный дебаг-хук) | − | + | − |
| **Метрики SDF до/после** (ин-движок: размер, PopMode, MultiAtlas, атлас, Point Size, глифы, Padding) | + | + | − |
| Валидатор зелёный (ключи + чарсет) | − | + | − |
| Ноль grep-нарушений §15 | − | + | − |

> Билды не собираются (F5): «вес» меряется ин-движком как метрики Font Asset'а, а не байтами
> собранного игрока. Арифметика дельты файла по-прежнему не считается доказательством —
> 26.65 МБ YAML-таблиц жмутся, поэтому в приёмке задачи 1 основание — метрики ассета
> (base: 34.66 МБ, Dynamic, multiAtlas on, 2048², PS 90, 99 глифов — см. §20).

## 15. Grep-контроль «нулевых ссылок»

```bash
# Ноль вхождений в Assets/Scripts + _Platform + сцены + префабы (после задачи 7;
# LocalizedTextUI — уже после задачи 5, единая отсечка):
L10n.Bind   LocalizedTextUI   AstroDriftLanguageBridge
YG2.onSwitchLang   YG2.lang   onCorrectLang
LanguageYG   CorrectLang   AutoTranslateLangs   fontsTMP
Application.onBeforeRender   LanguageChanged   NotifyLanguageChanged
titleKey   descKey
```

Исключение: чтение кода языка платформы — только внутри `LanguageService` через источник гейта G1.

Вне `Assets/` (проверяется grep'ом по `ProjectSettings/ProjectSettings.asset`):

```bash
# После задачи 2 — ноль вхождений:
AutoTranslateLangs_yg
# После задачи 3 — ноль вхождений:
Localization_yg
```

---

## 16. Журнал решений (v1 → ревью → v2) — проверен ревью v2, расхождений нет

| # | Утверждение v1 | Коррекция ревью | Решение v2 |
|---|---|---|---|
| 1 | SDF тяжёлый из-за «гигантского чарсета» | 99 глифов; вес = кернинг 26.65МБ + атлас 2048 + Dynamic/multiAtlas | Диагноз исправлен; пересборка — задача 1 |
| 2 | «Коды YG2 ≠ коды Unity» | Для ru/en/tr совпадают (ISO 639-1) | Маппинг — только задел под региональные коды |
| 3 | «Вынести YG2 из ядра» | YG2 — рантайм всех платформ | Цель: забрать у модулей владение языком; рантайм не трогаем |
| 4 | «Авто-перевод замусорит таблицы» | Пишет в поля компонентов, не в таблицы | Проблема = параллельное хранилище + второй владелец языка |
| 5 | Стаб `menu_button/settings` | Это analytics id; ключ — `menu_settings` | Исправлено |
| 6 | Экран выбора языка — шаг миграции | Экрана нет — это новая фича | Вынесено из скоупа |
| 7 | «Вывести `L10n` полностью» | Держат флоатеры пула и `_treeText` | Тонкий ридер `Get`/`GetFormatted` |
| 8 | `_treeText` не покрыт | 24 ключа через StringBuilder | Синхронный `L10n` + refresh при смене локали |
| 9 | Пауза не упомянута | Ноды создаются кодом в `AstroDriftSceneSetup.cs:374` | Компонент вешается при создании |
| 10 | `AstroDriftSetup` не упомянут | Перезапишет перки; у `PickupDef` нет поля | Правка сетапа + миграция + новое поле |
| 11 | Варианты А/Б как равнозначные | Б = утечки подписок на ассете | Вариант Б запрещён |
| 12 | `Arguments` без оговорок | Аллокация на горячем пути `RefreshHud` | Гарды + кэш |
| 13 | Выбор игрока выше платформы | Противоречит EveryGameLaunch | Статус-кво в миграции; явный выбор отключит автоопределение (с экраном Настроек) |
| 14 | «Выключить и удалить» без решения | CorrectLang — второй владелец; fontsTMP vs конфиг | Удалить целиком до `LanguageService`; шрифты — конфиг v2 |
| 15 | Ссылки на сторонние сайты; `LocalizedTmpFont` не проверен | Нужна рефлексия в редакторе | Вариант Asset Tables убран; вариант А не зависит от API |

---

## 17. Журнал правок (v2 → ревью v2 → v2.1)

### Блокеры

| # | Проблема | Правка v2.1 |
|---|---|---|
| B1 | Чарсет §4.2 не покрывает «— → −» из таблиц → Static-атлас дал бы квадраты (сейчас рендерится только благодаря Dynamic) | §4.2: добавлены `2010-2027, 2190-2199, 2212`; §4.3: B1-строки в скриншотах приёмки; §11.2 п.6: coverage чарсета в валидаторе (рецидив исключён процессом) |
| B2 | Padding не зафиксирован (приёмка «без изменений» недостижима); 512 не влезет | §4.1: `Padding = 9` явно; старт с 1024×1024 |
| B3 | `YandexGamesInstaller.cs:35` читает `YG2.lang` без гарда → ошибка компиляции профиля при выключении модуля | §5.1 п.5: замена на источник `LanguageService` + grep по `_Platform`; приёмка §5.3: компиляция всех профилей |
| B4 | Владение нодами: `CtaText`/`StartBest*`/`Shield*` создаёт `MenuPrefabBuilder.BuildStartPanel`, не `AstroDriftSceneSetup`; LSE из сетапа сотрётся пересборкой префаба | §3.1 + §8.0: правило владения (компоненты — в источнике истины); §8.1: полная карта билдеров вкл. блок StartPanel; §8.2: только кодовые ноды Death/Pause |
| B5 | Потеря ролей: `LocalizedTextUI` — единственный апплаер `ApplyFontOnly`; задача 4 стала бы инертной; «5 независима от 4» — неверно | §2 п.5 + §3.5: `TypeRoleTag` + `TypeRoleApplier` (триггер — `LanguageService`); роли 1:1; §12: приёмка 5 после 4 |

### Пропуски

| # | Проблема | Правка v2.1 |
|---|---|---|
| G1 | `PerkChoiceUI.PerkTitle` (`:284`) читает `def.titleKey` → ошибка компиляции после §9.1 | §9.1: перевод на `def.title.GetLocalizedString()` + фолбэк; §3.3: строка в таблице; §9.4/§15: ноль `titleKey`/`descKey` |
| G2 | `ShieldCaption` двухсостоятельна: ветка `usedToday` пишет `.text` напрямую (нарушение §3.6) | §8.1: рантайм-смена `TableEntryReference` (`shield_caption` ↔ `shield_used_today`); приёмка обоих состояний |
| G3 | Гард `_lastBestShown` блокирует обновление при смене локали (противоречие §3.4/§8.3) | §3.4: сброс гарда по `SelectedLocaleChanged` (владелец — `LanguageService`) |
| G4 | `Typography.LanguageChanged` мёртв после сноса моста (инициатор — мост, подписчик — `GameUI`) | §3.5/§7.1: цепочка удаляется целиком; триггер апплаера — `LanguageService`; §15: grep-контроль |
| G5 | Контракт инициализации не записан (ждать `Completed`; отработать до `Bootstrap.Build` по `PlatformBoot.Ready`) | §6.1: контракт C1–C3 (подписка + флаг + ожидание в `Bootstrap` + watchdog 5с); риск R8 |
| G6 | Фолбэк `tr/zh/pt → ru` — отклонение от `CorrectLang`, не «статус-кво» | §6.3: решение продукта D1 + триггер пересмотра (локаль `tr`); §0.3/§14 трактуют приёмку с учётом D1 |
| G7 | R1 жёстче: envir = браузер, не аккаунт; `environmentData` нет в `.jslib` проекта | §5.2: обязательный гейт G1 с ранжированными вариантами (свой `.jslib` / браузер); `systemLanguage` — основной путь RuStore/itch |
| G8 | Разные отсечки удаления `LocalizedTextUI` (§8.3 vs §15) | Единая отсечка: удаление в задаче 5 (§8.3, §15, Приложение B) |
| G9 | `PickupDef` — вложенный класс, не SO: «сканирование SO рефлексией» не покрывает | §11.2 п.2: явное перечисление ассетов + регистрация новых типов одной строкой |
| G10 | «−30 МБ» — гипотеза (YAML жмётся); RuStore стоял «−» по весу | §14: замер до/после на WebGL и RuStore; §4.3 ссылается на замер |

---

## 18. Журнал правок (v2.1 → ревью v2.1 → v2.2)

### Новые дефекты

| # | Проблема | Правка v2.2 |
|---|---|---|
| N1 | Задача 1 численно несовместима: ~450 глифов + Padding 9 + лимит 1024² «не больше» + незафиксированный Point Size (текущий 90; ячейка ~108px → влезает ~200–250). Три критерия §4.3 взаимно исключающие | §4.1: `Sampling Point Size = 60` явно (опора — Bold@59: 220 глифов в 1024×512) + лестница фолбэков (1024² → 2048×1024 с записью причины; дроп глифов запрещён); §4.3: первый критерий — 0 failed glyphs; даунсемпл 90→60 — ожидаемый нулевой дифф, артефакты = провал |
| N2 | `TypeRoleApplier`: одноразовый свип не видит рантайм-карты перков (создание/уничтожение каждый левелап) → строка карт в §8.1 мёртвая | §3.5: self-apply в `TypeRoleTag.OnEnable()`; свип — только для смены локали; §7.1 п.4–5 обновлены |
| N3 | «Null-толерантность» — несуществующий контракт: `GetLocalizedString()` даёт `""`, не `null` | Единый контракт `string.IsNullOrEmpty` (§3.3, §9.1, §9.3, §10.1, R4) |

### Мелочи

| # | Проблема | Правка v2.2 |
|---|---|---|
| M1 | §12 «8.1 (сирота)» — коллизия с §8.1 (билдеры) | Перенумеровано на §11.1/§11.2 |
| M2 | §5.2 предлагал писать новый `.jslib` | Перенос существующего `LangRequest_js` из `Language.jslib` (ноль нового кода) |
| M3 | «Render Mode: SDF» — фактически SDFAA (`m_AtlasRenderMode: 4165`) | §4.1: SDFAA явно |
| M4 | `Btn_Home` дважды в §8.2 без скоупа | Панели указаны явно + пометка «в скоупе панели» |
| M5 | `Docs/` в `.gitignore` — ТЗ не версионируется | Риск R9 + действие до старта задачи 1 |

---

## 19. Журнал правок (v2.2 → финальная вычитка → v2.3)

| # | Проблема | Правка v2.3 |
|---|---|---|
| F1 | **Противоречие в критериях.** §6.4 требует ноль `YG2.lang` в `Assets/Scripts` после задачи 3, но это чтение живёт в `Typography.cs:47` (тоже `Assets/Scripts`), а его перенос §7.1 п.2 отнесён к задаче 4 → критерий задачи 3 недостижим | §6.2: перенос источника `Typography.CurrentLang` на `SelectedLocale.Identifier.Code` выполняется **в задаче 3**; §7.1 п.2 — подтверждение + подписка на `SelectedLocaleChanged`. Безопасно: `Identifier.Code` даёт `ru`/`en`, `TypographyConfig.GetFonts(langCode)` не меняется |
| F2 | Мёртвые scripting defines: `AutoTranslateLangs_yg` проставлен во всех билд-профилях и не имеет читателей; удаление не входило ни в одну задачу | §5.1 п.5: удалить `AutoTranslateLangs_yg`; §6.2: удалить `Localization_yg` после задачи 3; Приложение A: чек-боксы; §15: grep-контроль по `ProjectSettings`; Приложение B: строка на `ProjectSettings.asset` |
| F3 | §5.1 п.1–2 подавали работу, которой нет: grep даёт **0 вхождений** `LanguageYG`/`fontsTMP`/`AutoTranslateLangs` вне папки модуля (проверено на момент v2.3) | §5.1 п.1–2 переформулированы как подтверждение нуля, чтобы исполнитель не искал несуществующие компоненты |
| F4 | §8.3 не покрывал риск `fontStyle`: `Typography.ApplyFont` выставляет `FontStyles.Normal`, а `Title`/`Description` карт перков раньше вообще не проходили через апплаер — теперь получают `TypeRoleTag` | §8.3: добавлен критерий на начертание впервые тегированных нод (ожидаемый дифф нулевой: `Cta`=`ctaSemiBold`, `Body`=`bodyRegular`) |
| U1 | Сниппет `TypeRoleTag` не фиксировал имя поля `role`, хотя билдеры пишут через `SerializedObject.FindProperty("role")` | §3.5: комментарий в коде + пояснение про фиксированное имя поля |
| U2 | Приложение A названо «настройки YG2», но содержит решения уровня проекта (defines, D1) | Переименовано в «Финальное состояние настроек проекта» |

---

## 20. Журнал правок (v2.3 → финальное решение продукта → v2.4)

| # | Проблема | Правка v2.4 |
|---|---|---|
| F5 | Приёмка опиралась на артефакты, которые продукт не собирает: замер веса билдов WebGL/RuStore (§4.3, §14) и скриншоты «до/после» (§4.1, §4.3, §8.3, R5). Критерии недостижимы без сборки, а сборка не входит в текущий процесс — приёмка была бы формально красной при корректной работе | §0.2: билды и скриншоты вынесены из процесса. §4.1/§4.3/§6.4/§8.3: визуальная приёмка RU/EN → **Editor Play Mode на активном профиле RuStore (Android)**. §4.3/§14: «замер веса билда» → **метрики Font Asset'а ин-движком** (размер файла, Population Mode, MultiAtlas, атлас, Point Size, глифы, Padding). §5.3: «компиляция всех билд-профилей» → компиляция ин-движком при переключении активного профиля (не сборка); проверка G1 на ЯИ помечена «отложено». §13 R5: скриншоты → ин-движок сверка. Приложение A: G1 — «отложено» |
| F6 | Два критерия задачи 1 оказались структурно недостижимы и подтверждены фактом: (а) «размер ≤2 МБ» — Unity-YAML хранит Alpha8 ~2 Б/тексел, поэтому 1024² не влезает, 2048×1024 = 4.2 МБ; (б) «0 missing» — 8 кодпоинтов диапазона `2010-2027` есть в cmap TTF, но без аутлайнов, и таблицами не используются | §4.1 п.3: исправлена арифметика fallback (2–2.5 МБ → ≈4.2 МБ) + пометка, что primary 1024² тоже не проходит лимит. §4.2: зафиксирован **эффективный чарсет** (443 глифа) + трактовка критерия «0 missing среди требуемых кодпоинтов». §4.3: критерии приведены к измеримым. §20: результат задачи 1 записан. **Шаг 3 лестницы не применялся** — дроп глифов/PS>72/2048² остаются запрещёнными без решения продукта |

### Зафиксированный baseline задачи 1 (эталон «до», ин-движок, тег `baseline-loc-migration`)

| Метрика | Baseline SemiBold | Baseline Bold (опора §4.1) |
|---|---|---|
| Размер файла | **36 339 195 Б (34.66 МБ)** | 1 168 831 Б (1.11 МБ) |
| Population Mode | Dynamic (1) | **Static (0)** |
| Multi Atlas | **on** | off |
| Atlas | **2048×2048 A8** | **1024×512** |
| Point Size (face) | **90** | **59** |
| Padding | **9** | 5 |
| Глифов | **99** | **220** |
| Atlas Render Mode | 4165 (SDFAA) | 4165 (SDFAA) |
| Fallback-таблица | 0 | 0 |

Таблицы: `GameTexts_ru` = 76 записей / `GameTexts_en` = 76 / 0 пустых / 0 дубликатов id → 75 ключей + 1 сирота (§1.6). `Montserrat-SemiBold` занимает 3 слота из 4 (§1.4) — пересборка задачи 1 меняет 3 слота одновременно, поэтому ин-движок приёмка обязательна на всех экранах RU/EN.

### Результат задачи 1 (коммит `88448c4`, проверено Team Lead ин-движком)

| Метрика | До | После | Статус |
|---|---|---|---|
| Размер файла | 36 339 195 Б (34.66 МБ) | **4 431 136 Б (4.23 МБ)** | ✅ шаг 2 лестницы, причина записана (F6) |
| Population Mode | Dynamic (1) | **Static (0)** | ✅ |
| Multi Atlas | on | **off** | ✅ |
| Атлас | 2048×2048 Alpha8 | **2048×1024 Alpha8**, 1 текстура | ✅ (шаг 2) |
| Point Size | 90 | **60** | ✅ |
| Padding | 9 | **9** | ✅ не менялся |
| Render Mode | 4165 (SDFAA) | **4165 (SDFAA)** | ✅ |
| Глифов | 99 | **443** (441 упакованных + служебные) | ✅ |
| Kerning-пары | ~26.65 МБ таблиц | **0** | ✅ |
| Fallback-таблицы | 0 | 0 | — (§7.1 п.6 настраивается с нуля) |
| GUID ассета | `20fb9121a3c3043a4ad672971d76f0c0` | **тот же** | ✅ ссылки не потеряны |
| Кодпоинты таблиц, отсутствующие в шрифте | 0 (Dynamic добирал в рантайме) | **0** | ✅ *проверено Team Lead* |

Изменён **ровно один** файл: [`Assets/Fonts/Montserrat-SemiBold SDF.asset`](Assets/Fonts/Montserrat-SemiBold SDF.asset) (+11 065 / −1 009 944 строк YAML). Ссылки живы: Material-под-ассет тот же fileID, 14 ссылок в 6 префабах + 3 в `TypographyConfig.asset`.

> **Открытый долг, вскрытый задачей 1 (решение — за продуктом).** Ноды Death-экрана и части HUD висят
> **не** на `Montserrat-SemiBold`, а на `LiberationSans SDF`: ин-движок показал 29 `TMP_Text`, из них
> `Montserrat-SemiBold` = 13, `LiberationSans` = 15, `Montserrat-Bold` = 1. На LiberationSans сидят
> в т.ч. `DeathLevel` (`level_up_line` — именно строка с `→` из B1), `DeathUnlocked`, `DeathScore`,
> `DeathBest`, `ContinueText/Caption`, `DeathXp`. Следствие: **задача 1 не может визуально закрыть
> B1 для этих строк** — там `→` рисует LiberationSans, а не пересобранный SemiBold.
> Это не регрессия (было так же до пересборки). Дополнительно: `Montserrat-Bold` без 46 кодпоинтов
> из 120 используемых — тоже существующее состояние. Требуется отдельное решение: переназначить ноды
> на SemiBold (вне Приложения B) или принять как есть. К задачам 2–8 отношения не имеет.

> Примечание к C1: на момент baseline `LocalizationSettings.SelectedLocale` = `ru`, но `AvailableLocales.Locales.Count` = **0** — список локалей пуст до `InitializationOperation.Completed`. Это подтверждает требования C1 задачи 3, а не дефект таблиц.

### Результат задачи 2 (коммиты `423f587` + `eea4f4d`, проверено Team Lead ин-движком)

| Пункт §5 | Факт | Статус |
|---|---|---|
| §5.1 п.1–2 (grep-подтверждение нуля) | `LanguageYG` / `fontsTMP` / `AutoTranslateLangs` вне папки модуля = 0; сцены и префабы = 0 | ✅ подтверждено |
| §5.1 п.3 (модуль выключен) | `Modules/AutoTranslateLangs` удалён целиком (26 файлов + `.meta`); `AutoTranslateLangs` убран из [`PluginPrefs.json`](Assets/PluginYourGames/Editor/PluginPrefs.json:5) (`SelectModuleToggle_YG2`) и из [`ModulesListYG2.txt`](Assets/PluginYourGames/Editor/ModulesListYG2.txt:1); `Localization` в списке сохранён | ✅ **потребовалась доработка** (реестр выбранных модулей удерживал возврат модуля при `Basic.autoDefineSymbols: 1`) |
| §5.1 п.4 (условие удаления) | Внешних ссылок нет. Ссылки [`GetPlayerYG.cs:67/69/81/83`](Assets/PluginYourGames/Modules/Authorization/Scripts/GetPlayerYG.cs:67) указывают на `UtilsLang` из `Modules/Localization` — компиляция не разорвана | ✅ проверено |
| §5.1 п.5 (define `AutoTranslateLangs_yg`) | В `ProjectSettings.asset` = **0**; `Localization_yg` = 11 платформ сохранён (снимает задача 3) | ✅ |
| §5.2 G1 (источник языка, вариант A) | [`YandexLanguage.jslib`](Assets/_Platform/YandexGames/YandexLanguage.jslib:3) — тело идентично [`Language.jslib:3`](Assets/PluginYourGames/Modules/Localization/Plugins/Language.jslib:3) (`ysdk.environment.i18n.lang`), символ переименован в `AstroDriftLangRequest_js`; `.meta`: единственный enabled = WebGL. [`YandexLanguageSource.cs`](Assets/_Platform/YandexGames/YandexLanguageSource.cs:19) — `GetAccountLanguage()`, вне WebGL/ЯИ безопасный `""` | ✅ |
| §5.1 п.6 (фикс B3) | [`YandexGamesInstaller.cs:35`](Assets/_Platform/YandexGames/YandexGamesInstaller.cs:35) — `YG2.lang` из лога убран; чтений `YG2.lang` в `_Platform` = 0 | ✅ |
| §5.1 п.7 (`setLanguageMod`) | [`SettingsYG2.asset:33`](Assets/PluginYourGames/Resources/SettingsYG2.asset:33) `1` → `2` (`DoNotChangeLanguageStartup`). Ключ в `platformToggles` ([`:115`](Assets/PluginYourGames/Resources/SettingsYG2.asset:115)) не тронут — читателя в [`Lang_yg.InitLang()`](Assets/PluginYourGames/Modules/Localization/Scripts/Lang_yg.cs:20) не имеет | ✅ |
| Компиляция ин-движком | RuStore → ItchIO → YandexGames, новых CS-ошибок нет (предсуществующая Android Resolver игнорируется) | ✅ |
| `Modules/Localization` не изменена | `git status --porcelain` по папке пуст | ✅ |

**Находки Team Lead при приёмке (сверх отчёта Developer'а):**

1. **`FreeBuffer_js` существует** в [`PluginYGCommon.jslib:3`](Assets/PluginYourGames/Scripts/Utils/Plugins/PluginYGCommon.jslib:3) (WebGL enabled) → неопределённого символа в линковке нет. Маршалинг новой копии идёт через `IntPtr` + `YGInsides.FreeBuffer`, а не через `string`-маршалинг оригинала — это **устраняет** утечку `malloc`-буфера, которую оригинал модуля оставлял. Принято как улучшение, не отклонение.
2. **`YandexGamesPlatform_yg` не определён нигде** в `ProjectSettings.asset` и ни в одном build profile → [`Lang_yandexPlatform.cs:1`](Assets/PluginYourGames/Modules/Localization/Scripts/Lang_yandexPlatform.cs:1) не компилируется вообще, его `LangRequest_js` мёртв. Дубль символа `LangRequest_js` / `AstroDriftLangRequest_js` был **теоретическим**; переименование всё равно корректно (страховка на случай включения define).
3. **Паритет defines сохранён:** глобальные `scriptingDefineSymbols` в [`ProjectSettings.asset:705-715`](ProjectSettings/ProjectSettings.asset:705) несут `Localization_yg` для всех платформ, а `YandexGames.asset` задаёт только `STORE_YANDEX` — то есть [`YandexLanguageSource.cs`](Assets/_Platform/YandexGames/YandexLanguageSource.cs:1) компилируется только при `UNITY_WEBGL && STORE_YANDEX` и не ломает RuStore/itch.
4. **Риск R10 (новый, к мониторингу):** переключение build-профилей в редакторе побочно перезаписывает снапшот `m_ScriptingDefines` в `*.asset` профиля (Developer поймал на `ItchIO.asset`, откатил через `git checkout --`). На HEAD профили не изменены (`git diff` по `Assets/Settings/Build Profiles/` пуст). Правило для задач 3–8: **после каждого переключения профиля ин-движком проверять `git status` по папке профилей** и откатывать нежелательные правки defines.

### Протокол решений продюсера по задачам 1–2

| # | Решение | Санкция |
|---|---|---|
| 1 | **Задача 1 принята.** F6 ратифицирован по обоим пунктам (эффективный чарсет 443 глифа; критерий «0 missing» = среди требуемых кодпоинтов; модель ~2 Б/тексел) | Ратифицировано |
| 2 | **Долг LiberationSans** (Death-экран и часть HUD на `LiberationSans SDF`) — принимается как есть по §0.2 + §8.4; отдельная design-задача **после** милстоуна. Регрессии нет: LiberationSans покрывает `→`. Трактовка §11.2 п.6: покрытие считается по **объединению** назначенных Static-шрифтов, пер-нода маппинг — вне скоупа | Ратифицировано |
| 3 | **Гейт G1 = вариант A:** перенос `LangRequest_js` (язык аккаунта ЯИ). Обоснование: ноль нового кода + сохранение семантики без отклонений. B (язык браузера) — неоправданная уступка, C — лишний churn | Ратифицировано |
| 4 | **Границы задачи 2:** `Modules/Localization` НЕ удалять и содержимое НЕ менять; удаление модуля — задача 3. Перенос `.jslib` — задача 2, использование — задача 3. Проверка на реальном ЯИ — **отложена** (F5) | Ратифицировано |
| 5 | **Процесс:** правки ТЗ — только с санкции продюсера. Критерий недостижим → СТОП и эскалация **ДО** правки ТЗ. Постфактум-правки с доказательствами (как F6) — приняты как исключение | Действует |
| 6 | **Задача 3, вариант B санкционирован:** `Basic.autoDefineSymbols: 1→0` + удаление `Localization_yg`. A (удаление папки модуля) невозможен — правка чужого плагина вне скоупа; C (вечный мёртвый define) разъедает §15 вечно-красным критерием. B обратим одной строкой, цена задокументирована | Санкционировано |
| 7 | **Точечные правки ТЗ** разрешены: §6.2, Приложение A, §13 (R10), §20 (F7 + результат 3). **§6.4 и §15 — БЕЗ изменений:** критерий «0» сохраняется и теперь выполняется по-настоящему | Санкционировано |
| 8 | **9 диагностических логов** задачи 3 — оставить до приёмки задачи 4 (ими доказывается C2); снятие — отдельным cleanup-коммитом в задаче 7–8, крайний срок — приёмка задачи 8 | Санкционировано |
| 9 | **NRE `GameViewLanguageMenu`** (editor-only, на чистом прогоне не воспроизвёлся) — мониторинг, действий не требуется | Принято |
| 10 | **Задача 4 принята**, тег `task4-accepted` в силе. Отклонение «два подписчика `LanguageChanged`» принято как безопасное: mid-session переключения языка в продукте нет, стартовый путь корректен через C2 + self-apply `OnEnable` | Ратифицировано |
| 11 | **Правки ТЗ `7b7c31e` ратифицированы** (изменений критериев нет — фиксация фактов по прецеденту F6). Расширение §7.1 п.6 на **оба** базовых шрифта (не только SemiBold) принято: в духе шага, цена нулевая | Ратифицировано |

### Клоузаут задачи 3 (п.6 решения продюсера)

- **Приёмка задачи 2 подтверждена:** тег `task2-accepted` (рядом с `task1-accepted`, `baseline-loc-migration`, `baseline-loc-migration-ready`). Перенос `LangRequest_js` по гейту G1 выполнен в задаче 2 → [`Assets/_Platform/YandexGames/YandexLanguage.jslib:3`](Assets/_Platform/YandexGames/YandexLanguage.jslib:3), символ **`AstroDriftLangRequest_js`** (тело идентично [`Language.jslib:3`](Assets/PluginYourGames/Modules/Localization/Plugins/Language.jslib:3)); C#-потребитель — [`YandexLanguageSource.cs:14`](Assets/_Platform/YandexGames/YandexLanguageSource.cs:14).
- **Компиляция абсолютными числами** (ин-движок, по одному компиляционному проходу на профиль): **`RuStore=1 / ItchIO=0 / YandexGames=0`**, где `RuStore=1` — единственная предсуществующая ошибка Android Resolver `Resolution Failed.` (возникает только на Android-профиле, к defines и к миграции не относится). Прочих ошибок нет ни на одном профиле.

### F7 (санкция продюсера, 2026-09-18) — снятие `Localization_yg`

| # | Проблема | Решение |
|---|---|---|
| F7 | Критерий §6.4 «`Localization_yg` = 0» был **структурно недостижим**: [`DefineSymbols.ModulesDefineSymbols()`](Assets/PluginYourGames/Scripts/EditorScr/DefineSymbols.cs:226) регенерирует `<Folder>_yg` из папок `Modules/*` при каждом refresh, а папка `Modules/Localization` неудаляема (её `YG2.lang`/`onSwitchLang` используются модулем Authorization и ядром). Задача 3 вскрыла это и вернула `ProjectSettings.asset` к HEAD — правильное поведение (СТОП вместо самодеятельности) | **Вариант B** (санкция продюсера): [`SettingsYG2.asset:55`](Assets/PluginYourGames/Resources/SettingsYG2.asset:55) `autoDefineSymbols: 1→0` + снятие токена с 11 платформ. §6.4/§15 не меняются. Остаточный риск — **R11** (§13). Цена: defines модулей YG2 далее синхронизируются вручную |

### Результат задачи 3 (коммиты `93c0bd5` + `fbf66bb`, проверено Team Lead)

| Пункт §6.4 | Факт | Статус |
|---|---|---|
| Холодный старт ru/en → верная локаль | `SelectedLocale`: `'en'` → `'ru'` (RuStore, `systemLanguage = Russian`); источник и маппинг залогированы | ✅ |
| Неподдерживаемый → `ru` (D1) | Таблица [`LanguageService.cs:28-35`](Assets/Scripts/Core/LanguageService.cs:28) построчно = §6.1; региональные коды через базу (`pt-BR → pt`) | ✅ |
| Нет видимой вспышки языка (C2) | [`Bootstrap.Awake:23-31`](Assets/Scripts/Core/Bootstrap.cs:23) — «И» (`PlatformBoot.IsReady && LanguageService.StartupApplied`); подтверждён порядок `[Lang] применено` → `[Boot] C2 Build` | ✅ |
| Watchdog 5 с (C3) | Сработал: дефолт `ru` + warning, старт не заблокирован; успешный путь снимает таймер. Корректно закрыт нетривиальный случай «init успел ровно на таймауте»: [`LanguageService.cs:173-174`](Assets/Scripts/Core/LanguageService.cs:173) не трогает локаль при пустом `AvailableLocales` — C1 не нарушается | ✅ |
| C1 (локаль не трогается до `Completed`) | `available=2` в момент применения; до init `Locales.Count = 0` | ✅ |
| Ноль `AstroDriftLanguageBridge` / `YG2.onSwitchLang` / `YG2.lang` | 0 в `Assets/Scripts` + `_Platform`; мост удалён вместе с `.meta` | ✅ |
| `Localization_yg` = 0 | **Вариант B:** `autoDefineSymbols: 0` + токен снят с 11 платформ; стойкость подтверждена (refresh ×3 + 4 домен-релоада → grep = 0); diff без посторонних правок (удалён ровно один токен, порядок остальных сохранён) | ✅ |
| Компиляция профилей | `RuStore=1 / ItchIO=0 / YandexGames=0`; `RuStore=1` — предсуществующая Android Resolver | ✅ |
| RU/EN визуально без изменений | Ин-движок: `StartPanel` / `Hud` / `DeathPanel` / `PerkPanel` активны, тексты на месте; [`TypographyConfig.languageOverrides`](Assets/Resources/TypographyConfig.asset:19) **пуст** → `GetFonts()` возвращает одинаковые слоты для ru/en, разрешение шрифтов инвариантно | ✅ |
| R10 (снапшоты профилей) | При переключении профилей снапшоты не изменились; правило «сверка + откат» работает | ✅ |

**Механика варианта B (подтверждена в коде плагина):** флаг выключает только автоматический путь — статический конструктор [`DefineSymbols()`](Assets/PluginYourGames/Scripts/EditorScr/DefineSymbols.cs:27) делает early-return до подписки на `EditorApplication.projectChanged`, а [`UpdateDefineSymbols()`](Assets/PluginYourGames/Scripts/EditorScr/DefineSymbols.cs:118) сам проверяет флаг (строка 123). Прямой вызов `ModulesDefineSymbols()` подписку не обходит → R11.

**Подтверждение допущения §7.1 п.1 (языковые оверрайды) — санкция продюсера, п.5:** [`TypographyConfig.asset:19`](Assets/Resources/TypographyConfig.asset:19) — `languageOverrides: []`, массив **пуст**. Слоты заполнены: `headingLight`/`bodyRegular`/`ctaSemiBold` = `20fb9121a3c3043a4ad672971d76f0c0` (`Montserrat-SemiBold SDF`), [`titleBold`](Assets/Resources/TypographyConfig.asset:16) = `2ebd00df0d84c41bc99f37ca0fbb92c2` (`Montserrat-Bold SDF`). Абзац §7.1 п.1 («существующий ассет (оверрайды пусты) продолжает работать») **подтверждён** — обработку оверрайдов в задачу 4 добавлять не нужно, §7.1 не меняется, эскалация не требуется. Замечание: наблюдение «RU `Title`=SemiBold / EN `Title`=Bold» из отчёта задачи 3 **из этого конфига следовать не может** — при пустом `languageOverrides` [`GetFonts()`](Assets/Scripts/Core/TypographyConfig.cs:41) возвращает одинаковые слоты для любого языка. Принято как ошибка наблюдения (не проверялось разрешение шрифта по ноде); на объект задачи 4 не влияет, к приёмке задачи 4 нода-факт `StartPanel` подлежит перепроверке.

### Результат задачи 4 (коммит `a8cc489`, проверено Team Lead)

| Пункт §7.1 / §7.2 | Факт | Статус |
|---|---|---|
| п.1 `langCode` → `localeCode` | [`TypographyConfig.GetFonts(string localeCode)`](Assets/Scripts/Core/TypographyConfig.cs:41) + поле [`TypographyLanguageFonts.localeCode`](Assets/Scripts/Core/TypographyConfig.cs:72) (тег тултипа — код локали). Слоты и фолбэк-цепочка не тронуты. Ассет [`TypographyConfig.asset`](Assets/Resources/TypographyConfig.asset:19) **не менялся**: `git diff task3-accepted..a8cc489` по нему пуст — пустой массив оверрайдов продолжает работать | ✅ |
| п.2 источник языка | [`Typography.CurrentLang`](Assets/Scripts/UI/Typography.cs:38) → `LocalizationSettings.SelectedLocale?.Identifier.Code`; собственной реакции на смену локали у `Typography` нет | ✅ |
| п.3 удаление цепочки `LanguageChanged` | Событие + `NotifyLanguageChanged()` удалены из [`Typography.cs`](Assets/Scripts/UI/Typography.cs:19); [`GameUI.ApplyTypography()`](Assets/Scripts/UI/GameUI.cs:460) и подписчик `OnEnable`/`OnDisable` удалены — 5 ручных применений переезжают на теги в §8.1 (задача 5). Ин-движок: `LanguageChanged=0`, `NotifyLanguageChanged=0`, `ApplyTypography=0`, `langCode=0`, `YG2` в `Scripts/UI` = 0 | ✅ |
| п.4 `TypeRoleTag` + `TypeRoleApplier` | [`TypeRoleTag.cs:17`](Assets/Scripts/UI/TypeRoleTag.cs:17) — имя поля `role` зафиксировано комментарием (контракт с билдерами §8.1); [`Apply():19`](Assets/Scripts/UI/TypeRoleTag.cs:19) → `Typography.ApplyFontOnly`; [`OnEnable():24`](Assets/Scripts/UI/TypeRoleTag.cs:24) — self-apply (N2, карты перков рантаймовые); [`TypeRoleApplier.ApplyAll():33`](Assets/Scripts/UI/TypeRoleTag.cs:33) — `FindObjectsByType(FindObjectsInactive.Include, FindObjectsSortMode.None)`, opt-in | ✅ |
| п.5 точка подписки | [`LanguageService.cs:55`](Assets/Scripts/Core/LanguageService.cs:55) — подписка типографики (идемпотентна через `_started`); [`OnSelectedLocaleChanged():72`](Assets/Scripts/Core/LanguageService.cs:72) → `TypeRoleApplier.ApplyAll()`. **Уточнение (продюсер, п.2а): не «единственная в проекте».** Подписок `SelectedLocaleChanged` в `Assets/Scripts` две: эта + [`LocalizedText.cs:91`](Assets/Scripts/UI/LocalizedText.cs:91) (`L10n.Subscribe()` → `OnLocaleChanged` → перечитывание `_bindings`), она живёт до задачи 7 — [`§10.1 п.2`](Assets/Docs/Localization_TZ.md:525) снимает её вместе с `Bind`/`RefreshAll`/`onBeforeRender`. Обе **intact**, взаимного перекрытия нет: `L10n` перечитывает строки, типографика — шрифты. Сброс гардов §3.4 живёт у владельца поля (`GameUI.RefreshBestValue`) и приезжает вместе с ним в задаче 5 — фиктивных полей/нод не создано (`_lastBestShown`/`_bestArgs` = 0) | ✅ |
| п.6 TMP Fallbacks | `m_FallbackFontAssetTable` базовых шрифтов → [`LiberationSans SDF - Fallback`](Assets/TextMesh%20Pro/Resources/Fonts%20&%20Materials/LiberationSans%20SDF%20-%20Fallback.asset) (`guid 2e498d1c…`): ровно по 1 записи в [`Montserrat-Bold SDF.asset`](Assets/Fonts/Montserrat-Bold%20SDF.asset) и [`Montserrat-SemiBold SDF.asset`](Assets/Fonts/Montserrat-SemiBold%20SDF.asset), обе цели — `type: 2` + `fileID 11400000` (корректный `TMP_FontAsset`). Дифф шрифтов не содержит больше ничего — атлас/глифы задачи 1 не задеты | ✅ |
| §7.2 смена локали на тегированных нодах | Ин-движок: временный `TypeRoleTag(Cta)` на `Hud/ComboChip`, `SelectedLocale ru→en` + временный оверрайд `en.cta` → шрифт стал `Montserrat-Bold SDF` по реальному событию `SelectedLocaleChanged`; тег и оверрайд откачены (сцена и ассет конфига в коммит не попали — `*.unity`/`*.prefab` в коммите = 0) | ✅ |
| §7.2 нетегированные ноды | `Hud/Btn_Pause/Text` = `LiberationSans SDF` до и после смены локали; `StartPanel/CtaText` = запечённый SemiBold. `grep TypeRoleTag` по `Assets/Scenes/` + `Assets/Prefabs/` = 0 (теги — задача 5, §8.0) | ✅ |
| §7.2 пустые оверрайды / пустые слоты | [`GetFonts()`](Assets/Scripts/Core/TypographyConfig.cs:41) при пустом массиве возвращает базовые слоты; [`ApplyFont():70`](Assets/Scripts/UI/Typography.cs:70) при `null` берёт `TMP_Settings.defaultFontAsset` (LiberationSans SDF) — ни Missing, ни Null | ✅ |
| R10 / R11 | `git status --porcelain` по `Assets/Settings/Build Profiles/` пусто; `Localization_yg` в `ProjectSettings.asset` = 0 | ✅ |

**Отклонение (принято):** подписчиков `LanguageChanged` в проекте было больше одного — [`LocalizedTextUI.OnEnable`](Assets/Scripts/UI/LocalizedTextUI.cs:21) тоже подписывался. Цепочка §7.1 п.3 удалена целиком (мёртвая подписка снята); `ApplyFont()`/`L10n.Bind` не тронуты, удаление файла остаётся за задачей 5 (§8.3, единая отсечка).

**Отложено в задачу 5 (не пробел):** `startBestValue` + гард §3.4 `_lastBestShown`/кэш `Arguments` — по §8.1 приезжают вместе с нодой `StartBestValue`; 5 ручных `Typography.Apply` переносятся на теги билдерами §8.1/§8.2; нода-факт «запечённый шрифт карт перков (`Title`/`Description`) == ролевой» проверяется критерием §8.3.

**Уточнения по требованию продюсера (п.2а–2г):**

- **(п.2а)** Строка «единственная подписка `SelectedLocaleChanged`» **исправлена** (см. строку «п.5 точка подписки» выше): подписок две, вторая — `L10n` (§10.1 п.2 снимает её в задаче 7). Grep-число по `Assets/Scripts`: `SelectedLocaleChanged` = 4 вхождения, из них **2 живые подписки** ([`LanguageService.cs:55`](Assets/Scripts/Core/LanguageService.cs:55), [`LocalizedText.cs:91`](Assets/Scripts/UI/LocalizedText.cs:91)) и 2 — упоминания (комментарий [`Typography.cs:16`](Assets/Scripts/UI/Typography.cs:16) + имя метода `OnSelectedLocaleChanged`). Обе подписки **intact**.
- **(п.2б) Temp-override тест §7.2 — цитата исполнителя (единственное доказательство, артефакта не осталось):** *«временный `TypeRoleTag(role=Cta)` на `Hud/ComboChip`; `SelectedLocale = ru → en` + временный оверрайд `en.cta = Montserrat-Bold SDF` → шрифт ноды сменился на `Montserrat-Bold SDF` по реальному событию `SelectedLocaleChanged`; тег и оверрайд откачены»*. Подмена: роль-тег на ноде + запись `en.cta` в конфиге. Наблюдение: смена шрифта ноды. Откат: сцена и `TypographyConfig.asset` в коммит не попали (`*.unity`/`*.prefab` в коммите = 0; `git diff task3-accepted..a8cc489` по конфигу пуст). **Проверяемость: пост-фактум не верифицируется** — рантайм-наблюдение без артефакта. Поэтому критерий **переносится в приёмку задачи 5 именованным пунктом** (там он сильнее — на реальных тегах §8.1/§8.2, без синтетической ноды), зафиксирован **перенос, а не пропуск**.
- **(п.2в) R11 (одной строкой, новое — записано):** при `autoDefineSymbols = 0` прямой вызов [`DefineSymbols.ModulesDefineSymbols()`](Assets/PluginYourGames/Scripts/EditorScr/DefineSymbols.cs:226) **не проверяет** `AutoDefinesEnabled()` и вернёт `Localization_yg` на все таргеты; вызовы — [`VersionControlWindow.cs:93`](Assets/PluginYourGames/Scripts/Server/Editor/VersionControlWindow.cs:93), [`VersionControlWindow.cs:708`](Assets/PluginYourGames/Scripts/Server/Editor/VersionControlWindow.cs:708), [`ModuleQueue.cs:78`](Assets/PluginYourGames/Scripts/Server/Editor/Utils/ModuleQueue.cs:78). Митигация — повторный grep после любого ручного импорта модулей YG2 (полная запись — §13, R11).
- **(п.2г) FREEZE — подтверждение:** заморозка **ДЕЙСТВУЕТ до приёмки задачи 7**. Формулировка «снят по задачам 1–7» в моём отчёте **неверна** (употреблена ошибочно). По задачам 1–4 заморозка **соблюдена**: в коммиты не попало ничего вне скоупа ТЗ — состав коммитов проверен пофайлово (`a8cc489` = ровно 9 файлов §7.1/§7.2; `*.unity`/`*.prefab`/`TypographyConfig.asset`/профили — 0 изменений).

### Клоузаут задачи 4

- Тег **`task4-accepted`** (коммит `a8cc489`) — рядом с `task1-accepted`…`task3-accepted`, `baseline-loc-migration`.
- Остаточные риски: **R10** (снапшоты профилей — сверка после каждого переключения) и **R11** (ручной импорт модулей через окно YG2 вернёт `Localization_yg`) остаются действующими для задач 5–8.
- 9 диагностических логов задачи 3 (7 в [`LanguageService.cs`](Assets/Scripts/Core/LanguageService.cs) + 2 в [`Bootstrap.cs`](Assets/Scripts/Core/Bootstrap.cs)) остаются до приёмки задачи 8 (крайний срок, п.8 протокола). **Поправка (задача 6, п.4):** упоминание «логи задачи 4» в исходной формулировке — **фантомное**, задача 4 не добавила ни одной `Debug.Log`-строки (проверено `git show a8cc489` = 0); распределение cleanup актуализировано [ниже](Assets/Docs/Localization_TZ.md:931).

### Результат задачи 5 (коммит `81566c8`, проверено Team Lead пофайлово и grep'ом)

| Пункт §8.1 / §8.2 / §8.3 | Факт | Статус |
|---|---|---|
| Состав коммита | `81566c8` (родитель `693787e`), ровно **23 файла**: 9 префабов + [`Game.unity`](Assets/Scenes/Game.unity) + 6 `.cs` (+ [`AstroDriftSceneSetup.cs`](Assets/Scripts/Core/AstroDriftSceneSetup.cs), [`LanguageService.cs`](Assets/Scripts/Core/LanguageService.cs), [`MenuPrefabBuilder.cs`](Assets/Scripts/Editor/MenuPrefabBuilder.cs), [`LevelUpPrefabBuilder.cs`](Assets/Scripts/Editor/LevelUpPrefabBuilder.cs), [`GameUI.cs`](Assets/Scripts/UI/GameUI.cs), [`LevelCardUI.cs`](Assets/Scripts/UI/LevelCardUI.cs), [`PerkChoiceUI.cs`](Assets/Scripts/UI/PerkChoiceUI.cs)) + [`PrefabBuilderSprites.cs`](Assets/Scripts/Editor/PrefabBuilderSprites.cs)/[`.asset`](Assets/Scripts/Editor/PrefabBuilderSprites.asset) (+`.meta`) − [`LocalizedTextUI.cs`](Assets/Scripts/UI/LocalizedTextUI.cs)/`.meta`. Запрещённые пути — 0: `New UI`, `PluginYourGames`, `TypographyConfig.asset`, `Build Profiles`, `LiberationSans SDF - Fallback` не затронуты | ✅ |
| §8.1 карта тегов (16 в `Assets/Prefabs`) | Разобрано парсером YAML (GameObject→роль), совпадает с таблицей §8.1 1:1: `LevelUpPanel` — `LevelUpTitle`=6/`LevelUpChoose`=1/`RerollText`=2/`RerollCaption`=1; `UpgradeCard` — `Title`=2/`Description`=5; `UpgradeCard_New` — `NewBadge`=2; `LevelCard` — `LevelLabel`=4; `MenuButton_{Settings,Shop,Upgrade}` — `Label`=2; `StartPanel` — `CtaText`=2/`ShieldText`=2/`StartBest`=1/`StartBestValue`=1/`ShieldCaption`=1 (enum: `Cta=2, Secondary=1, Body=5, LevelUpTitle=6, Button=4`) | ✅ |
| §8.2 теги сцены (3 в `Game.unity`) | `DeathScore`=3/`DeathBest`=1/`DeathNewBest`=1 — точно по таблице §8.2 (динамика через `Arguments`, см. [`AstroDriftSceneSetup.cs`](Assets/Scripts/Core/AstroDriftSceneSetup.cs:329)). Итого тегов **19 = 16 + 3**, базлайн был **0** | ✅ |
| §8.2 статика без тегов | `AddLocalize(...)` на `PauseTitle`/`Btn_Resume`/`Btn_Home`×2/`ContinueText`/`ContinueCaption`/`DeathXp`/`DeathLevel`/`DeathNewBest`; тегов на них нет — совпадает с §8.4 | ✅ |
| §8.0 правило владения | LSE и теги навешены **в билдерах префабов** (`AddLocalized`/`AddRole`) и в **коде сцены** (`AstroDriftSceneSetup`), не на чужих нодах. Вложенные префабы (`LevelCard`, `MenuButton_{Settings,Shop,Upgrade}` — по 23 ссылки guid внутри `StartPanel.prefab`) учтены: в сцене их собственных GameObject'ов нет, теги живут в префабах | ✅ |
| §8.3 п.5 `L10n.Bind` = 0 / `LocalizedTextUI` удалён | `grep L10n.Bind` по `Assets` = **1** вхождение — док-комментарий [`LocalizedText.cs:10`](Assets/Scripts/UI/LocalizedText.cs:10), вызовов **0**. Критерий §8.3 формально выполнен (он про **вызовы**); остаточная строка документации снимается в задаче 7 → на её приёмке grep = **буквально 0**. `LocalizedTextUI` = **0** вхождений в `Assets/**` (`*.cs`/`*.prefab`/`*.unity`), файл + `.meta` удалены. [`BindPauseTexts`](Assets/Scripts/UI/GameUI.cs) = 0, [`ApplyTypography`](Assets/Scripts/UI/GameUI.cs) = 0 | ✅ |
| §8.3 п.4 ноль диффа начертания карт | `UpgradeCard.prefab`: `Title`/`Description` — `m_fontAsset` (`2ebd00df…`/`20fb9121…`) + `m_fontStyle: 0` **байт-в-байт равны** родителю `693787e` (сравнение Team Lead) → дифф нулевой, как и требует критерий | ✅ |
| §8.3 п.3 гард горячего пути | [`GameUI.cs:55`](Assets/Scripts/UI/GameUI.cs:55) `_bestArgs` — `readonly object[1]`, создан один раз; [`GameUI.cs:1141`](Assets/Scripts/UI/GameUI.cs:1141) ранний выход `if (best == _lastBestShown) return;`; [`GameUI.cs:1158`](Assets/Scripts/UI/GameUI.cs:1158) сброс из [`ResetLanguageGuards()`](Assets/Scripts/UI/GameUI.cs:1152), который зовёт [`LanguageService.cs:78`](Assets/Scripts/Core/LanguageService.cs:78) — точка подписки одна | ✅ |
| §8.1 билдеры на контейнере (решение продюсера) | Поиск спрайтов по имени удалён: `LoadSprite`/`GetSprite(`/`LoadAllAssetsAtPath` = **0** в обоих билдерах. Вход — [`PrefabBuilderSprites`](Assets/Scripts/Editor/PrefabBuilderSprites.cs) (13 полей), громкий стоп до записи: [`PrefabBuilderSprites.cs:72`](Assets/Scripts/Editor/PrefabBuilderSprites.cs:72) и [`:105`](Assets/Scripts/Editor/PrefabBuilderSprites.cs:105) `Debug.LogError(... «префабы не записаны»)`. Ассет закоммичен с 12 ссылками, `usePanelBg: 0`, `panelBg: {fileID: 0}` (законно) | ✅ |
| R5 один коммит | Код §8 + 9 префабов + сцена — в **одном** `81566c8`; играбельность сверена ин-движком до/после (Play Mode, активный профиль RuStore) | ✅ |
| R10 сверка снапшотов | `git status --porcelain "Assets/Settings/Build Profiles/"` — **пусто**; побочных правок `defines` не осталось | ✅ |
| §8.4 нулевой дифф нетегированных | `ContinueText`/`Hud`/пауза: `m_fontAsset` до и после смены локали = `LiberationSans SDF` (`8f586378…`); ноды остались без тегов; temp-override тест показал, что нетегированные **не** переприменяются | ✅ |
| Компиляция 3 профилей | RuStore: 1 ошибка — **предсуществующий** Android Resolver `Resolution Failed.` (не C#); CS-ошибок = 0. ItchIO = 0, YandexGames = 0. Ин-движок: `read_console` по `CS\d{4}` = 0 записей | ✅ |

**Отклонения / наблюдения (приняты, не блокируют приёмку):**

- **3 новые пустые ссылки `LocalizeStringEvent` в `GameUI`** ([`GameUI.cs:49-51`](Assets/Scripts/UI/GameUI.cs:49)): `startBestValueLse`, `shieldTextLse`, `shieldCaptionLse` = `{fileID: 0}` в сцене. Все три восстанавливаются в рантайме через [`GetLse()`](Assets/Scripts/UI/GameUI.cs:1205) → `GetComponent` с ноды (LSE на `StartPanel/StartBestValue`, `StartPanel/ShieldText`, `StartPanel/ShieldCaption` присутствуют — проверено). `shieldTextLse` фактически не читается (0 обращений) — мёртвое поле, безвредно.
- **`menuUpgradeBtn: {fileID: 0}`** — **предсуществующее** (подтверждено: `693787e:4561`, тот же ноль в родителе), вместе с [`Debug.LogError` «не найдена кнопка «ПРОКАЧКА»»](Assets/Scripts/Core/AstroDriftSceneSetup.cs:276) в родителе. **Не регрессия задачи 5.** Требует отдельного решения продукта вне миграции (кнопка «ПРОКАЧКА» не подключена → дерево разблокировок не открыть).
- **`Setup Scene UI` сообщил о ненайденной кнопке как об ошибке, а не предупреждении** — отчёт исполнителя неточен в классификации, факт совпадает с предсуществующим дефектом выше.

### Клоузаут задачи 5

- Тег **`task5-accepted`** (коммит `81566c8`) — рядом с `baseline-loc-migration`, `task1-accepted`…`task4-accepted`.
- **Перенесённый temp-override тест §7.2 (решение продюсера, п.2б) — выполнен на реальных тегах:** временный in-memory оверрайд EN (все слоты → `Montserrat-Bold SDF`), `SelectedLocale ru → en` посреди сессии → **тегированные** ноды переприменены (19 нод получили новый шрифт), **нетегированные** (`Hud/*`, пауза, `ContinueText`) остались `LiberationSans SDF`; оверрайд откачен, дифф пуст. **Оговорка о доказательности (принята):** плейсхолдер-конфиг мапит `headingLight`/`bodyRegular`/`ctaSemiBold` на **один** guid `20fb9121…` ([`TypographyConfig.asset:15-18`](Assets/Resources/TypographyConfig.asset:15)), поэтому видимой дельты RU→EN «по слотам» нет — наблюдался факт переприменения, а не визуальный сдвиг. Это ограничение **данных продукта**, не кода; снимается при заполнении ролей разными шрифтами (вне скоупа милстоуна).
- **Предсуществующий дефект ассетов (отдельная запись, не регрессия):** `Sheet.png` был пере-нарезан авто-сеткой в коммите `d9bf5e1` (тег `baseline-loc-migration`, размер 855×1024 → 1661×1024), из-за чего именованные слайсы `Level_Icon`/`Settings_Icon`/`Shop_Icon`/`Upgrade` (существовавшие на `f0155fd`) сменились на `Player`/`Star`/`Sheet_2..4`. Это и вызвало падение билдеров. **Миграция не виновата.** Последствие для продукта: переслайсить `Sheet.png` вручную или оставить контейнер как источник истины (принято второе).
- **9 остаточных `LocalizedTextUI` в YAML префабов** — устранены в этой задаче вместе с удалением файла (теперь `LocalizedTextUI` = 0 в `Assets/**`); отдельная запись закрыта.
- Остаточные риски **R10**/**R11** — действуют для задач 6–8. Диагностические логи — до приёмки 8.

### Уточнения по требованию продюсера (задача 5, п.4а–4д)

- **(п.4а) Долг закрыт ранее — снимок продюсера протух.** Формулировка «единственная подписка `SelectedLocaleChanged`» исправлена ещё в коммите `693787e`, блок «**Уточнения по требованию продюсера (п.2а–2г)**», строка **(п.2а)**: там прямо сказано «не «единственная в проекте»», перечислены **две живые подписки** — [`LanguageService.cs:55`](Assets/Scripts/Core/LanguageService.cs:55) и [`LocalizedText.cs:91`](Assets/Scripts/UI/LocalizedText.cs:91) — и указано, что вторая снимается в задаче 7 ([§10.1 п.2](Assets/Docs/Localization_TZ.md:526)). Повторной правки не требует; ниже фиксируется только факт закрытия.
- **(п.4б) R11 — определение дословно (повтор для снимка продюсера, полная запись в §13):** *«При `autoDefineSymbols = 0` прямой вызов `DefineSymbols.ModulesDefineSymbols()` не проверяет `AutoDefinesEnabled()` и вернёт `Localization_yg` на все таргеты; вызовы — `VersionControlWindow.cs:93`, `VersionControlWindow.cs:708`, `ModuleQueue.cs:78`. Митигация — повторный grep после любого ручного импорта модулей YG2.»*
- **(п.4в) Кратность grep-хитов — объяснена: хиты == ноды, дублирования нет.** Один компонент `TypeRoleTag` даёт ровно **одну** строку `m_EditorClassIdentifier: Assembly-CSharp::TypeRoleTag`; других вхождений строки `TypeRoleTag` в YAML нет (проверено `grep -v m_EditorClassIdentifier` → пусто). Итого: **16 хитов в `Assets/Prefabs` = 16 нод** (`LevelUpPanel` 4, `StartPanel` 5, `UpgradeCard` 2, `UpgradeCard_New` 1, `LevelCard` 1, `MenuButton_{Settings,Shop,Upgrade}` по 1), **3 хита в `Game.unity` = 3 ноды**. Вложенности и вариантов **нет**; префабы вариантов кнопок/`LevelCard` лежат **внутри** [`StartPanel.prefab`](Assets/Prefabs/Menu/StartPanel.prefab) (по 23 guid-ссылки), поэтому в сцене их нод нет — отсюда 19 = 16 (префабы) + 3 (сцена), а не 19 в сцене.
- **(п.4г) Паритет спрайтовых ссылок подтверждён — потерь нет (R5).** Подсчёт `m_Sprite` по 9 префабам, `693787e` vs `81566c8`: **14 ссылок / 6 пустых до и 14 / 6 после** — совпадение пофайлово (`LevelUpPanel` 3/1↔3/1, `UpgradeCard` 2/1↔2/1, `LevelCard` 4/1↔4/1, `StartPanel` 3/3↔3/3, `MenuButton` 2/0↔2/0, `UpgradeCard_New`/`MenuButton_{Settings,Shop,Upgrade}` 0/0↔0/0). `panelBg: {fileID: 0}` при `usePanelBg: 0` — **состояние до пересборки** (пустой слот в `LevelUpPanel` существовал и раньше), а не потерянная ссылка.
- **(п.4д) Play Mode-обход задачи 5 — долг доказательности (по прецеденту п.2б).** Перечень экранов, пройденных глазами на профиле RuStore, в отчёте исполнителя **не зафиксирован**; артефактов нет. Аттестовано только: билдеры отработали без ошибок (`MenuLogo OK; LevelCard OK; MenuButton OK; 3 варианта OK; StartPanel OK` / `usePanelBg=false …; UpgradeCard OK; UpgradeCard_New OK; LevelUpPanel OK`) и R5-сверка ссылок до/после. **Долг:** перечень экранов RU/EN снять при первом же прогоне задачи 6 (там оверлей перка обязателен по §9.4) и закрыть этим.

### Пост-милстоун дефекты (вне скоупа миграции, заморозка; ведутся до конца милстоуна)

Решением продукта (задача 5, п.2) **не чинятся** в милстоуне. Список открыт и ведётся:

| # | Дефект | Приоритет | Суть |
|---|---|---|---|
| 1 | `menuUpgradeBtn: {fileID: 0}` в [`Game.unity`](Assets/Scenes/Game.unity:4863) | **P1** | Кнопка «ПРОКАЧКА» не подключена — дерево разблокировок недоступно из UI. Предсуществующее (`693787e:4561`), не регрессия миграции |
| 2 | `Sheet.png` — потерянные именованные слайсы | P2 | Пере-нарезка авто-сеткой в `d9bf5e1` (тег `baseline-loc-migration`): 855×1024 → 1661×1024, слайсы `Level_Icon`/`Settings_Icon`/`Shop_Icon`/`Upgrade` → `Player`/`Star`/`Sheet_2..4`. Миграция не виновата; вход билдеров переведён на контейнер |

**Следствие для задачи 7 (§10.2):** проверка дерева разблокировок выполняется **прямым вызовом** (кнопка не подключена) — метод фиксируется в брифе задачи 7 до выдачи.

### Распределение cleanup между задачами 7 и 8 (зафиксировано по фактам: решение продюсера, задача 6, п.4)

Принцип раздела — **по владельцу файла**, без переносов на «конец милстоуна»:

| Задача | Пункт | Файл / состав | Почему здесь |
|---|---|---|---|
| **7** | Мёртвое поле `shieldTextLse` ([`GameUI.cs:50`](Assets/Scripts/UI/GameUI.cs:50), 0 обращений) | `Assets/Scripts/UI/GameUI.cs` | Файл уже правится задачей 7 (точки `L10n.Get`) — нулевая цена, один коммит |
| **7** | Док-комментарий `L10n.Bind` ([`LocalizedText.cs:10`](Assets/Scripts/UI/LocalizedText.cs:10)) | `Assets/Scripts/UI/LocalizedText.cs` | **Файл целиком в скоупе задачи 7** — вместе со снятием `Bind`/`_bindings`/`RefreshAll`/`onBeforeRender`/`DetachRefresh` |
| **8** | **9 диагностических логов задачи 3** — `[Lang]` ×7 + `[Boot] C2` ×2 | `Assets/Scripts/Core/LanguageService.cs` (7) + `Assets/Scripts/Core/Bootstrap.cs` (2, [`Bootstrap.cs:41`](Assets/Scripts/Core/Bootstrap.cs:41), [`:61`](Assets/Scripts/Core/Bootstrap.cs:61)) | `Bootstrap.cs` задачей 7 **не** трогается; логи сторожат C1–C3, снимать их **до** финальной приёмки преждевременно |
| **8** | Валидатор + сирота «ДЕРЕВО» | `Assets/Localizations/*` + новый editor-скрипт | Скоуп §11 по ТЗ |

**Факты, которыми распределение подтверждено (проверено Team Lead по git-истории, не по памяти):**

- **`9 диагностических логов задачи 3` — цифра ТЗ верна и адресована точно.** Коммит [`93c0bd5`](Assets/Docs/Localization_TZ.md) добавил ровно **9** вызовов `Debug.Log*`: **7** в [`LanguageService.cs`](Assets/Scripts/Core/LanguageService.cs) (`:49`, `:95`, `:144`, `:151`, `:171`, `:179`, `:187`) + **2** в [`Bootstrap.cs`](Assets/Scripts/Core/Bootstrap.cs) (`[Boot] C2`). Авторство обоих файлов — задача 3 (в задаче 4 `Bootstrap.cs` не менялся).
- **`Логи задачи 4` — фантомные: задача 4 не добавила ни одного `Debug.Log`.** Коммит `a8cc489` правил 6 `.cs`-файлов, добавленных `Debug.Log` строк — **0**; и [`Typography.cs`](Assets/Scripts/UI/Typography.cs), и [`TypeRoleTag.cs`](Assets/Scripts/UI/TypeRoleTag.cs) содержат **0** логов сегодня. Пункт «логи задачи 4» из формулировки cleanup **снят** (нечего чистить); в §20 веду как расхождение журнала.
- **Следствие для плана:** cleanup освободился от задачи 7 целиком, кроме двух файлов, которые она и так правит.

- **Точка контроля задачи 7 (обязательна):** §15-grep по `L10n.Bind` = **буквально 0** (сейчас **1** — док-комментарий, [`LocalizedText.cs:10`](Assets/Scripts/UI/LocalizedText.cs:10)), `Application.onBeforeRender` в `Assets/Scripts` = **0** (сейчас **4** строки — 1 комментарий + 3 использования в `LocalizedText.cs:99,110,160`), `shieldTextLse` = **0** (сейчас **1** — объявление поля).
- **Крайний срок логов задачи 3 — приёмка задачи 8** (перенос крайнего срока с 7 на 8 санкционирован продюсером; обоснование — логи C1–C3 остаются полезны до финальной сверки).

### Решения продюсера по задаче 6 (приняты, записаны в ТЗ)

- **п.1 Задача 6 ратифицирована**, тег `task6-accepted` в силе; журнал (таблица результата, клоузаут, Приложение B — 6 строк → ✅) ратифицирован. Отклонения приняты.
- **п.1 (условие по `UnlockTreePanel/Title`).** Наблюдение «ложное срабатывание» принято **с условием**: приёмка задачи 7 **обязана явно подтвердить резолв заголовка дерева** — прямым вызовом `FillUnlockTree()` + Play Mode. Внесено отдельным критерием в [§10.2](Assets/Docs/Localization_TZ.md:530); этим наблюдение закрывается.
- **п.2 Долг 4д — существенно закрытый с переносом вперёд.** На **финальной приёмке милстоуна ([§14](Assets/Docs/Localization_TZ.md:598))** выполняется полный обход экранов явным списком: старт / смерть / пауза / оверлей перка / дерево разблокировок × RU/EN.
- **п.3 Санкция на выдачу задачи 7 выдана**; бриф подтверждён фактами (5 точек `L10n.Get`), метод проверки дерева зафиксирован, переподписка для видимого дерева — в скоупе. **Заморозка — до ПРИЁМКИ задачи 7** (не выдачи).
- **п.4 Cleanup зафиксирован сейчас** — таблица выше, без дрейфа.
- **п.5 Четыре долга журнала закрываются разом к отчёту задачи 7, без новых переносов:** 4а, 4б, 4в, 4г — [закрыты дословно](Assets/Docs/Localization_TZ.md:912) в записи «Уточнения по требованию продюсера (задача 5, п.4а–4д)». Процессная фиксация: переносы однострочников не накапливаются.

### Результат задачи 6 (коммит `42ddefc`, проверено Team Lead пофайлово и grep'ом)

| Пункт §9.1 / §9.2 / §9.3 / §9.4 | Факт | Статус |
|---|---|---|
| Состав коммита | `42ddefc` (родитель `3f44be3`), ровно **14 файлов**, +252/−64. Запрещённые пути — 0: `New UI`, `PluginYourGames`, `TypographyConfig.asset`, `Build Profiles`, `LiberationSans SDF - Fallback` не затронуты; `.unity`/`.prefab` в коммите **нет** (задача 6 сцену и префабы не касается) | ✅ |
| §9.1 `PerkDefinition` | `string titleKey`/`descKey` → `LocalizedString title`/`desc` ([`PerkDefinition.cs`](Assets/Scripts/Core/PerkDefinition.cs)); сигнатуры полей совпадают с §9.1 | ✅ |
| §9.4 п.1 ссылки перков и пикапов | `m_TableCollectionName: GameTexts` = **2 × 8 = 16** в [`Perks/*.asset`](Assets/Resources/Perks) + **3** в [`PickupConfig.asset`](Assets/Resources/PickupConfig.asset) = **19**. `m_Key` **не переименованы** (`perk_*_title`/`perk_*_desc`, `pickup_*`) — §0.3 соблюдён | ✅ |
| §9.2 `AstroDriftSetup` | Добавлен `MakeRef(string entry)` → `ls.SetReference("GameTexts", entry)`; `PerkSeed.titleEntry`/`descEntry` (были `…Key`); сиды перков и пикапов пишутся через `MakeRef` | ✅ |
| §9.4 п.2 идемпотентность | Гард `if (asset == null) { AssetDatabase.CreateAsset… }` — существующие ассеты не перезаписываются. **Независимо перепроверено Team Lead ин-движком:** прогон `AstroDrift/Setup Assets` → `AstroDrift Perks: ассетов создано 0 из 8`, `git status` пуст | ✅ |
| §9.3 `PickupDef` + `PickupManager` | Новое поле `LocalizedString name` ([`PickupConfig.cs`](Assets/Scripts/Core/PickupConfig.cs)); чтение — `def.name.GetLocalizedString()` + fallback `string.IsNullOrEmpty(...) ? def.type.ToString()` (проверка **N3**, не `== null`); метод `PickupNameKey` удалён | ✅ |
| §9.4 п.5 ноль чтений | grep `titleKey\|descKey\|PickupNameKey` по `Assets/**` = **0** (11 вхождений — только в [`Localization_TZ.md`](Assets/Docs/Localization_TZ.md) как исторические упоминания) | ✅ |
| §9.4 п.3 Play Mode (RuStore) | Дословно снято исполнителем: карты RU `СКОРОСТЬ ПУЛЬ+` / EN `BULLET SPEED+`; бейдж RU `НОВОЕ` / EN `NEW`; `CYRILLIC=0 LATIN=8`; реролл — 3× `BulletSpeed`, `ReferenceEquals=True` (замена, не дубль); 11 значений флоатеров пикапов | ✅ |
| §9.4 п.4 смена локали при открытом оверлее | Карты обновляются событием `LocalizeStringEvent` (`StringReference` перерисован), пересоздание карт не требуется | ✅ |
| §3.2 вариант А (§9.4 п.4) | [`PerkChoiceUI.cs`](Assets/Scripts/UI/PerkChoiceUI.cs) `FillCard`: `lse.StringReference = def.title;` / `… = def.desc;` — **ровно** формулировка §3.2 вариант А; метод `SetEntry` удалён; подписки на `StringChanged` нет | ✅ |
| Компиляция 3 профилей | ItchIO = 0, YandexGames = 0, RuStore = 1 (**предсуществующий** Android Resolver `Resolution Failed.`, не C#); CS-ошибок = **0** | ✅ |

### Клоузаут задачи 6

- Тег **`task6-accepted`** (коммит `42ddefc`) — рядом с `baseline-loc-migration`, `task1-accepted`…`task5-accepted`.
- **Новые отклонения (приняты, не блокируют приёмку):**
  - **Легаси-парсер `.txt` перков — эвристика по префиксу.** Вместо явного списка ключей ветка `default:` распознаёт `perk_*_title`/`perk_*_desc` через `StartsWith`/`EndsWith`. Принято: парсер обслуживает одноразовый импорт, источник истины — таблица `GameTexts`, ключи в ассетах не переименовывались.
  - **`FillCard` больше не подставляет фолбэк-строку при пустой ссылке карты.** Соответствует §3.2 вариант А дословно; пустое значение гасится на уровне `PerkTitle` (`GetLocalizedString()` + `string.IsNullOrEmpty` → `def.id`).
  - **`UnlockTreePanel/Title` — «пустой `StringReference`» из отчёта исполнителя оценено как ложное срабатывание.** Ноды нет в сцене: она создаётся в рантайме в [`BuildTreePanel()`](Assets/Scripts/UI/GameUI.cs:189), ключ назначается там же — [`AddLocalized(titleTmp, "unlock_tree_title")`](Assets/Scripts/UI/GameUI.cs:220). Дефектом не заведено, наблюдение ведётся до задачи 7 (её скоуп — `GameUI`).
  - **Артефактов не осталось:** скриншоты задачи 6 удалены, код `execute_code` ин-движка на диск не писался — подтверждено чистым `git status` и составом коммита.
- **Долг доказательности п.4д прошлого периода (перечень экранов Play Mode) — ЗАКРЫТ** дословным перечнем строк RU/EN в отчёте задачи 6.
- **Скоуп задачи 7 подтверждён фактом:** чтения `L10n.Get` остались ровно в точках §10.1 п.3 — [`GameUI.cs:252`](Assets/Scripts/UI/GameUI.cs:252), [`:269`](Assets/Scripts/UI/GameUI.cs:269), [`:601`](Assets/Scripts/UI/GameUI.cs:601), [`:606`](Assets/Scripts/UI/GameUI.cs:606) + [`GameManager.cs:566`](Assets/Scripts/Core/GameManager.cs:566) (флоатер комбо).
- **Следствие из пост-милстоун дефектов для задачи 7:** проверка дерева разблокировок — **прямым вызовом** `FillUnlockTree()` (кнопка «ПРОКАЧКА» не подключена, `menuUpgradeBtn: {fileID: 0}`). Метод фиксируется в брифе задачи 7 до выдачи.
- Остаточные риски **R10**/**R11** — действуют для задач 7–8. Диагностические логи задачи 3 и логи задачи 4 — до приёмки 8. **Заморозка действует до приёмки задачи 7.**

---

## Приложение A. Финальное состояние настроек проекта (заполнено по факту задач 2–3)

- [x] Модуль Localization: **включён, содержимое не изменено** — осознанно (запрет продюсера на правку модуля). Влияние на язык снято функционально: [`setLanguageMod = DoNotChangeLanguageStartup`](Assets/PluginYourGames/Resources/SettingsYG2.asset:33), поэтому [`Lang_yg.InitLang()`](Assets/PluginYourGames/Modules/Localization/Scripts/Lang_yg.cs:20) выходит до `GetLanguage()`. Удаление модуля — задача 3.
- [x] Модуль AutoTranslateLangs: **удалён** — папка `Modules/AutoTranslateLangs/` + `.meta` (26 файлов). Компоненты снимать не потребовалось: на момент удаления внешних ссылок не было (§5.1 п.1–2). Дополнительно убран из `SelectModuleToggle_YG2` в [`PluginPrefs.json`](Assets/PluginYourGames/Editor/PluginPrefs.json:5) и из [`ModulesListYG2.txt`](Assets/PluginYourGames/Editor/ModulesListYG2.txt:1) — иначе модуль восстанавливался бы при `Basic.autoDefineSymbols: 1`.
- [x] Define `AutoTranslateLangs_yg` **удалён** из [`ProjectSettings.asset`](ProjectSettings/ProjectSettings.asset:705) (все платформы, проверено ин-движком: Android/Standalone/WebGL/iPhone) — читателей нет;
- [x] Define `Localization_yg` **удалён** (задача 3, вариант B): снят со всех 11 платформ, читателей в `Assets/Scripts` нет; стойкость подтверждена refresh'ем и домен-релоадом (grep = 0). Остаточный риск — **R11**;
- [x] **`Basic.autoDefineSymbols: 1 → 0`** в [`SettingsYG2.asset:55`](Assets/PluginYourGames/Resources/SettingsYG2.asset:55) — санкция продюсера (вариант B, F7). **Следствие:** defines модулей YG2 больше не синхронизируются автоматически, включая `PLUGIN_YG_2`, `TMP_YG2`, `NJSON_YG2` и платформенные `*Platform_yg`; при добавлении/удалении модуля через окно YG2 их нужно править **вручную**. **Обратимость:** одна строка; возврат `1` + refresh восстановит авто-режим (и вернёт `Localization_yg`, пока существует папка модуля). Условие возврата к `1` — удаление папки `Modules/Localization`, что невозможно без правки плагина;
- [x] `setLanguageMod` = **2 (`DoNotChangeLanguageStartup`)** в [`SettingsYG2.asset:33`](Assets/PluginYourGames/Resources/SettingsYG2.asset:33). Язык теперь применяет **`LanguageService`** (задача 3) — на переходный период язык на старте не применяет никто (санкционировано продюсером).
- [x] **Гейт G1:** вариант **A** — источник = язык аккаунта ЯИ. Перенос выполнен: [`YandexLanguage.jslib`](Assets/_Platform/YandexGames/YandexLanguage.jslib:3) (`AstroDriftLangRequest_js`, тело `ysdk.environment.i18n.lang`) + [`YandexLanguageSource.GetAccountLanguage()`](Assets/_Platform/YandexGames/YandexLanguageSource.cs:19). Проверено ин-движком: `.meta` (WebGL only), компиляция профилей, отсутствие дубля символа. Ограничение: `YandexGamesPlatform_yg` не определён → модульный `LangRequest_js` мёртв; на реальном ЯИ **не проверено** — проверка отложена (F5) до первой сборки ЯИ после милстоуна. `GeneralLanguage_js` (язык браузера) в проекте остаётся без читателя — использовался только как запасной вариант B, отклонён продюсером.
- [x] **TMP Fallbacks** (задача 4, §7.1 п.6): `m_FallbackFontAssetTable` обоих базовых шрифтов → `LiberationSans SDF - Fallback` (Dynamic, 25 глифов, `hasCyrillic=True`, `hasCJK_Han=False`). Это инфраструктура под будущие CJK-строки; сам CJK-шрифт — вне скоупа милстоуна;
- [x] Решение D1 (`unsupported → ru`) известно команде; триггер пересмотра — локаль `tr`. Проверено в задаче 3 (§6.3).

## Приложение B. Связанные файлы (карта изменений)

| Файл | Действие | Задача |
|---|---|---|
| `Assets/Fonts/Montserrat-SemiBold SDF.asset` | Пересобрать (Static, 1024, Padding 9, Point Size 60, SDFAA, kerning off, чарсет §4.2) | 1 |
| `Assets/PluginYourGames/Modules/AutoTranslateLangs/` | **Удалено** (папка + `.meta`, 26 файлов); define снят; реестр модулей YG2 очищен | 2 ✅ |
| `Assets/_Platform/YandexGames/YandexLanguage.jslib` (+ `.meta`) | **Создано**: перенос `LangRequest_js` → `AstroDriftLangRequest_js` (тело идентично), `.meta` WebGL-only | 2 ✅ |
| `Assets/_Platform/YandexGames/YandexLanguageSource.cs` | **Создано**: `GetAccountLanguage()` — источник гейта G1; вне WebGL/ЯИ no-op | 2 ✅ |
| `Assets/_Platform/YandexGames/YandexGamesInstaller.cs:35` | **Сделано**: чтение `YG2.lang` убрано из лога (B3 закрыт) | 2 ✅ |
| `Assets/Scripts/Core/AstroDriftLanguageBridge.cs` | **Удалено** (+ `.meta`) | 3 ✅ |
| `Assets/Scripts/Core/LanguageService.cs` | **Создано**: маппинг §6.1 + D1, C1–C3, заглушка `TryApplyPlayerOverride()` | 3 ✅ |
| `Assets/Scripts/Core/Bootstrap.cs` | **Сделано**: C2 — `Build` по «И» (`PlatformBoot.Ready && StartupApplied`) | 3 ✅ |
| `Assets/PluginYourGames/Resources/SettingsYG2.asset` | **Сделано** (F7, вариант B): `autoDefineSymbols: 1 → 0` | 3 ✅ |
| `Assets/Scripts/UI/Typography.cs` + `Core/TypographyConfig.cs` | **Сделано**: `langCode → localeCode`; событие `LanguageChanged` снято, подписка на `SelectedLocaleChanged` — у `LanguageService` | 4 ✅ |
| `Assets/Scripts/UI/TypeRoleTag.cs` | **Создано**: `TypeRoleTag` (поле `role`, self-apply в `OnEnable`) + `TypeRoleApplier.ApplyAll()` | 4 ✅ |
| `Assets/Fonts/Montserrat-Bold SDF.asset`, `Assets/Fonts/Montserrat-SemiBold SDF.asset` | **Сделано**: `m_FallbackFontAssetTable` → `LiberationSans SDF - Fallback` (§7.1 п.6) | 4 ✅ |
| `Assets/Scripts/UI/GameUI.cs` | **Сделано** (4): `ApplyTypography` + подписчик `LanguageChanged` удалены. **Сделано** (5): `BindPauseTexts` удалён, `L10n.Bind` → `LocalizeStringEvent`; `Arguments` + гарды §3.4 (`_bestArgs`/`_lastBestShown`), `ResetLanguageGuards()`, `GetLse()`, рантайм-смены entry (щит в обоих состояниях, `DeathLevel`) | 4–5 ✅ |
| `Assets/Scripts/Editor/MenuPrefabBuilder.cs` | **Сделано**: LSE + теги — варианты кнопок, LevelCard, блок StartPanel (B4). Спрайты — из контейнера, поиск по имени удалён | 5 ✅ |
| `Assets/Scripts/Editor/LevelUpPrefabBuilder.cs` | **Сделано**: LSE + теги — панель, бейдж, реролл, пустые LSE карт. Спрайты — из контейнера | 5 ✅ |
| `Assets/Scripts/Editor/PrefabBuilderSprites.cs` + `.asset` (+`.meta`) | **Создано**: контейнер входных ссылок билдеров (13 полей) + громкий стоп до записи (решение продюсера) | 5 ✅ |
| `Assets/Scripts/Core/AstroDriftSceneSetup.cs` | **Сделано**: LSE (+ теги где была роль) при создании текстов Death/Pause; `NewTextButton(...)` получил параметр `key` | 5 ✅ |
| `Assets/Prefabs/**` (9 префабов), `Assets/Scenes/Game.unity` | **Сделано**: 19 `TypeRoleTag` (16 в префабах + 3 в сцене), базлайн 0; состав GameObject'ов сцены не изменился (42↔42) | 5 ✅ |
| `Assets/Scripts/Core/PerkDefinition.cs` | **Сделано**: `LocalizedString title/desc` вместо `titleKey/descKey` | 6 ✅ |
| `Assets/Scripts/Core/AstroDriftSetup.cs` | **Сделано**: `MakeRef()` → `SetReference("GameTexts", …)`; сиды `titleEntry`/`descEntry`; легаси-парсер `.txt` под `LocalizedString` | 6 ✅ |
| `Assets/Resources/Perks/*.asset` (8 шт.) | **Сделано**: разовая миграция ссылок (16 ссылок `GameTexts`, ключи не переименованы); повторный `Setup Assets` — 0 изменений | 6 ✅ |
| `Assets/Scripts/Core/PickupConfig.cs` | **Сделано**: новое поле `PickupDef.name` (`LocalizedString`), 3 ссылки в `PickupConfig.asset` | 6 ✅ |
| `Assets/Scripts/Spawners/PickupManager.cs` | **Сделано**: чтение из дефа (`GetLocalizedString()` + проверка N3), `PickupNameKey` удалён | 6 ✅ |
| `Assets/Scripts/UI/PerkChoiceUI.cs` | **Сделано**: `FillCard` — вариант А (`StringReference = def.title/desc`), `SetEntry` удалён; `PerkTitle` — `GetLocalizedString()` | 6 ✅ |
| `Assets/Scripts/UI/LocalizedText.cs` (`L10n`) | Ужать до `Get`/`GetFormatted` | 7 |
| `Assets/Scripts/UI/LocalizedTextUI.cs` | Подписка `LanguageChanged` снята (4); **удалить** файл в задаче 5 (единая отсечка) | 4–5 |
| `Assets/Localizations/GameTexts_{ru,en}.asset` | Удалить сироту «ДЕРЕВО» | 8 |
| Новый editor-скрипт валидатора | **Создать** (ключи + coverage чарсета) | 8 |
| `ProjectSettings/ProjectSettings.asset` | **Сделано**: defines `AutoTranslateLangs_yg` (задача 2) и `Localization_yg` (задача 3, вариант B) удалены со всех платформ | 2–3 ✅ |
