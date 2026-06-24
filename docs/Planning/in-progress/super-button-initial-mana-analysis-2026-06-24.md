# Анализ регресса: начальное значение маны на кнопке суперудара

Дата: 2026-06-24

## Scope

- Регресс: при старте уровня до первого напрыга на кнопке суперудара показывается `S`.
- Expected: при старте уровня мана равна `0`, кнопка должна сразу показывать `0`; первый напрыг уже после старта добавляет ману.
- Actual: до первого напрыга отображается `S`, после первого напрыга отображение меняется на `0`.
- Affected cases: старт игрового уровня до первого jump-on/напрыга; далее состояние корректируется первым изменением ресурса.

## Authoritative source для expected

- Описание пользователя в задаче: "мана должно быть ноль", "там сразу должно быть ноль, а первый напрыг, когда происходит, он уже должен добавлять ману".

## Минимальная команда воспроизведения

- `.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_jump_on' -TimeoutSeconds 120`

## Источники и команды

- Прочитан `docs/rules/AGENTS.md`.
- Прочитан `docs/rules/agent_tools.md`, потому что задача требует Unity automation и диагностических логов.
- `rg -n "super|Super|mana|Mana|мана|напрыг|jump|Jump" -S . --glob "*.cs" --glob "*.prefab" --glob "*.unity" --glob "*.asmdef"`: первичный широкий поиск, подтвержден Unity-проект в `LostCyberHamster/`.
- `rg -n "test_.*jump.*on|should.*jump.*on|should.*напрыг|JumpOn|jump on" -S LostCyberHamster/Assets/Content/locations --glob "*.json"`: найден минимальный test-level `01_New_York/Morning/test_jump_on`.
- `.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_jump_on' -TimeoutSeconds 120`: Unity bridge выполнил тест, результат `WIN`.
- `.\tools\read_log_channel.ps1 -Channel ECO -Tail 240`: получены диагностические факты с `cid=super-button-init`.

## Факты выполнения

- `Hamster.UltaChargeAmount` стартует как `new(0)` в `LostCyberHamster/Assets/Scripts/Gameplay/Hamster.cs`.
- `GameScreen.uxml` задаёт `btn_ultra` статическим `text="S"`.
- `GameUi.Initialize` вызывает `_uiGameScreenMechanics.Subscribe()` до `await _uiManager.LoadScreenAsync(ScreenEnum.GameScreen)`.
- `AtomicVariable<T>.Subscribe(Action<T>)` только добавляет listener в `onChanged` и не вызывает listener с текущим `Value`.
- `UiGameScreenMechanics.Subscribe()` подписывается на `_character.UltaChargeAmount`, но не вызывает `OnUltaChargeAmountChanged(_character.UltaChargeAmount.Value)` для initial snapshot.
- `GameScreenController.SetUltraValue(int value)` отображает число при `value < 100`, иначе `S`.
- Runtime log `21:27:32.167`: `GameUi.Initialize before-subscribe ulta=0 energy=100`.
- Runtime log `21:27:32.171-21:27:32.172`: `UiGameScreenMechanics.Subscribe before currentUlta=0` и `after currentUlta=0`; между ними нет `OnUltaChargeAmountChanged`.
- Runtime log `21:27:32.222`: `GameScreenController.OnLoadAsync ultraText='S' ultraExists=True`.
- Runtime log `21:27:32.224`: `GameUi.Initialize after-load ulta=0 energy=100`.
- Runtime log `21:27:36.070`: первый `UiGameScreenMechanics.OnUltaChargeAmountChanged value=0`.
- Runtime log `21:27:36.070`: `GameScreenController.SetUltraValue input=0 beforeText='S' afterText='0'`.
- Runtime log `21:27:36.072`: первый `UltaChargeMechanics.OnJumpOnEvent obstacle='SmallCitizenPrefab(Clone)' before=0 skinCharge=0 add=0 after=0`.
- `PlayerData.AppliedSkinId = 0`, `SkinManager.CurrentSkin` выбирается по `AppliedSkinId`, а `skins.json` для skin id `0` задаёт `UltaCharge: 0`.

## Таблица проверки

| case | expected | actual | выбранная ветка/result | исключение альтернатив | первая точка расхождения |
|---|---|---|---|---|---|
| Старт `01_New_York/Morning/test_jump_on` до первого напрыга | `btn_ultra` показывает `0` при `UltaChargeAmount=0` | `btn_ultra` показывает `S` | UXML default `text="S"` остаётся после `LoadScreenAsync`; initial callback не произошёл | модель не 100/готовая: log `ulta=0`; кнопка найдена: `ultraExists=True`; `SetUltraValue` до первого напрыга не вызван | `GameUi.Initialize`: подписка до `LoadScreenAsync` + `AtomicVariable.Subscribe` без initial emit |
| Первый напрыг | заряд увеличивается на величину скина и UI обновляется | UI получает `value=0`, текст меняется `S -> 0` | `UltaChargeMechanics` пишет `_ultaChargeAmount.Value += 0`, что эмитит `OnUltaChargeAmountChanged(0)` | не UI fallback: `SetUltraValue input=0`; не потеря события: `OnJumpOnEvent` был; причина add=0: `skinCharge=0` | `skins.json` skin id `0` содержит `UltaCharge: 0`, а он выбран через `AppliedSkinId=0` |

## Диагностический проход

Вопрос: при старте `GameScreen` остаётся ли `btn_ultra` со статическим UXML-текстом `S`, потому что `UiGameScreenMechanics.Subscribe()` только подписывается на `UltaChargeAmount`, но не получает initial callback/current snapshot до или после `LoadScreenAsync`; и является ли первое изменение после напрыга первым вызовом `SetUltraValue`?

Точечные временные логи добавляются с correlation id `super-button-init`:
- порядок `GameUi.Initialize`: до подписок, после подписок, после `LoadScreenAsync`;
- `UiGameScreenMechanics.Subscribe()` и `OnUltaChargeAmountChanged`;
- `GameScreenController.OnLoadAsync()` и `SetUltraValue()`;
- `UltaChargeMechanics.OnJumpOnEvent()`.

## Гипотезы

1. UI-кнопка суперудара при инициализации показывает статический label/key `S`, а не читает начальное значение маны.
2. Runtime-модель маны на старте действительно не равна `0`, а первое изменение приводит её к `0`.
3. Первый напрыг не добавляет ману, а только инициирует первый push состояния в UI.
4. Есть порядок инициализации, где UI подписывается после установки начального значения и не получает snapshot.

## Статус гипотез

1. Подтверждена: статический `text="S"` остаётся после загрузки экрана, потому что initial snapshot не отправлен в `SetUltraValue`.
2. Опровергнута: runtime-модель на старте равна `0`, подтверждено log `ulta=0` до/после загрузки.
3. Подтверждена для текущего выбранного скина: первый напрыг вызывает push состояния, но прибавка равна `0`, потому что `skinCharge=0`.
4. Подтверждена: подписка происходит до `LoadScreenAsync`, `AtomicVariable.Subscribe` не эмитит текущее значение, после загрузки кнопка остаётся с UXML-текстом.
