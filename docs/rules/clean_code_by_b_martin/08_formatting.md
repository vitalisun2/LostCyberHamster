# Форматирование и порядок чтения

## Концепция

Форматирование передает структуру мысли. Связанные строки должны быть рядом, разные концепции - разделены. Файл должен читаться сверху вниз: сначала важная идея, затем детали.

## Когда читать

- Метод или класс трудно просканировать глазами.
- Вносятся правки в большой файл.
- Появился шум от пустых строк, выравнивания или случайного порядка методов.

## Правила

- Держи связанные строки рядом.
- Разделяй пустой строкой разные логические шаги.
- Не выравнивай колонки вручную, если это усложняет поддержку.
- Локальные переменные объявляй рядом с использованием.
- Методы располагай так, чтобы верхний уровень шел перед деталями.
- Следуй formatter'у и локальному стилю проекта.

## Пример

Плохо: переменные и шаги перемешаны.

```csharp
float jumpDistance = CalculateDistance(start, target);
Vector2 direction = target - start;
if (!CanJump(jumpDistance)) return;
float impulse = CalculateImpulse(jumpDistance);
Vector2 velocity = direction.normalized * impulse;
rigidbody.velocity = velocity;
```

Лучше: чтение идет блоками.

```csharp
float jumpDistance = CalculateDistance(start, target);
if (!CanJump(jumpDistance))
    return;

Vector2 direction = target - start;
float impulse = CalculateImpulse(jumpDistance);
Vector2 velocity = direction.normalized * impulse;

rigidbody.velocity = velocity;
```

## Правило для агента

Если форматирование не помогает увидеть сценарий, проблема может быть не в пробелах, а в структуре функции. Тогда читай `03_functions_one_operation.md`.

