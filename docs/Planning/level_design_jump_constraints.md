# Level Design: Jump Constraints per Strategy

## Назначение

Документ фиксирует, какое количество и какие типы препятствий хомяк способен преодолеть в каждом типе прыжка / стратегии без потери жизни.

Используется для:
- проектирования уровней (валидные паттерны препятствий);
- настройки логики бота (какие стратегии применимы в конкретной ситуации).

---

## Термин: «препятствие»

Под «препятствием» в этом документе подразумевается любой obstacle, **не являющийся**:
- крышей (`bigNotAlive`, `mediumNotAlive`);
- коллектаблом (`collectableEnergetic`, `collectablePizza`, `collectableCrystal`, `collectableLife`, `collectableCoin`).


###                                     список ситуаций

<br>
---

### Jump Over - 1 препятствие

### Super Jump Over - 2 препятствий

<br>
---

### Roof Jump Over - 1 препятствие

### Super Roof Jump Over - 2 препятствий

<br>
---

### Jump On Roof - 1 препятствие

### Super Jump On Roof - 2 препятствий

<br>
---

### Jump From Roof - 2 препятствий

### Super Jump From Roof - 3 препятствий

<br>
---

### Jump On From Roof - 1 target `smallAlive` / `bigAlive`

Действие уничтожает один дорожный target при сходе с крыши на дорогу. Остальные препятствия вокруг target только ограничивают окно запуска и post-action safety; фактический travel и количество покрываемых объектов подтверждаются runtime-прогоном.

### Super Jump On From Roof - 1 target `smallAlive` / `bigAlive`

Действие уничтожает один дорожный target при super-сходе с крыши на дорогу. Остальные препятствия вокруг target только ограничивают окно запуска и post-action safety; фактический travel и количество покрываемых объектов подтверждаются runtime-прогоном.

<br>
---

### Jump From Roof On Roof - 2 препятствий

### Super Jump From Roof On Roof - 3 препятствий

<br>
---

### Jump On - 1 препятствие

### Super Jump On - 2 препятствие

<br>
---
