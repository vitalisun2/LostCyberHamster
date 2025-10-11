# Проектирование универсального загрузчика Addressables

Документ фиксирует архитектурные решения для общего механизма загрузки ресурсов Addressables, предназначенного для использования как в рантайме, так и в редакторе. Задача — предоставить единый слой управления `AsyncOperationHandle` и удобный API для клиентов, без избыточной специализации.

## Основные цели

- **Единый lifecycle**: гарантировать, что каждый загруженный ресурс удерживается до завершения использования и корректно освобождается (`Addressables.Release`).
- **Поддержка разных паттернов обращения**: прямые ключи (addresses), загрузка по лейблам, получение `IResourceLocation`.
- **Минимум новых сущностей**: избежать множества специализированных фасадов; весь функционал сосредоточить в одном универсальном классе и его вспомогательных структурах.
- **Совместимость с редактором и рантаймом**: один и тот же API должен работать в обоих контекстах.
- **Тестируемость**: предоставить возможность писать юнит/интеграционные тесты на удержание и освобождение ресурсов, на корректность построения ключей/лейблов.

## Структура API

### 1. Generic-обёртки для ресурсов

- `AddressableLease<T>` — структура/класс с полями:
  - `AsyncOperationHandle<T>` или `AsyncOperationHandle<IList<T>>` (в зависимости от метода).
  - `T Value` или `IReadOnlyList<T> Values`.
  - `Dispose()` (или `Release()`) освобождает handle, обнуляет поля.
- `AddressableLease<T>` реализует `IDisposable`, чтобы можно было использовать `using` при краткоживущих загрузках и явно вызывать `Dispose` в длительноживущих сценариях.
- Для массовых загрузок (списки) — отдельная обёртка `AddressableSetLease<T>` или тот же тип с признаком `IsCollection`.

### 2. Универсальный загрузчик

- Статический класс `AddressableLoader` предоставляет базовый набор операций:
  - `Task<AddressableLease<T>> LoadAssetAsync<T>(string key, CancellationToken cancellationToken = default)` — загрузка по адресу.
  - `Task<AddressableSetLease<T>> LoadAssetsByLabelAsync<T>(string label, CancellationToken cancellationToken = default)` — загрузка по лейблу.
  - `Task<AddressableLocationsLease> LoadLocationsAsync(string label, Type assetType, CancellationToken cancellationToken = default)` — получение `IResourceLocation`.
- Для редакторского синхронного доступа используются методы `LoadAssetSync<T>(string key)` и `LoadAssetsByLabelSync<T>(string label)`, которые оборачивают `WaitForCompletion` и выполняют те же проверки на пустые аргументы.
- Вся логика `try/catch`, проверка валидности хэндла и гарантированное освобождение расположенны внутри загрузчика; внешние потребители всегда получают готовый lease.

### 3. Простые адаптеры/extension-методы

- Для доменных сценариев (например, получение лейбла препятствий) используются приватные или статические методы в существующих классах (`LevelDataProvider.BuildLocationLabel`).
- Дополнительные фасады (например, `SpriteLoader`, `PrefabLoader`) добавляются только при реальной необходимости и могут быть thin-extension над `AddressableLoader`.

### 3.1 Хелперы построения ключей и лейблов

- Для схем «локация + постфикс» используются свободные функции/статические методы в том же модуле, где потребляется ресурс:
  ```csharp
  static string BuildLocationLabel(string location, string postfix, string fallbackLocation)
  {
      var baseName = string.IsNullOrWhiteSpace(location) ? fallbackLocation : location.Trim();
      return $"{baseName} {postfix}";
  }
  ```
- Постфиксы и константы берутся из `Consts`, что исключает магические строки.
- Fallback-логика (например, `LocationAssetFallback.TryBuildFallbackLabel`) остаётся в доменных классах; общий загрузчик не принимает на себя эту ответственность.
- Для прямых адресов (`skip_button`, `shopItems.json`) рекомендуется держать константы в местах использования или в `Consts`, чтобы избежать ошибок в строках.

