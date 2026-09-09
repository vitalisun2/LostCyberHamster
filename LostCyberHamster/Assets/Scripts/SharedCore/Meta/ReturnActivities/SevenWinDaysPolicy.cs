using System;
using System.Linq;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Начисляет один шаг за новую дату; Claim не участвует в переходе цикла.</summary>
    public static class SevenWinDaysPolicy
    {
        public static ActivityReward Apply(ReturnActivityState state, string day, ReturnActivityConfig config)
        {
            if (string.CompareOrdinal(day, state.LastCreditedDay) <= 0) return null;

            // Новый цикл закрепляет состав, предыдущие earned остаются в очереди.
            if (state.TotalDays % 7 == 0)
            {
                state.CycleRewards = config.Days.Select(reward => reward.Copy()).ToList();
                state.CycleConfigVersion = config.Version;
            }
            state.TotalDays = checked(state.TotalDays + 1);
            state.LastCreditedDay = day;
            var amount = state.CycleRewards[state.Step - 1];
            var reward = new ActivityReward
            {
                Id = $"{state.Epoch}/cycle/{state.Cycle}/day/{state.Step}", Kind = "cycle",
                OriginDay = day, Cycle = state.Cycle, Step = state.Step,
                ConfigVersion = state.CycleConfigVersion, Coins = amount.Coins, Gems = amount.Gems
            };
            state.Rewards.Add(reward);
            return reward;
        }
    }
}
