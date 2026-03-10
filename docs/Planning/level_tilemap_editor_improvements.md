# Level Tilemap Editor: план доработок

## Контекст
Анализ кодовой базы LevelTilemapEditor выявил ряд проблем, которые могут усложнить поддержку в будущем. Ниже — конкретные доработки, выполнимые малой кровью без переделки архитектуры.

**Статус: ВСЕ ВЫПОЛНЕНЫ**

---

## 1. [DONE] Свойство `IsTemplateMode` вместо дублирования проверки
**Проблема:** `string.Equals(_currentLocationName, Consts.TemplatesLocationName, StringComparison.OrdinalIgnoreCase)` встречается 9+ раз.  
**Решение:** Computed property `IsTemplateMode`.  
**Файл:** `LevelTilemapEditor.cs`

## 2. [DONE] Исправление опечатки `_tipeMapInScene` → `_tilemapInScene`
**Проблема:** Поле используется 20+ раз, имя с опечаткой ухудшает читаемость.  
**Решение:** Rename symbol через весь файл (34 замены).  
**Файл:** `LevelTilemapEditor.cs`

## 3. [DONE] Исправление бага `Math.Max(x, x)`
**Проблема:** Строка `float totalWidth = Math.Max(singlePatternWidth, singlePatternWidth);` — бессмысленная операция (max от одного значения).  
**Решение:** Заменена на `float totalWidth = singlePatternWidth;`  
**Файл:** `LevelTilemapEditor.cs`, метод `LoadTemplatesDirectly`

## 4. [DONE] Замена хардкода `"01_New_York"` на `Consts.TemplatesFallbackLocation`
**Проблема:** Строка `"01_New_York"` захардкожена в 5 местах. При смене fallback локации придётся менять вручную.  
**Решение:** Использована `Consts.TemplatesFallbackLocation` во всех 5 местах.  
**Файл:** `LevelTilemapEditor.cs`

## 5. [DONE] Замена `_currentPattern` на computed property `CurrentPattern`
**Проблема:** Поле `_currentPattern` может рассинхронизироваться с `_currentLevelInfo.patterns[_selectedPatternIndex]` после модификации массива.  
**Решение:** Computed property с безопасным доступом, удалены все присвоения `_currentPattern = ...`.  
**Файл:** `LevelTilemapEditor.cs`

## 6. [DONE] Удаление поля Pattern Duration
**Проблема:** Поле `_patternDurationMinutes` не персистится, не сохраняется в JSON, сбрасывается при перезапуске. Используется только для расчёта ширины фоновых сегментов сцены — это можно вычислять автоматически из bounds паттернов.  
**Решение:** Убрано поле из UI (UXML), удалён event handler, удалено поле из LevelTilemapUi, используется `DefaultTilemapWidth` для ширины сцены.  
**Файлы:** `LevelTilemapEditor.cs`, `LevelTilemapUi.cs`, `LevelTilemapEditor.uxml`

## 7. [DONE] Замена магического числа `3.8f` на `Consts.GameSpeedBase`
**Проблема:** В `LoadTemplatesDirectly` используется `3.8f` напрямую, хотя есть константа.  
**Решение:** Использована `Consts.GameSpeedBase`.  
**Файл:** `LevelTilemapEditor.cs`, метод `LoadTemplatesDirectly`