### 4. Менеджер кэша (опционально)

- `AddressableLeaseCache` — класс для reference counting.
  - Хранит `Dictionary<string, (lease, refCount)>` для повторно используемых ресурсов.
  - `AcquireAsync(label)` → если объект уже в кэше, увеличивает счётчик и возвращает `AddressableLeaseView<T>` (обёртку без `Dispose`, а с `Release()` → уменьшает счётчик).
  - `Release` при достижении нуля вызывает `lease.Dispose()`.
- Менеджер не обязателен на первом этапе; его добавляют, если появится потребность в глобальном кешировании.

## Использование

### Рантайм (`LevelDataProvider` пример)

```csharp
using var spritesLease = await AddressableLoader.LoadAssetsByLabelAsync<Sprite>(label);
levelData.ObstaclesSprites = spritesLease.Values.ToList();
_currentLevel.LeaseTokens.Add(spritesLease); // сохранить, чтобы освободить при выгрузке уровня
```

- Классы, которые должны держать ресурсы долго, сохраняют ссылку на `AddressableLease`/`AddressableSetLease` и освобождают их в своём `Dispose`/`OnDestroy`.
- Краткоживущие операции (взять texture, показать UI) используют `using`.

### Редактор (LevelEditor)

```csharp
using var paletteSprites = AddressableLoader.LoadAssetsByLabelSync<Sprite>(label);
PopulatePalette(paletteSprites.Values);
```

- Синхронные методы доступны только для редактора; в рантайме вместо них применяется асинхронная версия.

## Жизненный цикл и контракт

- Любой метод, возвращающий lease, гарантирует, что handle активен до вызова `Dispose`.
- Потребитель обязан `Dispose` (через `using` или вручную).
- При повторной попытке `Dispose` — безопасно (проверка `IsValid`).
- В режиме DEBUG можно добавить финализатор с предупреждением, если lease был собран без `Dispose`.
- Обязательное правило: владелец ресурса (уровень, окно, менеджер) хранит ссылку на lease до момента выгрузки и вызывает `Dispose` в своём lifecycle-обработчике (`OnDestroy`, `Dispose`, `OnDisable`).
- Краткоживущие операции (разовая загрузка текстуры для UI) оборачиваются в `using`.
- Для кэш-менеджера — явный reference counting: `Acquire` увеличивает счётчик, `Release` уменьшает и вызывает `lease.Dispose()` при переходе через ноль. Double-release считается ошибкой и может логироваться в DEBUG.

## Покрытие тестами

- Юнит-тесты в `Assets/Tests/EditMode/AddressableLeaseTests.cs` проверяют:
  - Создание lease из корректных и некорректных хэндлов, двойной `Dispose` и освобождение ресурсов.
  - Защиту от пустых ключей/лейблов в синхронных и асинхронных методах загрузчика.
- Интеграционные/толерантные: загрузка и release нескольких наборов подряд, проверка `Addressables.Release` счётчика (по логам или по `Addressables.ResourceManager`).
- Редакторские тесты (optional): имитация смены локации, ensure `Dispose` вызывается.

## Риски и меры

- Ошибки при забытом `Dispose` → лог предупреждения (DEV-сборки), регулярные ревью на `using`.
- `WaitForCompletion` в редакторе может блокировать UI → использовать по минимуму, предпочтительно асинхронные методы.
- Совместимость с уже существующим кодом: на первых этапах возможно использование адаптеров (`LegacyLoadSprites` вызывает новый API внутри, сохраняя сигнатуру) для постепенной миграции.

## Итоги

- Нужен один универсальный загрузчик с lease-обёртками: он решает проблему lifecycle и покрывает все Addressables-случаи.
- Специализация по типам ресурсов откладывается до появления конкретной необходимости.
- Документ служит ориентиром на этапах реализации и тестирования (Вехи 2 и 3 плана рефакторинга).