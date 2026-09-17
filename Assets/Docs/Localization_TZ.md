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
   2. Fallback: 2048×1024 @60 (payload 2 МБ; файл ~2–2.5 МБ) — допустим, цель «≤2 МБ» при этом не применяется;
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

Без добавленных диапазонов Static-пересборка даёт квадраты в `tap_to_play` («ТАП — СТАРТ»), `level_up_line` («УРОВЕНЬ {0} → {1}»), EN-описаниях перков («−15%»). Максимальный кодпоинт таблиц — `U+2212`, т.е. чарсет полон для текущих таблиц; покрытие новых символов идёт через валидатор (§11.2, п. 6). Итого ~450+ глифов (было 99) — посадка в 1024² при Point Size 60 опирается на Bold (220 глифов в 1024×512 при 59); лестница фолбэков — §4.1, п. 3.

### 4.3. Критерии приёмки

- [ ] Отчёт Creator: **0 failed/missing glyphs** (первый и главный критерий — N1);
- [ ] Размер файла: **≤2 МБ** (primary 1024²) или ≤2.5 МБ (fallback 2048×1024 с записанной причиной);
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
- Удалить define `Localization_yg` из `ProjectSettings/ProjectSettings.asset` (после шагов выше у него не остаётся читателей — проверить grep'ом `Localization_yg` по `Assets/Scripts`).

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

- [ ] Смена локали переключает шрифты тегированных нод (проверка: временный оверрайд на тестовом шрифте, затем откат);
- [ ] Нетегированные ноды (HUD и др.) не меняются; пустые оверрайды → базовые слоты; пустые слоты → LiberationSans;
- [ ] Ноль ссылок на YG2, `LanguageChanged`, `NotifyLanguageChanged` в UI-слое.

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

- [ ] Все экраны RU/EN визуально как было (ин-движок, Play Mode на активном профиле RuStore, вкл. паузу и оверлей перка);
- [ ] Смена локали посреди сессии обновляет все компонентные тексты (включая паузу, оверлей перка, подпись щита в обоих состояниях);
- [ ] `RefreshHud` не аллоцирует на каждое изменение счёта (гард §3.4 + сброс при смене локали);
- [ ] **Ноды, впервые получающие `TypeRoleTag` (`Title`/`Description` карт перков), не меняют начертание:** `Typography.ApplyFont` выставляет `fontStyle = FontStyles.Normal`, а карты раньше не проходили через апплаер вообще. Запечённые шрифты совпадают с ролевыми (`Cta` = `ctaSemiBold`, `Body` = `bodyRegular`), ожидаемый дифф — нулевой; отличие = провал приёмки;
- [ ] Ноль `L10n.Bind` в проекте; **`LocalizedTextUI` удалён в этой задаче** (единая отсечка — §15).

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

- [ ] 8 ассетов перков + 3 дефа пикапов имеют заполненные ссылки (видно в инспекторе с превью);
- [ ] `Setup Assets` повторным прогоном ничего не меняет;
- [ ] Карты перков (включая дубли при reroll) и флоатеры показывают корректные тексты RU/EN;
- [ ] Смена локали при открытом оверлее обновляет карты (владелец подписки — компонент);
- [ ] Ноль чтений `titleKey` / `descKey` в проекте.

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
- [ ] Флоатеры корректны; поведение при неготовой таблице — фолбэк, не исключение.

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

---

## 14. Приёмочная матрица

| Проверка | RuStore (активный профиль) | Editor | ЯИ WebGL (отложено, F5) |
|---|---|---|---|
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

> Примечание к C1: на момент baseline `LocalizationSettings.SelectedLocale` = `ru`, но `AvailableLocales.Locales.Count` = **0** — список локалей пуст до `InitializationOperation.Completed`. Это подтверждает требования C1 задачи 3, а не дефект таблиц.

---

## Приложение A. Финальное состояние настроек проекта (зафиксировать после задачи 2)

- [ ] Модуль Localization: **выключен** (или задокументировано, почему оставлен, + доказательство отсутствия влияния на язык);
- [ ] Модуль AutoTranslateLangs: **выключен**, компоненты сняты, папка удалена или задокументирована;
- [ ] Define `AutoTranslateLangs_yg` **удалён** из `ProjectSettings/ProjectSettings.asset` (все платформы) — читателей нет;
- [ ] Define `Localization_yg` **удалён** после задачи 3 (читателей нет: мост и `Typography` переведены на `SelectedLocale`);
- [ ] `setLanguageMod`: значение зафиксировано + комментарий, кто теперь применяет язык (`LanguageService`);
- [ ] **Гейт G1:** источник кода языка (перенос `LangRequest_js` на `i18n.lang` / язык браузера) — что проверено **ин-движком**, то записано; проверка на реальном ЯИ **отложена** (F5, билды не собираются) до первой сборки ЯИ после милстоуна;
- [ ] Решение D1 (`unsupported → ru`) известно команде; триггер пересмотра — локаль `tr`.

## Приложение B. Связанные файлы (карта изменений)

| Файл | Действие | Задача |
|---|---|---|
| `Assets/Fonts/Montserrat-SemiBold SDF.asset` | Пересобрать (Static, 1024, Padding 9, Point Size 60, SDFAA, kerning off, чарсет §4.2) | 1 |
| `Assets/PluginYourGames/Modules/AutoTranslateLangs/` | Отключить, снять компоненты, удалить по возможности | 2 |
| `_Platform/YandexGames/*.jslib` (перенос, опционально) | `LangRequest_js` из `Language.jslib` — по решению гейта G1 | 2 |
| `Assets/_Platform/YandexGames/YandexGamesInstaller.cs:35` | Заменить `YG2.lang` на источник `LanguageService` (+ `:10` комментарий) | 2–3 |
| `Assets/Scripts/Core/AstroDriftLanguageBridge.cs` | **Удалить** | 3 |
| `Assets/Scripts/Core/LanguageService.cs` | **Создать** (маппинг, C1–C3, триггер апплаера, сброс гардов) | 3 |
| `Assets/Scripts/Core/Bootstrap.cs` | C2: `Build` ждёт `LanguageService.StartupApplied` | 3 |
| `Assets/Scripts/UI/Typography.cs` + `Core/TypographyConfig.cs` | Коды локалей, подписка на `SelectedLocaleChanged` | 4 |
| `Assets/Scripts/UI/TypeRoleTag.cs` | **Создать** (`TypeRoleTag` + `TypeRoleApplier`) | 4 |
| `Assets/Scripts/UI/GameUI.cs` | Удалить `ApplyTypography`, подписчика `LanguageChanged`, `BindPauseTexts`; `Arguments` + гарды; рантайм-смены entry (щит, `DeathLevel`) | 4–5 |
| `Assets/Scripts/Editor/MenuPrefabBuilder.cs` | LSE + теги: варианты кнопок, LevelCard, **блок StartPanel** (B4) | 5 |
| `Assets/Scripts/Editor/LevelUpPrefabBuilder.cs` | LSE + теги: панель, бейдж, реролл, пустые LSE + теги на картах | 5 |
| `Assets/Scripts/Core/AstroDriftSceneSetup.cs` | LSE (+ теги где была роль) при создании текстов Death/Pause | 5 |
| `Assets/Scripts/Core/PerkDefinition.cs` | `LocalizedString title/desc` вместо `titleKey/descKey` | 6 |
| `Assets/Scripts/Core/AstroDriftSetup.cs` | Сиды и парсер под `LocalizedString` (перки + пикапы) | 6 |
| `Assets/Resources/Perks/*.asset` (8 шт.) | Разовая миграция ссылок (до удаления полей из класса) | 6 |
| `Assets/Scripts/Core/PickupConfig.cs` | Новое поле `PickupDef.name` | 6 |
| `Assets/Scripts/Spawners/PickupManager.cs` | Чтение из дефа, удалить `PickupNameKey` | 6 |
| `Assets/Scripts/UI/PerkChoiceUI.cs` | `FillCard` — вариант А; `PerkTitle` — `GetLocalizedString()` | 6 |
| `Assets/Scripts/UI/LocalizedText.cs` (`L10n`) | Ужать до `Get`/`GetFormatted` | 7 |
| `Assets/Scripts/UI/LocalizedTextUI.cs` | **Удалить** в задаче 5 (единая отсечка) | 5 |
| `Assets/Localizations/GameTexts_{ru,en}.asset` | Удалить сироту «ДЕРЕВО» | 8 |
| Новый editor-скрипт валидатора | **Создать** (ключи + coverage чарсета) | 8 |
| `ProjectSettings/ProjectSettings.asset` | Удалить defines `AutoTranslateLangs_yg` (задача 2) и `Localization_yg` (задача 3) | 2–3 |
