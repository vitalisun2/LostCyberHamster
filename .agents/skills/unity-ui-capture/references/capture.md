# Контракт адаптера

Ядро не знает названий игры, экранов, менеджеров или формата сохранения. UI Toolkit проверяется готовым probe. Для uGUI и других UI адаптер задаёт `Context.Probe` и `Context.TextProbe`.

## План

```json
{
  "title": "Запрошенный flow",
  "continueOnError": true,
  "defaultAttempts": 2,
  "cases": [{
    "id": "feature",
    "title": "Название кейса",
    "source": "Путь к controller/presenter текущего проекта",
    "folder": "01_Название_экрана",
    "states": [{
      "id": "ready",
      "title": "Готово к действию",
      "fixture": "project-defined-key",
      "data": {"progress": 5},
      "expected": ["actual-visible-element-name"],
      "expectedText": [],
      "note": "Описание подготовленных данных",
      "attempts": 2
    }]
  }]
}
```

Сохранить реальные имена из исходников. `cases` / `states` задают порядок flow. `folder` создаёт смысловую подпапку; путь должен быть относительным и без `..`. `data` — произвольный JSON-объект адаптера. `expected` содержит имена контрольных элементов; `expectedText` — подстроки видимого текста с учётом регистра. `settleMs`: 600 по умолчанию, допустимо 100–10000. `attempts`: 1–3; `defaultAttempts` задаёт значение плана. `continueOnError` позволяет доснять независимые состояния, но итог остаётся неуспешным при любом пропущенном кадре. Идентификаторы — `[a-z0-9-]+`.

CLI-фильтры работают поверх этого плана и не меняют адаптер: `--case <case-id>` оставляет все состояния кейса, `--state <case:state>` оставляет одно состояние, `--only <pattern>` применяет wildcard к `case` или `case:state`.

## Адаптер проекта

Все объявления C# заключить в namespace `UiGallery`, включая using. Готовый модуль хранить в `adapters/<module>/`: `adapter.json`, план и C# файлы. Descriptor содержит `id`, `projectMarkers`, `adapter`, `plan`, `outputName` и optional `lockFile`. Runner определяет модуль по всем маркерам и компилирует его совместно вне Assets через Unity CLI `run_script`, без project recompile. Полный простой пример чтения текущего UI — [current-screen-adapter.cs.txt](../assets/current-screen-adapter.cs.txt).

Класс `GalleryProjectAdapter : IProjectAdapter` реализует:

| Метод | Ответственность |
|---|---|
| `Task Begin(Context)` | Дождаться готовности приложения; выбрать реальный UI root; сохранить исходное состояние и создать изолированные тестовые данные |
| `Task Reset(Context)` | Закрыть прошлое представление; восстановить базовое тестовое состояние, чтобы следующий кадр не зависел от предыдущего |
| `Task Prepare(Context, Shot)` | Открыть экран/модалку по `shot.fixture`, заполнить `shot.dataJson`, указать `shot.method` |
| `void Restore(Context)` | Вернуть исходное состояние и свои временные изменения; метод синхронный, идемпотентный, работает после частичного Begin |
| `bool RestorationVerified` | Результат фактической проверки восстановления |

Данные читать через `Newtonsoft.Json.JsonConvert.DeserializeObject<T>(shot.dataJson)`. JsonUtility подходит для штатных типов проекта, но при run_script может пропустить вложенные типы из динамической сборки; план и результаты ядро сериализует через Json.NET.

UI Toolkit: назначить `context.Document` реальным UIDocument; доступны `context.Root`, reflection helpers `Call/Field/Set`. Временные view/подписки/static overrides восстанавливать через `context.Defer(Action)` с захваченным исходным значением. Эти действия исполняются перед следующим состоянием и при завершении.

Другой UI: задать `context.Probe = async shot => Evidence[]` и `context.TextProbe = () => visibleText`. Probe проверяет существование, видимость и стабильные bounds целевых элементов; ошибка означает неснятое состояние. `Evidence`: name, x, y, width, height. Подменять отсутствующий UI успешной проверкой нельзя.

Использовать реальные контроллеры и представления. Для незнакомого flow сначала исследовать навигацию и условия состояний. Mock-модель допустима; визуальные overrides подписывать. Штатные async-загрузки дожидаться. Прямой вызов представления подтверждает его вид, а не прохождение flow.

Исходное сохранение защищает адаптер: предпочтителен штатный изолированный профиль или временный preview context. Завершение выполняет Restore также при выходе из Play Mode. Если приложение само меняет данные на старте, подготовить его штатный automation/bootstrap режим до capture; один только выход из Play не откатывает файлы или внешние записи. Сетевые транзакции и игровые кнопки наград при съёмке не нажимать.

## Команды

```powershell
python <skill>/scripts/capture.py validate --plan <plan.json>
python <skill>/scripts/capture.py inspect --project <Unity-project>
python <skill>/scripts/capture.py inspect --project <Unity-project> --list
python <skill>/scripts/capture.py capture --project <Unity-project>
python <skill>/scripts/capture.py capture --project <Unity-project> --case <case-id>
python <skill>/scripts/capture.py capture --project <Unity-project> --state <case:state>
python <skill>/scripts/capture.py capture --project <Unity-project> --only "shop:*"
python <skill>/scripts/capture.py audit --out <output-folder>
python <skill>/scripts/capture.py build --out <output-folder>
python <skill>/scripts/capture.py serve --out <output-folder> --port 8767
```

Без `--out` создаётся новая папка в Windows Downloads known folder; учитывается перенаправленный профиль. `capture` создаёт только PNG и служебные JSON. `--gallery`, `build` и `serve` используют соседний image-gallery только по явному запросу и создают viewer в sidecar-папке `<output>_gallery`. Ручные `--adapter ... --plan ...` сохраняются для проекта без модуля.

## Результат и восстановление после ошибки

PNG снимается `ScreenCapture.CaptureScreenshot` после стабилизации bounds; снимок камеры может пропустить overlay UI. Размер берётся из Game View. Другие разрешения/языки — отдельные наборы.

Файлы: PNG по подпапкам, `capture-manifest.json`, `run.json`, `capture-result.json`, `plan.json`, `capture-plan.json`. Manifest содержит подписи, SHA256, размер, bounds и видимый текст. При `--gallery` или `build` рядом появляется отдельная папка viewer с `index.html` и `manifest.json`; исходная папка capture остаётся raw evidence. Проверить кадры визуально перед выдачей.

Долгий batch работает как Task в AppDomain, ход сохраняется в capture-result.json. При отмене runner запрашивает остановку Task, дожидается завершения и останавливает только свою Play-сессию. При неясном восстановлении lock остаётся: прочитать результат, проверить Editor и адаптер; повторный capture до проверки не запускать. Истёкший timeout CLI не доказывает отмену команды, поэтому после editor_play проверяется реальное состояние Editor.

Ядро универсально. Детали конкретного приложения хранить отдельным подключаемым модулем рядом со скиллом. Новый проект получает новый каталог модуля; общую логику менять только для переносимого поведения.
