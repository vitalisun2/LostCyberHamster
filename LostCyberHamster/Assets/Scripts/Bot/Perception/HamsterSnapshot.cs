using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Perception
{
    /// <summary>
    /// Хранит состояние хомяка в момент построения snapshot.
    /// </summary>
    public sealed class HamsterSnapshot
    {
        /// <summary>
        /// Создает snapshot состояния хомяка для planning-слоя.
        /// </summary>
        public HamsterSnapshot(
            HamsterStateEnum hamsterState,
            bool isOnBottomLine,
            bool isOnRoof,
            int energy,
            int lives,
            bool isDamaged,
            bool isShifting,
            int? roofSupportInstanceId,
            float hamsterLeftX,
            float hamsterRightX)
        {
            HamsterState = hamsterState;
            IsOnBottomLine = isOnBottomLine;
            IsOnRoof = isOnRoof;
            Energy = energy;
            Lives = lives;
            IsDamaged = isDamaged;
            IsShifting = isShifting;
            RoofSupportInstanceId = roofSupportInstanceId;
            HamsterLeftX = hamsterLeftX;
            HamsterRightX = hamsterRightX;
        }

        /// <summary>
        /// Runtime-состояние хомяка.
        /// </summary>
        public HamsterStateEnum HamsterState { get; }

        /// <summary>
        /// Признак нижней линии.
        /// </summary>
        public bool IsOnBottomLine { get; }

        /// <summary>
        /// Признак roof-режима.
        /// </summary>
        public bool IsOnRoof { get; }

        /// <summary>
        /// Текущая энергия.
        /// </summary>
        public int Energy { get; }

        /// <summary>
        /// Текущее число жизней.
        /// </summary>
        public int Lives { get; }

        /// <summary>
        /// Признак полученного урона.
        /// </summary>
        public bool IsDamaged { get; }

        /// <summary>
        /// Признак смены линии.
        /// </summary>
        public bool IsShifting { get; }

        /// <summary>
        /// Instance id roof-obstacle, который runtime использует как текущую support-платформу.
        /// </summary>
        public int? RoofSupportInstanceId { get; }

        /// <summary>
        /// Левая граница хомяка.
        /// </summary>
        public float HamsterLeftX { get; }

        /// <summary>
        /// Правая граница хомяка.
        /// </summary>
        public float HamsterRightX { get; }

        /// <summary>
        /// Центр хомяка по X.
        /// </summary>
        public float CenterX => (HamsterLeftX + HamsterRightX) * 0.5f;

        /// <summary>
        /// Ширина хомяка.
        /// </summary>
        public float Width => HamsterRightX - HamsterLeftX;

    }
}
