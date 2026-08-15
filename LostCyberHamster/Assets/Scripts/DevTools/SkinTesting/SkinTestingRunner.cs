#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using GameManagement;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vues.GameCore;

namespace Assets.Scripts.DevTools.SkinTesting
{
    /// <summary>
    /// Покупает и применяет следующий скин через production services.
    /// </summary>
    public sealed class SkinTestingRunner
    {
        private const string GameplaySceneName = "Game";

        private bool _isBusy;
        private string _status = "Ожидание Play Mode и bootstrap.";
        private string _targetStatus = "—";
        private string _priceStatus = "—";
        private string _grantedStatus = "—";
        private string _purchaseStatus = "—";
        private string _appliedStatus = "—";

        private SkinTestingRunner()
        {
        }

        public static SkinTestingRunner Shared { get; } = new();

        public event Action Changed;

        public bool CanRun => IsReady && !_isBusy && FindNextSkin() != null;
        public string AvailabilityStatus => GetAvailabilityStatus();
        public string Status => _status;
        public string TargetStatus => _targetStatus;
        public string PriceStatus => _priceStatus;
        public string GrantedStatus => _grantedStatus;
        public string PurchaseStatus => _purchaseStatus;
        public string AppliedStatus => _appliedStatus;

        private static bool IsReady =>
            Application.isPlaying &&
            !IsGameplayActive() &&
            GameDataManager.PlayerData?.PurchasedSkinIds != null &&
            SkinManager.AvailableSkins.Count > 0;

        /// <summary>
        /// Начисляет недостающий ресурс, покупает и применяет следующий скин.
        /// </summary>
        public void UnlockBuyAndEquipNextSkin()
        {
            if (!CanRun)
            {
                _status = AvailabilityStatus;
                Changed?.Invoke();
                return;
            }

            _isBusy = true;
            ResetOperationDetails();

            try
            {
                // Берём цель, валюту и цену из реального каталога скинов.
                Skin target = FindNextSkin() ??
                    throw new InvalidOperationException(
                        "Все доступные скины уже куплены.");
                _targetStatus = $"{target.Name} (ID {target.Id})";
                _priceStatus = $"{target.Price} {target.PriceType}";

                // Добавляем ровно shortfall через владельца игрового ресурса.
                int balance = ResourceManager.GetCurrentBalance(target.PriceType);
                int missingResource = Math.Max(0, target.Price - balance);
                if (missingResource > 0 &&
                    !ResourceManager.AddResource(target.PriceType, missingResource))
                {
                    throw new InvalidOperationException(
                        $"Не удалось начислить {missingResource} {target.PriceType}.");
                }

                _grantedStatus = $"{missingResource} {target.PriceType}";
                if (!SkinManager.CanPurchaseSkin(target.Id))
                {
                    throw new InvalidOperationException(
                        "SkinManager не разрешил покупку после начисления.");
                }

                // Покупаем и надеваем через те же services, что и production UI.
                SkinManager.PurchaseSkin(target.Id);
                if (!target.IsPurchased)
                {
                    throw new InvalidOperationException(
                        "SkinManager не завершил покупку.");
                }

                _purchaseStatus = "Куплен через SkinManager";
                SkinManager.PutOnSkin(target.Id);
                if (GameDataManager.PlayerData.AppliedSkinId != target.Id)
                {
                    throw new InvalidOperationException(
                        "SkinManager не применил купленный скин.");
                }

                _appliedStatus = $"{target.Name} (ID {target.Id})";
                _status = "Операция завершена.";
                UIManager.OnRepaintScreen?.Invoke();
            }
            catch (Exception exception)
            {
                _status = $"Ошибка: {exception.Message}";
                if (_purchaseStatus == "—")
                    _purchaseStatus = "Не завершена";
            }
            finally
            {
                _isBusy = false;
                Changed?.Invoke();
            }
        }

        /// <summary>Сбрасывает вывод при смене Play Mode.</summary>
        public void ResetStatus()
        {
            _isBusy = false;
            _status = "Ожидание Play Mode и bootstrap.";
            ResetOperationDetails();
            Changed?.Invoke();
        }

        private static Skin FindNextSkin()
        {
            int appliedSkinId = GameDataManager.PlayerData?.AppliedSkinId ?? -1;
            return SkinManager.AvailableSkins.FirstOrDefault(
                skin => skin.Id != appliedSkinId && !skin.IsPurchased);
        }

        private static bool IsGameplayActive()
        {
            return Application.isPlaying &&
                   string.Equals(
                       SceneManager.GetActiveScene().name,
                       GameplaySceneName,
                       StringComparison.Ordinal);
        }

        private string GetAvailabilityStatus()
        {
            if (!Application.isPlaying)
                return "Доступно только в Play Mode после Bootstrap.";
            if (IsGameplayActive())
                return "В Gameplay операция запрещена. Вернитесь в Menu.";
            if (GameDataManager.PlayerData?.PurchasedSkinIds == null)
                return "Ожидание PlayerData.";
            if (SkinManager.AvailableSkins.Count == 0)
                return "Ожидание SkinManager.Init.";
            if (FindNextSkin() == null)
                return "Все доступные скины уже куплены.";
            if (_isBusy)
                return "Операция выполняется.";

            return "Готово: PlayerData и каталог скинов загружены.";
        }

        private void ResetOperationDetails()
        {
            _targetStatus = "—";
            _priceStatus = "—";
            _grantedStatus = "—";
            _purchaseStatus = "—";
            _appliedStatus = "—";
        }
    }
}
#endif
