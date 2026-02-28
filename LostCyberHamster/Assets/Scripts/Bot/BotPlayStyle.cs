namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стиль игры бота — определяет приоритеты и поведение.
    /// </summary>
    public enum BotPlayStyle
    {
        /// <summary>Выжить любой ценой (завершить уровень хотя бы с 1 жизнью).</summary>
        Survival,

        /// <summary>Выжить без потери жизней (3 звезды).</summary>
        ThreeStars,

        /// <summary>Охота за бонусами (максимум монет/кристаллов).</summary>
        BonusHunter,

        /// <summary>3 звезды + максимум бонусов.</summary>
        Perfectionist,

        /// <summary>Активное использование суперударов.</summary>
        UltaMaster,

        /// <summary>Режим "Бог" — всё по максимуму, с покупками.</summary>
        GodMode
    }
}
