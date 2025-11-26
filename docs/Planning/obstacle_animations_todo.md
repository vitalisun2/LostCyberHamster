# Obstacle Animations - TODO List

## Проблемы, требующие решения

### 1. ✅ Новые спрайт-шиты не видны в Level Tilemap Editor
**Статус:** ИСПРАВЛЕНО

**Проблема:**
После импорта анимаций через `ObstacleAnimationImporter` созданные спрайт-шиты не отображались в списке спрайтов Level Tilemap Editor.

**Причина:**
Несоответствие label'ов в Addressables:
- Устанавливался label: `"NewYork obstacles sprites"` (PascalCase, слитно)
- Editor искал label: `"New York obstacles sprites"` (Title Case, с пробелами)

**Решение:**
- Исправлены строки 446 и 472 в `ObstacleAnimationImporter.cs`
- Теперь используется `locationTitleCase` вместо `locationPascal` для label'ов
- Label'ы теперь правильные: `"New York obstacles sprites"` и `"New York obstacle animations"`

**Требуется:**
- Переимпортировать анимации, чтобы обновить label'ы в Addressables
- Проверить, что спрайты теперь видны в Level Tilemap Editor

---

## Выполненные задачи

### ✅ Исправлена регистрация ассетов в Addressables
- Добавлены функции `PascalCaseToLowerWithSpaces()` и `PascalCaseToTitleCase()`
- Исправлены имена групп: `"new york obstacles sprites"` и `"New York obstacle animations"`
- Спрайт-шиты и анимации теперь корректно добавляются в Addressables
