# Bot Architecture Diagram

Дата: 2026-04-29  
Папка: `LostCyberHamster/Assets/Scripts/Bot/`

Документ показывает текущую архитектуру runtime-бота: какие блоки логики есть, какие классы входят в каждый блок, кто от кого зависит и какой класс за что отвечает.

## 1. Общий pipeline

```mermaid
flowchart LR
    External["External runtime<br/>Hamster - live state/actions<br/>GameManager - game state<br/>Camera - viewport<br/>DebugManager - diagnostic log sink<br/>JumpOutcomeResolver / SuperJumpOutcomeResolver - runtime jump truth"]

    RBC["RuntimeBotController<br/>MonoBehaviour-оркестратор<br/>Создаёт pipeline, strategies, diagnostics; каждый tick ведёт perception -> planning -> execution"]

    subgraph Perception["Perception: live Unity state -> immutable snapshot"]
        SB["SnapshotBuilder<br/>Собирает WorldSnapshot из Hamster, Camera и runtime obstacles"]
        WS["WorldSnapshot<br/>Снимок мира для планировщика: hamster + видимые obstacles"]
        HS["HamsterSnapshot<br/>Позиция, lane, energy, lives, HamsterState, roof-state"]
        OS["ObstacleSnapshot<br/>Тип, instance id, lane, bounds/center препятствия"]
    end

    subgraph PlanState["PlanState: данные планов и действий"]
        BP["BotPlan<br/>Последовательность PlannedAction, score, committed-prefix"]
        PA["PlannedAction<br/>Одно действие: kind, triggerX, renderX, target/trigger obstacle, costs, shifts"]
        BAK["BotActionKind<br/>Enum action families: SwitchLane, JumpOver, SuperJumpOver, JumpOnRoof, SuperJumpOnRoof"]
    end

    subgraph Planning["Planning: построение и оценка графа решений"]
        PB["PlanBuilder<br/>Сохраняет валидный committed prefix и достраивает лучший новый план"]
        AG["ActionGenerator<br/>Опрашивает strategies и собирает candidate PlannedAction для DecisionPoint"]
        DPD["DecisionPointDetector<br/>Находит ближайшую ситуацию, где нужен выбор действия"]
        PGB["PlanningGraphBuilder<br/>Раскрывает дерево действий через lookahead transitions"]
        TS["TransitionSimulator<br/>Диспетчеризует Simulate() по action kind"]
        PE["PlanEvaluator<br/>Выбирает лучшую ветку по score/метрикам"]
        RAR["RetainedActionRevalidator<br/>Проверяет, можно ли оставить committed action из прошлого плана"]
        AIPP["ActionInProgressProjector<br/>Проецирует уже запущенный head-action в planning state"]
    end

    subgraph Strategies["Strategies: семейства действий бота"]
        SL["SwitchLane family<br/>SwitchLaneStrategy + Specification + FireWindowCalculator + Executor + Simulator + RetainedValidator + Timing"]
        JO["JumpOver family<br/>JumpOverStrategy + Specification + FireWindowCalculator + Executor + Simulator"]
        SJO["SuperJumpOver family<br/>SuperJumpOverStrategy + Specification + FireWindowCalculator + Executor + Simulator"]
        JOR["JumpOnRoof family<br/>JumpOnRoofStrategy + Specification + FireWindowCalculator + Executor + Simulator"]
        SJOR["SuperJumpOnRoof family<br/>SuperJumpOnRoofStrategy + Specification + FireWindowCalculator + Executor + Simulator"]
        Shared["Strategies/Shared<br/>Contracts, execution helpers, simulation helpers, jump-planning helpers, timing structs, retained context"]
    end

    subgraph Execution["Execution: применение выбранного плана в live runtime"]
        PEX["PlanExecutor<br/>Идёт по BotPlan, вызывает Executor.TryFire(), ждёт IsCompleted(), продвигает head action"]
    end

    subgraph Diagnostics["Diagnostics: просмотр и диагностические события"]
        BPR["BotPlanRenderer<br/>GL-render текущего плана поверх мира"]
        BD["BotDiagnostics<br/>Центральный gate category/level для bot diagnostic log"]
        BHD["Bot*Diagnostics helpers<br/>Execution / Replan / Strategy / RuntimeEvent факты"]
        RBET["RuntimeBotEventTracker<br/>Логирует damage, finish, win/fail markers для test levels"]
        HAL["HamsterActionLogger<br/>Единый лог FIRE / COMPLETE / CANCEL для action execution"]
    end

    RBC --> SB
    SB --> WS
    WS --> HS
    WS --> OS

    RBC --> PB
    PB --> AG
    PB --> PGB
    PB --> PE
    PB --> RAR
    PB --> AIPP
    PB --> BP
    BP --> PA
    PA --> BAK

    AG --> DPD
    AG --> Strategies
    PGB --> TS
    TS --> Shared
    RAR --> Shared
    AIPP --> Shared

    RBC --> PEX
    PEX --> Strategies

    RBC --> BPR
    RBC --> BD
    RBC --> BHD
    RBC --> RBET
    Planning --> BHD
    Strategies --> HAL
    Strategies --> BHD
    Execution --> BHD

    RBC --> External
    SB --> External
    PEX --> External
    Shared --> External
```

