# Блок 5: долгая мотивация

2026-09-10. Основа `7e321842`, ветка `integration/unity-live`. [План](long_term_plan.md), [решение пользователя](research/18_long_term_minimum.md).

## Реализация

- Stage-цель после всех побед: сумма best-звёзд / максимум активного каталога, первый открытый уровень с 1–2 звёздами. Используется `SavedProgressOverview`; DEV override не подменяет сохранённый best. Новые уровни меняют условия и максимум. После полного мастерства цель исчезает.
- NextGoal сохраняет четыре категории, конфигурацию, закрытие и cooldown. Приоритет категории теперь предшествует удержанию текущей карточки: появившаяся готовая награда вытесняет Stage. Урок первой сессии по-прежнему выше общих правил.
- JourneyComplete использует те же данные. Существующая Skills-кнопка получает готовую иконку карты и ведёт в Select. При отсутствии недобора предлагает оставшееся развитие; при полном развитии показывает его завершение и сохраняет Home/рейтинг.
- Select открывает существующую сетку части суток выбранного уровня; конкретный номер указан в цели. Автозапуска нет. `NextGoalNavigation.Prepare` вызывается до очереди LevelUp, как в обычном Win. Profile/Generation, ACK и `PrepareResume` сохранены.
- `CharacterDevelopmentService` проверяет готовность обоих каталогов, открытия всех их предметов и достигнутые максимальные tiers. Баланс и `CanUpgrade` не участвуют. Пока развитие осталось или каталог загружается — 1 DP; после исчерпания — 50 монет. Порог 240 XP и рост уровня сохранены.
- Награда и `LevelUpReward` сохраняются существующей транзакцией XP. `ExperienceGrantResult` содержит фактические DP/монеты. UI суммирует квитанции показанного диапазона, включая смешанный результат. Старые повышения без квитанций сохраняют прежнее отображение DP; прошлый баланс не конвертируется.
- ACK удаляет квитанции только до показанного уровня. Более поздний LevelUp остаётся pending. После нового пака прежние монеты остаются монетами, следующие повышения возвращаются к DP. Показ/повтор окна не содержит начисления.
- RU/EN дополнены. UXML и число элементов сохранены; меняются существующие подписи, видимость кнопки исчерпанного развития и иконка существующей кнопки JourneyComplete. Story и выплаты за звёзды сохранены.

## Проверки

- Итоговый code review: владельцы начисления, JSON/rollback, старые профили, смешанный диапазон, новый XP во время окна, новый каталог, неизвестный каталог, приоритеты и очередь переходов. Выполнен просмотр кода, а не исполнение этих сценариев.
- Исправлено при review: цель читает сохранённый best; отложенный `LocalizedLabel` не перезаписывает динамический JourneyComplete; готовая награда выше удерживаемой Stage-карточки; receipt XP перестал предполагать DP за каждое повышение.
- Первый compile выявил отсутствующий `Compile Include`: новый файл ещё не был импортирован Editor. Выполнен точечный `AssetDatabase.ImportAsset` через Unity CLI. Unity создал `.meta`, GUID `1aa365b24f886df4cae4d61a1db50be0`.
- Финальный `lch_project_regenerate_files`: Success, новый файл включён. `dotnet build Assembly-CSharp.csproj --no-restore -v:q`: **0 ошибок, 42 прежних предупреждения, 6,22 с**. Локальный лог: `LostCyberHamster/EditorLogs/long_term_compile.log`.
- Локализация RU/EN разобрана, новые ключи и параметры совпадают; дубликаты отсутствуют. Существующий Sprite карты найден. Scoped `git diff --check` чистый.
- По поручению этой задачи UI/Play/phone, APK, тестовые harness и игровые сценарии не запускались. Внешний вид и поведение на устройстве принимает пользователь. Compile подтверждает сборку C#, не игровое исполнение.

## Manifest собственного scope

Пути от `LostCyberHamster/Assets/`:

- `Scripts/GameManagement/PlayerProgress/PlayerData.cs`
- `Scripts/GameManagement/PlayerProgress/LevelUpReward.cs` и Unity-generated `.meta`
- `Scripts/GameManagement/PlayerProgress/PlayerExperienceService.cs`
- `Scripts/GameManagement/PlayerProgress/ExperienceGrantResult.cs`
- `Scripts/GameManagement/Persistence/PlayerDataValidator.cs`
- `Scripts/SharedCore/Meta/CharacterDevelopment/CharacterDevelopmentService.cs`
- `Scripts/SharedCore/Meta/Skins/SkinManager.cs`
- `Scripts/SharedCore/Meta/SuperAttacks/SuperAttackService.cs`
- `Scripts/UI/Notifications/PlayerLevelPresentation.cs`
- `Scripts/UI/Modals/LevelUpModalController.cs`
- `Scripts/UI/Modals/JourneyCompleteModalController.cs`
- `Scripts/UI/NextGoals/StageNextGoalRule.cs`
- `Scripts/UI/NextGoals/NextGoalCoordinator.cs`
- `Scripts/GameEngine/Mechanics/UiJourneyCompleteModalMechanics.cs`
- `Content/localization/lang.ru.json`, `lang.en.json`
- `Content/ui/styles/components/journey-complete-modal.uss`

Дополнительно: один штатный `Compile Include` в `LostCyberHamster/Assembly-CSharp.csproj`; `docs/products/economics/README.md`, `long_term_plan.md`, этот отчёт, `research/18_long_term_minimum.md`.

## Доставка и границы

Владельцы блоков 1/2 передали нужные файлы; блок 4 подтвердил отсутствие пересечения. Координатор согласовал узкие изменения каталогов, локализации и README. Все записи/компиляция/Git выполняются под `long-term-block5` lock.

Чужие изменения сохранены отдельно: docs/rules, docs/experience/tool_usage.md, .agents/, tools/ui_gallery/, docs/Planning, competition_plan.md, research/17_competition_current_state.md, Assets/EditorLogs. Собственный generated diff содержит только включение нового C#.

Git: реализация `74c5e6f1b7c848d3c650229e200052002eeeb48d` — `feat: добавить цели мастерства и награду после полного развития`. Обычный `git push origin integration/unity-live` успешен. `git ls-remote origin refs/heads/integration/unity-live` подтвердил тот же SHA. Эта запись доставки отправляется отдельным документационным commit; C# после успешного gate не менялся.

Ретроспектива: перед compile нового C# проверять наличие Unity-generated meta и фактического Compile Include. Regeneration сама не гарантирует импорт файла. Принцип проверки результата уже есть в базе опыта; новой записи не требуется.
