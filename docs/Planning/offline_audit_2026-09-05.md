# Проверка игры без интернета — 5 сентября 2026

Обновление: после аудита начата реализация утверждённого плана. [Изменения и текущая приёмка](offline_implementation_2026-09-05.md). Ниже сохранены исходные результаты до исправлений.

**Вывод:** игра пока не готова к надёжному офлайн-режиму. Контент и основное сохранение локальные, но меню блокирует отсутствие сети. Есть риски потери учёта офлайн-прогресса при cloud sync, зависшего аккаунта и устаревших обработчиков рекламы.

Scope: запуск, контент, игровой прогресс, аккаунт, облако, конфликты, реклама, воскрешение, магазин, рейтинг, квесты, аналитика и диагностические отправки. Исправления игрового кода в эту задачу не входят.

Исходная точка: `integration/unity-live`, commit `a096e204db21ca6918ec216cdfd18f29271c05b4`. К завершению диагностики HEAD — `54cabd92d2c07c458d0128acc4b72667a8fc6648`: чужой commit изменил кнопку паузы и опыт инструментов; исследуемый код систем остался прежним. Unity `6000.2.6f2`; установленный CLI `1.0.0-beta.8`; Pipeline `0.5.0-exp.1`.

**Какой режим подходит нашей игре**

Рекомендация для одиночного раннера: основной цикл полностью доступен с первого запуска без сети. Интернет расширяет возможности: реклама, аккаунт, облако, рейтинг. Это предлагаемый продуктовый контракт, а не утверждение, что все мобильные игры работают одинаково.