## 2. Planning internals

```mermaid
flowchart TB
    subgraph Inputs["Inputs"]
        WS2["WorldSnapshot<br/>Фактическая картина мира на текущий tick"]
        PrevPlan["Previous BotPlan<br/>Committed prefix, который можно попытаться сохранить"]
    end

    subgraph StateAndDecision["Planning state and decision points"]
        PS["PlanningState<br/>Прогнозируемое состояние hamster + next obstacle + projection world shift"]
        PSK["PlanningStateKey<br/>Ключ для дедупликации/caching planning states"]
        DP["DecisionPoint<br/>Описывает ситуацию выбора: obstacle, index, roof landing target"]
        DPK["DecisionPointKind<br/>BlockingObstacle / BlockingObstacleWithRoofLanding / RoofLanding"]
        DPE["DecisionPointExtensions<br/>Помощники для чтения semantics DecisionPoint"]
        PSP["PlanningSnapshotProjector<br/>Строит projected WorldSnapshot для planning state"]
        RRP["RoofRunProjection<br/>Расчёты поддержки/продления состояния бега по крыше"]
        OC["ObstacleClassifier<br/>Domain-классификация obstacles: damage, roof, jump-chain, target types"]
    end

    subgraph GraphBuild["Graph build"]
        PB2["PlanBuilder<br/>Top-level сборка итогового BotPlan"]
        RAR2["RetainedActionRevalidator<br/>Проверяет старые committed actions через strategy validators"]
        AG2["ActionGenerator<br/>Находит DecisionPoint и вызывает IPlanningStrategy.CollectActions()"]
        DPD2["DecisionPointDetector<br/>Выбирает ближайший meaningful DecisionPoint"]
        PGB2["PlanningGraphBuilder<br/>Строит дерево PlanningGraphNode через action candidates"]
        PGN["PlanningGraphNode<br/>Узел дерева: PlanningState, action, children"]
        TS2["TransitionSimulator<br/>Вызывает ISimulator.Simulate() для выбранного action kind"]
        AIPP2["ActionInProgressProjector<br/>Если action уже стартовал, проецирует его через ISimulator.ProjectInProgress()"]
    end

    subgraph Evaluate["Branch evaluate"]
        PBRA["PlanningBranch<br/>Линейная ветка действий, кандидат в итоговый план"]
        PBM["PlanningBranchMetrics<br/>Метрики ветки: score, travel/progress, penalties"]
        PEV["PlanEvaluator<br/>Сравнивает ветки и выбирает лучшую"]
    end

    WS2 --> PB2
    PrevPlan --> PB2
    PB2 --> RAR2
    PB2 --> AIPP2
    PB2 --> PGB2
    PGB2 --> AG2
    AG2 --> DPD2
    DPD2 --> DP
    DP --> DPK
    DP --> DPE
    AG2 --> PSP
    PSP --> PS
    PS --> PSK
    PSP --> RRP
    PSP --> OC
    AG2 -->|candidate actions| PGB2
    PGB2 --> PGN
    PGB2 --> TS2
    TS2 --> PS
    PGB2 --> PBRA
    PBRA --> PBM
    PBRA --> PEV
    PEV --> PB2
```

