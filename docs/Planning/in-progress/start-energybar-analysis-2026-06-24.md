# Start Energybar Regression Analysis — 2026-06-24

## Scope

- Регресс: при старте уровня визуальная полоска энергии отображается неполной.
- Expected: стартовая энергия уровня отображается как 100%.
- Actual: визуальный индикатор на старте выглядит неполным, при этом по описанию пользователя фактическая энергия равна 100%.
- Affected input/case/configuration: старт уровня; первичный минимальный target для проверки — `01_New_York/Morning/test_switch_lane`.

## Authoritative Source For Expected

- Описание пользователя: при старте уровня должно быть 100% энергии; текущее состояние выглядит как неполная полоска при фактических 100%.
- Кодовый контракт, подлежащий проверке: владелец стартового состояния `Hamster.Energy` должен инициализировать 100, UI должен отобразить то же значение без потери/подмены.

## Commands

- Поиск execution path: `rg -n "energy|Energy|stamina|Stamina|Power|power|Fill|fillAmount|slider|Slider|Image" LostCyberHamster/Assets -g "*.cs"`.
- Минимальный level target: `.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_switch_lane' -TimeoutSeconds 120`.
- CLI EditMode попытка: `Unity.exe -batchmode -projectPath LostCyberHamster -runTests -testPlatform EditMode -testFilter Assets.Tests.EditMode.EnergybarStartRegressionDiagnosticsTests ...` — не исполнилась, потому что проект уже открыт в Unity Editor.

## Facts

- `LostCyberHamster/Assets/Scripts/Gameplay/Hamster.cs` содержит `public AtomicVariable<int> Energy = new(100);`.
- `LostCyberHamster/Assets/Plugins/Atomic/Elements/Scripts/Implementations/AtomicVariable.cs`: `Subscribe(Action<T>)` только добавляет listener в `onChanged`; текущее значение при подписке не эмитится.
- `LostCyberHamster/Assets/Content/ui/uxml/EnergyBar.uxml`: `foreground` имеет inline `flex-grow: 0.7`.
- `LostCyberHamster/Assets/Scripts/UI/Components/Energybar.cs`: `_value = 100`, но constructor после `CloneTree()` не вызывает `UpdateEnergybar()`.
- `LostCyberHamster/Assets/Scripts/GameEngine/Mechanics/UiGameScreenMechanics.cs`: `Subscribe()` подписывается на `Energy`, но не вызывает `OnEnergyChanged(_character.Energy.Value)` для начального состояния.
- Execution log, `EditorLogs/diagnostic_log.txt`, CID `energy-start-ui`:
  - `UiGameScreenMechanics.Subscribe currentEnergy=100`.
  - `UiGameScreenMechanics.Subscribe completed currentEnergy=100`.
  - `Energybar.ctor defaultValue=100.000 foregroundFlexGrowAfterClone=0.000`.
  - `GameScreenController.OnLoadAsync energyBarValue=100.000 foregroundFlexGrow=0.000`.
  - Первый `OnEnergyChanged` после загрузки: `energy=90`, затем `SetEnergy input=90.000`, `UpdateEnergybar ... widthPercentage=0.900 ... actualFlexGrow=0.900`.
  - Последующий явный `SetEnergy input=100.000` даёт `UpdateEnergybar ... widthPercentage=1.000 ... actualFlexGrow=1.000`.

## Diagnostic Pass 1

- Вопрос: после `LoadScreenAsync(GameScreen)` получает ли `Energybar` setter со значением `100`, и какой `flex-grow` остаётся у `foreground` до первого изменения энергии?
- Логи будут добавлены только на путь: `Energybar` constructor/setter, `GameScreenController.OnLoadAsync/SetEnergy`, `UiGameScreenMechanics.Subscribe/OnEnergyChanged`.
- Полный target `.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_switch_lane' -TimeoutSeconds 120` не дошёл до `GameUi`: response `failed`, message `Play mode ended before a [TEST RESULT] marker was detected.`, `diagnostic_log.txt` содержит только стартовую строку.
- Так как полный target не исполнил UI path, добавлен временный EditMode diagnostic test `LostCyberHamster/Assets/Editor/Tests/EditMode/EnergybarStartRegressionDiagnosticsTests.cs`.
- Позднее `diagnostic_log.txt` получил UI-path строки с тем же CID; они зафиксированы в Facts.

## Case Table

| case | expected | actual | branch/result | excluded alternatives | first divergence |
|---|---|---|---|---|---|
| Start level UI energy | `Energy=100` отображается как full bar | `Energy=100`, `foregroundFlexGrow=0.000` при `OnLoadAsync` | `Subscribe` без initial replay; `Energybar` constructor не вызывает `UpdateEnergybar()` | H1: опровергнута `currentEnergy=100`; H3: опровергнута `SetEnergy(100) => widthPercentage=1.000`; H4 как primary причина не нужна, потому что числовое состояние foreground уже не full до layout tuning | `Energybar.ctor`: `defaultValue=100`, `foregroundFlexGrowAfterClone=0.000`; сохраняется в `GameScreenController.OnLoadAsync` |

## Hypotheses

- H1: фактическая энергия на старте меньше 100.
- H2: энергия равна 100, но UI получает неинициализированное/старое значение.
- H3: UI получает 100, но `Energybar` интерпретирует значение в другой шкале.
- H4: UI получает 100 и шкала верная, но геометрия/стиль `Energybar` визуально делает заполнение неполным.
