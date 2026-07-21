---
description: "Дефолтно выполнить feature-задачу или bugfix во временной task-ветке/worktree; Unity Live использовать только как стенд под lock."
name: "Task Branch Workflow"
agent: "agent"
argument-hint: "Описание задачи и, опционально, slug"
---

Task Branch workflow: дефолт для feature-задач и bugfix. Создаётся временная ветка `task/<slug>` и worktree `.worktrees/<slug>` от `integration/unity-live`; все правки живут в task-worktree. `integration/unity-live` — общий Unity-стенд, который можно занимать только под lock.

Не использовать для analysis-only/root cause задач. Если пользователь просит только анализ или доказательство причины, после доказанного root cause сразу ответь с причиной и предложением решения; не создавай ветку/worktree, не запускай cleanup, validation или дополнительные git/workflow checks без отдельного запроса.

## Подготовка

1. Выбери `slug` в kebab-case.
2. Убедись, что основной каталог на `integration/unity-live`.
3. Создай `task/<slug>` от `integration/unity-live` и worktree `.worktrees/<slug>`.
4. Все правки и доработки делай только в `.worktrees/<slug>`.
5. Не используй финальный `git merge` из `task/<slug>`: task-ветка — временное хранилище.

## Реализация

1. Прочитай целевой код, соседние call sites и ближайший execution path.
2. Реализуй минимальное полное изменение в task-worktree.
3. После basic review C#-правок выполни финальный gate из `docs/rules/code_conventions.md`. Для документации regeneration и build не нужны.
4. Если проверка упала по текущей задаче — исправь в task-worktree; если причина внешняя — остановись с блокером.

## Unity-стенд

Используй только если нужна Unity/Play Mode/manual проверка в основном открытом проекте.

1. Проверь `git status --short` в основном каталоге `integration/unity-live`. Если там есть чужие или несвязанные изменения, не перетирай их: сообщи блокер и жди освобождения стенда.
2. Захвати lock: атомарно создай `.worktrees/.integration-lock/`.
3. Запиши `owner.json` как лог стенда: `{"task":"<slug>","phase":"validation","branch":"task/<slug>","worktree":".worktrees/<slug>","timestamp":"<ISO 8601>"}`.
4. Под lock перенеси snapshot/diff из `.worktrees/<slug>` в основной каталог на `integration/unity-live`.
5. Запускай Unity/runtime-проверку только по явному запросу пользователя; она не входит в финальный C# gate.
6. После проверки убери из `integration/unity-live` только snapshot этой задачи, верни стенд в исходное состояние и сними lock.
7. Если проверка нашла проблему текущей задачи, исправь её в `.worktrees/<slug>` и повтори нужную валидацию.

## Build / Telegram

Если после проверки нужен билд или публикация в Telegram, выполняй это из `.worktrees/<slug>` по `docs/rules/build_and_telegram_publishing.md`.

## Финализация

1. Проверь, что финальный diff остался в `.worktrees/<slug>`, а `integration/unity-live` очищен от snapshot-а этой задачи.
2. Удали lock, если он ещё существует.
3. Коммит, push, merge и удаление worktree/ветки выполняй только по явной команде пользователя.
4. Финальный ответ: кратко — итог, изменённые файлы с одной фразой смысла, проверка, статус task-worktree, статус `integration/unity-live` и lock/cleanup.