## 3. Strategy pattern and concrete action families

```mermaid
flowchart TB
    subgraph Contracts["Shared/Contracts: strategy ports"]
        IPS["IPlanningStrategy<br/>Strategy facade: ActionKind, Executor, RetainedValidator, Simulator, CollectActions()"]
        IAEH["IActionExecutionHandler<br/>Execution port: TryFire() and IsCompleted()"]
        ISIM["ISimulator<br/>Planning simulation port: Simulate() and ProjectInProgress()"]
        IRAV["IRetainedActionValidator<br/>Retained action port: IsStillValid(context)"]
    end

    subgraph SharedExecution["Shared/Execution: live execution helpers"]
        ATG["ActionTriggerGate<br/>Проверяет, пора ли нажимать action по live obstacle distance"]
        LOR["LiveObstacleResolver<br/>Находит live obstacle по target/trigger identifiers"]
        AFR["ActionFireResult<br/>Execution result enum: in-progress, success, failed"]
        RAC["RetainedActionContext<br/>Контекст для проверки старого action: state, projected snapshot, target obstacle"]
    end

    subgraph SharedSimulation["Shared/Simulation and Timing"]
        PST["PlanningStateTransition<br/>Чистые переходы planning state после успешного action"]
        IPH["InProgressProjectionHelper<br/>Общий расчёт projection для уже запущенного head action"]
        AIP3["ActionInProgressProjector<br/>Диспетчер ProjectInProgress() по action kind"]
        SI["SafeInterval<br/>Безопасный timing interval с выбором interior point"]
        UI["UnsafeInterval<br/>Опасный timing interval для safety calculations"]
    end

    subgraph SwitchLane["SwitchLane family"]
        SLS["SwitchLaneStrategy<br/>Создаёт SwitchLane candidates для смены линии"]
        SLSPEC["SwitchLaneSpecification<br/>Проверяет применимость смены линии к DecisionPoint"]
        SLFW["SwitchLaneFireWindowCalculator<br/>Ищет safe window на target lane"]
        SLEX["SwitchLaneExecutor<br/>Нажимает смену линии и ждёт завершения shift"]
        SLSIM["SwitchLaneSimulator<br/>Прогнозирует lane/state после смены линии"]
        SLVAL["SwitchLaneRetainedValidator<br/>Проверяет, ещё валиден ли сохранённый SwitchLane"]
        SLTIM["SwitchLaneTiming<br/>Константы decision travel и timing для SwitchLane"]
    end

    subgraph JumpOver["JumpOver family"]
        JOS["JumpOverStrategy<br/>Создаёт обычный прыжок через ground obstacle"]
        JOSPEC["JumpOverSpecification<br/>Фильтрует obstacles, которые можно перепрыгнуть обычным jump"]
        JOFW["JumpOverFireWindowCalculator<br/>Ищет fire shift для outcome JumpOver"]
        JOEX["JumpOverExecutor<br/>Нажимает jump и ждёт state/completion"]
        JOSIM["JumpOverSimulator<br/>Прогнозирует Run state после прыжка через obstacle"]
    end

    subgraph SuperJumpOver["SuperJumpOver family"]
        SJOS["SuperJumpOverStrategy<br/>Создаёт super jump через ground obstacle"]
        SJOSPEC["SuperJumpOverSpecification<br/>Фильтрует obstacles для super jump-over"]
        SJOFW["SuperJumpOverFireWindowCalculator<br/>Ищет fire shift для outcome SuperJumpOver"]
        SJOEX["SuperJumpOverExecutor<br/>Нажимает super jump и контролирует завершение"]
        SJOSIM["SuperJumpOverSimulator<br/>Прогнозирует state после super jump-over"]
    end

    subgraph JumpOnRoof["JumpOnRoof family"]
        JORS["JumpOnRoofStrategy<br/>Создаёт jump на roof obstacle"]
        JORSPEC["JumpOnRoofSpecification<br/>Проверяет roof landing applicability"]
        JORFW["JumpOnRoofFireWindowCalculator<br/>Ищет fire shift для landing на крышу"]
        JOREX["JumpOnRoofExecutor<br/>Нажимает jump-on-roof и ждёт roof state"]
        JORSIM["JumpOnRoofSimulator<br/>Прогнозирует RoofRun после посадки"]
    end

    subgraph SuperJumpOnRoof["SuperJumpOnRoof family"]
        SJORS["SuperJumpOnRoofStrategy<br/>Создаёт super jump на roof obstacle"]
        SJORSPEC["SuperJumpOnRoofSpecification<br/>Проверяет roof landing для super jump"]
        SJORFW["SuperJumpOnRoofFireWindowCalculator<br/>Ищет fire shift для SuperJumpOnRoof outcome"]
        SJOREX["SuperJumpOnRoofExecutor<br/>Нажимает super jump-on-roof и контролирует state"]
        SJORSIM["SuperJumpOnRoofSimulator<br/>Прогнозирует RoofRun после super roof landing"]
    end

    IPS --> SLS
    IPS --> JOS
    IPS --> SJOS
    IPS --> JORS
    IPS --> SJORS

    SLS --> SLSPEC
    SLS --> SLFW
    SLS --> SLEX
    SLS --> SLSIM
    SLS --> SLVAL
    SLS --> SLTIM

    JOS --> JOSPEC
    JOS --> JOFW
    JOS --> JOEX
    JOS --> JOSIM

    SJOS --> SJOSPEC
    SJOS --> SJOFW
    SJOS --> SJOEX
    SJOS --> SJOSIM

    JORS --> JORSPEC
    JORS --> JORFW
    JORS --> JOREX
    JORS --> JORSIM

    SJORS --> SJORSPEC
    SJORS --> SJORFW
    SJORS --> SJOREX
    SJORS --> SJORSIM

    SLEX --> IAEH
    JOEX --> IAEH
    SJOEX --> IAEH
    JOREX --> IAEH
    SJOREX --> IAEH

    SLSIM --> ISIM
    JOSIM --> ISIM
    SJOSIM --> ISIM
    JORSIM --> ISIM
    SJORSIM --> ISIM

    SLVAL --> IRAV
    ATG --> LOR
    IAEH --> AFR
    IRAV --> RAC
    ISIM --> PST
    ISIM --> IPH
    AIP3 --> ISIM
```

