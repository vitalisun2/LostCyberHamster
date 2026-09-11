---
name: unity-ui-capture
description: Снять реальные экраны и модальные окна Unity-проекта во всех визуально различных UI-состояниях. Готовые проектные модули запускаются одной командой и складывают PNG по подпапкам в Downloads.
---

# Снимки Unity UI

Сначала искать готовый модуль в `adapters/<project>/adapter.json`. Если он подходит, не исследовать UI заново и не составлять новый план: выполнить `inspect`, затем один `capture`. Исследование кода нужно только для нового проекта, нового UI после изменения модуля или явно ограниченного пользовательского flow.

## Быстрый запуск

Из корня репозитория:

```powershell
python .agents/skills/unity-ui-capture/scripts/capture.py inspect --project <Unity-project>
python .agents/skills/unity-ui-capture/scripts/capture.py capture --project <Unity-project>
```

Модуль определяется по маркерам проекта. Результат по умолчанию: `Downloads/<project>_UI_<timestamp>/`. Внутри — смысловые подпапки, оригинальные PNG, `capture-manifest.json`, `capture-result.json`, `run.json` и копия плана. HTML не создаётся.

Для LostCyberHamster достаточно:

```powershell
python .agents/skills/unity-ui-capture/scripts/capture.py capture --project LostCyberHamster
```

Для части экранов использовать фильтры поверх того же плана:

```powershell
python .agents/skills/unity-ui-capture/scripts/capture.py inspect --project LostCyberHamster --list
python .agents/skills/unity-ui-capture/scripts/capture.py capture --project LostCyberHamster --case profile
python .agents/skills/unity-ui-capture/scripts/capture.py capture --project LostCyberHamster --state profile:empty
python .agents/skills/unity-ui-capture/scripts/capture.py capture --project LostCyberHamster --only "shop:*"
```

Перед запуском Unity Editor должен быть остановлен, стабилен, иметь чистую сцену Bootstrap/Menu. Инструмент сам берёт project lock, запускает Play Mode, готовит изолированный профиль, снимает все кадры, восстанавливает профиль и останавливает Play Mode. Не запускать второй capture, пока первый работает.

Печатать каждый переход только для диагностики: `--progress`. Создать HTML отдельно по явному запросу: `--gallery`; тогда нужен соседний [image-gallery](../image-gallery/SKILL.md). Viewer создаётся рядом с папкой результата capture в отдельной sidecar-папке `<output>_gallery`.

## Состав кадров

Сохранять только визуально различные состояния интерфейса. Разные числа, даты или текст при той же структуре и оформлении не дублировать. Каждый экран, модалка или связанный набор состояний получает отдельную подпапку через поле `folder` в плане.

При отсутствии готового модуля:

1. Через `rg` связать навигацию, controller/presenter, UXML/USS или Canvas и ветви отображения.
2. Составить план из реальных визуальных состояний с `expected` для видимого контрольного элемента.
3. Реализовать адаптер по [контракту](references/capture.md), используя реальные контроллеры и изолированные тестовые данные.
4. Проверить план командой `validate`, затем выполнить capture.

## Проверка

Успех требует: все ожидаемые PNG валидны, размеры совпадают с метаданными, профиль восстановлен, Play Mode остановлен, lock освобождён. Просмотреть по одному кадру каждого типа UI и все существенно отличающиеся состояния.

При сбое читать `run.json` и `capture-result.json`. Модуль может продолжить независимые состояния и повторить нестабильный кадр; итог остаётся неуспешным, пока число валидных PNG не равно плану. Повторный запуск допустим после подтверждённого восстановления.

## Модули проектов

Модуль лежит рядом с ядром:

```text
unity-ui-capture/
  adapters/<module>/
    adapter.json
    plan.json
    ProjectAdapter.cs
    Fixtures.cs
```

`adapter.json` задаёт id, маркеры проекта, C# файлы, план, имя выходной папки и optional lock. Ядро не содержит API игры. Для нового проекта добавлять новый каталог модуля, не менять универсальный runner без общей причины.

Ручной совместимый запуск поддерживается:

```powershell
python .agents/skills/unity-ui-capture/scripts/capture.py capture --project <Unity-project> --adapter <adapter.cs> --plan <plan.json> --out <new-output-folder>
```
