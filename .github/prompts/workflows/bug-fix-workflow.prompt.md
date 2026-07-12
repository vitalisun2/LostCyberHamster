---
description: "Исправить баг после доказанного root cause: отдельный task-worktree, правки там, Unity Live только как стенд под lock."
name: "Bug Fix Workflow"
agent: "agent"
argument-hint: "Доказанный root cause, ссылки на анализ/логи и ожидаемое исправление"
---

Bug Fix workflow: стадия реализации после Bug Regression Workflow. По процессу это Task Branch workflow с дополнительным входным условием: root cause уже доказан фактами выполнения и code references.

## Правила

1. Если root cause ещё не доказан, сначала выполнить Bug Regression Workflow и остановиться после вывода причины.
2. Создать отдельную ветку `task/<slug>` и worktree `.worktrees/<slug>` от `integration/unity-live`.
3. Все исправления, временные доработки и cleanup делать только в task-worktree.
4. `integration/unity-live` использовать только как Unity-стенд под lock из Task Branch workflow.
5. Если после fix нужен билд или Telegram-публикация, выполнять их из task-worktree по `docs/rules/build_and_telegram_publishing.md`.

## Процесс

1. Кратко зафиксируй доказанный root cause и expected behavior.
2. Следуй `.github/prompts/task-branch-workflow.prompt.md` для подготовки worktree, реализации, Unity-валидации и cleanup стенда.
3. Исправляй причину в слое-владельце инварианта, не добавляй symptom patch, threshold tuning, fallback/guard/override под конкретный симптом.
4. Удали временную диагностику, если она больше не является устойчивой частью решения.
5. Выполни релевантную проверку fix-а.

## Финальный ответ

Кратко сообщи: какой root cause исправлен, какие файлы изменены, какая проверка выполнена, где находится task-worktree, очищен ли `integration/unity-live` и нужен ли отдельный build/publish шаг.
