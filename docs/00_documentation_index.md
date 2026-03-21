# Documentation Index

Краткий справочник по документации проекта.

---

## Справочные документы

### docs/01_repo_settings.md
Правила работы с ветками (main/develop/feature/bugfix), установка git-хуков.

### docs/02_game_description.md
Описание геймплея: управление, энергия, жизни, ульта, типы препятствий, бонусы, уровни и локации, магазин.

### docs/architecture_knowledge_base.md
Ключевая база знаний проекта. Naming conventions, Addressables, система анимаций препятствий, Level Tilemap Editor, Data Flow, константы, Lessons Learned, план расширения на новые локации.

**Обязательно читать перед сложными задачами** (упомянуто в `.github/copilot-instructions.md`).

### docs/hamster_collision_test_scenarios.md
Чек-лист для ручного тестирования всех типов столкновений хомяка: Jump, Roof Jump, Roof Run, Super Jump, Super Roof Jump.

---

## Активные планы

### docs/Planning/Milestone New York.md
Дорожная карта первой локации: визуал, UI, экономика, геймдизайн, уровни для всех частей дня.

### docs/Planning/GameEconomy.md
Экономическая модель: монеты, кристаллы, баланс, рекомендации по улучшению.

### docs/Planning/bot concept brainstrom
Основной концептуальный документ по боту: цели, приоритеты, правила выбора шагов, достижимость целей, событийнoе перепланирование, короткий горизонт планирования.

### docs/Planning/bot_architecture.md
Целевая архитектура бота: что сохраняем из текущего кода, какие компоненты нужны, как должны быть разделены runtime-shell и planning-core.

### docs/Planning/bot_implementation_spec.md
Каноническая техническая спецификация реализации бота (без backup-файлов по фазам). Содержит статус этапов и детали внедрения pipeline.

### docs/Planning/sprite_loader_refactor_plan.md
Рефакторинг загрузки спрайтов. Вехи 1-3 завершены (ядро AddressableLoader). Вехи 4-6 в работе: миграция рантайма, редактора, диагностика.

---

## Прочая документация

### .github/copilot-instructions.md
Инструкции для AI-ассистента: правила работы, Unity API, workflow, отладка.

### LostCyberHamster/refactor_plan.md
Рефакторинг системы уровней на иерархическую структуру (Location/PartOfDay/Level). Шаги 1-5 завершены, шаг 6 (фича-флаг) почти готов.

### GameDesignDocWithGuidHistory/Addressables.md
Принципы организации Addressables: группы по локациям, shared-группы, правила именования.

---

## Инструменты разработчика

Скрипты в корне репозитория. Подробнее — в секции «Инструменты разработчика» в README.md.

### codebase_compact.ps1
Генерация + наблюдение за `.cs` файлами. Автоматически обновляет `docs/game_scripts_codebase_compact.txt` — сжатый снимок всего C#-кода для использования с LLM. Запускается автоматически при открытии проекта в VS Code (через `.vscode/tasks.json`).

### docs/game_scripts_codebase_compact.txt
Автогенерируемый файл со снимком всех `.cs` файлов из `Assets/Scripts` и `Assets/Editor`. Не редактировать вручную.
