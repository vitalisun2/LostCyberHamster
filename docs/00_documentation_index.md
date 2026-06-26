# Documentation Index

Краткий актуальный справочник по документации проекта.

---

## Источники истины по процессу

### docs/rules/AGENTS.md
Точка входа для всех AI-агентов: роутинг, профиль пользователя, стиль общения, процесс выполнения задачи, мета-правила управления правилами.

### docs/rules/code_conventions.md
Конвенции кода, валидация, Unity-специфика (Editor API, размеры, префабы).

### docs/rules/agent_efficiency_playbook.md
Единый владелец lessons: практические правила более быстрой и точной работы агента, promoted-ссылки на owner-файлы.

### docs/rules/temporary_current_rules.md
Временные обязательные правила текущего этапа разработки, включая статус `Bot/` как активной зоны разработки, а не окончательного источника истины.

### docs/rules/agent_tools.md
Каталог проектных инструментов: automation bridge, Diagnostic Log reader, test level launcher, редакторские утилиты и PowerShell-скрипты.

---

## Справочные документы

### docs/02_game_description.md
Краткое описание геймплея и базовых игровых сущностей.

### docs/architecture_knowledge_base.md
Ключевая база знаний проекта: naming conventions, Addressables, редактор уровней, data flow, runtime-декор, бот и устойчивые архитектурные выводы.

### docs/bot_architecture_diagram.md
Графическая Mermaid-схема runtime-архитектуры бота: pipeline, planning graph, strategy families, shared helpers и зависимости между блоками.

### docs/hamster_collision_test_scenarios.md
Чек-лист для ручного тестирования всех основных типов столкновений хомяка.

### README.md
Общее описание репозитория и короткий developer-facing обзор.

---

## Планы и спецификации

### docs/Planning/Milestone New York.md
Высокоуровневая дорожная карта контента и задач по первой локации.

### docs/Planning/hamster-obstacle-runtime-spec.md
Справочная спецификация runtime-логики взаимодействия хомяка с obstacle: линии, столкновения, прыжки, урон, энергия и награды.

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

### docs/Planning/in-progress/
Каталог активных рабочих спецификаций и task-файлов.

### docs/Planning/in-progress/tap-outcome-resolver-and-switch-lane-window-plan.md
План унификации runtime tap semantics и bot switch-lane window search через `TapOutcomeResolver` и `ActionWindowFinder`.

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

### tools/codebase_compact.ps1
Скрипт для генерации сжатого снимка C#-кода.

### tools/read_log_channel.ps1
Унифицированное чтение каналов Diagnostic Log `STAB`, `BOT`, `ECO`, которые пишутся через bot diagnostics helpers и `DebugManager` sink.

### tools/invoke_open_unity_test_level.ps1
Сценарий полного цикла: рекомпиляция, запуск тестового уровня и ожидание результата.
