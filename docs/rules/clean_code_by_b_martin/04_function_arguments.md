# Аргументы функций

## Концепция

Аргументы усложняют понимание и тестирование функции. Особенно опасны boolean-флаги, выходные аргументы и длинные списки параметров: они часто означают, что функция делает несколько разных вещей или скрывает недостающую концепцию.

## Когда читать

- Метод получает 3+ аргумента.
- Добавляется `bool`, `mode`, `type`, `kind`, `isPreview`.
- Метод меняет объект, переданный как параметр.
- Появляется группа параметров, которая часто передается вместе.

## Правила

- Предпочитай 0-2 аргумента.
- Boolean-флаг почти всегда заменить двумя методами с честными именами.
- Группу связанных параметров оформить в value object/parameter object.
- Не использовать выходные аргументы, если можно вернуть значение.
- Если метод меняет состояние, пусть это видно из имени.

## Пример

Плохо: флаги превращают один метод в набор скрытых сценариев.

```csharp
private void MoveBot(Vector2 target, bool jump, bool ignoreObstacles)
{
    if (!ignoreObstacles && IsBlocked(target))
        return;

    if (jump)
        JumpTo(target);
    else
        WalkTo(target);
}
```

Лучше: разные намерения выражены разными методами.

```csharp
private void WalkBotTo(Vector2 target)
{
    if (IsBlocked(target))
        return;

    WalkTo(target);
}

private void JumpBotToReachableTarget(Vector2 target)
{
    if (!CanJumpTo(target))
        return;

    JumpTo(target);
}
```

Если параметры образуют одну концепцию, дай ей имя.

```csharp
private readonly struct JumpRequest
{
    public JumpRequest(Vector2 start, Vector2 target, float maxImpulse)
    {
        Start = start;
        Target = target;
        MaxImpulse = maxImpulse;
    }

    public Vector2 Start { get; }
    public Vector2 Target { get; }
    public float MaxImpulse { get; }
}
```

## Правило для агента

Перед добавлением нового аргумента спроси: это действительно вход функции или признак того, что нужно выделить новый сценарий, объект параметров или метод?

