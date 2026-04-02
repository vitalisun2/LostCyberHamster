# Documentation Index

Краткий актуальный справочник по документации проекта.

---

## Источники истины по процессу

### docs/rules/AGENTS.md
Точка входа для всех AI-агентов: роутинг, профиль пользователя, стиль общения, процесс выполнения задачи, мета-правила управления правилами.

### docs/rules/workflow.md
Ветки, worktree, жизненный цикл задачи, рефакторинг-workflow, параллельное выполнение, git-правила, отчётность.

### docs/rules/iteration_cycle.md
Итерационный цикл тестирования бота: автопрогон, логи, визуальный фидбэк, экономика.

### docs/rules/code_conventions.md
Конвенции кода, валидация, Unity-специфика (Editor API, размеры, префабы).

### docs/rules/ai_workflow_lessons.md
Накопленные практические уроки по работе в этом репозитории.

### docs/rules/agent_tools.md
Каталог проектных инструментов: automation bridge, log reader, test level launcher, редакторские утилиты и PowerShell-скрипты.

---

## Справочные документы

### docs/02_game_description.md
Краткое описание геймплея и базовых игровых сущностей.

### docs/architecture_knowledge_base.md
Ключевая база знаний проекта: naming conventions, Addressables, редактор уровней, data flow, runtime-декор, бот и устойчивые архитектурные выводы.

### docs/hamster_collision_test_scenarios.md
Чек-лист для ручного тестирования всех основных типов столкновений хомяка.

### README.md
Общее описание репозитория и короткий developer-facing обзор.

---

## Планы и спецификации

### docs/Planning/Milestone New York.md
Высокоуровневая дорожная карта контента и задач по первой локации.

### docs/Planning/GameEconomy.md
Документ по экономике игры: ресурсы, баланс и идеи по улучшению.

### docs/Planning/Bot/bot concept brainstrom
Концептуальный документ по боту: цели, поведенческие приоритеты и модель принятия решений.

### docs/Planning/Bot/bot universal algorithm brainstrom.md
Краткий алгоритмический скелет decision loop для бота.

### docs/Planning/Bot/bot_architecture.md
Целевая архитектура бота и распределение ответственности между pipeline-компонентами.

### docs/Planning/bot_implementation_plan.md
Канонический план реализации бота и текущий статус этапов.

### docs/Planning/level_assembly_refactor.md
Подробная спецификация reference-based сборки уровней и миграции форматов.

### docs/Postponed Analysis/level_editor_ui_refactor_spec.md
Техническое задание по UI-рефакторингу Level Tilemap Editor.

### docs/Planning/sprite_loader_refactor_plan.md
План рефакторинга загрузки спрайтов и миграции на общее ядро Addressables.

### docs/Planning/current/README.md
Placeholder-документ для каталога активного плана. Одноразовые завершённые планы в `current/` не хранятся.

---

## Внешние и исторические материалы

### .github/copilot-instructions.md
Короткая точка входа для Copilot, перенаправляющая к `docs/rules/AGENTS.md`.

### GameDesignDocWithGuidHistory/Addressables.md
Исторический документ по организации Addressables.

### LostCyberHamster/refactor_plan.md
Исторический план старого рефакторинга уровней внутри Unity-проекта.

---

## Инструменты разработчика

### codebase_compact.ps1
Скрипт для генерации сжатого снимка C#-кода.

### read_log_channel.ps1
Унифицированное чтение каналов логов `STAB`, `BOT`, `ECO`.

### invoke_open_unity_test_level.ps1
Сценарий полного цикла: рекомпиляция, запуск тестового уровня и ожидание результата.
