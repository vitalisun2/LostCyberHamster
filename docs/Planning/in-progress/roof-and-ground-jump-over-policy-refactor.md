# Roof and Ground Jump-Over Policy Refactor

## Goal

Сделать единый понятный подход для стратегий, где есть обычный прыжок и super-вариант:

- `JumpOver` / `SuperJumpOver` на дороге.
- `JumpOnRoof` / `SuperJumpOnRoof` для посадки на крышу.
- `RoofJumpOver` / `SuperRoofJumpOver` для перепрыгивания препятствий на крыше.

Стратегии не должны повторять runtime-логику. Outcome всегда проверяется через соответствующий resolver.

## Policy Decision

Один общий policy для всех прыжковых стратегий делать не стоит.

Причина простая: у разных семейств разный смысл успешного outcome.

- `JumpOver` ожидает over-state и попадание target index в рассчитанную chain.
- `JumpOnRoof` ожидает roof-landing state и конкретную target roof.
- `RoofJumpOver` ожидает roof-run continuation state и конкретную support roof после прыжка.

Также `JumpOver` и `JumpOnRoof` используют `JumpResolveContext`, а `RoofJumpOver` использует `RoofJumpResolveContext` с двумя travel values.

Правильнее оставить общий стиль, но делать policy на уровне семейства:

- `IJumpOnRoofPolicy` уже есть.
- Добавить policy для `JumpOver` / `SuperJumpOver`.
- Добавить policy для `RoofJumpOver` / `SuperRoofJumpOver`.

Общий маленький contract можно использовать только для метаданных: `ActionKind`, `EnergyCost`, `DescriptionPrefix`, `LogTag`. Resolver-вызовы и expected outcome лучше держать в family-specific policy.

## Current State

- `JumpOnRoof` и `SuperJumpOnRoof` уже сделаны по хорошему образцу: общий shared-код плюс policy.
- `JumpOver` и `SuperJumpOver` сейчас почти дублируют друг друга: specification, chain calculator, fire window finder, retained validator, simulator.
- `RoofJumpOver` уже использует `RoofJumpOutcomeResolver`, но пока существует только обычная стратегия без super-варианта.
- `SuperRoofJumpMechanics` все еще держит outcome-логику внутри mechanics. Для стратегии `SuperRoofJumpOver` сначала нужен корректный `SuperRoofJumpOutcomeResolver`.

## Required Work

1. Принять `JumpOnRoof` / `SuperJumpOnRoof` как канон: две конкретные стратегии, общий shared-блок, family-specific policy, resolver-вызов внутри общего finder/validator через policy.

2. Распространить этот канон на `JumpOver` / `SuperJumpOver`.

3. Вынести общий код `JumpOver` и `SuperJumpOver` в shared-блок.

4. Добавить policy для ground jump-over пары.

5. Для ground jump-over оставить различия в policy:
   - action kind;
   - energy cost;
   - travel;
   - allowed obstacle types;
   - expected over state;
   - resolver;
   - `damageBigAliveWithoutYByReach`.

6. В shared ground jump-over оставить общий смысл:
   - найти chain препятствий;
   - посчитать fire window;
   - выбрать fire shift;
   - проверить runtime outcome через resolver;
   - сохранить action;
   - перепроверить retained action;
   - симулировать возврат в `Run`.

7. Убрать параллельные ground-классы, которые дублируют один и тот же смысл:
   - два chain calculator-а должны стать одним shared calculator-ом;
   - два fire window finder-а должны стать одним shared finder-ом;
   - два retained validator-а должны стать одним shared validator-ом;
   - два simulator-а должны стать одним shared simulator-ом;
   - concrete strategy должна только подставлять policy, executor и описание action.

8. После ground jump-over привести roof jump-over к тому же стилю: concrete strategy + shared-блок + policy.

9. Вынести outcome-логику из `SuperRoofJumpMechanics` в `SuperRoofJumpOutcomeResolver`.

10. Переделать `SuperRoofJumpMechanics` по тому же подходу, что `RoofJumpMechanics`: mechanics собирает runtime data, вызывает resolver, применяет результат.

11. Добавить `SuperRoofJumpOver` как отдельный bot action.

