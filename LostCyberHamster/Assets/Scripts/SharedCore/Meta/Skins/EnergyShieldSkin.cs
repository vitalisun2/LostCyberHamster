using System.Collections;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using UnityEngine;

public class EnergyShieldSkin : Skin
{

    protected override void ApplyUltaLogic()
    {
        var hamster = LevelController.Instance.LevelData.Hamster;

        hamster.StartCoroutine(RunUltaLogic());
    }

    protected override void UpdateUltaLogic()
    {
    }

    private IEnumerator RunUltaLogic()
    {
        var hamster = LevelController.Instance.LevelData.Hamster;

        hamster.IsProtected.Value = true;
        hamster.IsDestructiveOnCollision.Value = true;

        var ultaEffect = HelpMethods.CreateUltaEffect(UltaPrefab, hamster);

        // Wait for the duration of the shield effect
        yield return new WaitForSeconds(UltaDuration);

        hamster.IsProtected.Value = false;
        hamster.IsDestructiveOnCollision.Value = false;

        DestroyUltaEffect(ultaEffect);
    }

    private void DestroyUltaEffect(GameObject ultaEffect)
    {
        if (ultaEffect != null)
        {
            GameObject.Destroy(ultaEffect);
        }
    }
}

