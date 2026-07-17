# Local Save System Refactoring

## Роль Local Save

Local Save — автономный источник игрового прогресса. Хранит экономику, уровни, скины, квесты, настройки прогресса и системные флаги. После сбоя восстанавливает последний подтверждённый checkpoint.

Сейчас `GameDataManager` шифрует `PlayerData` в `PlayerPrefs`. После bootstrap Money/Crystals копируются в отдельные stateful storages. Settings хранятся отдельным JSON-ключом.

## Основные проблемы

- Money/Crystals имеют два расходящихся состояния: storages и `PlayerData`.
- Частичные checkpoint’ы допускают потерю награды, возврат расхода, устаревший баланс.
- Skin, current level, tutorial transitions, quest progress сохраняются не всегда.
- Storyline quest dictionary не имеет полноценного writer и сериализуемой формы.
- Load сохраняет данные до нормализации. Повреждённый pref не имеет fallback. Reset удаляет все `PlayerPrefs`.

## Целевые ответственности

- `PlayerData`: единственное состояние Money/Crystals, полный сериализуемый snapshot прогресса.
- `ResourceManager`: stateless API и `ResourceType` routing напрямую над `PlayerData`; обработка collection events.
- `GameDataManager`: local load, save, нормализация, scoped reset.
- Gameplay-системы: доменные изменения и запрос checkpoint, без persistence-кода.
- Settings: отдельный локальный контур.

## Этапы

### 1. Единое состояние экономики

- **Суть и ценность:** устранить рассинхронизацию Money/Crystals, сохранить generic routing для shop и skins.
- **Минимальные изменения:** перевести `ResourceManager` на `GameDataManager.PlayerData`; перенести collection-event subscriptions; удалить `MoneyStorage`, `CrystalStorage`, bootstrap `Init`, storage-to-`PlayerData` sync.

### 2. Целостный snapshot

- **Суть и ценность:** каждый local save фиксирует согласованный `PlayerData`, без runtime-copy.
- **Минимальные изменения:** оставить `SaveData()` единственной записью игрового snapshot; сохранять актуальные поля через существующие serialization и encryption.

### 3. Checkpoint policy

- **Суть и ценность:** необратимые операции и завершённый прогресс переживают немедленный выход; частые collect events не пишут на диск.
- **Минимальные изменения:** вызывать `SaveData()` после purchase, reward, результатов уровня, входа в menu, существующих app pause/quit boundaries. Не сохранять каждую монету.

### 4. Остальные поля и quests

- **Суть и ценность:** важные выборы, переходы, daily/storyline progress становятся долговечными.
- **Минимальные изменения:** добавить checkpoint’ы для skin, current level, tutorial transitions, daily quest progress/claim. Заменить storyline dictionary сериализуемой формой, обновляемой quest flow.

### 5. Безопасные load и reset

- **Суть и ценность:** валидное состояние при первом запуске, повреждении данных, reset.
- **Минимальные изменения:** порядок load, normalize, save; fallback на defaults; удаление только Local Save keys без Settings и чужих prefs.

## Будущая Cloud Save

Cloud Save дополняет Local Save: получает готовый snapshot, передаёт или применяет его. Игровые данные не формирует.

## Правило реализации

Дополнительный рефакторинг выполняется только при подтверждённой необходимости и после одобрения пользователя; архитектура заранее не усложняется.
