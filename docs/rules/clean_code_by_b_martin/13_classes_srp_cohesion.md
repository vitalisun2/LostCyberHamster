# Классы: SRP и связность

## Концепция

Класс должен иметь одну причину для изменения. Размер класса важен, но главный сигнал - количество ответственностей. Связный класс использует свои поля и методы вокруг одной концепции; несвязный класс выглядит как склад случайных операций.

## Когда читать

- Класс растет и получает новый сценарий.
- В одном классе смешаны input, gameplay, UI, persistence, debug.
- Новое поле используется только одним маленьким подмножеством методов.

## Правила

- Опиши ответственность класса одной фразой.
- Если появилась вторая независимая фраза, нужен новый класс или сервис.
- Держи данные рядом с поведением, которое их использует.
- Не делай класс "manager" для всего.
- Инкапсуляцию можно ослабить для тестов или инфраструктуры только с явной причиной.

## Пример

Плохо: контроллер знает все.

```csharp
public sealed class BotManager
{
    public void ReadInput() { }
    public void PlanJump() { }
    public void ApplyPhysics() { }
    public void SaveDebugLog() { }
    public void DrawEditorGizmos() { }
}
```

Лучше: ответственности разделены по причинам изменения.

```csharp
public sealed class BotMovementController
{
    public void ApplyMovement(BotMovementPlan plan) { }
}

public sealed class BotJumpPlanner
{
    public BotMovementPlan PlanJump(BotState state) { }
}

public sealed class BotDebugReporter
{
    public void WritePlan(BotMovementPlan plan) { }
}
```

## Правило для агента

Перед добавлением метода в класс проверь: изменится ли этот метод по той же причине, что и остальной класс. Если нет, ищи нового владельца ответственности.

