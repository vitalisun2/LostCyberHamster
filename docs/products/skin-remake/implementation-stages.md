# Skin Remake — этапы реализации

## 1. Контракт visual-действий

Ввести семантические действия Hamster, mapping действий на visual clips и единый `FitToAction`. Transform Animation остаётся источником траектории, длительности и gameplay events.

## 2. Общая структура Hamster

Добавить `collision_body`, `skin_slot` и постоянный visual host. Collider, collision, Shift Transform Animation, Transform Animation и effects остаются общей частью Hamster.

## 3. Runtime SkinVisual

Реализовать загрузку выбранного prefab, подключение к visual host, запуск нужных состояний, pause/start/finish и освобождение ресурсов после забега.

## 4. Контентный стандарт

Создать новые каталоги, prefab contract, Addressables-группу и правила sprite sheets. Добавить проверку структуры скина, action mapping и обязательных visual-ассетов.

## 5. Pilot skateboard skin

Собрать технический prefab на art default skin, но с отдельными `run`, `jump`, `jump_on`, `jump_on_from_roof`. Проверить заменяемость будущих sheets, переиспользование клипов, Normal/Super действия и синхронизацию с существующими transform clips.

## 6. Перенос текущих скинов

Перевести default, neon runner и quantum scout на отдельные SkinVisual prefabs. Сохранить ID, preview, цены, покупки, выбранный скин, tutorial и quests.

## 7. Полное переключение системы

Перевести каталог и экипировку только на prefab-визуалы. Удалить Animator Override Controllers, legacy controller field, старую логику применения и неиспользуемые animation states.

## 8. Финальная проверка

Проверить gameplay collision, все jump/roof/super/damage действия, паузу, старт, сохранения, Addressables lifetime и сборку. Зафиксировать визуальный паритет текущих скинов и готовность инфраструктуры skateboard к замене заглушки финальным art.
