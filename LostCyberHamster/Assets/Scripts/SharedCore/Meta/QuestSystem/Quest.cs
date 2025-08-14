using System;

namespace Vues.GameCore
{
    [Serializable]
    public class Quest
    {
        /// <summary>
        /// Идентификатор квеста
        /// </summary>
        public string Id;

        /// <summary>
        /// Название квеста
        /// </summary>
        public string Title;

        /// <summary>
        /// Описание квеста
        /// </summary>
        public string Description;

        /// <summary>
        /// Выполнено ли условие квеста
        /// </summary>
        public bool IsCompleted;

        /// <summary>
        /// Сколько нужно выполнить
        /// </summary>
        public int TargetAmount;

        /// <summary>
        /// Сколько выполнено из условия квеста
        /// </summary>
        public int CurrentAmount;

        /// <summary>
        /// Тип награды
        /// </summary>
        public int RewardTypeId;

        /// <summary>
        /// Количество награды
        /// </summary>
        public int RewardAmount;
        
        /// <summary>
        /// Тип действия
        /// </summary>
        public string ActionTypeString;
        public bool IsRewardRecieved;


        public ActionTypeEnum ActionType => Enum.TryParse(ActionTypeString, out ActionTypeEnum result)
                                         ? result
                                         : ActionTypeEnum.None;
        public ResourceType RewardType => (ResourceType)RewardTypeId;

        public void Progress(int progressAmount)
        {
            CurrentAmount += progressAmount;
            if (CurrentAmount >= TargetAmount)
            {
                IsCompleted = true;
            }
        }

    }

}
