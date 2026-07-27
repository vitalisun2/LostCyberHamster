# Experience and Player Level

## 1. Целевое поведение

- Игрок получает XP за звёзды, новый личный рекорд по очкам и квесты.
- Одна звезда даёт `10 XP`: результат в 1/2/3 звезды даёт `10/20/30 XP`.
- При повторном прохождении уровня XP начисляется только за улучшение лучшего результата по звёздам: `max(0, новые лучшие звёзды - старые лучшие звёзды) × 10`. Максимум с одного уровня — `30 XP`.
- Новый личный рекорд даёт `50 XP`.
- Дневной квест даёт `20 XP`, длинный сюжетный квест — `60 XP`.
- Счёт забега сам по себе и score-множители XP не дают. Отдельной XP-награды за достижения нет.
- XP накапливается до фиксированного порога текущего Player Level. При достижении порога происходит Player Level Up.
- Каждый Player Level Up требует ровно `240 XP`.
- Излишек XP от одной награды переносится в следующий Player Level и не теряется.

## 2. Текущее состояние

Модель сохранения и расчёт XP уже существуют. Источники будущего XP работают как независимые игровые системы, но пока не подключены.

- `Assets/Scripts/GameManagement/PlayerProgress/PlayerData.cs` хранит XP в `ExperiencePoints`, а Player Level — в `PlayerLevel`.
- `Assets/Scripts/GameManagement/Persistence/PlayerDataValidator.cs` нормализует XP до неотрицательного значения, а `PlayerLevel` — минимум до `1`.
- `Assets/Scripts/GameManagement/PlayerProgress/PlayerExperienceService.cs` принимает подтверждённую XP-награду, применяет фиксированный порог `240 XP`, повышает `PlayerLevel` и переносит остаток.
- `Assets/Scripts/GameEngine/Mechanics/UiGameOverMechanics.cs` передаёт число оставшихся жизней как звёзды события завершения уровня. `Assets/Scripts/System/LevelManagement/LevelManager.cs` сохраняет результат через `ProgressService`.
- `Assets/Scripts/GameManagement/PlayerProgress/LevelProgressEntry.cs` хранит лучший результат до трёх звёзд; `ApplyStars` не понижает уже сохранённый максимум. XP за звёзды сейчас не начисляется.
- `Assets/Scripts/GameEngine/Mechanics/RunScoreMechanics.cs` считает очки забега. `Assets/Scripts/Leaderboard/LeaderboardService.cs` сравнивает результат с серверным рекордом уровня, а `RunResultData.IsNewRecord` сообщает подтверждённый новый рекорд. XP за рекорд сейчас не начисляется.
- `Assets/Scripts/SharedCore/Meta/QuestSystem/QuestManager.cs` отслеживает дневные и сюжетные квесты и однократно выдаёт их награду через `GetReward`. Этот метод поддерживает только монеты и кристаллы, а `ResourceType` не содержит XP.
- `Assets/Scripts/GameManagement/Persistence/PlayerProgressCommitter.cs` сохраняет весь `PlayerData`, включая `ExperiencePoints` и `PlayerLevel`.

## 3. Нужно изменить

- Использовать сохранённые `ExperiencePoints` и `PlayerLevel` как состояние прогрессии игрока.
- Использовать `PlayerExperienceService` как единую точку начисления XP и Player Level Up.
- Начислять XP только за прирост лучшего результата по звёздам, не меняя существующее звёздное открытие уровней и локаций.
- Начислять XP за рекорд только после подтверждённого результата `IsNewRecord`.
- Подключить XP к одноразовой награде дневных и сюжетных квестов, сохранив защиту от повторного получения.
- Сохранять изменившиеся XP и `PlayerLevel` через существующий механизм контрольных точек.

## 4. Шаги реализации

1. **Модель прогресса и сохранение.** `PlayerData` хранит XP в `ExperiencePoints` и Player Level в `PlayerLevel`; `PlayerDataValidator` нормализует значения; `PlayerProgressCommitter` сохраняет их вместе с `PlayerData`.
2. **XP Service и Player Level Up.** `PlayerExperienceService` принимает подтверждённую награду, обновляет XP и `PlayerLevel`, применяет `240 XP` к каждому Player Level Up и переносит остаток.
3. **XP за звёзды.** В цепочке `UiGameOverMechanics` → `LevelManager.HandleLevelCompleted` → `ProgressService.HandleLevelCompleted` использовать сохранённый и новый лучший результат `LevelProgressEntry`, чтобы начислять XP только за положительную разницу звёзд.
4. **XP за рекорд.** В цепочке `PartOfDayScoreMechanics.SubmitScoreAsync` → `LeaderboardService.SubmitSuccessfulRunAsync` → `RunResultData.IsNewRecord` начислять XP только после успешного серверного подтверждения нового рекорда.
5. **XP за квесты.** В `QuestManager.GetReward` подключить XP-награду к существующей одноразовой выдаче награды; различать дневные и сюжетные квесты по уже существующим коллекциям и не нарушать текущую выдачу монет и кристаллов.
