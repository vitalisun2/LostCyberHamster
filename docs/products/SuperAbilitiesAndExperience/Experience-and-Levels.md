# Experience and Player Level

## 1. Целевое поведение

- Игрок получает XP за звёзды, новый личный рекорд по очкам и квесты.
- Одна звезда даёт `10 XP`: результат в 1/2/3 звезды даёт `10/20/30 XP`.
- При повторном прохождении уровня XP начисляется только за улучшение лучшего результата по звёздам: `max(0, новые лучшие звёзды - старые лучшие звёзды) × 10`. Максимум с одного уровня — `30 XP`.
- Новый недельный личный рекорд игрока в leaderboard конкретной локации и части дня даёт `50 XP`. Это один лучший score одного успешного забега среди всех уровней этой части дня, без сложения результатов.
- Дневной квест даёт `20 XP`, длинный сюжетный квест — `60 XP`.
- Счёт забега сам по себе и score-множители XP не дают. Отдельной XP-награды за достижения нет.
- XP накапливается до фиксированного порога текущего Player Level. При достижении порога происходит Player Level Up.
- Каждый Player Level Up требует ровно `240 XP`.
- Излишек XP от одной награды переносится в следующий Player Level и не теряется.

## 2. Текущее состояние

Модель сохранения, расчёт XP и утверждённые источники XP подключены.

- `Assets/Scripts/GameManagement/PlayerProgress/PlayerData.cs` хранит XP в `ExperiencePoints`, а Player Level — в `PlayerLevel`.
- `Assets/Scripts/GameManagement/Persistence/PlayerDataValidator.cs` нормализует XP до неотрицательного значения, а `PlayerLevel` — минимум до `1`.
- `Assets/Scripts/GameManagement/PlayerProgress/PlayerExperienceService.cs` хранит правила всех XP-источников и их награды. Внешние системы передают только подтверждённые факты; сервис рассчитывает XP, применяет порог `240 XP`, повышает `PlayerLevel` и переносит остаток.
- `Assets/Scripts/GameEngine/Mechanics/UiGameOverMechanics.cs` передаёт число оставшихся жизней как звёзды события завершения уровня. `Assets/Scripts/System/LevelManagement/LevelManager.cs` получает обновлённый progress snapshot и передаёт его с ключом уровня в `PlayerExperienceService`; сервис сам извлекает старый и новый best stars.
- `Assets/Scripts/GameManagement/PlayerProgress/LevelProgressEntry.cs` хранит лучший результат до трёх звёзд; `ApplyStars` не понижает уже сохранённый максимум. `PlayerExperienceService` начисляет `10 XP` за каждую улучшенную звезду.
- `Assets/Scripts/GameEngine/Mechanics/RunScoreMechanics.cs` считает очки забега. `Assets/Scripts/Leaderboard/LeaderboardService.cs` хранит в Unity Leaderboards один лучший score успешного забега за неделю для конкретной локации и части дня. После серверного улучшения `PartOfDayScoreMechanics` передаёт сервису факт нового weekly record и выполняет отдельный checkpoint.
- `Assets/Scripts/SharedCore/Meta/QuestSystem/QuestManager.cs` однократно выдаёт награду через `GetReward`. Монеты и кристаллы сохраняются без изменений; после claim дневного или сюжетного квеста manager передаёт сервису только тип подтверждённой награды, затем выполняет тот же claim-checkpoint.
- `Assets/Scripts/GameManagement/Persistence/PlayerProgressCommitter.cs` сохраняет весь `PlayerData`, включая `ExperiencePoints` и `PlayerLevel`.

## 3. Нужно изменить

- Использовать сохранённые `ExperiencePoints` и `PlayerLevel` как состояние прогрессии игрока.
- Использовать `PlayerExperienceService` как единую точку правил XP, начисления и Player Level Up. Конкретные XP-значения не должны находиться в системах звёзд, leaderboard или квестов.
- Начислять XP только за прирост лучшего результата по звёздам, не меняя существующее звёздное открытие уровней и локаций.
- Начислять `50 XP` только после серверного подтверждения `IsNewRecord` для улучшенного недельного leaderboard-рекорда конкретной локации и части дня.
- Подключить XP к одноразовой награде дневных и сюжетных квестов, сохранив защиту от повторного получения.
- Сохранять изменившиеся XP и `PlayerLevel` через существующий механизм контрольных точек.

## 4. Шаги реализации

1. **Модель прогресса и сохранение.** `PlayerData` хранит XP в `ExperiencePoints` и Player Level в `PlayerLevel`; `PlayerDataValidator` нормализует значения; `PlayerProgressCommitter` сохраняет их вместе с `PlayerData`.
2. **XP Service и Player Level Up.** `PlayerExperienceService` принимает подтверждённые факты источников, сам выбирает соответствующую XP-награду, обновляет XP и `PlayerLevel`, применяет `240 XP` к каждому Player Level Up и переносит остаток.
3. **XP за звёзды.** В цепочке `UiGameOverMechanics` → `LevelManager.HandleLevelCompleted` → `ProgressService.HandleLevelCompleted` передавать в `PlayerExperienceService` обновлённый snapshot и ключ уровня; сервис сам сравнивает его с сохранённым progress и начисляет XP только за положительную разницу звёзд.
4. **XP за рекорд.** В цепочке `PartOfDayScoreMechanics.SubmitScoreAsync` → `LeaderboardService.SubmitSuccessfulRunAsync` → `RunResultData.IsNewRecord` начислять `50 XP` только после успешного серверного подтверждения улучшенного недельного leaderboard-рекорда конкретной локации и части дня.
5. **XP за квесты.** В `QuestManager.GetReward` подключить XP-награду к существующей одноразовой выдаче награды; различать дневные и сюжетные квесты по уже существующим коллекциям и не нарушать текущую выдачу монет и кристаллов.
