using Sirenix.OdinInspector;
using System.Collections.Generic;
using Assets.Scripts.System;
using UnityEngine;

public class SkinManagerDebugger : MonoBehaviour
{
    [ShowInInspector]
    public List<string> AvailableSkinsNames => SkinManager.AvailableSkinsNames;
    [ShowInInspector]
    public string CurrentSkinName => SkinManager.CurrentSkinName;
    [ShowInInspector]
    public bool IsUltaActive => SkinManager.CurrentSkin?.IsUltaActive.Value ?? false;
    [ShowInInspector]
    public int UltaChargeAmount => LevelController.Instance.LevelData.Hamster?.UltaChargeAmount.Value ?? 0;

    [Button("Put On Skin")]
    public void PutOnSkin(int skinId) => SkinManager.PutOnSkin(skinId);

    [Button("Apply Ulta")]
    public void ApplyUlta()
    {
        LevelController.Instance.LevelData.Hamster.UltaEvent?.Invoke();
    }
}
