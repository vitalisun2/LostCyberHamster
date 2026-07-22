---
description: "Анализ регресса evidence-first. Воспроизвести. Найти root cause."
name: "Bug Regression Workflow"
agent: "agent"
argument-hint: "Регресс: тест/вход, expected, actual, логи, артефакты."
---

Bug Regression Workflow: один регресс за цикл. Цель — доказать root cause из кода и фактов, без подбора фикса по симптому.

## Правила

1. Язык: русский. Читать [docs/rules/AGENTS.md](docs/rules/AGENTS.md).
2. Ветка: `integration/unity-live`. Без создания новых branch/worktree.
3. Код не править. Нужен только root cause и архитектурная рекомендация.
4. Параметры, пороги, guards, fallback, порядок под симптом не подгонять.
5. Доказательство = execution path + факты выполнения + исключение альтернатив.
6. Root cause доказан — стоп сразу. Ответить пользователю. Без выполнения cleanup, git checks, validation, self-review, реализации.

## Подготовка

1. Выделить один конкретный регресс: место, expected, actual, affected inputs.
2. Источник expected: тест, док, snapshot, fixture, юзер.
3. Найти минимальную команду воспроизведения.
4. Вести записи в docs/Planning/in-progress/<short-regression-slug>-analysis-<yyyy-mm-dd>.md (источники, команды, факты, гипотезы).
5. Итог в этот файл не писать. Сразу выдать юзеру при доказательстве root cause.

## Диагностические логи

1. Использовать готовую Diagnostic Log инфраструктуру. Без ручных `Debug.Log`, `Console.WriteLine`, ad-hoc хелперов.
2. База bot-диагностики: [LostCyberHamster/Assets/Scripts/Bot/Diagnostics/BotDiagnostics.cs](LostCyberHamster/Assets/Scripts/Bot/Diagnostics/BotDiagnostics.cs). Лог-транспорт: [LostCyberHamster/Assets/Scripts/GameEngine/DebugManager.cs](LostCyberHamster/Assets/Scripts/GameEngine/DebugManager.cs) (`DiagLog`, `DiagLogVerbose`, `DiagChannel`).
3. Линковать логи через классы в папке [LostCyberHamster/Assets/Scripts/Bot/Diagnostics/](LostCyberHamster/Assets/Scripts/Bot/Diagnostics/): [LostCyberHamster/Assets/Scripts/Bot/Diagnostics/BotExecutionDiagnostics.cs](LostCyberHamster/Assets/Scripts/Bot/Diagnostics/BotExecutionDiagnostics.cs), [LostCyberHamster/Assets/Scripts/Bot/Diagnostics/BotReplanDiagnostics.cs](LostCyberHamster/Assets/Scripts/Bot/Diagnostics/BotReplanDiagnostics.cs), [LostCyberHamster/Assets/Scripts/Bot/Diagnostics/BotStrategyDiagnostics.cs](LostCyberHamster/Assets/Scripts/Bot/Diagnostics/BotStrategyDiagnostics.cs), [LostCyberHamster/Assets/Scripts/Bot/Diagnostics/BotRuntimeEventDiagnostics.cs](LostCyberHamster/Assets/Scripts/Bot/Diagnostics/BotRuntimeEventDiagnostics.cs).
4. Нет нужного метода? Добавить в соответствующий класс диагностики, `BotDiagnosticCategory`, `BotDiagnosticLevel`. Использовать в коде.
5. Вызов `DebugManager.DiagLog*` напрямую: только если слой уже использует его. Для bot-логов всегда выбирать `BotDiagnostics` и профильные классы.

## Факты

1. Мало фактов? Добавить временные diagnostic logs под гипотезу: вход, конфиг, состояние, ветки, fallback, результат.
2. В логах использовать correlation id: вход -> расчет -> выход.
3. Запустить тест. Сделать таблицу: case, expected, actual, выбранная ветка, причины отсечения альтернатив, первая точка расхождения.

## Анализ кода

1. Пройти execution path.
2. Проверить контракт входа/выхода, потерю/подмену данных, guards/fallback, владельца expected инварианта на каждом уровне.
3. Гипотезы: доказать/опровергнуть фактами. Отрицательные выводы тоже аргументировать.

## Второй диагностический проход

Данных мало? Разрешен один доп-проход логирования после анализа кода. Описать точный вопрос. Логировать только ключевые развилки гипотез.

## Root Cause

Доказательство готово, если есть: code reference точки расхождения, значения из логов, контрактное обоснование expected, причина actual, исключение альтернатив. Запрещено: "похоже", "возможно", "скорее всего".

## Рекомендация

1-2 предложения. Изменять какой слой, почему соответствует SOLID/DRY/KISS. Запрещены: костыли, тюнинг порогов, обход контрактов, правки симптомов. Только архитектурное решение. Для исправления бага по запросу юзера: использовать [.github/prompts/workflows/bug-fix-workflow.prompt.md](.github/prompts/workflows/bug-fix-workflow.prompt.md).

## Финальный ответ

Выдать ответ сразу после доказательства root cause. Стоп сразу. Состав ответа: scope, расхождение expected/actual, root cause со ссылками на код и логи, исключенные альтернативы, архитектурная рекомендация, список временных логов. Логи не удалять, диагностику не откатывать, проверки не делать до команды юзера.
