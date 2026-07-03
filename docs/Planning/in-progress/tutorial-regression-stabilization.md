# Tutorial regression stabilization

Дата: 2026-07-02

## Цель

Стабилизировать полный tutorial flow:

1. Tutorial Level: уроки 1-8.
2. Переход в меню и покупка/надевание скина молнии через обычный UI.
3. Tutorial Level 2: урок суперудара.
4. Финальное окно завершения.

Build APK делать только после полного успешного прогона в Unity Editor.

## Рабочий протокол

- Не делать смысловую правку без записи доказанного корня проблемы.
- Каждую гипотезу фиксировать отдельно: факты, проверка, статус.
- Временные логи разрешены только для доказательства; перед финалом удалить временную диагностику.
- Tutorial engine должен использовать существующие runtime/UI/loading механизмы и не требовать знания tutorial со стороны обычного UI.

## Проверенные факты

### F1. Tutorial sandbox перед skin lesson стартует из default state

Лог:

```text
[TUTORIAL SANDBOX] prepared skin purchase crystals=20
[TUTORIAL UI] skin lesson started targetSkin=2 trainingCrystals=20 skinOrder=0,2,1 currentSkin=0 targetIndex=1
```

Вывод: проблема перехода к финальному уроку не объясняется уже купленным/надетым скином.

### F2. Ранее был найден отдельный стоп на CharacterScreen background load

До фикса лог останавливался на:

```text
[UI] background load start asset=BackgroundScreenSprite
```

После удаления `ConfigureAwait(false)` из `AddressableLoader` тот же участок проходит:

```text
[UI] background load done asset=BackgroundScreenSprite spriteNull=False
```

Вывод: использование `ConfigureAwait(false)` в общем Addressables loader было небезопасно для Unity main-thread continuation.

### F6. Проверка 18:00 снова остановилась на CharacterScreen background load

После правок sandbox reset и deferred scene transition новый прогон не дошел до H4.

Лог остановился на:

```text
[UI] background load start asset=BackgroundScreenSprite
```

Нет следующего события:

```text
[UI] background load done asset=BackgroundScreenSprite ...
```

Вывод: H1 доказала симптом и частичный вклад `ConfigureAwait(false)`, но решение неполное. Нельзя считать Addressables/background load закрытым.

### F3. Текущий стоп происходит после завершения skin lesson и загрузки второго Game scene

Лог 2026-07-02 17:52:

```text
[TUTORIAL UI] skin lesson completed appliedSkin=2
[TUTORIAL UI] loading super hit lesson level=01_New_York/Morning/Tutorial Level 2
[LOADING] GameEntryPoint construct scene=Game bundleKeys=5
[LOADING] GameEntryPoint awake scene=Game active=True enabled=True
[LOADING] GameEntryPoint on_enable scene=Game active=True enabled=True
```

После этого нет:

```text
[LOADING] GameEntryPoint start ...
```

Скрин Unity Game view после стопа: черный экран. Unity Editor responsive, исключений в `Editor.log` нет.

### F4. GameEntryPoint не уничтожается до Start

Добавленные lifecycle-логи показывают `Construct/Awake/OnEnable` второго `GameEntryPoint`.
`OnDisable/OnDestroy` после второго `OnEnable` не приходит.

Вывод: гипотеза "объект уничтожен до Start" не доказана.

### F5. LevelController не висит на том же объекте, что GameEntryPoint

YAML `Assets/Scenes/Game.unity`:

- `GameEntryPoint` находится на `[ENTRY_POINT]`.
- `LevelController` находится в `Bootstrap.unity` и живет через `DontDestroyOnLoad`.

Вывод: гипотеза "LevelController duplicate destroy уничтожает GameEntryPoint" отклонена.

### F7. Прогон 18:03 прошел UI skin lesson и запустил второй Game scene

После переноса synthetic click в scheduled callback HomeScreen и CharacterScreen загрузились полностью:

```text
[UI] background load done asset=HomeScreenSprite spriteNull=False
[UI] home character click
[UI] background load done asset=BackgroundScreenSprite spriteNull=False
[TUTORIAL UI] skin lesson completed appliedSkin=2
[TUTORIAL UI] scheduling super hit lesson scene transition
[TUTORIAL UI] loading super hit lesson level=01_New_York/Morning/Tutorial Level 2
[LOADING] GameEntryPoint start scene=Game ...
```

