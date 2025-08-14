using System;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Atomic.Elements;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assets.Scripts.GameEngine.Mechanics
{
    [Serializable]
    public class BoomEffectAction : IAtomicAction<Vector3, GameManager>
    {
        private readonly AtomicVariable<BoomEffect> _boomEffectPrefab;

        public BoomEffectAction(AtomicVariable<BoomEffect> boomEffectPrefab)
        {
            _boomEffectPrefab = boomEffectPrefab;
        }

        public void Invoke(Vector3 pos, GameManager gameManager)
        {
            Object.Instantiate(_boomEffectPrefab.Value, pos, Quaternion.identity);
        }
    }
}
