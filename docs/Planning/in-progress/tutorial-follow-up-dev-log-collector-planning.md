# Tutorial follow-up и dev-only log collector

Дата: 2026-07-02
Статус: in progress

## Цель

Довести tutorial до удобной проверки на Android-устройстве и добавить dev-only инфраструктуру выгрузки логов, чтобы после ручного прогона уровня на телефоне агент мог получить диагностические файлы без USB-отладки, скриншотов debug UI и ручного копирования.

## Принципы реализации

- Встраиваться в существующую архитектуру, не ломать загрузчик и gameplay-пайплайн.
- Runtime-диагностику писать через существующий `DebugManager` / diagnostic log, не плодить ad-hoc gameplay logger.
- Новую инфраструктуру логирования держать отдельно от gameplay и tutorial-кода.
- Dev-only поведение должно быть явно ограничено debug/development/test-сборками и выключено в production.
- Не встраивать Telegram bot token в APK. Телефон отправляет логи только на collector endpoint, а collector уже сохраняет файлы локально и при необходимости зеркалит в Telegram.
- KISS: первый этап должен решать текущую боль с минимальным количеством движущихся частей.

## Доработка 1: Dev-only Log Collector

### Пользовательский сценарий

1. Агент запускает collector на компьютере разработчика.
2. Собирается dev/test APK с URL collector endpoint.
3. Пользователь устанавливает APK на Android и проходит tutorial или другой уровень.
4. Игра пишет diagnostic log и session metadata.
5. В конце tutorial, при exception/loading failure и при ручном/служебном flush игра отправляет пакет логов на collector.
6. Collector сохраняет пакет в `DeviceLogs/android/<timestamp>_<device>_<session>/`.
7. Агент читает эти файлы из репозитория и анализирует регрессию.
8. Опционально collector отправляет уведомление или zip-файл в Telegram Buffer.

### Unity runtime слой

Папка: `LostCyberHamster/Assets/Scripts/Diagnostics/DeviceLogs/`.

Классы:

- `DeviceLogSettings`
  - Хранит dev-only настройки: enabled, endpoint URL, upload timeout, upload triggers.
  - Читает конфиг из `Resources/Diagnostics/device_log_settings.json` или аналогичного lightweight source.
  - Не содержит логики отправки.

- `DeviceLogSession`
  - Формирует session id.
  - Собирает metadata: build hash, branch/build label, Unity version, platform, device model, app version, current level, timestamp.
  - Не знает про HTTP.

- `DeviceLogPackageBuilder`
  - Берет `DebugManager.GetDiagLogPath()`.
  - Добавляет metadata JSON.
  - Готовит multipart payload или zip-пакет.
  - Не знает про gameplay события.

- `DeviceLogUploader`
  - Отвечает только за HTTP upload через `UnityWebRequest`.
  - Имеет короткий timeout и failure handling без падения игры.
  - Пишет результат upload в diagnostic log через `DebugManager.DiagStability`.

- `DeviceLogService`
  - Facade для runtime: `Initialize`, `UploadAsync(reason)`, `RecordContext`.
  - Гейтит работу через `DeviceLogSettings.Enabled`, endpoint URL и platform flags. Это internal/dev config, а не обязательный Unity Development Build, чтобы не включать Unity Development Console на телефоне.
  - Подписывается на `Application.logMessageReceived` только если нужно сохранить критические runtime exceptions.

Точки вызова первого этапа:

- После успешного завершения tutorial: upload с reason `tutorial_completed`.
- При loading exception: upload с reason `loading_exception`.
- При необработанном exception в `Application.logMessageReceived`: upload с reason `unity_exception`.

Ограничение первого этапа: не отправлять логи каждый кадр и не стримить live logs. Только session пакеты по событиям, чтобы не усложнять сеть и хранение.

### Collector слой на ПК

Папка: `tools/device-log-collector/`.

Файлы:

- `server.js`
  - HTTP server без тяжелого фреймворка или с минимальной зависимостью, если уже есть Node runtime.
  - Endpoint `POST /upload`.
  - Принимает multipart или JSON+base64 пакет.
  - Проверяет простой shared token/header для dev-защиты.
  - Сохраняет входящий пакет в `DeviceLogs/android/...`.
  - Возвращает JSON `{ ok: true, id, savedPath }`.

- `start_device_log_collector.ps1`
  - Запускает server.
  - Печатает LAN URL для телефона.
  - Печатает путь сохранения логов.

- `README.md`
  - Короткая инструкция запуска.
  - Как прописать endpoint в сборку.
  - Как проверить curl-ом.