Новый стоп:

```text
[LOADING] start task='Инициализация текущего уровня' type='InitCurrentLevelAssetsLoadingTask'
```

В `Editor.log` дополнительно видно, что intro-спрайты для `Tutorial Level 2` не найдены, но это не fatal: после этого pipeline переходит к `InitCurrentLevelAssetsLoadingTask`.

Вывод: корень текущего стопа находится глубже загрузки текущего уровня, внутри `LevelDataProvider.LoadLevelData()` или вызванных им операций.

## Review gate: изменения вне Tutorial

Все изменения вне tutorial/UI automation считаются подозрительными до отдельного доказательства.

- `AddressableLoader`: контрольный эксперимент 18:10 показал, что с исходным `ConfigureAwait(false)` flow снова зависает на `BackgroundScreenSprite`, уже после исправления H6. Повтор 18:12 без `ConfigureAwait(false)` тоже завис на `BackgroundScreenSprite`, значит это не полный root cause. Следующая проверка: handle-level диагностика самого Addressables load.
- Временные lifecycle/UI логи в `GameEntryPoint`, `UIManager`, `ScreenController`, `HomeScreenController`, `CharacterScreenController`, `LevelDataProvider` допустимы только для доказательства причины. Перед финальным билдом их нужно убрать или заменить на устойчивую диагностическую инфраструктуру, если она действительно нужна.
- Нельзя оставлять изменения в загрузчике/общем UI только потому, что они маскировали симптом туториала.

## Гипотезы

### H1. Небезопасный `ConfigureAwait(false)` в AddressableLoader

Статус: влияет на симптом, но не является полным root cause.

Доказательства: F2, F6.

Решение-кандидат 1: убрать `ConfigureAwait(false)` в `AddressableLoader` для Addressables await.

Результат: один прогон прошел дальше, следующий снова завис на `BackgroundScreenSprite`. После H6 был проведен контрольный откат `AddressableLoader` к исходному виду: flow снова завис на `BackgroundScreenSprite`, хотя synthetic click уже был отложен. После повторного удаления `ConfigureAwait(false)` зависание на `BackgroundScreenSprite` воспроизвелось снова.

Вывод: нельзя считать `AddressableLoader` fix достаточным. Нужно доказать, что именно происходит на уровне `AsyncOperationHandle`: создается ли handle, вызывается ли `Completed`, завершается ли `await handle.Task`.

### H2. Сбой из-за сохраненного состояния скина

Статус: отклонено.

Доказательства: F1.

### H3. Повторный LevelController уничтожает GameEntryPoint

Статус: отклонено.

Доказательства: F4, F5.

### H4. `SceneManager.LoadScene("Game")` вызывается внутри UI Toolkit click-event / synthetic click

Статус: частично подтверждено и закрыто для перехода в Game scene.

Факты:

- `TutorialUiRuntime.OnSkinLessonCompleted()` сразу вызывает `StartSuperHitGameplayLesson()`.
- `StartSuperHitGameplayLesson()` сразу делает `SceneManager.LoadScene("Game")`.
- В automated run `TutorialUiAutomationDriver` отправляет synthetic `ClickEvent` через `target.SendEvent(clickEvent)`.
- До правки стоп происходил сразу после этого перехода: второй `GameEntryPoint` получал `Construct/Awake/OnEnable`, но до `Start` не доходил.
- После deferred transition второй `GameEntryPoint.Start` выполняется, значит переход через границу UI event действительно был нужен.

Проверка:

- Перенести request на загрузку финального gameplay-урока за границу текущего UI event dispatch.
- Делать это не как локальную задержку, а как отдельную ответственность tutorial runtime: "запросить переход после завершения текущего UI кадра".
- После проверки ожидать в логе:

```text
[LOADING] GameEntryPoint start scene=Game ...
[TUTORIAL SANDBOX] prepared super hit skin=2
[TUTORIAL] super hit lesson started skin=2 charge=100
```

### H5. Состояние tutorial sandbox не идемпотентно при повторном запуске

Статус: подтверждено как архитектурный дефект, не как текущий root cause стопа.