12. Решить naming для `BotActionKind`: существующий `SuperRoofJump` лучше заменить или дополнить более точным `SuperRoofJumpOver`, чтобы action name описывал стратегию, а не только runtime input.

13. Вынести общий код `RoofJumpOver` и будущего `SuperRoofJumpOver` в отдельный shared-блок.

14. Для roof jump-over оставить различия в policy:
   - action kind;
   - energy cost;
   - description;
   - roof jump travel;
   - jump-from-roof travel;
   - expected success state: `RoofJump` или `SuperRoofJump`;
   - resolver: `RoofJumpOutcomeResolver` или `SuperRoofJumpOutcomeResolver`;
   - runtime request: `RoofJumpRequest` или `SuperRoofJumpRequest`.

15. В shared roof jump-over оставить общий смысл:
    - работать только из `RoofRun`;
    - искать опасный `smallNotAliveRoadAndRoof` на текущей roof path;
    - найти support roof под этим obstacle;
    - посчитать окно fire shift для перепрыгивания obstacle;
    - проверить resolver outcome на выбранном fire shift;
    - убедиться, что resolver возвращает ожидаемый success state;
    - убедиться, что resolver target указывает на ожидаемую support roof;
    - сохранить support id в action;
    - после выполнения продолжить planning как `RoofRun`.

16. Для roof jump-over отдельно учесть отличие крыши от дороги:
    - дорога бесконечна, крыша нет;
    - после прыжка support roof может быть текущей крышей или следующей roof-платформой;
    - нельзя считать action успешным только по факту перелета hazard;
    - успешность должна подтверждаться resolver target support id.

17. Проверить, нужно ли roof jump-over поддерживать chain из нескольких roof hazards, как ground jump-over поддерживает chain на дороге.

18. Если chain на крыше нужна, рассчитывать окно не только по первому hazard, а по всей группе hazards, которую можно перелететь одним roof jump.

19. Если chain на крыше пока не нужна, явно оставить стратегию single-hazard и не смешивать ее с ground chain model.

20. Обновить executors:
    - обычный roof jump over вызывает `RoofJumpRequest`;
    - super roof jump over должен вызывать `SuperRoofJumpRequest`, потому что runtime уже имеет отдельную механику super roof jump.

21. Обновить simulator:
    - оба roof jump-over варианта должны оставлять hamster в `RoofRun`;
    - `RoofSupportInstanceId` должен брать support id из resolver-validated action.

22. Зарегистрировать новую стратегию в runtime bot strategy list.

23. Обновить renderer / diagnostics только если новый action kind не отображается или логируется неясно.

24. Не добавлять unit-тесты без отдельной необходимости. Основная проверка должна идти через тестовые уровни.

## Suggested Order

1. Сначала обобщить `JumpOver` / `SuperJumpOver` по канону `JumpOnRoof` / `SuperJumpOnRoof`.
2. Затем привести `SuperRoofJumpMechanics` к resolver-подходу.
3. Затем добавить `SuperRoofJumpOutcomeResolver`.
4. Затем обобщить `RoofJumpOver` в shared + policy.
5. Затем добавить `SuperRoofJumpOver`.
6. После этого пройтись по naming, diagnostics и registration.

## Open Questions

Closed decisions:

- Roof jump-over должен поддерживать chain из нескольких roof hazards.
- На крыше в этом сценарии ожидаются только `smallNotAliveRoadAndRoof`, но таких obstacles может быть больше одного.
- `BotActionKind.SuperRoofJump` нужно переименовать или заменить на `SuperRoofJumpOver`, потому что речь идет о bot action для перепрыгивания roof hazard, а не просто о runtime input.
- `RoofJumpOver` и `SuperRoofJumpOver` должны оставаться отдельными стратегиями от будущего сценария прыжка с крыши на крышу.
- Executors для обычного и super roof jump-over нужно оставить отдельными: обычный roof jump fires одним событием, super roof jump требует отдельной runtime-последовательности.
- Shared ground jump-over должен стать образцом для roof jump-over chain/window logic.
- Chain-подход нужен для всех jump-over семейств, потому что super-варианты могут закрывать больше одного препятствия за действие.
