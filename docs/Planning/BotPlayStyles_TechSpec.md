# ТЗ #1: Режимы игры бота (Play Styles)

**Версия:** 1.0  
**Дата:** 2026-02-28  
**Автор:** AI Assistant

---

## 1. Обзор

Текущий бот имеет один режим `Play` с фиксированной стратегией. Необходимо реализовать систему **Play Styles** — набор предустановленных стратегий, отражающих разные стили прохождения уровня.

### 1.1 Цели

1. Бот должен понимать **игровую экономику** (жизни, энергия, бонусы, монеты, Улта, покупки)
2. Разные стили = разные **приоритеты** и **веса** в дереве решений
3. База для будущего **модуля самообучения** (ТЗ #2)

---

## 2. Анализ текущего состояния

### 2.1 Текущее дерево решений (BotBrain.Evaluate)

```
Priority 1: Нерабочие состояния (Dead, Damaged, Shifting) → DoNothing
Priority 2: RoofRun → EvaluateRoofRun (прыжок через препятствия на крыше)
Priority 3: InJump → EvaluateWhileJumping (SuperJump только для bigAlive)
Priority 4: UrgentThreat → HandleUrgentThreat (реакция на опасность в окне 0.6s)
Priority 5: Ulta → UseUlta (когда 100% и кластер опасностей ≥2 или lives≤1)
Priority 6: JumpOnSmall → Jump на smallAlive для бонусов (если энергия ≥40)
Priority 7: RoofJump → Jump на bigNotAlive для крыши (если энергия ≥40)
Priority 8: Collectibles → SwitchLane для сбора (если другая линия безопасна)
Priority 9: DoNothing
```

### 2.2 Что бот понимает сейчас

| Аспект | Понимание | Комментарий |
|--------|-----------|-------------|
| Угрозы | ✅ Хорошо | Типы: smallAlive, bigAlive, smallNotAlive*, bigNotAlive. Опасные vs безопасные |
| Энергия | ⚠️ Частично | Знает стоимость прыжков (10/20). Экономит при пороге 30. НЕ знает о покупке |
| Жизни | ⚠️ Частично | Использует Улту при lives≤1. НЕ оптимизирует сохранение 3 жизней |
| Улта | ⚠️ Частично | Активирует при 100% + кластер. НЕ знает о покупке маны |
| Бонусы | ⚠️ Частично | Прыгает на smallAlive. НЕ приоритизирует монеты/кристаллы целенаправленно |
| Покупки | ❌ Нет | Не использует BuyEnergy (50 coins → 100 energy) и BuyUltra (100 coins → 100% ulta) |
| Коллектиблы | ⚠️ Частично | Меняет линию для сбора, есть приоритеты: life > crystal > energetic > pizza > coin |

### 2.3 Чего не хватает (Gap Analysis)

1. **Покупки во время игры:**
   - `BuyEnergy` — 50 монет за 100 энергии
   - `BuyUltra` — 100 монет за 100% ульты
   - Бот НЕ содержит действия `BuyEnergy` / `BuyUltra` в `BotAction` enum

2. **Агрессивный сбор бонусов:**
   - Нет активного поиска монет/кристаллов
   - Нет trade-off: риск vs награда

3. **Оптимизация жизней:**
   - Нет режима "0 смертей"
   - Нет превентивного избегания рисков

4. **Планирование (BotPlanner):**
   - Существует, но `EnablePlanner = false` по умолчанию
   - Не используется в реальном геймплее

5. **Ресурсный менеджмент:**
   - Бот не знает сколько монет у игрока
   - Не может принять решение о покупке

---

## 3. Режимы игры (Play Styles)

### 3.1 Enum расширение

```csharp
public enum BotPlayStyle
{
    /// <summary>Выжить любой ценой (завершить уровень)</summary>
    Survival,

    /// <summary>Выжить без потери жизней (3 звезды)</summary>
    ThreeStars,

    /// <summary>Охота за бонусами (максимум монет/кристаллов)</summary>
    BonusHunter,

    /// <summary>3 звезды + максимум бонусов</summary>
    Perfectionist,

    /// <summary>Активное использование Улты</summary>
    UltaMaster,

    /// <summary>Режим "Бог" — всё по максимуму, с покупками</summary>
    GodMode
}
```

### 3.2 Описание режимов

#### 3.2.1 Survival (Выживание)

**Цель:** Завершить уровень, даже с 1 жизнью

**Приоритеты:**
- Выживание: `10.0` (максимум)
- Энергия: `5.0` (экономить для экстренных прыжков)
- Бонусы: `0.5` (только если безопасно)
- Позиция: `2.0` (предпочитать нижнюю линию = контроль)

**Поведение:**
- `_aggressionLevel = 0.3` (осторожный)
- `_energyConserveThreshold = 40` (больший резерв)
- Улта только при `lives ≤ 1`
- Избегать рисков, не охотиться за бонусами

#### 3.2.2 ThreeStars (Три звезды)

**Цель:** Пройти уровень с 3 жизнями

**Приоритеты:**
- Выживание: `15.0` (ещё выше!)
- Энергия: `4.0`
- Бонусы: `1.0`
- Позиция: `3.0`

**Поведение:**
- `_aggressionLevel = 0.2` (очень осторожный)
- `_energyConserveThreshold = 50`
- Улта при `lives ≤ 2` или `dangerCount ≥ 3`
- `_urgentWindowSec = 0.8` (раньше реагировать)
- Превентивная смена линии при любой угрозе

#### 3.2.3 BonusHunter (Охотник за бонусами)

**Цель:** Собрать максимум монет и кристаллов

**Приоритеты:**
- Выживание: `5.0`
- Энергия: `2.0`
- Бонусы: `10.0` (максимум!)
- Позиция: `0.5`

**Поведение:**
- `_aggressionLevel = 0.9` (агрессивный)
- `_energyConserveThreshold = 20` (меньше экономить)
- Активно прыгать на smallAlive для монет
- Активно менять линии для коллектиблов
- Использовать `BuyEnergy` когда энергия < 30 и есть монеты

#### 3.2.4 Perfectionist (Перфекционист)

**Цель:** 3 звезды + максимум бонусов

**Приоритеты:**
- Выживание: `12.0`
- Энергия: `3.0`
- Бонусы: `8.0`
- Позиция: `2.0`

**Поведение:**
- `_aggressionLevel = 0.6` (сбалансированный)
- `_energyConserveThreshold = 35`
- Прыгать на smallAlive только если безопасно
- Собирать бонусы только при низком риске
- Использовать Улту при `lives ≤ 2` и `dangerCount ≥ 2`

#### 3.2.5 UltaMaster (Мастер Улты)

**Цель:** Максимально использовать суперудары

**Приоритеты:**
- Выживание: `8.0`
- Энергия: `2.0`
- Бонусы: `5.0`
- Улта: `10.0` (новый вес!)

**Поведение:**
- `_aggressionLevel = 0.8`
- `_ultaClusterThreshold = 1` (использовать сразу как готова)
- Активно прыгать на smallAlive для зарядки Улты
- Использовать `BuyUltra` если 100 монет есть и Улта < 50%

#### 3.2.6 GodMode (Режим Бога)

**Цель:** Идеальное прохождение — 3 звезды + все бонусы + активные покупки

**Приоритеты:**
- Выживание: `12.0`
- Энергия: `4.0`
- Бонусы: `9.0`
- Позиция: `2.0`
- Улта: `7.0`

**Поведение:**
- `_aggressionLevel = 0.7`
- `_energyConserveThreshold = 30`
- Активно использовать покупки:
  - `BuyEnergy` при energy < 40 и coins > 100
  - `BuyUltra` при ulta < 70% и coins > 150 и потенциальная угроза
- Использовать BotPlanner (`EnablePlanner = true`) для многошагового планирования
- Оптимальный баланс всех метрик

---

## 4. Архитектура изменений

### 4.1 Новые/изменённые файлы

```
Assets/Scripts/Bot/
├── BotPlayStyle.cs           # NEW: enum режимов
├── BotPlayStyleConfig.cs     # NEW: параметры для каждого стиля
├── BotAction.cs              # MODIFY: добавить BuyEnergy, BuyUltra
├── BotBrain.cs               # MODIFY: принимать PlayStyle, менять веса
├── BotResourceManager.cs     # NEW: отслеживает coins, делает покупки
├── HamsterBot.cs             # MODIFY: хранить текущий PlayStyle
└── Planning/
    ├── BotPlanner.cs         # MODIFY: учитывать PlayStyle
    └── IStateEvaluator.cs    # MODIFY: PlayStyle-aware evaluator
```

### 4.2 BotPlayStyleConfig

```csharp
[CreateAssetMenu(fileName = "BotPlayStyleConfig", menuName = "Bot/Play Style Config")]
public class BotPlayStyleConfig : ScriptableObject
{
    public BotPlayStyle Style;

    [Header("Weights")]
    public float WeightSurvival = 10f;
    public float WeightEnergy = 3f;
    public float WeightCollectibles = 2f;
    public float WeightPosition = 1f;
    public float WeightUlta = 2f;

    [Header("Behavior")]
    public float AggressionLevel = 0.7f;
    public float UrgentWindowSec = 0.6f;
    public int EnergyConserveThreshold = 30;
    public int UltaClusterThreshold = 2;

    [Header("Purchases")]
    public bool AllowBuyEnergy = false;
    public int BuyEnergyThreshold = 40;       // покупать когда energy < X
    public int BuyEnergyCoinMinimum = 100;    // минимум монет для покупки

    public bool AllowBuyUltra = false;
    public int BuyUltraThreshold = 50;        // покупать когда ulta < X%
    public int BuyUltraCoinMinimum = 150;

    [Header("Planner")]
    public bool EnablePlanner = false;
    public int PlannerDepth = 3;
}
```

### 4.3 BotResourceManager

```csharp
public class BotResourceManager
{
    public int CurrentCoins => ResourceManager.GetResource(ResourceType.Coins);

    public bool CanBuyEnergy() => CurrentCoins >= 50;
    public bool CanBuyUltra() => CurrentCoins >= 100;

    public void BuyEnergy(Hamster hamster)
    {
        if (!CanBuyEnergy()) return;
        ResourceManager.SpendResource(ResourceType.Coins, 50);
        hamster.AddEnergy(100);
    }

    public void BuyUltra(Hamster hamster)
    {
        if (!CanBuyUltra()) return;
        ResourceManager.SpendResource(ResourceType.Coins, 100);
        hamster.AddUltaCharge(100);
    }
}
```

### 4.4 Расширение BotAction

```csharp
public enum BotAction
{
    None,
    SwitchLane,
    Jump,
    SuperJump,
    RoofJump,
    SuperRoofJump,
    UseUlta,
    BuyEnergy,    // NEW
    BuyUltra      // NEW
}
```

### 4.5 Изменения в BotBrain

```csharp
public class BotBrain
{
    private readonly BotPlayStyleConfig _styleConfig;
    private readonly BotResourceManager _resourceManager;

    public BotBrain(BotPlayStyleConfig styleConfig, BotResourceManager resourceManager = null)
    {
        _styleConfig = styleConfig;
        _resourceManager = resourceManager ?? new BotResourceManager();
        // инициализация из _styleConfig вместо hardcoded значений
    }

    public BotDecision Evaluate(...)
    {
        // ... existing priorities ...

        // NEW: Priority для покупок (между Ulta и JumpOnSmall)
        if (_styleConfig.AllowBuyEnergy &&
            hamster.Energy.Value < _styleConfig.BuyEnergyThreshold &&
            _resourceManager.CurrentCoins >= _styleConfig.BuyEnergyCoinMinimum)
        {
            return BotDecision.Tactical(BotAction.BuyEnergy,
                $"buying energy (current={hamster.Energy.Value}, coins={_resourceManager.CurrentCoins})");
        }

        if (_styleConfig.AllowBuyUltra &&
            hamster.UltaChargeAmount.Value < _styleConfig.BuyUltraThreshold &&
            _resourceManager.CurrentCoins >= _styleConfig.BuyUltraCoinMinimum)
        {
            return BotDecision.Tactical(BotAction.BuyUltra,
                $"buying ulta (current={hamster.UltaChargeAmount.Value}%, coins={_resourceManager.CurrentCoins})");
        }

        // ... rest of priorities ...
    }
}
```

---

## 5. Интеграция

### 5.1 UI для выбора стиля

В `HamsterBotUI.OnGUI()` добавить:
- Dropdown для выбора PlayStyle
- Отображение текущих весов
- Отображение монет и возможности покупки

### 5.2 Горячие клавиши

- `F1` — вкл/выкл бота (существует)
- `F2` — цикл режимов Play/Test/Analytics (существует)
- `F3` — цикл PlayStyle (Survival → ThreeStars → BonusHunter → ...)

### 5.3 Логирование

Расширить `BotLogger`:
- Логировать текущий PlayStyle
- Логировать покупки (BuyEnergy, BuyUltra)
- Логировать баланс монет

---

## 6. Тестирование

### 6.1 Acceptance Criteria

| Режим | Критерий успеха |
|-------|-----------------|
| Survival | Завершение уровня 1 хотя бы с 1 жизнью в 95% случаев |
| ThreeStars | Завершение уровня 1 с 3 жизнями в 80% случаев |
| BonusHunter | Сбор ≥80% доступных монет |
| Perfectionist | 3 жизни + ≥60% монет |
| UltaMaster | Использование Улты ≥3 раз за уровень |
| GodMode | 3 жизни + ≥70% монет + ≥2 покупки + ≥2 Улты |

### 6.2 Метрики для логов

- `livesLost`: потеряно жизней
- `coinsCollected`: собрано монет
- `ultaUsed`: использований Улты
- `energyPurchased`: покупок энергии
- `ultaPurchased`: покупок Улты
- `levelCompleted`: завершён ли уровень
- `completionTime`: время прохождения

---

## 7. Оценка трудозатрат

| Задача | Часы |
|--------|------|
| BotPlayStyle enum + BotPlayStyleConfig | 1 |
| BotResourceManager | 1 |
| Расширение BotAction + HamsterBot execution | 1 |
| Изменения BotBrain (параметризация) | 3 |
| Изменения BotPlanner/Evaluator | 2 |
| HamsterBotUI (отображение стиля) | 1 |
| Создание preset-конфигов для 6 стилей | 2 |
| Тестирование и тюнинг | 4 |
| **Итого** | **~15 часов** |

---

## 8. Зависимости

- ТЗ #2 (Модуль самообучения) будет использовать веса из PlayStyleConfig
- После реализации — веса можно будет автоматически корректировать

---

## 9. Риски

| Риск | Вероятность | Импакт | Митигация |
|------|-------------|--------|-----------|
| GodMode слишком сложный | Средняя | Средний | Начать с Survival/ThreeStars, GodMode последним |
| Покупки ломают баланс | Низкая | Высокий | Тщательные пороги, тестирование |
| BotPlanner слишком медленный | Низкая | Средний | Профилирование, отключение для мобилок |

---

## 10. Следующие шаги

1. ✅ Анализ текущего состояния (этот документ)
2. ⬜ Реализация BotPlayStyle + Config
3. ⬜ Реализация BotResourceManager
4. ⬜ Параметризация BotBrain
5. ⬜ Интеграция в HamsterBot
6. ⬜ Тестирование каждого стиля
7. ⬜ Перейти к ТЗ #2 (Самообучение)