Факт: `TutorialSandboxState.Begin()` сейчас делает early return, если `IsActive == true`.

Риск: повторный прогон туториала может не вернуть money/crystals/current skin в tutorial default, если предыдущий прогон оборвался до `RestoreRealState()`.

Решение-кандидат:

- Сделать явные методы подготовки состояния:
  - core lesson: default skin, default purchased skins, zero resources.
  - skin purchase lesson: default skin, default purchased skins, training crystals.
  - super hit lesson: electric skin equipped, default + electric purchased, zero resources.
- Каждый метод должен полностью переустанавливать sandbox state, без early return.

### H6. Automation driver отправляет synthetic click синхронно во время загрузки UI surface

Статус: подтверждено.

Факты:

- `TutorialUiRuntime.OnSurfaceLoaded()` вызывается сразу после `UIManager.LoadScreenAsync(...)`.
- Внутри `OnSurfaceLoaded()` сразу вызывается `RunAutomationActionIfNeeded()`.
- `TutorialUiAutomationDriver.DispatchClickIfEnabled()` сейчас делает `target.SendEvent(clickEvent)` синхронно.
- На HomeScreen это запускает переход в CharacterScreen до выхода из текущего UI load/surface-loaded call stack.
- Текущий стоп возникает на следующей Addressables-загрузке CharacterScreen background.

Гипотеза:

- Автоклик должен имитировать пользователя, то есть происходить после завершения текущего UI event/layout cycle, а не внутри call stack загрузки экрана.

Проверка:

- Перенести synthetic click в scheduled callback на target/root после текущего UI event.
- Успешный критерий: CharacterScreen background стабильно пишет `background load done`, после чего можно вернуться к проверке H4.

Результат 18:03: критерий выполнен, CharacterScreen загрузился, skin lesson завершился, второй Game scene дошел до `Start`.

### H7. Tutorial Level 2 зависает при загрузке текущего уровня

Статус: проверяется.

Факты:

- Второй `GameEntryPoint.Start` выполняется.
- Pipeline проходит intro task; отсутствие intro-спрайтов для tutorial level не fatal.
- Последний маркер перед таймаутом: `InitCurrentLevelAssetsLoadingTask`.
- `InitCurrentLevelAssetsLoadingTask.LoadAsync()` делегирует в `LevelController.Instance.LoadLevelData()`.

Гипотезы-кандидаты:

- зависает загрузка самого TextAsset уровня `01_New_York/Morning/Tutorial Level 2`;
- зависает загрузка `PatternsCollection`;
- зависает загрузка theme mapping `01_New_York/obstacle_sprite_to_type_mappings`;
- `LevelResolver.Resolve(...)` или последующие загрузки окружения/спрайтов не возвращаются из-за данных `Tutorial Level 2`/`tutorial_9`.

Следующая проверка: временно разметить `LevelDataProvider.LoadLevelData()` и `LoadLevelInfo(...)` маркерами `start/done` вокруг каждого await и resolver шага. Исправление делать только после точного последнего `start` без соответствующего `done`.

### H8. UIManager не деактивирует предыдущий экран перед загрузкой нового

Статус: не доказано, правку не принимать без новых фактов.

Факты:

- `ScreenController` хранит `_backgroundSpriteLease`, но сам background `VisualElement` общий для screen root.
- `UIManager.LoadScreenAsync(...)` до проверки не вызывал `UnsubscribeFromEvents()` у предыдущего screen controller перед загрузкой нового экрана.
- `ScreenController.UnsubscribeFromEvents()` освобождает background lease через `ReleaseBackgroundSprite()`.
- На зависшем прогоне `HomeScreenSprite` загрузился полностью, после чего `BackgroundScreenSprite` получил `located=True` и valid handle, но не получил `Completed`.

Гипотеза:

- Предыдущий экран остается активным с точки зрения ownership ресурсов/подписок, поэтому при переходе HomeScreen -> CharacterScreen новый background load запускается в некорректном lifecycle состоянии.

Проверка:

- `UIManager` должен перед загрузкой нового screen вызвать `UnsubscribeFromEvents()` на текущем screen controller, пока его UXML еще находится в root.
- `UIManager.SubscribeToEvents()` не должен подписывать все screen controllers заранее; конкретный screen подписывается после `LoadScreenAsync()`.
- Успешный критерий: после `[UI] deactivate screen=HomeScreen` должен появиться `asset completed/await done` для `BackgroundScreenSprite`.

