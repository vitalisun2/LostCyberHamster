namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Перечень возможных действий бота.
    /// </summary>
    public enum BotAction
    {
        /// <summary>Ничего не делать.</summary>
        None,

        /// <summary>Тап — сменить линию (верх ↔ низ).</summary>
        SwitchLane,

        /// <summary>Обычный прыжок.</summary>
        Jump,

        /// <summary>Суперпрыжок (двойной, из воздуха).</summary>
        SuperJump,

        /// <summary>Прыжок с крыши bigNotAlive.</summary>
        RoofJump,

        /// <summary>Суперпрыжок с крыши.</summary>
        SuperRoofJump,

        /// <summary>Активировать ульта-способность.</summary>
        UseUlta,

        /// <summary>Купить энергию за монеты (50 coins → 100 energy).</summary>
        BuyEnergy,

        /// <summary>Купить ульту за монеты (100 coins → 100% ulta).</summary>
        BuyUlta
    }
}
