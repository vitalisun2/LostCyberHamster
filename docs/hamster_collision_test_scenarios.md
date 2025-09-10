# Руководство по ручному тестированию пересечений хомяка

> **Порядок прохождения**: 1) Jump Mechanics → 2) Roof Jump Mechanics → 3) Roof Run Mechanics → 4) Super Jump Mechanics → 5) Super Roof Jump Mechanics\
> Таблица: **#** — номер сценария, **Условие** — краткое описание, **Ожидаемый стейт** — значение `HamsterStateEnum`, **Статус** — ✔ / ✖ / 🟦 (не проверено), **Результат** — комментарий.

> При проверке столкновений учитывайте допуск по правому краю препятствия:
> центр хомяка считается внутри, пока он не выйдет за `Right + 20%` ширины хомяка.

---

## 1. Jump Mechanics (прыжок с земли)

| # | Условие | Ожидаемый стейт | Статус | Результат |
| - | ----------------------------------------------- | ---------------------------- | --------------- | --------- |
| 1 | **Напрыгнули** на `smallAlive` | `JumpOnObstacle` | 🟦 НЕ ПРОВЕРЕНО | |
| 2 | **Перепрыгнули** `smallAlive` | `JumpOver` | 🟦 НЕ ПРОВЕРЕНО | |
| 3 | **Столкнулись** с `smallNotAliveRoad` на дороге | `JumpDamageForSmallNotAlive` | 🟦 НЕ ПРОВЕРЕНО | |
| 4 | **Перепрыгнули** `smallNotAliveRoad` | `JumpOver` | 🟦 НЕ ПРОВЕРЕНО | |
| 5 | **Столкнулись** с `smallNotAliveRoadAndRoof`, запрыгивая на крышу `bigNotAlive` | `JumpOnRoofDamage` | 🟦 НЕ ПРОВЕРЕНО | |
| 6 | **Столкнулись** с `smallNotAliveRoadAndRoof` на дороге | `JumpDamageForSmallNotAlive` | 🟦 НЕ ПРОВЕРЕНО | |
| 7 | **Столкнулись** с `bigAlive` (по X или Y) | `JumpDamageForBigAlive` | 🟦 НЕ ПРОВЕРЕНО | |
| 8 | **Запрыгнули** на крышу чистого `bigNotAlive` | `JumpOnRoof` | 🟦 НЕ ПРОВЕРЕНО | |
| 9 | **Запрыгнули** на `bigNotAlive`, на крыше мелкое препятствие | `JumpOnRoofDamage` | 🟦 НЕ ПРОВЕРЕНО | |

---

## 2. Roof Jump Mechanics (прыжок, когда хомяк уже на крыше `bigNotAlive`)

| # | Условие | Ожидаемый стейт | Статус | Результат |
| - | ------------------------------------------------------------ | ------------------------ | --------------- | --------- |
| 1 | **Прыгнули**, находясь на крыше чистого `bigNotAlive` | `RoofJump` | 🟦 НЕ ПРОВЕРЕНО | |
| 2 | **Прыгнули**, находясь на крыше `bigNotAlive`, и столкнулись с `smallNotAliveRoadAndRoof` | `RoofJumpDamage` | 🟦 НЕ ПРОВЕРЕНО | |
| 3 | **Напрыгнули** на `bigAlive`, спрыгивая с крыши `bigNotAlive` | `JumpOnObstacleFromRoof` | 🟦 НЕ ПРОВЕРЕНО | |
| 4 | **Напрыгнули** на `smallAlive`, спрыгивая с крыши `bigNotAlive` | `JumpOnObstacleFromRoof` | 🟦 НЕ ПРОВЕРЕНО | |
| 5 | Спрыгивая с крыши `bigNotAlive`, **столкнулись** с `smallNotAliveRoad` | `JumpFromRoofDamage` | 🟦 НЕ ПРОВЕРЕНО | |
| 6 | Спрыгивая с крыши `bigNotAlive`, **столкнулись** с `smallNotAliveRoadAndRoof` на дороге | `JumpFromRoofDamage` | 🟦 НЕ ПРОВЕРЕНО | |
| 7 | Спрыгивая с крыши `bigNotAlive`, **не задели** препятствий | `JumpFromRoof` | 🟦 НЕ ПРОВЕРЕНО | |

---

## 3. Roof Run Mechanics (бег по крышам)

