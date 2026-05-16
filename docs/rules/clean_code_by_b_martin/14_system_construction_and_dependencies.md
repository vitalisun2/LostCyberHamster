# Системы: создание и использование

## Концепция

Код, который создает систему, должен быть отделен от кода, который ее использует. Бизнес-логика не должна сама собирать граф зависимостей, искать глобальные объекты или знать, какие конкретные реализации нужно создать.

## Когда читать

- Добавляется сервис, фабрика, singleton, dependency injection.
- Система инициализируется в Unity scene/bootstrap.
- Класс сам создает зависимости через `new`, хотя должен только выполнять сценарий.

## Правила

- Создание зависимостей держи в composition root, factory или bootstrap-слое.
- Runtime-логика получает готовые зависимости через конструктор, init-метод или сериализованные ссылки Unity.
- Не смешивай `new ConcreteService()` с доменным алгоритмом.
- Фабрика уместна, если создание зависит от типа, конфигурации или платформы.
- Стандарт или фреймворк использовать только там, где он реально упрощает систему.

## Пример

Плохо: use-case сам знает, как создать инфраструктуру.

```csharp
public sealed class LevelLoader
{
    public async Task Load(string levelId)
    {
        var addressables = new AddressablesGateway();
        LevelConfig config = await addressables.LoadLevel(levelId);
        // ...
    }
}
```

Лучше: зависимость приходит снаружи.

```csharp
public sealed class LevelLoader
{
    private readonly ILevelAssetGateway _assetGateway;

    public LevelLoader(ILevelAssetGateway assetGateway)
    {
        _assetGateway = assetGateway;
    }

    public async Task Load(string levelId)
    {
        LevelConfig config = await _assetGateway.LoadLevel(levelId);
        // ...
    }
}
```

## Правило для агента

Если класс одновременно строит зависимости и выполняет игровой сценарий, раздели construction и use. Это почти всегда снижает связность и упрощает тестирование.

