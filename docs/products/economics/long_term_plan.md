# Блок 5: план долгой мотивации

2026-09-10. Основа: [brief 18](research/18_long_term_minimum.md). Работа в `integration/unity-live`; владельцы блоков 1/2 передали scope, блок 4 подтвердил отсутствие пересечения.

## Задачи

1. **Факт награды LevelUp.** `PlayerData.cs`, новый `LevelUpReward.cs`, `PlayerDataValidator.cs`, `CharacterDevelopmentService.cs`, `PlayerExperienceService.cs`, `ExperienceGrantResult.cs`, `SkinManager.cs`, `SuperAttackService.cs`. Проверять загруженные каталоги, открытия и реальные максимальные tiers. После исчерпания: 50 монет за повышение; иначе 1 DP. Порог 240 XP. Квитанция входит в существующую транзакцию. Старые pending-повышения показывают прежний DP. Приёмка: новый пак возвращает DP; сохранённая выплата остаётся прежней.
2. **Существующий LevelUp.** `PlayerLevelPresentation.cs`, `LevelUpModalController.cs`, локализация RU/EN. Зависит от задачи 1. Показать фактические суммы, включая смешанный диапазон; ACK очищает только показанные квитанции. Сохранить очередь, профиль/pоколение, обучение и возврат. Приёмка: повтор окна ничего не начисляет; новое повышение во время окна остаётся pending.
3. **Мастерство кампании.** `StageNextGoalRule.cs`, `NextGoalCoordinator.cs`. Общая функция строит цель из `LevelManager.SavedProgressOverview`: все уровни пройдены, первый открытый результат 1–2 звезды, сумма/максимум из каталога. Существующие Action.Stage/NextGoalNavigation/Select доставляют цель. Приёмка: после последней победы цель появляется; после всех 3 звёзд исчезает; новый пак пересчитывает условия. Готовая награда имеет приоритет над уже показанным Stage.
4. **JourneyComplete.** `JourneyCompleteModalController.cs`, `UiJourneyCompleteModalMechanics.cs`, локализация RU/EN; при необходимости существующие UXML/USS. Зависит от 1/3. Переиспользовать текущие надписи и кнопку: мастерство, оставшееся развитие, затем рейтинг. Сохранить LevelResultNavigationCoordinator и ACK. Приёмка: переход выбирает конкретный открытый уровень, исчерпанное развитие не предлагается.
5. **Доставка.** `long_term_implementation.md`, economics/README, brief18, Unity-generated meta нового C#. Scoped review, project regeneration, dotnet compile; исправить найденное. Затем один scoped commit и обычный push под lock, подтвердить remote.

## Границы проверки

Новые UI-элементы и выплаты за звёзды исключены решением пользователя. Story и экономика блоков 3/4 сохраняются. Ручные UI/Play/phone и APK пользователь в этой задаче не поручал. Проверки кода отделить от исполнения.

## Владение

Все пути C# относительно `LostCyberHamster/Assets/Scripts`: GameManagement/PlayerProgress, GameManagement/Persistence, SharedCore/Meta/CharacterDevelopment, SharedCore/Meta/Skins, SharedCore/Meta/SuperAttacks, UI/Notifications, UI/Modals, UI/NextGoals, GameEngine/Mechanics. Только перечисленные файлы; общий dirty и generated-шум других задач сохранить.
