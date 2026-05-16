# Switch, if/else и полиморфизм

## Концепция

Большие `switch` и цепочки `if/else` по типу часто нарушают SRP и OCP: при добавлении нового типа приходится менять много мест. Допустимое место для такого ветвления - граница создания объектов или выбора стратегии. После выбора поведение должно идти через полиморфизм, интерфейс или таблицу стратегий.

## Когда читать

- Добавляется `switch` по enum/type/category.
- Один и тот же `switch` повторяется в нескольких методах.
- Новый тип препятствия, поведения бота, режима движения требует правок по всему коду.

## Правила

- Повторяющийся `switch` заменить стратегиями или полиморфными объектами.
- `switch` допустим в фабрике, parser'е, adapter'е или composition root.
- Условие по типу не должно расползаться в бизнес-логику.
- Не вводить абстракцию ради одного маленького стабильного условия.

## Пример

Плохо: при добавлении нового типа препятствия придется менять каждый метод.

```csharp
private bool CanPass(Obstacle obstacle)
{
    switch (obstacle.Type)
    {
        case ObstacleType.LowBarrier:
            return _hamster.CanJump;
        case ObstacleType.RoofGap:
            return _hamster.CanRoofJump;
        default:
            return false;
    }
}
```

Лучше: тип выбран на границе, поведение живет в стратегии.

```csharp
private interface IObstaclePassRule
{
    bool CanPass(HamsterState hamster);
}

private sealed class LowBarrierPassRule : IObstaclePassRule
{
    public bool CanPass(HamsterState hamster) => hamster.CanJump;
}

private sealed class RoofGapPassRule : IObstaclePassRule
{
    public bool CanPass(HamsterState hamster) => hamster.CanRoofJump;
}
```

## Правило для агента

Если видишь второй такой же `switch`, это уже архитектурный сигнал. Проверь, не пора ли оставить ветвление только в месте выбора стратегии.

