# Roof switch lane на дорогу

## Цель

Расширить `RoofSwitchLaneStrategy`, чтобы бот мог делать switch lane из `RoofRun` не только на roof support другой линии, но и на безопасную дорогу другой линии.

## Статус

Реализовано 2026-07-01.

## Тестовые паттерны

- `test_roof_switch_lane_04`: defensive-сценарий. Нижняя линия ведет по цепочке `mediumNotAlive`, после roof-chain нижняя дорога перекрыта `smallNotAliveRoad`. Верхняя линия после ранних road hazards свободна и не содержит roof support в зоне switch. Expected: бот запрыгивает на нижнюю крышу и делает roof switch lane на верхнюю дорогу до опасного схода.
- `test_roof_switch_lane_05`: reward-сценарий. Нижняя линия ведет по цепочке `mediumNotAlive` с безопасным обычным сходом, верхняя линия содержит road coins без roof support сразу после верхних `smallNotAliveRoad`. Expected: бот запрыгивает на нижнюю крышу и делает roof switch lane на верхнюю дорогу ради достижимых монет, не дожидаясь конца roof-chain.

Паттерны подключены в `test_roof_switch_lane` после существующих трех roof-to-roof сценариев через `test_relief_energy`.

## Текущее ограничение

- `ActionGenerator` уже передает opposite collectible route в `RoofSwitchLaneStrategy`, если `planningState.Hamster.IsOnRoof`.
- `CollectibleDecisionPointDetector.TryDetectOppositeCollectibleRoute` в roof state сейчас ищет только opposite roof collectible route: collectable должен лежать на roof support.
- `RoofSwitchLaneWindowFinder.TryFind` всегда вызывает `TrySelectRelevantFireWindowSample(... requireTargetRoofSupport: true)` и затем требует `TryFindTargetRoofSupportAtFireShift`.
- `RoofSwitchLaneWindow` хранит только `TargetRoof/TargetRoofIndex`.
- `RoofSwitchLaneExecutor` отменяет action без `ResultRoofSupportInstanceId`.
- `PlanningStateTransition.ApplyLaneSwitch` уже умеет planning-модель road landing: если `ResultRoofSupportInstanceId` отсутствует, `RoofRun` переходит в `RunFromRoof`, `IsOnRoof=false`.

## Архитектурная форма

Сохраняем один action kind `RoofSwitchLane`: runtime input тот же, меняется только landing contract.

- `RoofSwitchLaneTargetResolver` остается владельцем сценария: defensive current-lane threat или reward opposite-lane collectible.
- `RoofSwitchLaneWindowFinder` остается владельцем safe fire window, но выбирает landing: roof support, если он доступен, иначе road landing, если дорога целевой линии безопасна.
- `SwitchLaneFireWindowCalculator` переиспользуется: для roof landing `requireTargetRoofSupport=true`, для road landing `requireTargetRoofSupport=false`.
- `RoofSwitchLaneStrategy` строит один `PlannedAction`; `ResultRoofSupportInstanceId` заполняется только для roof landing.
- `RoofSwitchLaneExecutor` должен принимать `RoofSwitchLane` без `ResultRoofSupportInstanceId`, потому что это валидный road landing.

## План изменений

1. Ввести явную модель landing для roof switch lane.
   - Минимально: заменить roof-only поля в `RoofSwitchLaneWindow` на landing-состояние с nullable roof support.
   - Не добавлять отдельный action kind и не дублировать executor/simulator.

2. Расширить `RoofSwitchLaneWindowFinder`.
   - Сначала пробовать текущее roof-support окно, чтобы сохранить поведение roof-to-roof.
   - Если roof-support окно не найдено, пробовать road окно через `requireTargetRoofSupport:false`.
   - Road window должно использовать те же unsafe intervals целевой линии и тот же deadline текущей roof-chain.
   - Dead-end reason должен различать "нет безопасного окна вообще" и "нет roof support, но road landing тоже небезопасен".

3. Расширить `CollectibleDecisionPointDetector` для roof reward на road collectables.
   - В roof state opposite collectible route должен рассматривать collectables на другой линии без roof support как road reward route.
   - Если collectable лежит на roof support, сохраняется текущий roof route.
   - Если collectable лежит на дороге, строится optional-only chain этой линии от road collectable/ближайшей active chain и передается в тот же strategy pipeline.

4. Обновить `RoofSwitchLaneStrategy.BuildAction`.
   - Для roof landing target action остается target roof.
   - Для road landing target action должен оставаться context obstacle: defensive threat или reward collectable.
   - `description` и diagnostics должны явно писать `roof`/`road` landing.
   - `ResultRoofSupportInstanceId` передается только для roof landing.

5. Обновить `RoofSwitchLaneExecutor`.
   - Убрать требование обязательного `ResultRoofSupportInstanceId`.
   - Оставить обязательным `TargetBottomLine`.
   - Логировать `targetLanding=roof` с roof id или `targetLanding=road`.

6. Проверить simulator.
   - `RoofSwitchLaneSimulator` может остаться без специальной ветки, если `ApplyLaneSwitch` корректно переводит road landing в `RunFromRoof`.
   - Проверить, что после road landing replan видит road collectables и hazards через обычный route/collectible pipeline.

## Проверка

- JSON parse для `PatternsCollection.json` и `test_roof_switch_lane.json`.
- Compile / Unity recompile после C# реализации.
- `test_roof_switch_lane`: все 5 паттернов.
- Отдельно подтвердить, что первые 3 roof-to-roof паттерна не поменяли поведение.
- Для новых паттернов: defensive `04` должен уходить на дорогу до нижних road hazards; reward `05` должен уходить на верхнюю дорогу и собрать достижимые road coins после ранней серии hazards.

## Фактическая проверка

- `dotnet build LostCyberHamster/Assembly-CSharp.csproj --no-restore --nologo`: `0 Error(s)`.
- JSON parse: `PatternsCollection.json` и `test_roof_switch_lane.json` валидны, все refs найдены.
- `tools/invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_roof_switch_lane' -TimeScale 1`: response `WIN`, diagnostic log содержит `[TEST RESULT] WIN`.
- `test_roof_switch_lane_04`: `RoofSwitchLane ... targetLanding=road`, завершение `state=RunFromRoof`.
- `test_roof_switch_lane_05`: `RoofSwitchLane[Coin] ... targetLanding=road`, после перехода собраны достижимые `collectableCoin` на верхней road-линии.
