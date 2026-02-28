# ТЗ #2: Модуль автоматического самообучения бота (Bot Self-Learning)

**Версия:** 1.0  
**Дата:** 2026-02-28  
**Автор:** AI Assistant

---

## 1. Обзор

После внедрения системы **Play Styles** (ТЗ #1), бот получит набор параметризованных стратегий.
Модуль самообучения (BotLearningModule) предназначен для **автоматического тюнинга (fine-tuning)** этих параметров (весов и порогов) на основе результатов прохождения уровней (post-session analysis).

Это не нейросеть (online learning), а offline parameter tuning с сохранением истории (evolution/GA).

### 1.1 Цели

1. Улучшение качества игры бота без ручного вмешательства.
2. Адаптация бота к различным уровням (уровень 1 может требовать других весов, чем уровень 5).
3. Достижение целевых метрик для каждого PlayStyle.

---

## 2. Архитектура модуля

Модуль состоит из 4 основных компонентов:

```
BotLearningModule/
├── 1. SessionAnalyzer      — Парсит логи и игровые события после Game Over, вычисляет метрики (Fitness Score).
├── 2. ParameterTuner       — Изменяет параметры (мутации) на основе результатов SessionAnalyzer.
├── 3. GenomeManager        — Хранит и загружает текущие лучшие наборы весов (геномы) из JSON.
└── 4. LearningOrchestrator — Связывает всё вместе, управляет циклом "Игра -> Анализ -> Мутация -> Сохранение".
```

---

## 3. Компоненты детально

### 3.1 SessionAnalyzer (Анализатор сессии)

Запускается по событию `GameManager.OnFinish`.

**Входные данные:**
- `BotSessionReport` (время жизни, собранные монеты, причины смертей, использование Улты/покупок).
- Текущий `BotPlayStyle`.
- Текущий набор параметров (`BotPlayStyleConfig`).

**Выходные данные:** `FitnessScore` (очки успешности) и `FailReasons` (причины неудач).

**Расчёт FitnessScore (зависит от PlayStyle):**
- Для `ThreeStars`: `LivesLeft * 1000 + TimeAlive * 10 - ObstaclesHit * 500`.
- Для `BonusHunter`: `CoinsCollected * 100 + CrystalsCollected * 500 + LivesLeft * 100`.
- Для `UltaMaster`: `UltaUses * 500 + LivesLeft * 200`.

**Fail Reasons (триггеры для мутаций):**
- `DiedToBigAlive`: Смерть от bigAlive.
- `EnergyDepleted`: Умер при Energy < 10.
- `MissedOpportunities`: Пропущено много монет (для Bonus Hunter).
- `UnusedResources`: Закончил игру с 300+ монетами, но умер (для GodMode).

### 3.2 ParameterTuner (Тюнер параметров / Мутатор)

Интеллектуально (или случайно в заданных границах) изменяет геном (`BotPlayStyleConfig`) перед следующей игрой.

**Методы тюнинга:**
1. **Targeted Mutation (Целевая мутация):**
   - Если `FailReason == EnergyDepleted`, то `EnergyConserveThreshold += 5` или `WeightEnergy += 1.0`.
   - Если `FailReason == DiedToBigAlive`, то `UrgentWindowSec += 0.1` (реагировать раньше).
2. **Random Mutation (Случайная мутация):**
   - Для избежания локальных оптимумов, с 10% шансом слегка изменяет 1-2 случайных параметра (`WeightCollectibles ± 0.5`).

Ограничить диапазоны параметров (например, `UrgentWindowSec` только в пределах `[0.3, 1.2]`).

### 3.3 GenomeManager (Менеджер геномов)

Управляет сохранением и версионированием парамтеров. Данные хранятся в `Application.persistentDataPath + "/BotGenomes/"`.

**Структура JSON (BotGenome.json):**
```json
{
  "Level": "Level_1",
  "PlayStyle": "ThreeStars",
  "Generation": 14,
  "BestFitness": 3450.5,
  "CurrentConfig": {
    "WeightSurvival": 15.5,
    "WeightEnergy": 4.2,
    "UrgentWindowSec": 0.85,
    "EnergyConserveThreshold": 55
  },
  "History": [ ...массив предыдущих поколений для графика... ]
}
```

### 3.4 LearningOrchestrator (Оркестратор обучения)

Рабочий цикл (Loop):
1. **Начало уровня:** `GenomeManager` загружает лучший геном для текущего уровня и `PlayStyle`.
2. **Если режим обучения включён (`IsTrainingMode = true`):**
   - `ParameterTuner` создаёт "мутированную" версию генома.
   - `HamsterBot` инициализируется с этим новым `BotPlayStyleConfig`.
3. **Геймплей:** Бот играет.
4. **Game Over:**
   - `SessionAnalyzer` считает `FitnessScore` мутированного генома.
   - Если `NewFitness > BestFitness`, новый геном сохраняется как лучший (`GenomeManager.SaveAsBest()`).
   - Если `NewFitness <= BestFitness`, изменение откатывается.
5. **Авто-рестарт:** `LevelController.Instance.Replay()`.

---

## 4. Интеграция с BotBrain и Planning

- Модуль обучения будет тюнить **Weights** для `IStateEvaluator` в дереве планирования (`BotPlanner`).
- Если `BotPlanner` включён (`EnablePlanner = true`), то изменение весов (`WeightSurvival`, `WeightCollectibles`) даст максимальный эффект, т.к. планер будет выбирать ветвь, максимизирующую эти веса на N шагов вперёд.

**Тюнинг дерева решений бота:**
- Помимо весов планировщика, тюнятся пороги для реактивной системы (например, `BuyEnergyThreshold`).
- Пример эволюции: Бот понял, что в режиме `GodMode` покупка энергии выгодна при `Energy < 50`, а не `30`, так как это повышает `FitnessScore` (снижает риск смерти от нехватки энергии на поздних этапах уровня).

---

## 5. UI и Визуализация обучения

В `HamsterBotUI` добавить отдельную вкладку **Learning**:
- Индикатор `Generation: 42`.
- График (или просто текстовый вывод) `Current Fitness vs Best Fitness`.
- Статус: `Mutating UrgentWindowSec from 0.6 -> 0.7`.
- Кнопки: `[Start Training Loop]`, `[Stop Training]`, `[Reset Genome]`.

---

## 6. План внедрения (Этапы)

### Этап 1: Сбор данных и Fitness (1-2 дня)
- Создать `BotSessionReport` сборщик полных данных по окончанию игры.
- Реализовать `SessionAnalyzer` и калькулятор `FitnessScore` для каждого `PlayStyle`.
- Вывести `FitnessScore` в лог сессии.

### Этап 2: Сохранение и загрузка генома (1 день)
- Создать `GenomeManager`, сериализацию/десериализацию `BotPlayStyleConfig` в JSON.
- Убедиться, что `BotBrain` может на лету (OnSceneLoaded) подхватывать загруженные параметры из JSON, а не из ScriptableObject.

### Этап 3: Мутации и Оркестрация (2-3 дня)
- Написать `ParameterTuner` (базовые правила случайных и целевых мутаций).
- Написать `LearningOrchestrator`, связывающий загрузку -> игру -> оценку -> мутацию -> рестарт.
- Включить авто-рестарт уровня на скорости x3 (`Time.timeScale = 3` или `Consts.GameSpeedBase *= 3` для быстрого обучения).

### Этап 4: Интеграция с UI и тестирование (1 день)
- Вывести данные в `HamsterBotUI`.
- Провести тестовый прогон (оставить бота на ночь на уровне 1, проверить, улучшились ли результаты).

---

## 7. Оценка трудозатрат

Общее время: **~5-7 дней** плотной разработки. Около 20-25 часов.
Модуль самообучения требует полностью рабочего и протестированного ТЗ #1 (Режимы игры бота и экономика).
