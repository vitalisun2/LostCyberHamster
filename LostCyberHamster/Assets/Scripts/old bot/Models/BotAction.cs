namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Семантические действия (манёвры), которые может выбрать планер бота.
    /// Каждое действие отражает реальное поведение из *Mechanics и имеет однозначное ожидаемое состояние.
    /// </summary>
    public enum BotAction
    {
        None,

        // === Дорога: смена линии
        SwitchLane,

        // === Дорога: прыжки (перепрыгнуть препятствие)
        JumpOver,           // Перепрыгнуть small препятствие (smallNotAliveRoad, smallNotAliveRoadAndRoof)
        JumpOnRoof,         // Запрыгнуть на крышу (bigNotAlive, mediumNotAlive) — skeleton
        JumpOnTarget,       // Напрыгнуть на alive врага (smallAlive) — skeleton

        // === Дорога: суперпрыжки
        SuperJump,          // Суперпрыжок: перелететь big врага или запрыгнуть на крышу/врага
        SuperJumpOnRoof,    // Суперпрыжком запрыгнуть на крышу (bigNotAlive, mediumNotAlive) — skeleton
        SuperJumpOnTarget,  // Суперпрыжком напрыгнуть на alive (smallAlive) — skeleton

        // === Крыша: прыжки на крыше
        RoofJumpOver,       // Перепрыгнуть на крыше (smallNotAliveRoadAndRoof on roof)
        RoofJumpDown,       // Спрыгнуть с крыши на дорогу (нет препятствий впереди)
        RoofJumpToRoof,     // Перепрыгнуть на другую крышу (другую bigNotAlive/mediumNotAlive)
        RoofJumpOnTarget,   // Напрыгнуть на alive с крыши (smallAlive, bigAlive)

        // === Крыша: суперпрыжки на крыше
        RoofSuperJumpOver,  // Суперпрыжком перепрыгнуть на крыше (smallNotAliveRoadAndRoof on roof)
        RoofSuperJumpDown,  // Суперпрыжком спрыгнуть с крыши (нет препятствий)
        RoofSuperJumpToRoof, // Суперпрыжком на другую крышу (bigNotAlive, mediumNotAlive)
        RoofSuperJumpOnTarget, // Суперпрыжком напрыгнуть на alive с крыши (smallAlive, bigAlive)

        // === Крыша: смена линии
        RoofSwitchLane,     // Сместиться с крыши на другую линию или спрыгнуть на дорогу
    }
}
