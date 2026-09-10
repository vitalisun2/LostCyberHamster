# Retention UI — интеграция принятых макетов

Владелец: `01a08aec-a032-7361-9734-fd39e41afa58`, Unity Lead `integration/unity-live`.
Разрешение пользователя: 10.09.2026, все текущие варианты приняты, разрешена нарезка и интеграция.

## Эталон

Галерея: `.temp/retention-ui-review/concepts-2026-09-10-v1/index.html`.
Принятые кадры: `0001` Home, `0004` победные дни, `0005` неделя, `0006` Quests,
`0008` Skills, `0010` Hero, `0012` улучшение; иконки `0014–0016`.
Home: логотип над Play по центру, цель слева, две активности справа.
Текст, числа, прогресс, цены и состояния задаются кодом из действующих сервисов.

## Владения

Пути относительно `LostCyberHamster/Assets/`:

- `Scripts/UI/Screens/{HomeScreen,ReturnActivitiesScreen,QuestsScreen,CharacterDevelopmentScreen,CharacterScreen}Controller.cs`.
- `Scripts/UI/ReturnActivities/HomeActivityPresenter.cs`.
- `Scripts/UI/Components/{NextGoalCardView,SuperAttackDescriptionFormatter}.cs`.
- Новое `Scripts/UI/Modals/AbilityUpgradeModalController.cs` — общее окно улучшения для Skills/Hero.
- `Content/ui/uxml/{HomeScreen,ReturnActivitiesScreen,QuestsScreen,CharacterScreen,AbilityUpgradeModal}.uxml`.
- Соответствующие `Content/ui/styles/screens/*.uss`; `Content/ui/styles/components/next-goal-card.uss`; новое `ability-upgrade.uss`.
- Новые спрайты `Content/ui/sprites/shared/retention/`, `shared/abilities/`, `quests/bubble_daily_rule.png`.
- `Content/localization/lang.ru.json`, `lang.en.json`: новые UI-ключи; согласованы с задачей монетизации.
- Импорт/Addressables затронутых ресурсов через Unity Editor; сгенерированные метаданные этого набора.
- Регистрация в `Scripts/Entry Points/MenuEntryPoint.cs`, `Scripts/UI/Common/ScreenEnum.cs`, Addressables `UI.asset`.
- Этот документ; подготовка и capture-планы в `.temp/retention-ui-implementation/`.

Другие текущие dirty-файлы экономики, монетизации, правил и EditorLogs принадлежат другим задачам.

## Шаги

1. Подготовить чистые спрайты, проверить alpha и 9-slice, переиспользовать готовые кнопки и фон.
2. Разложить Home и два самостоятельных окна активностей; сохранить Claim/ACK и очередь наград.
3. Разместить правило и бонус Quests по бокам; сохранить карточки, вкладки и пагинацию.
4. В Skills оставить название, уровень, статус и действие. В Hero — текущий эффект и действие.
5. Подключить единое окно сравнения текущего и следующего уровня; покупка после явного нажатия.
6. Basic review, regeneration, compile, стартовый экран и Console под Unity lock.
7. Реальные кадры через unity-ui-capture, независимый visual QA, галерея через image-gallery.

## Реализовано

- Home: цель слева, логотип и основные действия по центру, две самостоятельные активности справа. Скрытие цели сохраняет композицию.
- Победные дни: семь почтовых слотов, заработанные и полученные награды, выдача и подтверждение в одном окне.
- Неделя: два горизонтальных ряда с отдельными отметками побед и разных дней, срок и награда. Восстановление истории получает собственную область пояснения и действия.
- Quests: правило слева, бонус справа; карточки, вкладки и пагинация сохранены.
- Skills: название, римский уровень, статус и кнопка улучшения.
- Hero: общий значок способности, текущий эффект, улучшение и выбор экипировки.
- Улучшение: текстовое сравнение уровней, стоимость, остаток очков; покупка после подтверждения. Выбранная способность Hero сохраняется при обновлении.
- Три иконки перенесены в `shared/abilities` через AssetDatabase с сохранением GUID и Addressables-адресов.
- Семь элементов оформления нарезаны с alpha; общие ресурсы хранятся один раз. Надписи, числа и состояния выводятся кодом.

## Проверка

- Импорт через Unity Editor: Sprite Single, FullRect, sRGB, Clamp, Bilinear, mipmaps off, bundle `ui`.
- Basic review: проверены lifecycle иконки, сохранение выбора Hero, привязка покупки к ожидаемому уровню, Claim/ACK к снимку награды.
- `lch_project_regenerate_files` и `dotnet build Assembly-CSharp.csproj --no-restore`: 0 ошибок, 43 предупреждения.
- Итог: 7 кейсов, 15 состояний в 1920×1080 и 3 дополнительных кадра 2160×1080 (Home, Quests, неделя). Все 15 основных состояний прошли независимый visual QA. Профиль изолирован, данные и локальный журнал подготовлены согласованно, DEV-оверлей скрыт на время съёмки.
- Первый visual QA выявил обрезки сравнения, цены в Skills, перенос внутри названия Hero, контраст HUD и геометрию CTA Quests. Исправлены по реальным кадрам. Максимальный уровень показан компактным информационным окном.
- Alpha: светлый, тёмный и шахматный фон. Две новые 9-slice-оболочки: исходный, узкий, широкий размер; borders согласованы с USS. Артефакты: `asset-alpha-qa.png`, `slice-qa.png`.
- Маршруты Home/Quests, сравнение Skills/Hero, фиксация ожидаемого уровня и снимка награды проверены по изменённому execution path. Состояния покупки, награды и восстановления подготовлены в изолированном профиле. Сами операции покупки, Claim/ACK и восстановления оставлены ручной игровой приёмке.

Подготовка, manifest и SHA256: `.temp/retention-ui-implementation/`.
Финальные PNG также сохранены в `C:\Personal\ChatGpt\WorkOnScreens\Prepared for Unity Integration\`.
Общие Menu/RU-EN после публикации переданы задаче монетизации с сохранением этого diff.

Статус: интеграция завершена. Галерея: `.temp/retention-ui-implementation/gallery-final/index.html`.
Финальная компиляция: 0 ошибок / 43 предупреждения. Проверены старт Home и новые ошибки игровых UI-контроллеров.
Дополнительная широкая съёмка остановилась по таймауту после 3 кадров; повтор отменён ради сокращения работы. `capture-result.json`: `done=true`, `restored=true`; отдельная проверка Unity: `playing=false`, `backup=false`, `testing=false`. Lock освобождён, исходный пресет Game View восстановлен.
Собственные изменения оставлены в рабочем дереве. Чужие изменения экономики, монетизации и EditorLogs сохранены.

## Ретроспектива

- Перед нарезкой проверить реальный alpha-канал. Нарисованная шахматка в RGB требует выделения внешнего фона по контуру.
- До большой серии кадров зафиксировать разрешение Game View. Для UI-примеров синхронизировать модель и изолированный журнал; исходный envelope восстанавливать целиком.
- Между capture-сессиями lock захватывать атомарно. Публикацию запускать только после успешного захвата.
