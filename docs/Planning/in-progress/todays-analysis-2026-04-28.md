# Today's Analysis — 2026-04-28

1. **Рефакторинг DecisionPoint и новая семантика roof-entry**
   Кратко: логика точек решения для бота была вынесена в отдельную подпапку и расширена новым типом ситуации, когда перед ботом есть блокирующее препятствие, а сразу за ним доступна крыша для приземления.
   Затронутые файлы:
   - `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPoints/DecisionPoint.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPoints/DecisionPointKind.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPoints/DecisionPointDetector.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPoints/DecisionPointExtensions.cs`
   - `LostCyberHamster/Assets/Editor/Tests/EditMode/ObstacleLaneResolverTests.cs`

2. **Новая фича: SuperJumpOnRoof**
   Кратко: бот получил новый полноценный манёвр — суперпрыжок с заходом на крышу, включая выбор действия в планировщике, расчёт окна нажатия, симуляцию результата и реальное исполнение в runtime.
   Затронутые файлы:
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOnRoof/SuperJumpOnRoofStrategy.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOnRoof/SuperJumpOnRoofSpecification.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOnRoof/SuperJumpOnRoofFireWindowCalculator.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOnRoof/SuperJumpOnRoofSimulator.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOnRoof/SuperJumpOnRoofExecutor.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/BotStrategyFactory.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/PlanState/BotActionKind.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/BotPlanRenderer.cs`

3. **Усиление planning-логики для прыжков на крышу**
   Кратко: обычный JumpOnRoof и соседние jump-стратегии были адаптированы под новую семантику roof-entry, чтобы бот корректно различал блокер, целевую крышу и допустимый тип манёвра.
   Затронутые файлы:
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOnRoof/JumpOnRoofStrategy.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOnRoof/JumpOnRoofSpecification.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOnRoof/JumpOnRoofFireWindowCalculator.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOver/JumpOverSpecification.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOver/JumpOverStrategy.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOver/JumpOverFireWindowCalculator.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOver/SuperJumpOverSpecification.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOver/SuperJumpOverStrategy.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOver/SuperJumpOverFireWindowCalculator.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneStrategy.cs`

4. **Pre-fire safety и точный расчёт fire window**
   Кратко: планировщик научился проверять не только исход прыжка после нажатия, но и безопасность ожидания до момента нажатия, чтобы обычный прыжок не считался валидным там, где хомяк погибает ещё до fire-момента.
   Затронутые файлы:
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpOutcomeFireWindowCalculator.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/IPreFireSafetyPolicy.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/GroundContactPreFireSafetyPolicy.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOnRoof/JumpOnRoofFireWindowCalculator.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOnRoof/SuperJumpOnRoofFireWindowCalculator.cs`

5. **Разделение trigger obstacle и target obstacle в плане**
   Кратко: действие бота теперь хранит отдельно препятствие, относительно которого надо вовремя нажать action, и отдельно реальную цель приземления. Это потребовалось для сценариев вида “перепрыгни блокер и сядь на следующую крышу”.
   Затронутые файлы:
   - `LostCyberHamster/Assets/Scripts/Bot/PlanState/PlannedAction.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Planning/RetainedActionRevalidator.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/Models/RetainedActionContext.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpOutcomeRetainedValidator.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOnRoof/JumpOnRoofStrategy.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOnRoof/SuperJumpOnRoofStrategy.cs`

6. **Фикс тайминга execution и replanning после action**
   Кратко: был исправлен баг, из-за которого chained-действия после SwitchLane могли срабатывать слишком рано. Дополнительно контроллер бота стал переснимать snapshot после execution tick, чтобы replanning видел уже актуальное состояние.
   Затронутые файлы:
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/Execution/ActionTriggerGate.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/InProgressProjectionHelper.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/Execution/HamsterActionLogger.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`
   - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneStrategy.cs`

7. **Диагностика и тестовый контент под roof-entry сценарии**
   Кратко: были добавлены и обновлены отдельные тестовые паттерны и уровень для проверки super jump on roof, а также обновлён рабочий сценарный документ по стратегиям движения.
   Затронутые файлы:
   - `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`
   - `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/test_super_jump_on_roof/test_super_jump_on_roof.json`
   - `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/test_jump_on_roof/test_jump_on_roof.json`
   - `LostCyberHamster/Assets/AddressableAssetsData/AssetGroups/levels_by_daypart.asset`
   - `docs/Planning/in-progress/bot-movement-strategy-scenarios.md`