Результат: изменение lifecycle `UIManager` не принято как root cause. Ручная навигация по этим экранам работает, а проблема проявляется именно в tutorial runtime/automation flow. Любые изменения `UIManager` вне событий наблюдения surface (`OnScreenLoaded`/`OnModalShown`) нужно пересмотреть и убрать, если они были сделаны только для лечения симптома.

### H9. Tutorial UI-flow завершает навигационный шаг по клику, а не по загрузке целевого surface

Статус: подтверждено как дефект flow-модели, но не полный root cause зависания.

Факты:

- WinModal -> HomeScreen: автоклик по `btn__home` запускает обычный UI-переход, после чего `HomeScreen` загружается полностью.
- HomeScreen -> CharacterScreen: автоклик по `btn_character` вызывает обычный обработчик `HomeScreenController.OnClickBtnCharacter()`.
- После клика tutorial runtime сразу вызывает `Notify(OpenCharacterScreen)` и переводит stage в `AwaitingSkinSelection`, хотя `CharacterScreen` еще грузится.
- Лог последнего прогона:

```text
[UI] home character click
[UI] show screen requested screen=CharacterScreen
[UI] load screen start screen=CharacterScreen
[UI] screen asset load start screen=CharacterScreen
[TUTORIAL UI] advanced stage=AwaitingSkinSelection
[TUTORIAL UI] waiting next surface currentSurface=HomeScreen action=SelectNextSkin
```

- После этого нет `screen asset load done screen=CharacterScreen`.

Вывод: для навигационных UI-шагов tutorial engine не должен завершать action по самому клику. Клик только разрешает штатному UI выполнить переход. Завершение шага должно происходить при событии загрузки ожидаемого surface:

- `OpenMenuFromWin` завершается на `HomeScreen`.
- `OpenCharacterScreen` завершается на `CharacterScreen`.

Решение-кандидат:

- В `TutorialUiStep` добавить optional completion surface.
- В `TutorialUiFlowController` добавить `NotifySurfaceLoaded(ScreenEnum surface)`.
- В `TutorialUiRuntime.OnSurfaceLoaded(...)` сначала сообщать flow о загруженном surface, затем брать prompt уже для актуального stage.
- В `ObserveAllowedClick(...)` не вызывать `Notify(action)` для шагов, которые завершаются через surface transition.

Результат проверки 18:40:

```text
[TUTORIAL UI] click accepted, waiting surface completion action=OpenMenuFromWin
[TUTORIAL UI] surface completed stage=AwaitingWinHome action=OpenMenuFromWin surface=HomeScreen
[TUTORIAL UI] advanced stage=AwaitingHomeCharacter
[TUTORIAL UI] click accepted, waiting surface completion action=OpenCharacterScreen
```

Вывод: навигационный state теперь не уезжает вперед до загрузки целевого surface. После этого зависание осталось на `CharacterScreen` VisualTreeAsset load, значит нужен следующий root cause.

### H10. Tutorial automation отправляет UI Toolkit click через `Task.Delay`, а не через UI scheduler

Статус: не принимать как root cause до проверки H11.

Факты:

- Обычная ручная навигация по меню работает.
- Зависание возникает в automated tutorial flow после `TutorialUiAutomationDriver.DispatchClickAfterDelayAsync()`.
- Текущий driver ждет через `await Task.Delay(...)`, затем вручную вызывает `target.SendEvent(clickEvent)`.
- Последний маркер перед зависанием:

```text
[TUTORIAL UI] automation dispatch click target=btn_character
[UI] home character click
[UI] show screen requested screen=CharacterScreen
[UI] load screen start screen=CharacterScreen
[UI] screen asset load start screen=CharacterScreen
```

Гипотеза:

- Автодрайвер должен оставаться внутри UI Toolkit lifecycle. Для delayed click использовать `VisualElement.schedule.Execute(...).ExecuteLater(...)`, а не `Task.Delay`, чтобы callback гарантированно исполнялся через panel scheduler живого UI element.

Решение-кандидат:

