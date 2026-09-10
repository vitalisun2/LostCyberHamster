# Контракт адаптера

Ядро не знает названий игры, экранов, менеджеров или формата сохранения. UI Toolkit проверяется готовым probe. Для uGUI и других UI адаптер задаёт `Context.Probe` и `Context.TextProbe`.

## План

```json
{
  "title": "Запрошенный flow",
  "cases": [{
    "id": "feature",
    "title": "Название кейса",
    "source": "Путь к controller/presenter текущего проекта",
    "states": [{
      "id": "ready",
      "title": "Готово к действию",
      "fixture": "project-defined-key",
      "data": {"progress": 5},
      "expected": ["actual-visible-element-name"],
      "expectedText": [],
      "note": "Описание подготовленных данных"
    }]
  }]
}
```

Сохранить реальные имена из исходников. `cases` / `states` задают порядок flow. `data` — произвольный JSON-объект адаптера. `expected` содержит имена контрольных элементов; `expectedText` — подстроки видимого текста с учётом регистра. `settleMs`: 600 по умолчанию, допустимо 100–10000. Идентификаторы — `[a-z0-9-]+`, имена PNG — `case--state.png`.

## Адаптер проекта

Все объявления C# заключить в namespace `UiGallery`, включая using. Файл передаётся через `--adapter`, несколько файлов — через пробел. Runner компилирует их совместно вне Assets через Unity CLI `run_script`, без project recompile. Полный рабочий пример чтения текущего UI — [current-screen-adapter.cs.txt](../assets/current-screen-adapter.cs.txt).

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
python <skill>/scripts/gallery.py validate --plan <plan.json>
python <skill>/scripts/gallery.py capture --project <Unity-project> --adapter <adapter.cs> --plan <plan.json> --out <new-output-folder>
python <skill>/scripts/gallery.py build --out <output-folder>
python <skill>/scripts/gallery.py serve --out <output-folder> --port 8767
```

При наличии общего project lock добавить `--lock-file <путь>`. `serve` работает до остановки процесса; на Windows запускать скрытым. Галерея открывается и как локальный HTML, без сервера.

## Результат и восстановление после ошибки

PNG снимается `ScreenCapture.CaptureScreenshot` после стабилизации bounds; снимок камеры может пропустить overlay UI. Размер берётся из Game View. Другие разрешения/языки — отдельные наборы.

Файлы: `index.html`, PNG, `manifest.json`, `run.json`, `capture-result.json`. Manifest содержит подписи, SHA256, размер, bounds и видимый текст. Неполный набор помечается на странице. Проверить кадры визуально перед выдачей.

Долгий batch работает как Task в AppDomain, ход сохраняется в capture-result.json. При отмене runner запрашивает остановку Task, дожидается завершения и останавливает только свою Play-сессию. При неясном восстановлении lock остаётся: прочитать результат, проверить Editor и адаптер; повторный capture до проверки не запускать. Истёкший timeout CLI не доказывает отмену команды, поэтому после editor_play проверяется реальное состояние Editor.

Скрипты и шаблон универсальны. Адаптеры конкретного приложения и его планы хранить в самом проекте отдельно от скилла; перенос скилла в другую игру не требует переносить их.
