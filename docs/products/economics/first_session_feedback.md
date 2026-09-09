# Первая сессия: правки после игровой проверки

2026-09-09. Основа: замечания пользователя. Стартовый commit `72a48b40`, рабочее дерево чистое.

## Задачи

1. Обучение: заголовок и урок ниже фактической нижней границы верхнего HUD, отступ 14 единиц художественного масштаба. Заголовок крупнее, прописными буквами, bold. Подсказка сохраняет место рядом с заголовком.
2. Skip: размер 272×68 вместо 136×34, шрифт 26 вместо 13. Нижний отступ 14 единиц художественного масштаба; центр кнопки опущен.
3. Карточки заданий: подпись `+{0} XP`; строка отображается только при положительной награде. Число берётся из `QuestExperienceRewardPolicy`.
4. Выбор локаций: горизонтальный свайп через существующий `ChangeLocation`. Стрелки и правила открытия сохранены. Жест подавляет последующий случайный клик по карточке или стрелке.
5. Плашка цели на Win/Home/Select: кремовая карточка №1 реализована для четырёх случаев показа. [План](next_goal_cards_plan.md), [результат](next_goal_cards_delivery.md). Next Level сохраняет переход к следующему уровню; задание открывается отдельной кнопкой карточки.
6. Play в Home продолжает ближайший следующий открытый непройденный уровень, если текущий уже завершён. После поражения на ещё непройденном уровне сохраняет его. В конце доступного пути остаётся повтор выбранного уровня. Кнопки Replay и явный Select Level сохраняют свои маршруты.

Причина повторного level01: `HomeScreenController.OnClickBtnStart` загружал Game без выбора продолжения; выход с Win в меню оставлял `PlayerData.CurrentLevel` пройденным. Теперь `LevelManager.TryGetContinueLevelKey` читает сохранённый прогресс в порядке каталога, а Home сохраняет выбранное продолжение через `LevelController.SetCurrentLevel` перед загрузкой Game. Это работает и для уже сохранённого профиля с завершённым level01.

## XP

Первый сюжетный квест `story-primary-01_New_York-Morning`: 60 XP. Другие Story: 20 XP; цели уровня игрока: 0 XP. Daily: 5 XP.

XP выдаётся вместе с валютной наградой при нажатии Claim/«Забрать». Прохождение завершает условие задания. Код награды сохранён: `QuestManager.ClaimReward`, `QuestExperienceRewardPolicy`.

## Визуальные варианты

- [№1](../../../.temp/first-session-feedback/variant_1.png): кремовая карточка, тёмный контур, голубая кнопка. Ближе к существующей панели победы; рекомендованный вариант.
- [№2](../../../.temp/first-session-feedback/variant_2.png): компактный свиток с тёплой бумажной фактурой, мотив иконки Quests.
- [№3](../../../.temp/first-session-feedback/variant_3.png): светло-голубая плашка с объёмной рамкой в стиле HUD и Select Level.

На Win/Select выбран фиксированный правый боковой слот. На Home — место под Select Level, над нижней навигацией. При интеграции учитываются геометрия экрана и существующая панель активностей блока 3.

Основы: сохранённый игровой кадр Home `.temp/skateboard-android/fixed-menu-ready.png`; текущие `WinModal.uxml`, `SelectLevelScreen.uxml`, `HomeScreen.uxml` и их стили; игровые PNG фона, reward-панели, заголовка Win и select-панели. Эталонный внешний каталог из UI-гайда отсутствует на машине. Концепты реконструируют контекст из этих источников; соседние элементы ImageGen местами перерисовал.

Генератор: встроенный ImageGen. [Точные промпты](../../../.temp/first-session-feedback/imagegen_prompts.md).

## Файлы

Относительно `LostCyberHamster/Assets/`:

- `Scripts/Tutorial/Gameplay/TutorialGameplayView.cs`
- `Content/ui/styles/screens/Tutorial.uss`
- `Scripts/UI/Components/QuestItem.cs`
- `Content/localization/lang.ru.json`
- `Content/localization/lang.en.json`
- `Scripts/UI/Screens/SelectLevelScreenController.cs`
- `Scripts/UI/Screens/HomeScreenController.cs`
- `Scripts/System/LevelManagement/LevelManager.cs`

Новые runtime-файлы карточек, игровые PNG и связанные изменения перечислены в [общем результате](next_goal_cards_delivery.md).

## Проверки

Review своего diff выполнен; отдельное review геометрии tutorial выполнено вторым агентом. Конкретных дефектов в финальной версии не найдено.

`lch_project_regenerate_files`: Success. `dotnet build LostCyberHamster/Assembly-CSharp.csproj --no-restore -v:q`: **0 ошибок, 42 предупреждения**, 6,81 сек. [Лог](../../../LostCyberHamster/EditorLogs/first_session_feedback_compile.log). Generated `.csproj` остался без diff. `git diff --check`: пройден.

После исправления Home Play: собственное и независимое review завершены. Повторный gate только после нового C# diff: regeneration Success; Runtime build **0 ошибок, 42 предупреждения**, 5,62 сек. [Лог Play](../../../LostCyberHamster/EditorLogs/home_play_continuation_compile.log).

Пользователь проверяет на устройстве: отступ от HUD, удобство Skip, читаемость заголовка, скрытие нулевого XP, свайпы и обычные тапы. Ручное управление, Play и сборки не запускались.

Для Play: победа level01 → Home → League → Home → Play запускает level02; после поражения level02 Home Play повторяет level02. Replay с окна победы запускает тот же уровень. На границе времени суток продолжение следует каталогу и правилам открытия.