| # | Условие | Ожидаемый стейт | Статус | Результат |
| - | --------------------------------------------- | ------------------------------------ | --------------- | --------- |
| 1 | Крыша закончилась — хомяк **спрыгнул на дорогу** | `RunFromRoof` | 🟦 НЕ ПРОВЕРЕНО | |
| 2 | **Перебежали** на следующую крышу `bigNotAlive` | `RoofRun` (обновить `_lastObstacle`) | 🟦 НЕ ПРОВЕРЕНО | |

---

## 4. Super Jump Mechanics (супер‑прыжок с земли)

| # | Условие | Ожидаемый стейт | Статус | Результат |
| -- | ------------------------------------------------------------------------------ | ----------------------- | --------------- | --------- |
| 1 | **Запрыгнули** на крышу чистого `bigNotAlive` | `SuperJumpOnRoof` | 🟦 НЕ ПРОВЕРЕНО | |
| 2 | **Запрыгнули** на `bigNotAlive` и столкнулись с `smallNotAliveRoadAndRoof` | `SuperJumpOnRoofDamage` | 🟦 НЕ ПРОВЕРЕНО | |
| 3 | **Столкнулись** с `bigAlive` | `SuperJumpDamage` | 🟦 НЕ ПРОВЕРЕНО | |
| 4 | **Перепрыгнули** `bigAlive` | `SuperJumpOver` | 🟦 НЕ ПРОВЕРЕНО | |
| 5 | **Напрыгнули** на `smallAlive` | `SuperJumpOnObstacle` | 🟦 НЕ ПРОВЕРЕНО | |
| 6 | **Перепрыгнули** `smallAlive` | `SuperJumpOver` | 🟦 НЕ ПРОВЕРЕНО | |
| 7 | **Столкнулись** с `smallNotAliveRoad` | `SuperJumpDamage` | 🟦 НЕ ПРОВЕРЕНО | |
| 8 | **Перепрыгнули** `smallNotAliveRoad` | `SuperJumpOver` | 🟦 НЕ ПРОВЕРЕНО | |
| 9 | **Столкнулись** с `smallNotAliveRoadAndRoof`, запрыгивая на крышу `bigNotAlive` | `SuperJumpOnRoofDamage` | 🟦 НЕ ПРОВЕРЕНО | |
| 10 | **Столкнулись** с `smallNotAliveRoadAndRoof` на дороге | `SuperJumpDamage` | 🟦 НЕ ПРОВЕРЕНО | |
| 11 | **Перепрыгнули** `smallNotAliveRoadAndRoof` | `SuperJumpOver` | 🟦 НЕ ПРОВЕРЕНО | |
| 12 | **Не задели** препятствий | `SuperJump` | 🟦 НЕ ПРОВЕРЕНО | |

---

## 5. Super Roof Jump Mechanics (супер‑прыжок, когда хомяк уже на крыше `bigNotAlive`)

| # | Условие | Ожидаемый стейт | Статус | Результат |
| - | ---------------------------------------------------------------------------------------------------------------------------- | ----------------------------- | --------------- | --------- |
| 1 | **Прыгнули**, находясь на крыше чистого `bigNotAlive` | `SuperRoofJump` | 🟦 НЕ ПРОВЕРЕНО | |
| 2 | **Прыгнули**, находясь на крыше `bigNotAlive`, и столкнулись с `smallNotAliveRoadAndRoof` | `SuperRoofJumpDamage` | 🟦 НЕ ПРОВЕРЕНО | |
| 3 | **Напрыгнули** на `bigAlive`, спрыгивая с крыши `bigNotAlive` | `SuperJumpOnObstacleFromRoof` | 🟦 НЕ ПРОВЕРЕНО | |
| 4 | **Напрыгнули** на `smallAlive`, спрыгивая с крыши `bigNotAlive` | `SuperJumpOnObstacleFromRoof` | 🟦 НЕ ПРОВЕРЕНО | |
| 5 | Спрыгивая с крыши `bigNotAlive`, **столкнулись** с `smallNotAliveRoad` | `SuperJumpFromRoofDamage` | 🟦 НЕ ПРОВЕРЕНО | |
| 6 | Спрыгивая с крыши `bigNotAlive`, **столкнулись** с `smallNotAliveRoadAndRoof` на дороге | `SuperJumpFromRoofDamage` | 🟦 НЕ ПРОВЕРЕНО | |
| 7 | Спрыгивая с крыши `bigNotAlive`, **не задели** препятствий | `SuperJumpFromRoof` | 🟦 НЕ ПРОВЕРЕНО | |

---

> **Памятка:** при проверке меняйте «🟦 НЕ ПРОВЕРЕНО» на ✔ или ✖ и заполняйте колонку «Результат».

