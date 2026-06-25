# New York daypart pattern draft

Черновик последовательностей для New York после готового Morning-блока.

Источник: `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`.

## Проверенный контекст

Готовые утренние уровни уже используют формат по 8 паттернов:

| Уровень | Последовательность |
|---|---|
| Morning 01 | `easy_run` -> `easy_run` -> `small_jumps` -> `small_jumps` -> `easy_run` -> `medium_difficulty` -> `medium_difficulty_energy` -> `easy_run` |
| Morning 02 | `easy_run` -> `easy_run` -> `small_jumps` -> `small_jumps` -> `easy_run` -> `jump_challenge` -> `jump_challenge` -> `easy_run` |
| Morning 03 | `easy_run_2` -> `small_jumps_2` -> `small_jumps_2` -> `easy_run_2` -> `medium_difficulty_2` -> `jump_challenge_2` -> `peak` -> `easy_run_2` |

## Принятые ограничения

- Каждый новый уровень состоит из 8 gameplay-паттернов.
- Уровень начинается легко, затем вводит механику, наращивает плотность, получает короткую передышку/награду и выходит в сложный участок.
- `tunnel_*` не использованы по запросу.
- Диагностические `test_*` не использованы в боевых набросках. Для передышки используются `easy_run_*`, `bonus_strip_*`, `roof_bonus_run_*`.
- `roof_long_run` тоже не использован в основном наборе: по данным шаблона он заметно длиннее типового паттерна (`x` до 111.6 против примерно 66-73 у большинства).
- Паттерны `shift_zigzag_easy `, `shift_mirror_trap ` и `roof_bonus_chain ` имеют хвостовой пробел в имени в JSON. Я не использую их в черновике, чтобы не закладывать риск ручного переноса.

## День: 5 уровней

### Afternoon 01 - бонусы и умеренный разгон

Цель: мягко продолжить Morning, дать бонусные дорожки и умеренную плотность без jump peak.

| Слот | Паттерн | Роль |
|---:|---|---|
| 1 | `easy_run_2` | лёгкий старт |
| 2 | `bonus_strip` | ранняя награда |
| 3 | `small_jumps_2` | одиночные прыжки |
| 4 | `easy_run_3` | короткая стабилизация |
| 5 | `medium_difficulty` | первая плотность |
| 6 | `bonus_strip_2` | передышка с бонусами |
| 7 | `medium_difficulty_energy` | умеренная финальная связка |
| 8 | `easy_run_2` | мягкий выход |

### Afternoon 02 - первое заманивание на крыши

Цель: показать roof-бонусы и простые узкие промежутки между крышами.

| Слот | Паттерн | Роль |
|---:|---|---|
| 1 | `easy_run` | лёгкий старт |
| 2 | `small_jumps` | базовые прыжки |
| 3 | `roof_bonus_run` | мотивация подняться на крышу |
| 4 | `roof_narrow_gap` | простой roof-gap |
| 5 | `bonus_strip_3` | награда/передышка |
| 6 | `medium_difficulty_2` | средняя плотность |
| 7 | `roof_narrow_gap_2` | повтор roof-gap чуть плотнее |
| 8 | `easy_run_3` | выход |

### Afternoon 03 - первый выбор линии

Цель: ввести lane choice и микс прыжков со смещениями.

| Слот | Паттерн | Роль |
|---:|---|---|
| 1 | `easy_run_3` | лёгкий старт |
| 2 | `small_jumps_3` | прыжковая разминка |
| 3 | `shift_line_choice` | безопасный выбор линии |
| 4 | `medium_difficulty_2` | уплотнение |
| 5 | `bonus_strip` | передышка |
| 6 | `shift_jump_mix` | прыжки плюс смещения |
| 7 | `jump_challenge` | сложный блок дня |
| 8 | `easy_run_2` | выход |

### Afternoon 04 - давление прыжками и первая широкая крыша

Цель: поднять темп, дать первый roof-wide challenge без финального peak.

| Слот | Паттерн | Роль |
|---:|---|---|
| 1 | `easy_run_2` | лёгкий старт |
| 2 | `bonus_strip_2` | ранняя награда |
| 3 | `small_jumps_2` | прыжковая разминка |
| 4 | `medium_difficulty_3` | плотная середина |
| 5 | `easy_run_3` | короткая передышка |
| 6 | `jump_challenge_2` | частые прыжки |
| 7 | `roof_wide_gap` | сложная roof-связка |
| 8 | `bonus_strip_3` | награда после пика |

### Afternoon 05 - дневной мини-финал

Цель: собрать road, shift, roof и первый дневной peak в одном уровне.

