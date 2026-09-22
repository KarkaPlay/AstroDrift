# GDD: Death Screen v3 + мета-прогрессия на главном экране

Статус: implementation-ready (без кода). Референс канваса 1080×1920, все ноды — anchor = pivot = (0.5, 0.5),
позиции в таблицах указаны ОТНОСИТЕЛЬНО центра родителя. Единственный источник сборки сцены —
[`AstroDriftSceneSetup.cs`](../Scripts/Core/AstroDriftSceneSetup.cs:311) (блок «DeathPanel»), главный экран —
[`MenuPrefabBuilder.cs`](../Scripts/Editor/MenuPrefabBuilder.cs:253) + [`StartPanel.prefab`](../Prefabs/Menu/StartPanel.prefab).

## 1. Решения (приняты здесь, владельцу не возвращаются)

| Вопрос | Решение |
|---|---|
| `DeathNewBest` («NEW BEST!») | УДАЛИТЬ узел, поле `deathNewBest`, ключ `new_best`. Фидбэк рекорда не теряется: правило «одно золото в кадре» перенаправляется — при `newBest` золотым становится ЧИСЛО рекорда (`DeathBest`), а CTA Continue — белый. Без `newBest` — наоборот (CTA золотой). Плюс сохраняется `AudioManager.PlayRecord()`. |
| `DeathXp`, `DeathLevel`, `DeathXpBarBg/Fill`, `DeathUnlocked` | УДАЛИТЬ с экрана смерти. Мета-блок переезжает на главный экран (§7). Ключи `xp_gain`, `level_up_line` переиспользуются там; `level_line`, `unlocked_title` — удаляются (см. §4). |
| `SepLine` | УДАЛИТЬ. Границу «предложение / домой» теперь несут спрайты кнопок. |
| `ContinueTimerLine` (линия 600×4) | ЗАМЕНИТЬ на прогресс-бар таймера оффера в кнопке Continue: `ContinueTimerBg` + `ContinueTimerFill` (`UiProgressBar.Set`, спрайт `RoundedBar.png`), бар убывает 1 → 0. Поле `continueTimerLine` → переименовать в `continueTimerFill`. `_offerLineFullWidth` / `_offerLineRestWidth` удаляются. |
| `DeathScore`, `DeathBest` | Не удалять, а СМЕНИТЬ СМЫСЛ: теперь это только ЧИСЛА (без подписи в строке). Подписи — отдельные ноды. Причина: у подписи и числа разные шрифтовые роли/размер/цвет (30 px Secondary vs 88 px DeathScore), и в спрайте `DeathScore Panel.png` под них два фиксированных слота. Одной строкой «СКОР 24 500» этого не собрать. Дом-паттерн `StartBest`/`StartBestValue` повторяется 1:1. |
| Счёт: отдельные ноды «подпись» + «число» | ДА, для обеих строк (4 нода). Лейблы — статичные LSE (поля в `GameUI` не нужны), значения — сериализованные поля (обновляются кодом). |
| Цвет бара таймера | `Palette.XpBar` (#66FF66) — владелец просил «такой же, как XP-бар». Единственное золото экрана — CTA (§ выше). |

## 2. Дерево узлов экрана смерти (сверху вниз)

| # | Имя | Тип | Родитель | Anchor/Pivot | Позиция | Размер | Спрайт | Роль / шрифт | Ключ локализации |
|---|---|---|---|---|---|---|---|---|---|
| 0 | `DeathPanel` | RectTransform + Image(a=0) + CanvasGroup | Canvas | 0.5/0.5 | (0,0) | 1080×1920 | — | — | — |
| 1 | `DeathScrim` | Image | DeathPanel | 0.5/0.5 | (0,0) | 1680×2520 | — (a=0.7, raycast=false) | — | — |
| 2 | `DeathSkull` | Image | DeathPanel | 0.5/0.5 | (0, 690) | 200×200 | [`Skull.png`](../New%20UI/Skull.png) | — | — |
| 3 | `DeathTitle` | TextMeshProUGUI | DeathPanel | 0.5/0.5 | (0, 500) | 900×110 | — | `TypeRole.Title`, 72, `Palette.ScoreText` | `death_title` |
| 4 | `DeathSubtitle` | TextMeshProUGUI | DeathPanel | 0.5/0.5 | (0, 420) | 900×60 | — | `TypeRole.Secondary`, 30, `Palette.SecondaryText` | `death_subtitle` |
| 5 | `DeathScorePanel` | Image | DeathPanel | 0.5/0.5 | (0, 60) | 800×478 | [`DeathScore Panel.png`](../New%20UI/DeathScore%20Panel.png) (a=1) | — | — |
| 5.1 | `DeathBestLabel` | TextMeshProUGUI | DeathScorePanel | 0.5/0.5 | (0, 150) | 700×50 | — | `TypeRole.Secondary`, 30, `Palette.SecondaryText` | `best_label` |
| 5.2 | `DeathBest` | TextMeshProUGUI | DeathScorePanel | 0.5/0.5 | (0, 75) | 700×80 | — | `TypeRole.DeathScore`, 64, `Palette.ScoreText` (→`Palette.Gold` при newBest) | `best_value` |
| 5.3 | `DeathRunLabel` | TextMeshProUGUI | DeathScorePanel | 0.5/0.5 | (0, -60) | 700×50 | — | `TypeRole.Secondary`, 30, `Palette.SecondaryText` | `score_label` |
| 5.4 | `DeathScore` | TextMeshProUGUI | DeathScorePanel | 0.5/0.5 | (0, -140) | 700×110 | — | `TypeRole.DeathScore`, 88, `Palette.ScoreText` | `best_value` |
| 6 | `Btn_Continue` | Image + Button + CanvasGroup | DeathPanel | 0.5/0.5 | (0, -400) | 860×285 | [`Death Continue Ad.png`](../New%20UI/Death%20Continue%20Ad.png) | — | — |
| 6.1 | `ContinueText` | TextMeshProUGUI | Btn_Continue | 0.5/0.5 | (0, 70) | 800×60 | — | `TypeRole.Cta`, 40, `Palette.UiAccent` (белый при newBest) | `continue_cta` |
| 6.2 | `ContinueCaption` | TextMeshProUGUI | Btn_Continue | 0.5/0.5 | (0, 18) | 800×40 | — | `TypeRole.Secondary`, 22, `Palette.SecondaryText` | `continue_caption` |
| 6.3 | `ContinueTimerBg` | Image | Btn_Continue | 0.5/0.5 | (0, -85) | 520×16 | — (тёмная, a≈0.25, `RaycastTarget=false`) | — | — |
| 6.4 | `ContinueTimerFill` | Image (Filled/Horizontal/Left) | ContinueTimerBg | stretch 0..1 | (0,0) | 0 (offsetMin/Max = 0) | [`RoundedBar.png`](../New%20UI/Generated/RoundedBar.png) | цвет `Palette.XpBar`, raycast=false | — |
| 7 | `Btn_Home` | Image + Button + CanvasGroup | DeathPanel | 0.5/0.5 | (0, -760) | 420×168 | [`Home Button.png`](../New%20UI/Home%20Button.png) | — | — |
| 7.1 | `HomeText` | TextMeshProUGUI | Btn_Home | 0.5/0.5 | (0, 0) | 380×60 | — | `TypeRole.Button`, 34, `Palette.ScoreText` | `home_to_menu` |

Правила иерархии:
- `DeathScrim` — ПЕРВЫЙ ребёнок `DeathPanel` (нижний слой), `raycastTarget = false`.
- `ContinueText/ContinueCaption/ContinueTimerBg` — ДЕТИ `Btn_Continue`: истечение оффера гасит весь блок ОДНИМ
  fade CanvasGroup'а родителя, а не тремя раздельными (текущее поведение). Их `raycastTarget = false`, тапы
  ловит только сам `Btn_Continue` (Image кнопки = `targetGraphic`).
- Внутренние координаты `DeathScorePanel` (5.1–5.4) — ОРИЕНТИР: владелец выравнивает под реальные слоты спрайта.
  Обязательны только порядок и размеры шрифтов.
- Импорт спрайтов: Texture Type = Sprite (2D and UI), Sprite Mode = Single, Alpha Is Transparency = on для `Skull.png`.

## 3. Каскад входа (PlayDeathIn) — обновлённая задержка

Длительности и кривые не меняются (всё `UiAnim.EaseOutSoft`, unscaled):

| Узел | Время старта | Длительность | Сдвиг (`SlideFade`) |
|---|---|---|---|
| `DeathSkull` | 0.00 | 0.35 | `SlideTitle` (0, -60) |
| `DeathTitle` | 0.07 | 0.35 | `SlideTitle` |
| `DeathSubtitle` | 0.12 | 0.35 | `SlideBest` (0, 32) |
| `DeathScorePanel` | 0.19 | 0.40 | `SlideScore` (0, 48) |
| `Btn_Continue` (если offerVisible) | 0.26 | 0.30 | `SlideButton` (0, 24) |
| `Btn_Home` | 0.33 | 0.30 | `SlideButton` |

Перед каскадом: `UiProgressBar.Set(continueTimerFill, 1f)` — бар ВСЕГДА стартует полным.

## 4. Локализация

| Ключ | RU | EN | Статус |
|---|---|---|---|
| `death_title` | ВЫ ПОГИБЛИ | YOU DIED | НОВЫЙ |
| `death_subtitle` | ВАШ ПУТЬ ЗАКОНЧЕН | YOUR JOURNEY IS OVER | НОВЫЙ |
| `best_label` | РЕКОРД | BEST | Переиспользован без правки значения (был только на StartPanel — теперь и на смерти) |
| `best_value` | {0} | {0} | Переиспользован как «чистое число»: ключ для `DeathBest` И `DeathScore` (оба подаются кодом с Arguments) |
| `score_label` | ТЕКУЩИЙ РЕЗУЛЬТАТ | RUN SCORE | НОВЫЙ |
| `continue_cta` | ПРОДОЛЖИТЬ ЗА РЕКЛАМУ | CONTINUE FOR AD | ЗНАЧЕНИЕ ИЗМЕНЕНО (было «ПРОДОЛЖИТЬ» / «CONTINUE»); используется только на экране смерти |
| `continue_caption` | Вернитесь в игру и сохраните свой прогресс | Return to the game and keep your progress | ЗНАЧЕНИЕ ИЗМЕНЕНО (было «ЗА ПРОСМОТР РЕКЛАМЫ» / «WATCH AN AD»); используется только на экране смерти |
| `home_to_menu` | В МЕНЮ | MAIN MENU | НОВЫЙ (для смерти). Ключ `home` НЕ трогаем — он шарится с `PausePanel/Btn_Home` |
| `xp_gain` | +{0} XP | +{0} XP | Переиспользован, переезд на главный экран (`StartXpGain`) |
| `level_up_line` | УРОВЕНЬ {0} → {1} | LEVEL {0} → {1} | Переиспользован, переезд на главный экран (капшн LevelCard при level-up) |
| `home` | В МЕНЮ | HOME | БЕЗ ИЗМЕНЕНИЙ (только PausePanel) |

Удаляемые ключи (нет ни одного потребителя после редизайна): `score`, `new_best`, `level_line`, `unlocked_title`.
Ключи `unlock_*` и `unlock_tree_title` ОСТАЮТСЯ — их использует дерево разблокировок.

Правки в [`LocalizationValidator.cs`](../Scripts/Editor/LocalizationValidator.cs:104):
`SyncKeysWhitelist` → `{ combo, best_label, best_value, xp_gain, level_up_line, pilot_level_label }`;
`SceneSetupKeys` → `{ death_title, death_subtitle, best_label, best_value, score_label, continue_cta, continue_caption, home_to_menu, home, pause_title, resume }`.
Строка 192 (рантайм-смены entry) → `{ shield_used_today, shield_caption, level_up_line, xp_gain, reroll_caption_free }`.
Новые ключи добавляются в [`AstroDriftLocalizationSetup.cs`](../Scripts/Core/AstroDriftLocalizationSetup.cs:22) (AstroDrift → Setup Localization) — RU+EN сразу.

## 5. Семантика бара на кнопке Continue

Механика: `UiProgressBar.Set(continueTimerFill, remaining / fullDuration)` в существующем
`OfferTimerRoutine()` ([`GameUI.cs`](../Scripts/UI/GameUI.cs:449)) — unscaled, пауза при потере фокуса.

| Состояние | Бар | Continue-блок | Btn_Home |
|---|---|---|---|
| `adReady && !continueUsedThisRun` — оффер показан | 1 → 0 за `GameConfig.continueOfferDuration` (5 с) линейно | активен, `interactable = true` | активен |
| Тап по `Btn_Continue` во время оффера | таймер стоп, бар = 1 (готов к следующей смерти) | `PlayContinueOut()` — fade панели 0.25 s `EaseInQuick` | скрыт вместе с панелью |
| Таймер дошёл до 0 | 0 | `interactable = false` немедленно, затем ОДИН fade `CanvasGroup` корня 0.25 s `EaseInQuick` → `SetVisible(false)`. Аналитика `continue_timer_expired` — без изменений | остаётся, работает |
| Реклама не готова / continue уже использован в забеге | не создаётся, таймер не стартует | скрыт с самого каскада | активен |
| Возврат после aborted-рекламы (`ShowDeathPanelNoOffer`) | = 1 (сброс) | скрыт, таймер НЕ перезапускается | активен |

## 6. Удаляемые / изменённые узлы

УДАЛИТЬ полностью: `DeathNewBest`, `DeathXp`, `DeathLevel`, `DeathXpBarBg`, `DeathXpBarFill`, `DeathUnlocked`, `SepLine`, `ContinueTimerLine`.
ПЕРЕИМЕНОВАТЬ/ПЕРЕСТРОИТЬ: `ContinueTimerLine` → `ContinueTimerBg` + `ContinueTimerFill`; `DeathScorePanel` — НОВЫЙ узел (спрайт).
ПЕРЕИСПОЛЬЗОВАТЬ (смена смысла): `DeathScore`, `DeathBest`, `ContinueText`, `ContinueCaption`, `Btn_Continue`, `Btn_Home`, `DeathScrim`.
`Btn_Home` был текстовой кнопкой (`NewTextButton`, ключ `home`) → теперь Image-спрайт `Home Button.png` + Button + `HomeText` с ключом `home_to_menu`.

## 7. Сериализованные поля `GameUI` после редизайна

МЁРТВЫЕ (удалить поля и все обращения): `deathNewBest`, `deathXp`, `deathLevel`, `deathXpBarFill`, `deathUnlocked`, `continueTimerLine`.
СМЕНА СМЫСЛА: `deathScore` (было «СЧЁТ {0}» → только число, ключ `best_value`), `deathBest` (было «РЕКОРД {0}» → только число),
`continueBtn` (была невидимая тап-зона → корень спрайт-кнопки с CanvasGroup и детьми),
`pilotLevelText` (была статичная подпись → двухсостоятельный капшн `pilot_level_label` ↔ `level_up_line`),
`levelCard` (та же роль + новый API анимации), `xpBarFill` (остаётся fallback'ом, если `levelCard == null`).
НОВЫЕ: `continueTimerFill` (RectTransform), `deathSkull` / `deathTitle` / `deathSubtitle` / `deathScorePanel` (RectTransform — каскад),
`startXpGain` (TextMeshProUGUI, главный экран).

## 8. Заглушечные анимации на главном экране

Точка входа — существующий [`GameUI.ShowStartCascade()`](../Scripts/UI/GameUI.cs:1088) (его вызывает `GameManager.EnterMenu` после `PlayPanelOut`, фикс-возврат в меню уже работает). Никаких новых систем: подписка
`GameUI` на уже существующее событие `PilotProgressManager.OnRunXpGranted` выставляет флаг `_pendingXpGain = delta`.

### (a) Анимация получения XP
1. `ShowStartCascade` → `RefreshPilotBlock()` уже обновляет `LevelCardUI.Refresh()` на НОВОЕ значение → затем
   бар принудительно ставится на СТАРОЕ (`UiProgressBar.Set(barFill, pilot.ProgressToNextLevelBeforeLastRun())` —
   новый маленький хелпер в существующем `PilotProgressManager`, считает прогресс от `Xp − LastRunXp` и `LevelBeforeLastRun`).
2. Тик `UiProgressBar.Set(barFill, Mathf.Lerp(old, new, k))`: старт 0.64 s (после прихода LevelCard: 0.29 + 0.35),
   длительность 0.80 s, кривая `UiAnim.EaseOutSoft`, unscaled, шаг — кадровый (или `Mathf.MoveTowards`, 6 шагов).
3. Одновременно `StartXpGain` («+{0} XP», ключ `xp_gain`): fade 0→1 за 0.15 s, hold 0.60 s, fade 1→0 за 0.25 s `EaseInQuick`,
   `deactivateWhenHidden: true`. Позиция — над `LevelCard` (например (0, 372), не в таблице смерти).
4. **Если `_pendingXpGain == 0`** (игрок вернулся без XP, первый вход, повторный показ меню) — ни бара, ни лейбла:
   бар просто стоит на новом значении. Флаг сбрасывается в 0 после проигрывания → повторный
   `ShowStartImmediate()`/`ShowStartCascade()` анимацию НЕ переигрывает.

### (b) Анимация повышения уровня
Условие: во время того же возврата `PilotLevel > LevelBeforeLastRun` (в т.ч. несколько уровней сразу — одна строка диапазона).
1. Капшн `LevelCard/LevelLabel`: `SetLocalized(pilotLevelText, "level_up_line", LevelBeforeLastRun, PilotLevel)`
   (та же рантайм-смена entry, что раньше делал `deathLevel`). Показывается 1.6 s, затем возврат на `pilot_level_label`.
2. Пульс числа уровня (`LevelCardUI` → новый метод `PulseLevelNumber`): масштаб 1 → 1.15 → 1 за 0.45 s
   (пик на 40 % времени), кривая `UiAnim.EaseOutSoft` для возврата, старт на 0.85 s (после прохода бара).
   Реализуется мини-хелпером `UiAnim.Pulse(RectTransform, peak, dur)` — одна корутина, без внешних твинеров.
3. Порядок при level-up: показ `level_up_line` и пульс стартуют ПОСЛЕ завершения заливки бара (0.85 s), затем
   капшн возвращается на 2.5 s. Повторно — только при новом гранте XP.
4. Если игрок вернулся в меню в момент, когда уровень уже поднят ранее (флаг пуст) — капшн статичен (`pilot_level_label`),
   пульса нет.

## 9. Критерии приёмки

1. Порядок узлов сверху вниз ровно: Skull → Title → Subtitle → ScorePanel (best label+value, run label+value) → Continue-блок → Home. Нет `SepLine`, `DeathNewBest`, XP/Level/XpBar/Unlocked.
2. Все 5 спрайтов назначены по указанным путям; при входе на смерть в консоли НЕТ `UiProgressBar: ... нет спрайта`.
3. Смена локали RU↔EN на экране смерти переводит все 8 строк; `PausePanel/Btn_Home` рендерит ровно как раньше (ключ `home` не изменён).
4. Бар Continue убывает 1 → 0 за `continueOfferDuration` (5 с), встаёт на паузу в фоне и продолжает с остатка.
5. По истечении: Continue-блок гаснет за 0.25 s, после начала fade тапы по нему невозможны, `Btn_Home` работает, `continue_timer_expired` отправлен один раз.
6. 3 смерти подряд → бар каждый раз стартует полным (нет остаточной заливки/нулевой ширины).
7. Тап Continue в окне оффера → реклама → возврат: бар полный, таймер не идёт; после aborted-рекламы Continue скрыт.
8. Возврат в меню без XP: бара-анимации нет, `StartXpGain` не появляется, пульса уровня нет.
9. Возврат с XP: бар доезжает old → new за 0.80 s, «+N XP» появляется и гаснет; при повторных заходах в меню анимация не повторяется.
10. Level-up: капшн `УРОВЕНЬ N → M` держится ~1.6 s и возвращается на `УРОВЕНЬ ПИЛОТА`; число уровня пульсирует 1 → 1.15 → 1 за 0.45 s; один раз.
11. `AstroDrift → Setup Scene UI` не печатает `MISSING` для полей `GameUI`; удалённые поля больше не встречаются в коде.
12. `AstroDrift → Validate Localization` не сообщает о missing/unused для перечисленного набора; удалённые ключи отсутствуют и в таблицах, и в вайтлистах.
13. Роли применены: Title / Secondary / CTA / DeathScore / Button; смена локали применяет шрифты через `TypeRoleApplier` (без ручных применений в `GameUI`).

## 10. Риски и заметки

- Красная/зелёная читаемость бара таймера: бар сопровождается подписью, поэтому цвет — не единственный сигнал.
- `best_value` = 「{0}」 обязан подаваться кодом с Arguments ДО показа: при пустых Arguments TMP покажет литерал `{0}`.
- `Format()` в `GameUI` — `N0` с InvariantCulture (тысячи через запятую и в RU). Поведение наследуется, не менять в этой итерации.
- Порядок правок: сначала локализация (Setup Localization + вайтлисты), затем `AstroDriftSceneSetup` (узлы/поля), затем префаб главного экрана, в конце — анимации.