Локальные данные должны сразу обслуживать игру; важные изменения сначала сохраняются на устройстве, затем отправляются с повторами и разрешением конфликтов. Такой подход описан в [Android offline-first architecture](https://developer.android.com/topic/architecture/data-layer/offline-first?hl=en).

| Система | Ожидаемое поведение без сети |
|---|---|
| Первый и повторный запуск | Меню, обучение и локальные уровни доступны. Сетевой вход выполняется отдельно от обязательной загрузки. |
| Забег, звук, настройки, скины, прокачка | Работают на локальных данных. Отсутствие сети не прерывает забег. |
| Сохранения | Прогресс фиксируется локально на значимых событиях и при уходе в фон. Для известного владельца сохраняется признак неотправленных изменений. |
| Облако | Показывает «Сохранено на устройстве / ожидает синхронизации». После связи восстанавливает ту же identity и отправляет очередь. Конфликт сохраняет обе версии. |
| Конфликт | Можно отложить решение и продолжить с локальной версией. Принудительная облачная запись требует успешного сетевого завершения. |
| Реклама | При недоступном ролике понятный отказ и возможность продолжить обычную игру. Награда — только за подтверждённый просмотр, ровно один раз. |
| Уже загруженная реклама | Возможность показа определяет конкретный SDK. Потеря сети сама по себе не является основанием ни выдать награду, ни отнять уже подтверждённую. |
| Магазин | Обмен локальных ресурсов доступен; рекламные предложения отражают готовность видео. Результат покупки сразу сохраняется. |
| Рейтинг | Экран быстро открывается с состоянием недоступности/кэшем и выходом назад. Правило отправки офлайн-рекордов явно определено. |
| Дневные квесты | Текущий набор и прогресс доступны; перевод часов назад не повторяет уже использованный день. |
| Аналитика и логи | Фоновые отправки с ограниченным буфером и временем ожидания; игровой цикл продолжается. |

Unity показывает rewarded-кнопку после загрузки ролика и выдаёт награду через callback завершённого просмотра: [rewarded ads](https://docs.unity.com/en-us/grow/ads/unity-sdk/rewarded-ads). Ошибка загрузки бывает как из-за сети, так и из-за отсутствия рекламного предложения: [load errors](https://docs.unity.com/en-us/ads-unity/4.20.0/sdk-integration/troubleshoot/load-errors). Пользовательское сообщение должно различать эти случаи, когда SDK позволяет.

`Application.internetReachability` сообщает тип доступного подключения, а не проверяет доступность серверов: Wi-Fi может существовать без интернета. Это прямо оговорено в [Unity API](https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Application-internetReachability.html). Состояния аккаунта, облака и рекламы определяются также результатами их собственных запросов.

**Что сейчас реализовано**

| Система | Факт текущего кода | Оценка |
|---|---|---|
| Bootstrap | Последовательно ждёт UGS init. В проверенных Core/Auth/Analytics initializer сетевой gate не найден; init использует локальную конфигурацию. | Офлайн-зависание UGS не доказано. Общий catch/recovery полезен как защита загрузки. |
| Меню | Активный `LicenseManager` каждые 3 секунды проверяет reachability и при `NotReachable` добавляет полноэкранный overlay. | P1: прямое препятствие офлайн-доступу. |
| Контент | Все 23 bundled Addressables-группы включены в build, используют Local.BuildPath/Local.LoadPath; remote catalog выключен. | Конфигурация подходит для офлайна с первого запуска. Состав конкретного APK отдельно не проверен. |
| Локальное сохранение | Primary, backup, валидация, `PlayerPrefs.Save()`; сетевых вызовов нет. Commit сначала сохраняет локально, затем уведомляет cloud. | Хорошая основа. Перезапуск Android и force-stop ещё требуют проверки. |
| Потеря сети после входа | Linked-аккаунт создаёт durable pending по владельцу. Snapshot остаётся до ACK; write lock и проверки lifecycle защищают отправку. | Защищено лучше, чем cold-start offline. |
| Запуск сразу offline | Авторизация получает Error; checkpoint не создаёт новый cloud pending. | P1: недостающий/устаревший pending и риск замены локального прогресса. |
| Восстановление связи | Нет самостоятельного auth retry; cloud повторяется по checkpoint/menu/resume, а не по возврату сети. | P1/P2: сервисы могут остаться недоступными. |
| Реклама | После меню запускается асинхронный init. SDK `Advertisement Legacy 4.4.2`, всегда `testMode:true`. | Меню не ждёт рекламу; recovery и UI состояния неполны. |
| Rewarded магазин | Ошибка/пропуск снимают pending и не выдают награду; success выдаёт награду. | Базовый terminal callback обработан правильно. |
| Revive | Каждый клик добавляет глобальный success-listener; отказ и уход с экрана его не снимают. | P1: поздняя реклама может вызвать старое воскрешение. |
| Обычный магазин | 50 монет за rewarded; 1 кристалл за 500 монет. Billing/IAP flow и Unity Purchasing dependency не найдены. | Локальный обмен доступен. Immediate checkpoint отсутствует. |
| Рейтинг | Ошибки загрузки перехватываются; есть Retry. Отправка результата не имеет долговременной очереди. | Потеря попытки отправки; первая загрузка задерживает навигацию. |
| Дневные квесты | Локальное время; смена даты в любую сторону допускает генерацию набора. | Доступны offline; нужен контроль возврата даты. |
| Аналитика | SDK буферизует события; проектный сбор стартует отдельно. | Фоновая возможность; доставка всех событий не гарантируется. |
| Device logs | Корутины, timeout 10 секунд на запрос; очередь причин в памяти. | Игру не блокируют; офлайн-отправки не имеют durable retry. |

Источники конфигурации: [LostCyberHamster/Packages/manifest.json](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Packages/manifest.json>); [LostCyberHamster/Assets/AddressableAssetsData/AddressableAssetSettings.asset:20,94–118](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/AddressableAssetsData/AddressableAssetSettings.asset:20>); схемы в `AssetGroups/Schemas`; [LostCyberHamster/Assets/Content/shop/shopItems.json](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/shop/shopItems.json>).

Unity Analytics описывает память, пакетную отправку и дисковый кэш до 5 MB при доступной файловой системе: [SDK behavior](https://docs.unity.com/en-us/analytics/sdks-and-apis/sdk-behaviour). Это свойство SDK, а не выполненный в этой задаче тест доставки.

**Проблемы и порядок исправления**

Приоритеты: P1 — исправить до обещания полноценного офлайна; P2 — надёжность, обратная связь и продуктовые правила.

**1. P1 — меню требует интернет.**

Сценарий: открыть меню на телефоне с выключенными Wi-Fi и мобильными данными. В [Menu.unity:435–450](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scenes/Menu.unity:435>) объект `[LICENSE]` активен, компонент включён. [LicenseManager.cs:26–50](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/SharedCore/LicenseManager.cs:26>) вызывает `ShowFullScreenView` при `NotReachable`; метод `:78–126` добавляет элемент шириной и высотой 100%, без действия продолжения. Стиль — [runtime-states.uss:63](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/styles/components/runtime-states.uss:63>).

Рекомендация: убрать сетевой запрет из обычного игрового меню. Назначение тестовой лицензии оформить отдельной явной политикой для соответствующих сборок. Офлайн-статус показывать рядом с зависимыми функциями.

Источник: [LostCyberHamster/Assets/Scripts/SharedCore/LicenseManager.cs](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/SharedCore/LicenseManager.cs>); [LostCyberHamster/Assets/Scenes/Menu.unity](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scenes/Menu.unity>).

**2. P1 — офлайн-прогресс может быть принят за неизменённый.**

Сценарий: связанный аккаунт синхронизирован; затем холодный запуск без сети, новый прогресс, закрытие, запуск online.

- [AccountService.cs:525–545](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/Account/AccountService.cs:525>): сетевой вход завершается Error.
- [AccountService.cs:64](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/Account/AccountService.cs:64>): `TryGetLinkedPlayerId` требует Linked.
- [CloudSyncService.cs:169–172](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/CloudSave/CloudSyncService.cs:169>): локальный checkpoint при Error/Resolving не создаёт pending.
- [CloudSyncService.cs:462–481](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/CloudSave/CloudSyncService.cs:462>): при пустом pending и прежней revision возвращает Synchronized; новый локальный прогресс ещё не отправлен. При новой cloud revision возвращает CloudChanged.
- [CloudSyncService.cs:388–394,486–495](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/CloudSave/CloudSyncService.cs:388>): CloudChanged может целиком заменить локальные данные. Конфликт не возникает, поскольку missing pending скрывает локальные изменения.

Наличие pending предыдущей сессии тоже требует внимания: новые offline-данные могут сохраниться локально, а pending останется старым. Та же щель существует до завершения авторизации при обычном медленном запуске.

Рекомендация: хранить локального владельца, base revision и durable dirty marker независимо от текущей сетевой авторизации. Перед cloud comparison восстановить pending последнего checkpoint именно этого владельца. Отдельно обработать локального гостя и смену аккаунта. Сохранить действующие write locks, точный ACK и проверки lifecycle.

Источники: [LostCyberHamster/Assets/Scripts/Account/AccountService.cs](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/Account/AccountService.cs>); [LostCyberHamster/Assets/Scripts/CloudSave/CloudSyncService.cs](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/CloudSave/CloudSyncService.cs>). Назначение write locks подтверждено [Unity Cloud Save](https://docs.unity.com/zh-cn/cloud-save/concepts/write-locks).

**3. P1 — аккаунт не возвращается после ошибки или истечения токена.**

`AccountService.Start():235–247` работает только из NotStarted; Error повторно не запускается. Settings разрешает привязку для Guest, но не даёт повторить определение Error. Resume запускает cloud sync, а не авторизацию.

Отдельный сценарий: долго играть offline после успешного входа. В embedded Auth SDK `IsSignedIn` включает Expired, но `IsAuthorized` уже false (`AuthenticationServiceInternal.cs:26–35`). `Expire():451–456` очищает access token и отменяет refresh. `RefreshAccessTokenAsync():256–258` из Expired ничего не делает. Для восстановления требуется новый `SignInAnonymouslyAsync` с сохранённым session token (`:117–135`). Проектный gateway не передаёт Expired в AccountService.

Рекомендация: lifecycle авторизации с ручным retry, повтором на reconnect/resume, backoff и обработкой Expired. Восстанавливать прежнюю identity. Пока сеть недоступна, игра использует локальный профиль.

Источник: [LostCyberHamster/Packages/com.unity.services.authentication/Runtime/AuthenticationServiceInternal.cs](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Packages/com.unity.services.authentication/Runtime/AuthenticationServiceInternal.cs>); [LostCyberHamster/Assets/Scripts/Account/UnityAccountAuthenticationGateway.cs:13–31](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/Account/UnityAccountAuthenticationGateway.cs:13>); [Unity session management](https://docs.unity.com/zh-cn/authentication/session-management).

**4. P1 — реклама воскрешения оставляет чужие callbacks.**

Сценарий: несколько неудачных попыток revive; выход или restart; позже успешная реклама магазина. [UiLoseModalMechanics.cs:43](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/GameEngine/Mechanics/UiLoseModalMechanics.cs:43>) добавляет success-listener при каждом клике; снимает только в success (`:49`). Error/cancel отправляют `AdFinished(false)`, который revive не слушает. Выход и restart подписку сохраняют.

Глобальные события не содержат идентификатора запроса/получателя. [AdsManager.cs:84–87](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/Ads/AdsManager.cs:84>) сначала вызывает `AdCompleted`, затем `AdFinished(true)`. Исключение старого revive-подписчика может прервать магазинный terminal result. Конкретное изменение старого персонажа требует полного сценового воспроизведения; дефект жизненного цикла подписок виден напрямую.

Рекомендация: одна активная rewarded-операция с владельцем, request ID и единственным конечным результатом. Revive реагирует на success только своего текущего запроса; очищается на failure, cancel, timeout, закрытии и смене забега. Поздние и повторные callbacks учитываются безопасно.

Источники: [LostCyberHamster/Assets/Scripts/GameEngine/Mechanics/UiLoseModalMechanics.cs:29–54](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/GameEngine/Mechanics/UiLoseModalMechanics.cs:29>); [LostCyberHamster/Assets/Scripts/Ads/AdsManager.cs:84–98](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/Ads/AdsManager.cs:84>); [LostCyberHamster/Assets/Scripts/GameEngine/GameEventsManager.cs:16–24](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/GameEngine/GameEventsManager.cs:16>).

**5. P1 — конфликт облака не даёт продолжить при потере сети.**

Сценарий: конфликт уже открыт, сеть исчезла до выбора. Закрытие скрыто ([CloudSaveConflictModalController.cs:44](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/UI/Modals/CloudSaveConflict/CloudSaveConflictModalController.cs:44>)). Оба варианта, включая «устройство», сначала читают cloud ([ConflictService.cs:98,162](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/CloudSave/ConflictService.cs:98>)). Ошибка возвращает false; coordinator оставляет модаль ([CloudSaveConflictCoordinator.cs:168–182](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/UI/Modals/CloudSaveConflict/CloudSaveConflictCoordinator.cs:168>)).

Рекомендация: отложенное решение с продолжением локальной игры. Сохранить конфликт, последние локальные изменения и версии; завершить согласование после связи. Ошибку показывать в самой модали.

Источники: [LostCyberHamster/Assets/Scripts/CloudSave/ConflictService.cs](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/CloudSave/ConflictService.cs>); [LostCyberHamster/Assets/Scripts/UI/Modals/CloudSaveConflict/](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/UI/Modals/CloudSaveConflict/>).

**6. P2 — сетевые функции молча не восстанавливаются.**

- Cloud retry запускают checkpoint, menu, resume, account и разрешение конфликта. После ошибки — только лог, без таймера/backoff ([CloudSyncService.cs:55–59,259–321](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/CloudSave/CloudSyncService.cs:55>)). Возврат сети при открытом экране не даёт новой попытки.
- Cloud Status может снова стать Saved после неудачного чтения, когда pending пуст ([CloudSyncService.cs:65–76](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/CloudSave/CloudSyncService.cs:65>)). Он не отражает свежесть подтверждения облака.
- Ads init failure сохраняет `_isInitialized=false`; повторный клик только завершается failure ([AdsInitializationListener.cs:15–18](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/Ads/AdsInitializationListener.cs:15>), [AdsManager.cs:54–62](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/Ads/AdsManager.cs:54>)). Повтор init сейчас возможен при новом входе в Menu, но не по восстановлению сети в текущем меню.
- Рекламные кнопки не отражают готовность ролика; магазин освобождает pending только по callback. Собственного timeout для потерянного callback нет ([ShopManager.cs:55–80](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/SharedCore/Meta/Shop/ShopManager.cs:55>)).

Рекомендация: общая координация повторов и отдельные состояния сервисов; в UI — «на устройстве», «ожидает отправки», «последний cloud sync», «видео недоступно», «загрузка». По одному активному запросу; тайм-ауты отделять от поздних SDK callbacks.

**7. P2 — магазин не фиксирует награду сразу.**

[ShopManager.cs:36–49,82–85](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/SharedCore/Meta/Shop/ShopManager.cs:36>) меняет ресурсы и вызывает ItemBought. Production-подписчики этого события не найдены; ResourceManager обновляет память и UI. Покупка/наградной ролик остаются без немедленного checkpoint. Краш или force-stop до следующего checkpoint может потерять результат. Обычный уход в фон сохраняет через `PlayerProgressLifecycleCheckpoint`.

Рекомендация: общий checkpoint успешной покупки после списания и выдачи, включая rewarded, с защитой от повторного применения результата.

Источники: [LostCyberHamster/Assets/Scripts/SharedCore/Meta/Shop/ShopManager.cs](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/SharedCore/Meta/Shop/ShopManager.cs>); [LostCyberHamster/Assets/Scripts/SharedCore/Meta/Storages/ResourceManager.cs:35–65](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/SharedCore/Meta/Storages/ResourceManager.cs:35>).

**8. P2 — офлайн-рекорд и weekly XP требуют явного правила.**

[PartOfDayScoreMechanics.cs:82–135](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/GameEngine/Mechanics/PartOfDayScoreMechanics.cs:82>) отправляет результат без долговременной очереди; при сбое пишет Failed. Weekly XP выдаётся после подтверждённого сервером нового рекорда. Офлайн-попытка автоматически позже не отправляется. Это потеря сетевой возможности; считать её продуктовым багом следует после выбора политики weekly.

Первое открытие рейтинга держит общий UI transition gate до сетевого результата: [UIManager.cs:67–102](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/UI/Common/UIManager.cs:67>), [ScreenController.cs:76–77](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/UI/Common/ScreenController.cs:76>), [LeaderboardScreenController.cs:153,195,271](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/UI/Screens/LeaderboardScreenController.cs:153>). SDK содержит timeout 10 секунд на HTTP-запрос; бесконечное ожидание не доказано, но навигация задерживается.

Рекомендация: сразу показывать оболочку экрана с рабочим выходом; данные загружать отдельно. Для weekly выбрать и показать игроку правило: участие online либо очередь проверяемых результатов с сезоном, защитой от повторов и ограничением срока отправки. Награду за серверный рекорд подтверждать однократно.

Источник: [LostCyberHamster/Assets/Scripts/GameEngine/Mechanics/PartOfDayScoreMechanics.cs](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/GameEngine/Mechanics/PartOfDayScoreMechanics.cs>); [LostCyberHamster/Assets/Scripts/UI/Screens/LeaderboardScreenController.cs](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/UI/Screens/LeaderboardScreenController.cs>).

**9. P2 — перевод часов назад обновляет дневной набор.**

[DailyQuestScheduler.cs:17–25](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/SharedCore/Meta/Quests/Daily/DailyQuestScheduler.cs:17>) сравнивает даты на неравенство. При сохранённой дате 2026-09-05 значение 2026-09-04 тоже требует генерации. Повторная инициализация может заменить набор; фактический повтор наград отдельно не воспроизведён.

Рекомендация: хранить последний использованный день и обработать возврат часов; определить timezone и поведение после долгого offline. Уже полученные награды связывать с устойчивым идентификатором набора.

Источник: [LostCyberHamster/Assets/Scripts/SharedCore/Meta/Quests/Daily/DailyQuestScheduler.cs](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/SharedCore/Meta/Quests/Daily/DailyQuestScheduler.cs>); [QuestManager.cs:112,478](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/SharedCore/Meta/Quests/Runtime/QuestManager.cs:112>).

**10. P2 — диагностика offline не гарантирует последующую доставку.**

[DeviceLogUploadRunner.cs:59–69](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/Diagnostics/DeviceLogUploadRunner.cs:59>) хранит причины только в памяти и удаляет перед попыткой. Uploader после ошибки завершает корутину; requeue отсутствует. Новый trigger может позднее отправить сохранившийся локальный лог, но возврат сети сам не обеспечивает доставку. Очередь причин не ограничена; частые ошибки создают избыточные попытки. Игровой запуск этими отправками не ожидается.

Рекомендация для QA: ограниченная durable очередь последних snapshots, объединение повторных причин и retry после связи. Для проверки офлайна сохранять локальный результат независимо от upload.

Источник: [LostCyberHamster/Assets/Scripts/Diagnostics/DeviceLogUploadRunner.cs](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/Diagnostics/DeviceLogUploadRunner.cs>); [DeviceLogUploader.cs:37–46,173–201](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/Diagnostics/DeviceLogUploader.cs:37>); [DeviceLogReporter.cs:59–78](</C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/Diagnostics/DeviceLogReporter.cs:59>).

**Границы доказательств и проверки**

Статический аудит охватил код, сцены, Addressables, подключённые SDK, существующие тесты и официальные документы. Дополнительно выполнены две изолированные диагностики текущего скомпилированного кода через Unity Pipeline MCP `eval`, 11:13–11:15 UTC. Ошибки задавались существующим fake аккаунта и синтетическим результатом рекламы; реальные SDK-запросы в этих проверках не выполнялись.

| Выполненная диагностика | Полученный результат | Что доказывает |
|---|---|---|
| AccountService: ошибка входа; fake снова успешен; повторный Start | Error, повторно Error; всего 1 вызов sign-in | Повторный Start не восстанавливает аккаунт после ошибки. |
| CloudSyncService.GetSyncState: pending отсутствует | Прежняя revision: Synchronized; новая: CloudChanged | Без pending классификация не распознаёт локальные изменения. Само облачное перезаписывание профиля не выполнялось. |
| Тот же classifier: pending есть и cloud revision новая | Conflict | Контрольная ветка конфликта работает. |
| LicenseManager.ShowFullScreenView с offline-сообщением | Один overlay 100% × 100%, Absolute, PickingMode.Position, только две текстовые метки | Полноэкранная структура подтверждена. Сеть и физический ввод телефона не проверялись. |
| UiLoseModalMechanics: три запроса с AdFinished(false) | Success-listeners после попыток: 1, 2, 3 | Обработчики revive сохраняются и накапливаются после отказа. |
| ShopManager: рекламная покупка с AdFinished(false) | Pending=false; товар очищен; terminal-listeners=0 | Обычный отказ освобождает ожидание магазина без выдачи награды. |
| DailyQuestScheduler: прежний, текущий и следующий день | Предыдущий=true; текущий=false; следующий=true | Возврат даты тоже требует нового набора. |

Сырые результаты: [JSON диагностики](/C:/Personal/crystal-wave/repos/LostCyberHamster_2025/docs/Planning/offline_audit_2026-09-05_evidence.json). После проб временная сцена закрыта, исходные события и состояние магазина восстановлены; активна Bootstrap, dirty=false, Play stopped. Профиль и облако этими пробами не менялись.

CLI discovery работал; выполнение команды через CLI beta.8 потребовало более новый Pipeline. Диагностика выполнена доступным совместимым MCP transport; версии проекта не менялись.

Полного Android-прогона с авиарежимом в этой проверке нет. Сеть компьютера не отключалась; опубликованный APK не собирался. Поэтому время реального запуска, поведение нативного рекламного кэша, kill/relaunch, долгий offline и переходы между сетями остаются непроверенными на устройстве.

Editor не заменяет Android: в Ads 4.4.2 `EditorPlatform.Load` локально отмечает placement загруженным после init, а Show открывает placeholder. Положительный результат такого показа не доказывает работу рекламы без сети на телефоне.

Существующие backup/account тесты прочитаны, без запуска Test Runner. `CloudSaveE2ERunner.DeferredSynchronization` требует ручного отключения сети и при продолжении сам вызывает resume; он проверяет resume retry, а не самостоятельный reconnect. Некоторые другие E2E-сценарии меняют реальные account/cloud данные и делают reset/unlink.

План `docs/Planning/in-progress/cloud-sync.md` описывает reconnect/backoff как целевое поведение; текущий код его полностью не реализует. `cloud-save-refactoring.md` содержит отсутствующие сейчас типы. Реализованное поведение определялось по текущему коду.

**Приёмочная матрица Android**

Все строки ниже — план проверки, не список уже пройденных тестов. Профиль и cloud-аккаунт — отдельные QA-данные. Для сравнения сохранить build SHA, сессию и локальные snapshots до/после.

| Сценарий | Критерий |
|---|---|
| Чистая установка; Wi-Fi/mobile off; первый запуск | Меню, обучение и забег доступны; сетевой init не задерживает обязательную загрузку. |
| Ранее связанный профиль; cold-start offline | Локальные данные доступны; каждый checkpoint сохраняет owner/dirty/pending. |
| Забег, настройки, скин, прокачка, награда квеста offline; фон/kill/relaunch | Подтверждённые изменения сохранены; валидный backup восстанавливается при повреждении primary. |
| Cold-start offline; новый прогресс; online restart; cloud revision прежняя | Последний локальный checkpoint отправляется. |
| То же; cloud изменён на втором устройстве | Обе версии сохранены; видимый конфликт вместо тихой замены. |
| Уже linked; сеть пропала; checkpoint; restart | Pending переживает закрытие и отправляется после восстановления identity. |
| Офлайн дольше срока access token; reconnect/resume | Та же identity повторно авторизуется; cloud/rating доступны без перезапуска. |
| Wi-Fi есть, внешнего интернета нет; DNS/timeout/сервер недоступен | UI остаётся отзывчивым; запрос имеет ограничение времени и понятный результат. |
| Открыт конфликт; сеть исчезла | Можно продолжить локально; обе версии и pending сохранены. |
| Ads init offline; сеть вернулась в том же меню | Доступность ролика восстанавливается без перезапуска/смены сцены. |
| Реклама: loss при load/show, cancel, фон/resume, поздний и повторный callback | Один итог; награда ровно за подтверждённый запрос; pending освобождён. |
| Несколько failed revive; exit/restart; успешная реклама магазина | Старое воскрешение не срабатывает; награда только выбранного предложения. |
| Награда магазина/обмен; немедленный force-stop | Завершённый результат сохранён однократно. |
| Offline leaderboard; возврат назад; reconnect | Выход работает во время запроса; выбранная политика weekly соблюдается. |
| Следующий день offline; часы назад/вперёд; timezone | Набор обновляется по принятому правилу, уже выданные награды не повторяются. |
| Длинная offline-сессия; затем reconnect | Лог/аналитика не тормозят игру; буферы ограничены; восстановление сети не создаёт лавину запросов. |

**Рекомендуемый порядок работ:** сначала пункты 1–5; затем lifecycle/retry и немедленное сохранение магазина; после этого weekly, часы и QA-доставка. Завершить полным Android-прогоном по матрице, привязанным к конкретному APK.

**Результат работы в репозитории**

Созданы этот отчёт, JSON фактических результатов и [памятка проверки offline](/C:/Personal/crystal-wave/repos/LostCyberHamster_2025/docs/experience/offline_validation.md); в [карте опыта](/C:/Personal/crystal-wave/repos/LostCyberHamster_2025/docs/experience/README.md) добавлена ссылка. Игровые исходники, сцены, assets, настройки SDK, локальный профиль и cloud не изменялись. Коммит этой задачи не создавался.

Проверены структура JSON, локальные ссылки отчёта, отсутствие незавершённых пометок и scoped diff. Сборка и C# regeneration для документации не требуются. Чужие изменения `error_diagnosis.md`, `existing_systems.md` и отчёты других задач сохранены.
