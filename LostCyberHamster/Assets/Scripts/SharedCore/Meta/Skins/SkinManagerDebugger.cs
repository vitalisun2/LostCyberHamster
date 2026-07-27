using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

public class SkinManagerDebugger : MonoBehaviour
{
    [ShowInInspector]
    public List<string> AvailableSkinsNames => SkinManager.AvailableSkinsNames;
    [ShowInInspector]
    public string CurrentSkinName => SkinManager.CurrentSkinName;

    [Button("Put On Skin")]
    public void PutOnSkin(int skinId) => SkinManager.PutOnSkin(skinId);
}