## 4. Jump-planning internals

```mermaid
flowchart LR
    subgraph ConcreteJumpCalculators["Concrete jump fire-window calculators"]
        JOFW2["JumpOverFireWindowCalculator<br/>Ground jump-over orchestration; validates retained jump shift"]
        SJOFW2["SuperJumpOverFireWindowCalculator<br/>Ground super-jump orchestration; validates retained super jump shift"]
        JORFW2["JumpOnRoofFireWindowCalculator<br/>Roof landing orchestration with pre-fire safety"]
        SJORFW2["SuperJumpOnRoofFireWindowCalculator<br/>Super roof landing orchestration with pre-fire safety"]
    end

    subgraph JumpContracts["JumpPlanning contracts and validators"]
        IJSFV["IJumpScheduledFireShiftValidator<br/>Narrow contract for retained jump fire-shift validation"]
        JORV["JumpOutcomeRetainedValidator<br/>IRetainedActionValidator adapter over IJumpScheduledFireShiftValidator"]
        JRD["JumpResolveDelegate<br/>Delegate to runtime jump resolver"]
    end

    subgraph Windows["Search window policies"]
        IJSW["IJumpSearchWindowPolicy<br/>Interface: compute first/last fire shift"]
        GJSW["GroundJumpSearchWindowPolicy<br/>Window for ground jump-over and chain-over cases"]
        RLSW["RoofLandingSearchWindowPolicy<br/>Window for landing overlap on roof obstacle"]
        IPFS["IPreFireSafetyPolicy<br/>Interface: can hamster safely wait until fire moment"]
        GCPF["GroundContactPreFireSafetyPolicy<br/>Rejects waits where ground obstacle hits hamster before fire"]
    end

    subgraph MatchAndScan["Outcome matching and interval scan"]
        JFS["JumpFireShiftScanner<br/>Scans fire-window, builds exact-outcome SafeIntervals, selects fireShift"]
        JOM["JumpOutcomeMatcher<br/>Calls runtime resolver and checks expected HamsterState + target match"]
        JOP["JumpObstacleProjection<br/>Maps WorldSnapshot obstacles to JumpObstacleData and applies fireShift"]
        JSFS["JumpScheduledFireShift<br/>Restores remaining fire shift for retained jump action"]
        JCT["JumpClipTravel<br/>Caches world travel of jump animation clips"]
    end

    subgraph RuntimeTruth["Runtime truth outside Bot"]
        JR["JumpOutcomeResolver<br/>Actual jump outcome resolver"]
        SJR["SuperJumpOutcomeResolver<br/>Actual super jump outcome resolver"]
        JRR["JumpResolveResult / JumpResolveContext<br/>Runtime result/context models"]
    end

    JOFW2 --> IJSFV
    SJOFW2 --> IJSFV
    JORFW2 --> IJSFV
    SJORFW2 --> IJSFV
    JORV --> IJSFV

    JOFW2 --> GJSW
    SJOFW2 --> GJSW
    JORFW2 --> RLSW
    SJORFW2 --> RLSW
    JORFW2 --> GCPF
    SJORFW2 --> GCPF

    GJSW --> IJSW
    RLSW --> IJSW
    GCPF --> IPFS

    JOFW2 --> JFS
    SJOFW2 --> JFS
    JORFW2 --> JFS
    SJORFW2 --> JFS

    JFS --> JOM
    JOM --> JOP
    IJSFV --> JSFS
    JOFW2 --> JCT
    SJOFW2 --> JCT
    JORFW2 --> JCT
    SJORFW2 --> JCT

    JOM --> JRD
    JRD --> JR
    JRD --> SJR
    JR --> JRR
    SJR --> JRR
```

