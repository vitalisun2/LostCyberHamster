# Функции: одна операция и один уровень абстракции

## Концепция

Функция должна быть маленькой единицей поведения с ясным именем. Читатель должен понимать ее назначение по имени и нескольким строкам тела, не проваливаясь сразу в детали реализации.

Главный критерий: все строки функции находятся на одном уровне абстракции. Если рядом стоят бизнес-решение, проверка Unity-компонента, арифметика координат, логирование и изменение состояния, функция почти наверняка делает больше одной операции.

## Когда читать

- Длинный метод.
- Сложная вложенность.
- Рефакторинг алгоритма.
- Добавление нового сценария в существующий метод.

## Правила

- Функция делает один шаг сценария и делает его явно.
- Внутри функции не смешиваются разные уровни детализации.
- Блоки `if`, `else`, `while` по возможности вызывают функции с говорящими именами.
- Верхний уровень читается сверху вниз как сценарий.
- Детали раскрываются в нижележащих функциях.

## Пример

Плохо: метод одновременно валидирует, считает, меняет состояние и логирует.

```csharp
private void ApplyJump(Hamster hamster, Vector2 target)
{
    if (hamster == null || !_ground.Contains(target))
        return;

    Vector2 direction = target - hamster.Position;
    float distance = direction.magnitude;
    Vector2 impulse = direction.normalized * Mathf.Min(distance * 2f, MaxImpulse);

    hamster.Rigidbody.velocity = impulse;
    hamster.State = HamsterState.Jumping;
    Debug.Log($"Jump to {target}");
}
```

Лучше: верхний метод показывает намерение, детали вынесены ниже.

```csharp
private void ApplyJump(Hamster hamster, Vector2 target)
{
    if (!CanJumpTo(hamster, target))
        return;

    Vector2 impulse = CalculateJumpImpulse(hamster.Position, target);
    StartJump(hamster, impulse);
}

private bool CanJumpTo(Hamster hamster, Vector2 target)
{
    return hamster != null && _ground.Contains(target);
}

private Vector2 CalculateJumpImpulse(Vector2 start, Vector2 target)
{
    Vector2 direction = target - start;
    float distance = direction.magnitude;
    return direction.normalized * Mathf.Min(distance * 2f, MaxImpulse);
}

private void StartJump(Hamster hamster, Vector2 impulse)
{
    hamster.Rigidbody.velocity = impulse;
    hamster.State = HamsterState.Jumping;
}
```

## Правило для агента

Если при чтении функции приходится держать в голове больше одного уровня деталей, предложи разбиение. Цель не в уменьшении числа строк сама по себе, а в явном разделении намерения и механики.