- Убрать `Task.Delay` из `TutorialUiAutomationDriver`.
- Планировать delayed click через `target.schedule.Execute(() => DispatchClick(target)).ExecuteLater(_clickDelayMs)`.
- Проверить, что после `screen asset load start screen=CharacterScreen` появляется `screen asset load done screen=CharacterScreen`.

Результат проверки 18:45:

```text
[TUTORIAL UI] automation scheduled target=btn_character
```

После этого `automation dispatch click target=btn_character` не появился. Значит переход с `Task.Delay` на `target.schedule` убрал риск off-thread dispatch, но конкретный scheduler target оказался ненадежной точкой для delayed automation на HomeScreen.

Уточнение решения:

- Планировать delayed click на стабильном root текущего UI surface (`_activeRoot.schedule`), а target использовать только как получателя события в момент dispatch.

Результат проверки 18:49:

```text
[TUTORIAL UI] automation scheduled root=[UI]-container target=btn_character
```

После этого `automation dispatch click target=btn_character` тоже не появился. Значит delayed UI Toolkit scheduler после перехода в Menu не является надежным таймером для automation.

Уточненное решение 2:

- Использовать `Task.Delay` только как real-time timer.
- Captured `SynchronizationContext` использовать для возврата dispatch в Unity main context: `unityContext.Post(_ => DispatchClick(target), null)`.
- UI event отправлять только внутри posted main-context callback.

Результат: последняя правка с `SynchronizationContext.Post` отклонена как недоказанная и снята. Следующая проверка переводится на H11.

### H11. Menu scene наследует `Time.timeScale = 0` после tutorial/game pause

Статус: подтверждено только для dev-autoplay, не принимать как production root cause.

Факты:

- `TutorialGameController.StartSkinTutorial()` после 8-го урока вызывает `_gameManager.Pause()`.
- `GameManager.Pause()` выставляет `TimeScaleCoefficient = 0`.
- `LevelController` живет через `DontDestroyOnLoad` и в `Update()` применяет глобальный `Time.timeScale = GetConfiguredTimeScale() * GameManager.TimeScaleCoefficient`.
- `OpenMenuForSkinTutorial()` грузит `Menu` без восстановления time scale.
- Обычный `UiPauseScreenMechanics.OnExit()` тоже грузит `Menu` без восстановления time scale.
- В проверках после загрузки HomeScreen delayed UI Toolkit scheduler (`target.schedule` и `root.schedule`) ставил задачу, но callback не исполнялся:

```text
[TUTORIAL UI] automation scheduled root=[UI]-container target=btn_character
```

Гипотеза:

- После перехода из paused gameplay в Menu глобальный `Time.timeScale` остается `0`.
- Из-за этого UI Toolkit delayed scheduler не исполняет `ExecuteLater`, а automation flow останавливается на HomeScreen.
- Это объясняет остановку dev-autoplay, но не доказывает проблему production tutorial, потому что реальный игрок нажимает кнопку напрямую и не зависит от delayed scheduler.

Проверка:

- Временно залогировать `Time.timeScale` в:
  - `StartSkinTutorial()` после `_gameManager.Pause()`;
  - `OpenMenuForSkinTutorial()` перед `SceneManager.LoadScene(Menu)`;
  - `MenuEntryPoint.Awake()` перед загрузкой HomeScreen;
  - `TutorialUiRuntime.OnSurfaceLoaded(...)`.
- Если в Menu/HomeScreen будет `timeScale=0`, root cause доказан.

Решение-кандидат после доказательства:

- Не менять `MenuEntryPoint` на основании H11: это было бы исправлением тестового драйвера через изменение игровой архитектуры.
- Исправлять dev-autoplay отдельно как test-only инфраструктуру: задержка через unscaled real-time (`Task.Delay`), dispatch обратно в Unity main context, затем обычный `ClickEvent` на тот же target.
- Production tutorial engine должен позволять игроку проходить тот же UI путь обычными кликами и завершать навигационные шаги по загрузке целевого surface (H9).

### H12. `CharacterScreen` зависает на загрузке `VisualTreeAsset`

Статус: подтверждено как симптом, причина уточнена в H14.

Факты:

- Dev-autoplay теперь отправляет клик по `btn_character`, обработчик `HomeScreenController.OnClickBtnCharacter()` выполняется.
- После этого вызывается обычный UI path:

