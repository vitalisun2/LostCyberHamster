using System;
using System.Globalization;
using GameManagement;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Читает поддерживаемые постоянные состояния напрямую из данных игрока.
    /// </summary>
    public static class PlayerStateValueResolver
    {
        /// <summary>
        /// Возвращает текущее значение состояния указанной сущности.
        /// </summary>
        public static bool TryGetCurrentValue(
            PlayerData playerData,
            string stateId,
            string entityId,
            out int value)
        {
            value = 0;
            if (playerData == null ||
                string.IsNullOrWhiteSpace(stateId) ||
                string.IsNullOrWhiteSpace(entityId))
            {
                return false;
            }

            switch (stateId)
            {
                case PlayerStateIds.PlayerLevel:
                    if (entityId != PlayerStateEntityIds.Player)
                    {
                        return false;
                    }

                    value = playerData.PlayerLevel;
                    return true;

                case PlayerStateIds.SkinOwned:
                    if (!TryParseEntityId(entityId, out int ownedSkinId))
                    {
                        return false;
                    }

                    value = playerData.PurchasedSkinIds?.Contains(ownedSkinId) == true
                        ? 1
                        : 0;
                    return true;

                case PlayerStateIds.SkinApplied:
                    return TryResolveSelectedEntity(
                        entityId,
                        playerData.AppliedSkinId,
                        out value);

                case PlayerStateIds.SuperAttackActive:
                    return TryResolveSelectedEntity(
                        entityId,
                        playerData.ActiveSuperAttackId,
                        out value);

                default:
                    return false;
            }
        }

        private static bool TryResolveSelectedEntity(
            string entityId,
            int selectedEntityId,
            out int value)
        {
            value = 0;
            if (!TryParseEntityId(entityId, out int parsedEntityId))
            {
                return false;
            }

            value = parsedEntityId == selectedEntityId ? 1 : 0;
            return true;
        }

        private static bool TryParseEntityId(
            string entityId,
            out int parsedEntityId)
        {
            return int.TryParse(
                entityId,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsedEntityId);
        }
    }
}