Опционально после базового варианта:

- Telegram mirror на стороне collector через уже настроенный publish config.
- Cloudflare Tunnel/ngrok для случаев, когда телефон не в той же Wi-Fi сети.

### Формат сохранения

Пример:

```text
DeviceLogs/android/2026-07-02_12-30-18_android_SM-S918B_tutorial_completed/
  metadata.json
  diagnostic_log.txt
  unity_player_log.txt
  package.json
```

`metadata.json`:

- `sessionId`
- `reason`
- `buildLabel`
- `branch`
- `shortSha`
- `dirty`
- `platform`
- `deviceModel`
- `operatingSystem`
- `unityVersion`
- `currentLevel`
- `tutorialStep`
- `createdAtUtc`

## Доработка 2: tutorial tap highlight

Проблема: в уроке 1 подсвечивается компактный квадратик рядом с хомяком, но это не фактическая input-зона.

Нужно:

- Подсвечивать фактическую область `tap`, которая принимает input.
- Если runtime `tap` занимает большую нижнюю/левую область, tutorial должен показывать именно ее границы, а не декоративный маленький прямоугольник.
- Finger texture показывать только для tap-области.
- Для button highlight finger texture не показывать.

## Доработка 3: мягкая подсветка focus-зон

Проблема: текущая подсветка очерчена жесткой белой рамкой.

Нужно:

- Убрать жесткую белую рамку.
- Сделать мягкий переход от прозрачной focus-зоны к затемнению.
- Для кнопок подсветка круглая, с мягким переходом.
- Для tap-зоны подсветка прямоугольная/скругленная, с мягким переходом.
- Не перекрывать рабочие UI элементы и не ломать input.

Возможный первый подход:

- Вместо четырех жестких dim panels и белой рамки использовать dim panels плюс отдельные semi-transparent soft-edge элементы вокруг focus rect.
- Для кнопок использовать radius по размеру focus rect.
- Для tap-зоны использовать скругленные углы и несколько полупрозрачных слоев с увеличивающимся inset/outset, чтобы получить визуальный fade без шейдера.

## Доработка 4: урок 8, напрыг с крыши

Проблема: в уроке 8 после нажатия подсвеченной кнопки хомяк немного не долетает до `smallAlive`, получает урон и теряет жизнь.

Нужно:

- Проверить механику `JumpFromRoof` / `JumpOnFromRoof` и существующие travel/fire window расчеты.
- Не подбирать дистанцию вслепую.
- Настроить tutorial pattern или pause distance так, чтобы действие соответствовало обычной runtime-логике: хомяк напрыгивает на `smallAlive` и выбивает бонус.
- Проверить через Unity autoplay/tutorial run, что нет `DAMAGE`, `FAIL`, `Exception`.

## Валидация

Перед сборкой APK:

1. Roslyn compile для `Assembly-CSharp.csproj` и `Assembly-CSharp-Editor.csproj`.
2. Unity `recompile_scripts` через automation bridge.
3. Self code review по SOLID/DRY/KISS и документам `docs/refactoring/`:
   - нет смешения ответственностей;
   - новая log collector инфраструктура введена рядом с существующей diagnostic log системой, без поломки старого пути;
   - нет усложнения сигнатур без пользы;
   - нет оберток ради оберток;
   - каждый новый класс/структура данных находится в своем файле.
4. Визуальный запуск tutorial в Unity Editor и скрины ключевых prompt-ов:
   - затемнение до краев экрана;
   - tap highlight соответствует input-зоне;
   - finger есть только на tap;
   - button highlight без finger;
   - нет debug UI поверх игры.
5. Полный autoplay-прогон tutorial 1-9:
   - `Result: WIN`;
   - в diagnostic log нет `FAIL`, `DAMAGE/damage`, `Exception`, `NullReference`;
   - урок 8 завершается без потери жизни.
   - урок 9 в sandbox покупает и надевает Electric Strike skin (`id = 2`) и завершает tutorial после мета-flow через меню.
   - реальные ресурсы, покупки скинов и надетый скин не меняются после tutorial sandbox.
6. Запустить local collector и проверить тестовый upload.
7. Собрать Android APK.
8. Отправить APK в Telegram Buffer.

## Открытые вопросы

- Для первого рабочего варианта использовать LAN URL collector endpoint. Если телефон и ПК окажутся в разных сетях, добавить Cloudflare Tunnel/ngrok.
- Нужно ли collector сразу зеркалить zip в Telegram или пока достаточно локального сохранения в `DeviceLogs/`.
