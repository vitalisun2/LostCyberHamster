# Реализация ядра загрузчика Addressables (Веха 3)

Документ описывает задачи только для третьей вехи: создание универсального загрузчика и минимальная интеграция.

## План работ

1. **Подготовка инфраструктуры**
   - ✅ Определить целевую папку для новых классов (`Assets/Scripts/System/Resources` или иное актуальное место).
   - ✅ Создать файлы с заготовками: `AddressableLease.cs`, `AddressableLoader.cs` (и при необходимости `AddressableLocationsLease.cs`).

2. **Реализация обёрток lease**
   - ✅ `AddressableLease<T>`: хранение `AsyncOperationHandle<T>` или `AsyncOperationHandle<IList<T>>`, доступ к данным, `Dispose()`.
   - ✅ Поддержка безопасного повторного `Dispose`, optional финализатор в DEBUG-сборке.

3. **Реализация `AddressableLoader`**
   - ✅ Метод `LoadAssetAsync<T>(string key)`.
   - ✅ Метод `LoadAssetsByLabelAsync<T>(string label)`.
   - ✅ Обработка ошибок/исключений, гарантированный release при неуспехе, поддержка cancellation token.

4. **Поддержка resource locations (при необходимости)**
   - ✅ Реализовать `AddressableLocationsLease` и метод `LoadLocationsAsync` для сценариев, где требуются `IResourceLocation`.

5. **Редакторские синхронные обёртки**
   - ✅ Методы `LoadAssetSync<T>` / `LoadAssetsByLabelSync<T>` (используют `WaitForCompletion`), возвращают те же lease.
   - ✅ Отметить, что применять их следует только в редакторском коде.

6. **Юнит-тесты**
   - ✅ Написать тесты для loader и lease (успешная загрузка, ошибка ключа, двойной `Dispose`).

7. **Актуализация документации**
   - ✅ Обновить `sprite_loader_api_design.md` и другие dev-документы по итогам реализации ядра.

## Примечание

- После выполнения каждого шага — локальная проверка/тесты и фиксация результатов (в соответствии с общим планом рефакторинга).