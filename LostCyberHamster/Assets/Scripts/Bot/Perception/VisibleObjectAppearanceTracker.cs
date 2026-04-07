using System;
using System.Collections.Generic;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Отслеживает только появление новых видимых obstacle ids.
    /// Исчезновение объектов не считается trigger-событием для replanning.
    /// </summary>
    internal class VisibleObjectAppearanceTracker
    {
        private bool _hasBaseline;
        private readonly HashSet<int> _visibleObjectIds = new HashSet<int>();
        private readonly HashSet<int> _scratch = new HashSet<int>();

        public event Action OnNewObjectAppeared;

        /// <summary>
        /// Обновляет baseline видимых ids и стреляет только если появился новый объект.
        /// </summary>
        public void Update(BotSceneSnapshot snapshot)
        {
            _scratch.Clear();

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
                _scratch.Add(snapshot.VisibleObjects[i].StableId);

            bool hasNewObject = HasNewVisibleObject();
            _visibleObjectIds.Clear();
            foreach (int stableId in _scratch)
                _visibleObjectIds.Add(stableId);

            _hasBaseline = true;
            if (hasNewObject)
                OnNewObjectAppeared?.Invoke();
        }

        private bool HasNewVisibleObject()
        {
            if (!_hasBaseline)
                return _scratch.Count > 0;

            foreach (int stableId in _scratch)
            {
                if (!_visibleObjectIds.Contains(stableId))
                    return true;
            }

            return false;
        }

        public void Reset()
        {
            _hasBaseline = false;
            _visibleObjectIds.Clear();
            _scratch.Clear();
        }
    }
}