| Слот | Паттерн | Роль |
|---:|---|---|
| 1 | `easy_run_3` | лёгкий старт |
| 2 | `small_jumps_3` | прыжковая разминка |
| 3 | `shift_line_choice_2` | выбор линии плотнее |
| 4 | `medium_difficulty_energy` | энергия перед сложной частью |
| 5 | `roof_bonus_run_2` | передышка с roof-бонусом |
| 6 | `jump_challenge_3` | частые прыжки |
| 7 | `peak` | финальный пик дня |
| 8 | `easy_run_2` | выход |

## Вечер: 5 уровней

### Evening 01 - контролируемый roof/shift mix

Цель: вечер начинается сложнее дня, но без резкого скачка после Afternoon 05.

| Слот | Паттерн | Роль |
|---:|---|---|
| 1 | `easy_run` | лёгкий старт |
| 2 | `small_jumps_2` | разминка |
| 3 | `roof_bonus_run_2` | roof-мотивация |
| 4 | `shift_line_choice` | выбор линии |
| 5 | `bonus_strip_2` | передышка |
| 6 | `medium_difficulty_3` | плотная середина |
| 7 | `jump_challenge` | сложный блок |
| 8 | `easy_run_3` | выход |

### Evening 02 - roof gaps и переключение крыши

Цель: развить крышную тему через narrow-gap, switch-line и jump challenge.

| Слот | Паттерн | Роль |
|---:|---|---|
| 1 | `easy_run_2` | лёгкий старт |
| 2 | `bonus_strip_3` | ранняя награда |
| 3 | `roof_narrow_gap_3` | roof-gap плотнее |
| 4 | `medium_difficulty_2` | road-плотность |
| 5 | `roof_bonus_run_3` | передышка на крышах |
| 6 | `roof_switch_line` | смена верхней/нижней крыши |
| 7 | `jump_challenge_2` | сложная прыжковая связка |
| 8 | `easy_run` | выход |

### Evening 03 - плотные смещения

Цель: сделать shift-уровень с пиком, но оставить понятную подготовку.

| Слот | Паттерн | Роль |
|---:|---|---|
| 1 | `easy_run_3` | лёгкий старт |
| 2 | `small_jumps_3` | разминка |
| 3 | `shift_jump_mix` | прыжки плюс смещения |
| 4 | `medium_difficulty_energy` | энергия перед сложной частью |
| 5 | `bonus_strip` | передышка |
| 6 | `shift_zigzag_tight` | плотный zigzag |
| 7 | `peak_2` | вечерний пик |
| 8 | `easy_run_2` | выход |

### Evening 04 - широкие roof-gap под давлением

Цель: усилить roof-тему и связать её с выбором линии.

| Слот | Паттерн | Роль |
|---:|---|---|
| 1 | `easy_run_2` | лёгкий старт |
| 2 | `roof_bonus_run` | roof-мотивация |
| 3 | `roof_narrow_gap_4` | подготовка к сложным крышам |
| 4 | `shift_line_choice_2` | выбор линии |
| 5 | `bonus_strip_2` | передышка |
| 6 | `roof_wide_gap_2` | сложный roof-gap |
| 7 | `jump_challenge_3` | сложный прыжковый блок |
| 8 | `easy_run_3` | выход |

### Evening 05 - вечерний финал

Цель: финальный вечерний уровень с forced switch и peak.

| Слот | Паттерн | Роль |
|---:|---|---|
| 1 | `easy_run_3` | лёгкий старт |
| 2 | `small_jumps_2` | разминка |
| 3 | `medium_difficulty_3` | плотная середина |
| 4 | `roof_switch_line_2` | точные прыжки по краям крыш |
| 5 | `bonus_strip_3` | передышка |
| 6 | `shift_force_switch` | резкая смена линии |
| 7 | `peak_3` | финальный пик вечера |
| 8 | `easy_run` | выход |

## Ночь: 7 уровней

### Night 01 - вход в ночную сложность

Цель: старт ночи уже требует чтения линии, но ещё оставляет привычный выход после peak.

| Слот | Паттерн | Роль |
|---:|---|---|
| 1 | `easy_run_2` | лёгкий старт |
| 2 | `small_jumps_3` | разминка |
| 3 | `shift_line_choice` | выбор линии |
| 4 | `medium_difficulty_3` | плотная середина |
| 5 | `roof_bonus_run_3` | передышка на крышах |
| 6 | `jump_challenge_2` | частые прыжки |
| 7 | `peak` | пик уровня |
| 8 | `easy_run_3` | выход |

### Night 02 - roof rhythm

Цель: почти целиком крышный уровень: бонусы, narrow-gap, цепочка, switch-line.