## 5. Dependency summary by direction

```mermaid
flowchart TD
    Runtime["RuntimeBotController<br/>Only top-level composer"]
    Data["PlanState + Perception models<br/>Pure data passed between blocks"]
    PlanningCore["Planning core<br/>Builds graph and selects plan"]
    StrategyPorts["Shared contracts<br/>IPlanningStrategy / IActionExecutionHandler / ISimulator / IRetainedActionValidator"]
    StrategyFamilies["Concrete strategies<br/>SwitchLane, JumpOver, SuperJumpOver, JumpOnRoof, SuperJumpOnRoof"]
    SharedHelpers["Shared helpers<br/>Execution gates, simulation transitions, jump-planning policies/scanners/matchers"]
    ExecutionCore["PlanExecutor<br/>Runs selected plan in live runtime"]
    DiagnosticsCore["Diagnostics<br/>Rendering, BotDiagnostics helpers and diagnostic logs"]
    ExternalRuntime["External runtime systems<br/>Hamster, GameManager, Camera, DebugManager sink, jump resolvers"]

    Runtime --> Data
    Runtime --> PlanningCore
    Runtime --> ExecutionCore
    Runtime --> DiagnosticsCore
    Runtime --> ExternalRuntime

    PlanningCore --> Data
    PlanningCore --> StrategyPorts
    PlanningCore --> StrategyFamilies
    PlanningCore --> SharedHelpers

    StrategyFamilies --> StrategyPorts
    StrategyFamilies --> SharedHelpers
    StrategyFamilies --> Data
    StrategyFamilies --> ExternalRuntime

    ExecutionCore --> StrategyPorts
    ExecutionCore --> StrategyFamilies
    ExecutionCore --> ExternalRuntime

    DiagnosticsCore --> Data
    DiagnosticsCore --> ExternalRuntime
```
