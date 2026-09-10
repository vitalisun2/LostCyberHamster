using System.Threading.Tasks;

namespace GamePurchases
{
    /// <summary>Граница серверной проверки и атомарной выдачи в существующий облачный профиль.</summary>
    public interface IPurchaseFulfillment
    {
        /// <summary>
        /// Возвращает true после проверки store/owner/SKU, атомарного cloud claim и сохранения
        /// согласованного профиля с валютой, правами и ledger на устройстве. Повтор transaction ID
        /// восстанавливает ту же выдачу. RestoreEntitlementOnly восстанавливает только постоянное право.
        /// </summary>
        Task<bool> FulfillAndPersistAsync(PurchaseProof proof);
    }
}
