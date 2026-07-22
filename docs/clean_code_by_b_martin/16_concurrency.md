# Многопоточность и асинхронность

## Концепция

Многопоточный код ломается редко, непредсказуемо и дорого. Поэтому concurrency-политику нужно отделять от основной логики, shared state минимизировать, а данные передавать через ясные границы.

Для Unity дополнительно помнить: большая часть Unity API должна вызываться на main thread.

## Когда читать

- `Task`, async/await, фоновые расчеты, Jobs, threads.
- Кеши, shared collections, события из разных потоков.
- Редкие неповторяемые сбои.

## Правила

- Отдели "что считаем" от "где и когда выполняем".
- Не передавай mutable Unity objects в фоновый поток.
- Используй immutable snapshots или копии данных.
- Минимизируй synchronized/locked секции.
- Не связывай несколько lock-зависимых методов скрытым порядком вызова.
- Проверяй не только happy path, но и завершение/отмену доступным способом.

## Пример

Плохо: фоновая задача читает mutable состояние сцены.

```csharp
private Task<JumpPlan> PlanJumpAsync(Hamster hamster)
{
    return Task.Run(() => _planner.Plan(hamster.transform.position, _level.Obstacles));
}
```

Лучше: в фон уходит снимок данных без Unity-объектов.

```csharp
private Task<JumpPlan> PlanJumpAsync(Hamster hamster)
{
    JumpPlanningSnapshot snapshot = CreateJumpPlanningSnapshot(hamster);
    return Task.Run(() => _planner.Plan(snapshot));
}
```

## Правило для агента

Если в асинхронном коде есть shared mutable state, считай это дефектом дизайна, пока не доказано обратное. Сначала ищи snapshot, ownership или main-thread boundary.
