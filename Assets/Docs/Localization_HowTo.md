# Как пользоваться локализацией — практический гайд

Короткая версия: **тексты лежат в одной таблице `GameTexts`, шрифты — в одном конфиге, а проверить всё можно одной кнопкой меню.**
Миграция завершена (тег `loc-migration-done`) — старый мост `L10n`/`AutoTranslateLangs` удалён, ручного кода на каждый язык больше нет.

---

## 0. Что делать в первый раз (5 шагов)

1. Обновить проект: `git fetch origin` и открыть Unity.
2. Открыть `Assets/Resources/TypographyConfig.asset` и убедиться, что 4 слота со шрифтами заполнены (если пусто — см. §4).
3. Прогнать валидатор: меню **`AstroDrift → Validate Localization (Missing translations report)`** (см. §5).
4. Прочитать отчёт в `Temp/LocalizationReport.txt` — это твоя «карта» текущего состояния.
5. Сделать учебное упражнение: поменять любой английский текст в `Assets/Localizations/GameTexts_en.asset`, в Play Mode нажать смену языка и увидеть результат.

Ожидаемое состояние после милстоуна: `missing=0`, `uncovered=0`, **`orphans=3`** — это известные 3 «сироты» (P3, решение: удалить пост-милстоуном). Всё остальное должно быть по нулям.

---

## 1. Три способа поставить текст — выбирай по месту

| Где текст | Как делать | Пример |
|---|---|---|
| Обычная нода UI в префабе или сцене | Компонент `LocalizeStringEvent` на ноде + ключ из таблицы | Кнопка, заголовок, лейбл |
| Строка собирается кодом или ноды нет | [`L10n.Get(key)`](Assets/Scripts/UI/LocalizedText.cs:19), [`L10n.GetFormatted(key, args)`](Assets/Scripts/UI/LocalizedText.cs:36) | Комбо-флоатер, дерево разблокировок |
| Текст в SO-ассете данных | Поле типа `LocalizedString` | [`PerkDefinition.title`](Assets/Scripts/Core/PerkDefinition.cs:13), [`PickupDef.name`](Assets/Scripts/Core/PickupConfig.cs:25) |

### Правила, которые нельзя нарушать

- **Ключи не переименовываются.** Никогда. Ни один. Если текст плохой — меняй перевод, не ключ.
- **`L10n.Get` может вернуть `null`** — таблица/локаль ещё не готова. Всегда проверяй `string.IsNullOrEmpty(...)` и держи фолбэк.
- **Не подписывайся сам на `StringChanged`** у `LocalizedString` — это запрещено (§3.6). Подписка только через `LocalizeStringEvent`. Единственный подписчик на смену локали — [`LanguageService`](Assets/Scripts/Core/LanguageService.cs:22).
- **Новый текст — только в таблицу `GameTexts`**, не в код и не в префаб напрямую.

---

## 2. Как посмотреть и поправить сами тексты

Все переводы — в `Assets/Localizations/`:

- `GameTexts Shared Data.asset` — **ключи** (структура коллекции),
- `GameTexts_ru.asset` — русские строки,
- `GameTexts_en.asset` — английские строки.

**Как менять:**
- Unity → **`Window → Asset Management → Localization Tables`** → коллекция `GameTexts`. Там видны все ключи и обе локали рядом — удобнее всего править здесь.
- Или прямо в файлах `.asset` (быстрее для массовых правок, но нужна аккуратность с YAML).

**Правка перевода существующего ключа** — безопасна: код, префабы и сцены трогать не нужно. Просто меняешь строку в `_ru` или `_en`.

**Когда добавляешь НОВЫЙ ключ** — это 3 шага, и про третий обычно забывают:

1. Добавить ключ в `GameTexts Shared Data.asset` (колонка ключей).
2. Заполнить текст в `GameTexts_ru.asset` и `GameTexts_en.asset`.
3. **Зарегистрировать ключ в валидаторе**, иначе он попадёт в отчёт как «сирота»:
   - ключ ставится нодой → достаточно `LocalizeStringEvent`, отдельной регистрации не нужно;
   - ключ ставится кодом синхронно → добавить в белый список [`SyncKeysWhitelist`](Assets/Scripts/Editor/LocalizationValidator.cs:104);
   - ключ ставится `AstroDriftSceneSetup` → добавить в [`SceneSetupKeys`](Assets/Scripts/Editor/LocalizationValidator.cs:119);
   - новый тип SO с полем `LocalizedString` → **одна строка** в [`CollectLocalizedStringData()`](Assets/Scripts/Editor/LocalizationValidator.cs:55) — там уже есть образцы для перков и пикапов.

---

## 3. Языки: кто и когда переключает

Переключением языка управляет **только** [`LanguageService`](Assets/Scripts/Core/LanguageService.cs:22): он читает язык платформы, держит «применённый» язык, чистит гарды и вызывает перерисовку типографики.

Тебе как автору контента это знать нужно ровно для одного: **если добавил локаль — проверь, что она есть в списке поддерживаемых локалей Unity Localization** и, при необходимости, в переопределениях шрифтов (§4). Код трогать не надо.

---

## 4. Шрифты и роли — один конфиг на весь UI

Файл: `Assets/Resources/TypographyConfig.asset` ([`TypographyConfig`](Assets/Scripts/Core/TypographyConfig.cs:22)).

**Четыре слота:**
- `headingLight` — заголовки и крупные цифры (например счёт на экране смерти),
- `titleBold` — заголовок оверлея левел-апа (пусто → берётся `headingLight`),
- `bodyRegular` — основной текст,
- `ctaSemiBold` — кнопки/CTA.