```text
[UI] home character click
[UI] show screen requested screen=CharacterScreen
[UI] load screen start screen=CharacterScreen
[UI] screen asset load start screen=CharacterScreen
```

- После ожидания нет:

```text
[UI] screen asset load done screen=CharacterScreen
```

Вывод: текущий стоп уже не в tutorial state machine и не в delayed click. Нужна диагностика самого Addressables handle для `VisualTreeAsset CharacterScreen`.

Проверка:

- Временно добавить handle-level логи в `ScreenController.LoadScreenAsync()`:
  - `located`;
  - `handle valid`;
  - `Completed` callback;
  - `await done`;
  - exception/failure.
- Исправление делать только после последнего подтвержденного маркера.

Результат 19:09:

```text
[UI] screen asset load start screen=CharacterScreen
```

После этого не появилось даже `screen asset locations`, но этот вывод признан недостаточно чистым: сама диагностика добавила `LoadResourceLocationsAsync` в runtime path. Дальше проверяем только настоящую операцию `Addressables.LoadAssetAsync<VisualTreeAsset>()` с handle-level логами, без дополнительного locations lookup.

Результат 21:29:

```text
[UI FLOW] ScreenController asset handle created screen=CharacterScreen address=CharacterScreen valid=True done=False status=None
[UI FLOW] ScreenController asset monitor screen=CharacterScreen attempt=1 done=False status=None percent=0.50 exception=:
...
[UI FLOW] ScreenController asset monitor screen=CharacterScreen attempt=10 done=False status=None percent=0.50 exception=:
```

Вывод: `CharacterScreen` не падает с exception и не получает failed status. Handle остается живым, но незавершенным на `0.50`.

### H13. Synthetic `ClickEvent` запускает UI-переход в некорректном event context

Статус: снято как основной root cause текущего зависания.

Факты:

- Клик по `btn_character` был отправлен dev-autoplay как synthetic `ClickEvent`.
- `HomeScreenController.OnClickBtnCharacter()` выполнился и вызвал обычный `UIManager.OnScreenShow(CharacterScreen)`.
- После этого Addressables locations lookup для `CharacterScreen` не завершился.
- Ручной игрок не использует synthetic `ClickEvent`; он генерирует полноценную pointer/click sequence Unity.

Проверка:

- Только для dev-autoplay шага `OpenCharacterScreen` вызвать тот же UI command напрямую: `UIManager.OnScreenShow(ScreenEnum.CharacterScreen)`.
- Если `CharacterScreen` загрузится, root текущего automated stall — synthetic click context, а не production tutorial flow.

Результат:

- Dev-autoplay больше не отправляет synthetic click для `OpenCharacterScreen`.
- Он вызывает тот же UI command напрямую:

```text
[TUTORIAL UI] automation open character hasScreenHandler=True
[UI FLOW] OnScreenShow enter screen=CharacterScreen
```

- После прямого command-вызова `CharacterScreen` все равно зависает на `0.50`.

Вывод: synthetic click не является корнем текущего зависания.

### H14. First-start `SigninModal` конфликтует с tutorial UI flow

Статус: частично подтверждено как конфликтующий симптом, но снято как полный root cause.

Факты:

- После перехода из gameplay tutorial в `Menu` обычный `MenuEntryPoint.Start()` видит первое открытие игры:

```text
[UI FLOW] MenuEntryPoint Start begin isGameJustStarted=True
```

- `HomeScreen` успешно загружается и tutorial показывает подсветку `btn_character`.
- Затем `MenuEntryPoint` запускает first-start sign-in modal:

```text
[UI FLOW] MenuEntryPoint auth linked=False isGameJustStarted=True
[UI FLOW] MenuEntryPoint showing SigninModal
[UI FLOW] ShowModal requested modal=SigninModal hasController=True
[UI FLOW] Modal asset handle created modal=SigninModal address=SigninModal valid=True done=False status=None
```

- Почти сразу после этого tutorial automation вызывает открытие `CharacterScreen`:

```text
[TUTORIAL UI] automation execute action=OpenCharacterScreen target=btn_character
[UI FLOW] OnScreenShow enter screen=CharacterScreen
[UI FLOW] ScreenController asset handle created screen=CharacterScreen address=CharacterScreen valid=True done=False status=None
```

