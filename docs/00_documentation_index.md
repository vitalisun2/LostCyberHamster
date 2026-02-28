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