**Как подставить свой шрифт:**
1. Положить `.ttf` в `Assets/Fonts/`.
2. ПКМ по `.ttf` → `Create → TextMeshPro → Font Asset`.
3. Перетащить созданный TMP Font Asset в нужный слот конфига.
4. Нужен другой шрифт для конкретного языка → добавить элемент в `languageOverrides`, указать код локали (`ru`, `en`, `zh-Hans`…) и заполнить **только те слоты, которые отличаются**.

Пустой слот = фолбэк на `LiberationSans SDF` из TMP Settings — игра работает и с полностью пустым конфигом, ничего не «Missing».

**Роли текста.** Узел «носит» роль через компонент [`TypeRoleTag`](Assets/Scripts/UI/TypeRoleTag.cs:17). Доступные роли (`TypeRole`): `Title`, `Secondary`, `Cta`, `DeathScore`, `Button`, `Body`, `LevelUpTitle`.

- Роль назначает тот, кто создаёт ноду (билдер префаба или код сцены).
- **Поле называется `role` и переименовывать его нельзя** — на этом имени держатся билдеры префабов.
- Ноды **без** тега не трогаются вообще — это сделано специально, чтобы не ломать авторский вид (HUD и прочее).
- **`Typography` не меняет размер и трекинг** — они остаются авторскими, такими, как в префабе. Роль влияет только на сам шрифт.

---

## 5. Проверка — одна кнопка

Меню: **`AstroDrift → Validate Localization (Missing translations report)`**.

Что делает: собирает все реально используемые ключи из 6 источников (все `LocalizeStringEvent` в сценах и префабах, поля `LocalizedString` в данных, рантайм-ключи дерева разблокировок, белые списки, карта сцены), сверяет с таблицей и отдельно проверяет, что все символы строк есть в назначенных шрифтах.

**Куда смотреть результат:**
- Console — лог при зелёном итоге, предупреждение при замечаниях;
- файл `Temp/LocalizationReport.txt` — тот же отчёт в виде строк, удобно дифать.

**Главные поля отчёта:**

| Поле | Что значит | Норма |
|---|---|---|
| `missing` | ключ есть в коде/нодах, но без перевода в какой-то локали | 0 |
| `orphans` | ключ лежит в таблице, но нигде не используется | 0 (сейчас ожидаемо 3 — P3) |
| `uncovered` | символ есть в строках, но его нет ни в одном назначенном шрифте | 0 |
| `green` | итог: все три нуля | `true` |

**Покрытие символов** считается по **объединению** назначенных шрифтов: символ покрыт, если он есть хотя бы в одном из шрифтов, назначенных ролям. Добавил новый язык с иероглифами → почти наверняка получишь `uncovered` и должен будешь подставить шрифт с этими глифами (слот или `languageOverrides`).

---

## 6. Внимание: меню `AstroDrift → …` генерирует и перезаписывает

В проекте есть генераторы, которые создают префабы и разметку заново:

- `AstroDrift → Build Menu Prefabs` ([`MenuPrefabBuilder.BuildAll()`](Assets/Scripts/Editor/MenuPrefabBuilder.cs:41)),
- `AstroDrift → Build LevelUp Prefabs` ([`LevelUpPrefabBuilder.BuildAll()`](Assets/Scripts/Editor/LevelUpPrefabBuilder.cs:35)),
- `AstroDrift → Setup Scene UI` ([`AstroDriftSceneSetup.SetupSceneUI()`](Assets/Scripts/Core/AstroDriftSceneSetup.cs:32)),
- `AstroDrift → Setup Assets`, `AstroDrift → Setup Localization`, `AstroDrift → Repair Localization (one-shot)`.

**Правило:** ручная правка префаба/разметки живёт до следующего запуска соответствующего генератора — он перезапишет. Если правишь руками — правь либо генератор, либо не запускай его снова. `Repair Localization` — одноразовый ремонтный инструмент, в обычной работе не нужен.

---

## 7. Мини-шпаргалка «что куда»

| Задача | Куда идти |
|---|---|
| Поправить перевод | `Assets/Localizations/GameTexts_ru.asset` / `_en.asset` (или окно Localization Tables) |
| Добавить новый текст | Shared Data + обе локали + регистрация в валидаторе (§2) |
| Привязать текст к ноде | Компонент `LocalizeStringEvent` + ключ |
| Прочитать строку из кода | [`L10n.Get()`](Assets/Scripts/UI/LocalizedText.cs:19) с проверкой на `null` |
| Подставить другой шрифт | `Assets/Resources/TypographyConfig.asset` |
| Задать роль шрифта ноде | Компонент `TypeRoleTag`, поле `role` |
| Проверить всё разом | `AstroDrift → Validate Localization (Missing translations report)` |
| Отчёт для дифа | `Temp/LocalizationReport.txt` |

---

## 8. Пять запретов (иначе сломается молча)

1. Не переименовывать существующие ключи таблицы.
2. Не подписываться на `LocalizedString.StringChanged` напрямую.
3. Не переименовывать поле `role` в `TypeRoleTag`.
4. Не менять размеры/трекинг текста через типографику — только в префабе.
5. Не удалять вручную модуль локализации YG2: при ручном переимпорте модулей YG2 в `ProjectSettings/ProjectSettings.asset` может вернуться строка `Localization_yg`. Если это случилось — удалить её и заново прогнать grep по `ProjectSettings.asset`.

---

Справочник по формальным требованиям, обоснованиям решений и полной истории задач — в [`Localization_TZ.md`](Assets/Docs/Localization_TZ.md:1). Этот файл — только «как пользоваться».