- Оба Addressables handle зависают одинаково:

```text
[UI FLOW] Modal asset monitor modal=SigninModal attempt=1 done=False status=None percent=0.50 exception=:
[UI FLOW] ScreenController asset monitor screen=CharacterScreen attempt=1 done=False status=None percent=0.50 exception=:
...
[UI FLOW] Modal asset monitor modal=SigninModal attempt=10 done=False status=None percent=0.50 exception=:
[UI FLOW] ScreenController asset monitor screen=CharacterScreen attempt=10 done=False status=None percent=0.50 exception=:
```

Вывод:

- Tutorial UI lesson входил в menu flow в состоянии `IsGameJustStarted=True`, из-за чего first-start `SigninModal` стартовал параллельно с tutorial-навигацией в `CharacterScreen`.
- Ошибки Unity Ads (`Invalid configuration request`, `Curl error 35`) идут в отдельном стеке `AdsManager` и не совпадают с доказанным UI/Addressables root cause.

Контрпроверка 21:33:

```text
[UI FLOW] MenuEntryPoint Start begin isGameJustStarted=False
[UI FLOW] MenuEntryPoint auth linked=False isGameJustStarted=False
[UI FLOW] MenuEntryPoint Start completed
[TUTORIAL UI] automation open character hasScreenHandler=True
[UI FLOW] ScreenController asset handle created screen=CharacterScreen address=CharacterScreen valid=True done=False status=None
[UI FLOW] ScreenController asset monitor screen=CharacterScreen attempt=10 done=False status=None percent=0.50 exception=:
```

Вывод после контрпроверки:

- Подавление `SigninModal` убирает параллельный modal load, но не чинит `CharacterScreen`.
- Значит настоящий root cause глубже: сам `Addressables.LoadAssetAsync<VisualTreeAsset>("CharacterScreen")` не завершается после перехода из tutorial gameplay в menu.

Решение-кандидат:

- В tutorial entry/transition перед загрузкой `Menu` перевести только tutorial sandbox/session state в состояние, где first-start modal не стартует во время guided UI lesson.
- Не менять общий `UIManager`, `ScreenController`, `ModalController` или обычную механику меню ради tutorial.
- После проверки убрать временные handle/lifecycle логи из UI common-классов.

### H15. В menu flow зависает повторная загрузка UI UXML из Addressables

Статус: проверяется.

Факты:

- `HomeScreen` грузится успешно.
- После `HomeScreen` любой следующий UI surface, проверенный сейчас (`CharacterScreen`; ранее параллельно `SigninModal`), остается на `Addressables` progress `0.50`.
- Automation уже не использует synthetic click для открытия `CharacterScreen`; вызывается тот же UI command напрямую:

```text
[TUTORIAL UI] automation open character hasScreenHandler=True
[UI FLOW] OnScreenShow enter screen=CharacterScreen
```

Гипотезы:

- H15a: проблема в build mode/stale Addressables data для UI-группы.
- H15b: проблема в жизненном цикле menu scene после tutorial gameplay transition.
- H15c: проблема в повторном UI load внутри `ScreenController`, не зависящая от tutorial click adapter.

Проверки:

- Найти группу/адреса `HomeScreen`, `CharacterScreen`, `SigninModal` и сравнить схемы/пути.
- Проверить, грузится ли `CharacterScreen` первым экраном в menu scene без tutorial transition.
- Если first-load проходит, проверить повторную загрузку после `HomeScreen` в обычном menu path.
- До результата не менять способ клика: текущий стоп доходит до `OnScreenShow`, значит click adapter не является текущим блокером.

## Следующий шаг

1. Доказать H15: локализовать, почему `CharacterScreen` Addressables handle остается на `0.50`.
2. После исправления H15 проверить варианты click adapter (`ClickEvent`, pointer sequence, direct intent) и выбрать лучший.
3. Перезапустить full tutorial autoplay и проверить, что skin lesson завершается, затем запускается Tutorial Level 2 и super hit lesson.
4. Убрать временные lifecycle/UI/load логи или оставить только обоснованную устойчивую диагностику.
5. После успешного полного прогона сделать self-review и только потом APK build.
