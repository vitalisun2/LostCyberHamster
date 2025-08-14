using System.Collections;
using Atomic.Elements;
using GameManagement;
using JetBrains.Annotations;
using UnityEngine;
using Vues.GameCore;

public abstract class Skin
{
    public int Id;
    public string Name => LocalizationManager.GetLocalizedString(NameLocalizationKey) ?? NameLocalizationKey;
    public string NameLocalizationKey;
    public int Price;
    public ResourceType PriceType;
    public Sprite HamsterSprite;
    public RuntimeAnimatorController HamsterOverrideController;
    public GameObject UltaPrefab;
    public float UltaDuration;
    public int UltaCharge;

    public AtomicVariable<bool> IsUltaActive = new(false);
    private float _ultaTimeLeft;

    public bool IsPurchased => GameDataManager.PlayerData.PurchasedSkinIds.Contains(Id);

    public void ApplyUlta()
    {
        _ultaTimeLeft = UltaDuration;

        if (IsUltaActive.Value)
        {
            Debug.LogWarning("Ulta is already active");
            return;
        }

        ApplyUltaLogic();
    }

    public void UpdateUlta()
    {
        IsUltaActive.Value = _ultaTimeLeft > 0;
        CalculateTimeLeft();

        UpdateUltaLogic();
    }

    protected abstract void ApplyUltaLogic();

    protected abstract void UpdateUltaLogic();

    private void CalculateTimeLeft()
    {
        if (_ultaTimeLeft > 0)
        {
            _ultaTimeLeft -= Time.deltaTime;

            //debug every second
            if ((int)_ultaTimeLeft % 1 == 0)
            {
                Debug.Log($"Ulta time left: {_ultaTimeLeft}");
            }
        }
    }
}