| Слот | Паттерн | Роль |
|---:|---|---|
| 1 | `easy_run_3` | лёгкий старт |
| 2 | `roof_bonus_run_2` | roof-мотивация |
| 3 | `roof_narrow_gap_4` | плотные узкие крыши |
| 4 | `roof_bonus_chain_2` | ритмичная цепочка бонусов |
| 5 | `bonus_strip_2` | передышка |
| 6 | `roof_wide_gap` | широкие gap |
| 7 | `roof_switch_line` | смена крыши |
| 8 | `easy_run_2` | выход |

### Night 03 - shift traps

Цель: night-уровень про выбор линии и вынужденные смещения.

| Слот | Паттерн | Роль |
|---:|---|---|
| 1 | `easy_run` | лёгкий старт |
| 2 | `small_jumps_2` | разминка |
| 3 | `shift_jump_mix` | прыжки плюс смещения |
| 4 | `shift_line_choice_2` | выбор линии плотнее |
| 5 | `bonus_strip_3` | передышка |
| 6 | `shift_force_switch_2` | forced switch |
| 7 | `peak_2` | пик уровня |
| 8 | `easy_run_3` | выход |

### Night 04 - jump-heavy

Цель: самый прямой прыжковый уровень ночного блока.

| Слот | Паттерн | Роль |
|---:|---|---|
| 1 | `easy_run_2` | лёгкий старт |
| 2 | `small_jumps_3` | разминка |
| 3 | `medium_difficulty_energy` | энергия перед прыжками |
| 4 | `jump_challenge` | первый jump-блок |
| 5 | `bonus_strip_2` | передышка |
| 6 | `jump_challenge_3` | второй jump-блок |
| 7 | `peak_3` | пик уровня |
| 8 | `easy_run` | выход |

### Night 05 - roof plus forced switch

Цель: смешать roof-routing и forced switch, завершить сложным участком без мягкого выхода.

| Слот | Паттерн | Роль |
|---:|---|---|
| 1 | `easy_run_3` | лёгкий старт |
| 2 | `roof_bonus_run_4` | roof-мотивация |
| 3 | `roof_narrow_gap_3` | roof-gap |
| 4 | `shift_bonus_path` | бонусы через правильную линию |
| 5 | `bonus_strip` | передышка |
| 6 | `roof_wide_gap_2` | сложные крыши |
| 7 | `shift_force_switch` | forced switch |
| 8 | `peak_2` | жёсткий финал |

### Night 06 - mirror/choice advanced

Цель: поздний night-уровень с обманным выбором линии, roof-switch и peak.

| Слот | Паттерн | Роль |
|---:|---|---|
| 1 | `easy_run_2` | лёгкий старт |
| 2 | `small_jumps_2` | разминка |
| 3 | `shift_mirror_trap_2` | обманный выбор линии |
| 4 | `medium_difficulty_3` | плотная середина |
| 5 | `roof_bonus_run_3` | передышка на крышах |
| 6 | `roof_switch_line_2` | точный roof-switch |
| 7 | `jump_challenge_2` | jump pressure |
| 8 | `peak_3` | жёсткий финал |

### Night 07 - ночной финал

Цель: финальный уровень без финальной разрядки: wide roof-gap, forced switch и двойной peak.

| Слот | Паттерн | Роль |
|---:|---|---|
| 1 | `easy_run_3` | лёгкий старт |
| 2 | `bonus_strip_3` | ранняя награда |
| 3 | `roof_wide_gap_3` | сложная roof-связка |
| 4 | `shift_force_switch_2` | forced switch |
| 5 | `easy_run_2` | короткая передышка перед финалом |
| 6 | `jump_challenge_3` | финальный jump-разгон |
| 7 | `peak_2` | первый peak |
| 8 | `peak_3` | второй peak |

## Сводка по progression

| Часть дня | Кол-во уровней | Основной фокус | Пики |
|---|---:|---|---|
| Afternoon | 5 | бонусы, первые крыши, первые shift-choice | `peak` только в Afternoon 05 |
| Evening | 5 | roof-switch, tight shift, wide roof-gap | `peak_2` в Evening 03, `peak_3` в Evening 05 |
| Night | 7 | roof routing, forced switch, mirror trap, jump pressure | peak почти в каждом уровне, финал с double peak |

## Кандидаты на ручное решение

- Если нужен настоящий пустой relief-паттерн, лучше завести production-имя `relief` или переименовать `test_relief`, чтобы не тащить `test_*` в боевые уровни.
- Если хочется использовать `roof_long_run`, его лучше ставить вручную в отдельный уровень или считать как более длинный слот, потому что он заметно длиннее обычных шаблонов.
- Стоит почистить хвостовые пробелы в именах `shift_zigzag_easy `, `shift_mirror_trap `, `roof_bonus_chain ` перед массовым переносом последовательностей в JSON.
