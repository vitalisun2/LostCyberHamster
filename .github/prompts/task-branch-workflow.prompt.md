---
description: "Выполнить задачу во временной task-ветке/worktree и публиковать ревью-снимок в integration/unity-live через lock."
name: "Task Branch Workflow"
agent: "agent"
argument-hint: "Описание задачи и, опционально, slug"
---

Task Branch workflow: временная ветка `task/<slug>` + worktree `.worktrees/<slug>`. Используется для изолированной/параллельной работы. `integration/unity-live` — только для ревью-снимка под lock.

## Подготовка

1. Выбери `slug` в kebab-case.
2. Убедись, что основной каталог на `integration/unity-live`.
3. Создай `task/<slug>` от `integration/unity-live` и worktree `.worktrees/<slug>`.
4. Все правки и доработки делай только в `.worktrees/<slug>`.
5. Не используй финальный `git merge` из `task/<slug>`: task-ветка — временное хранилище.

## Реализация

1. Прочитай целевой код, соседние call sites и ближайший execution path.
2. Реализуй минимальное полное изменение в task-worktree.
3. Для C# правок проверь compile errors и `.csproj` включение новых/перемещённых файлов; для не-C# правок выполни только релевантную лёгкую проверку.
4. Если проверка упала по текущей задаче — исправь в task-worktree; если причина внешняя — остановись с блокером.

## Публикация в `integration/unity-live`

1. Захвати lock: атомарно создай `.worktrees/.integration-lock/`.
2. Запиши `owner.json`: `{"task":"<slug>","phase":"review","timestamp":"<ISO 8601>"}`.
3. Если lock занят, прочитай `owner.json`, сообщи владельца и жди, повторяя попытку каждые 30 секунд. Stale lock не снимай сам.
4. Под lock перенеси snapshot/diff из `.worktrees/<slug>` в основной каталог на `integration/unity-live`.
5. Сообщи, что ревью-снимок готов на `integration/unity-live`, и остановись на ревью пользователя.

## Цикл ревью

Если нужны доработки:

1. Убери из `integration/unity-live` только ревью-снимок этой задачи.
2. Сними lock.
3. Доработай только `.worktrees/<slug>`.
4. Повтори валидацию и публикацию.

Если пользователь доволен, задай прямой вопрос:

`Если всё ок, я готов оставить изменения в integration/unity-live, удалить .worktrees/<slug> и task/<slug>, затем снять lock. Делаем?`

## Финализация после подтверждения

1. Проверь, что принятый diff полностью остался в `integration/unity-live`.
2. Удали `.worktrees/<slug>`, `task/<slug>` и remote-ветку, если она создавалась.
3. Сними lock.
4. Не коммить, не пушь и не мержи в `main` в рамках prompt-а.
5. Финальный ответ: кратко — итог, изменённые файлы с одной фразой смысла, проверка, статус `integration/unity-live`, lock/cleanup.
