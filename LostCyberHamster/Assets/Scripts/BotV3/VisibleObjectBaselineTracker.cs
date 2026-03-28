using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Отслеживает смену набора видимых obstacle ids, чтобы runtime мог триггерить replan только по факту изменений.
    /// </summary>
    public class VisibleObjectBaselineTracker
    {
        private bool _hasBaseline;
        private readonly HashSet<int> _visibleObjectIds = new HashSet<int>();
        private readonly HashSet<int> _scratch = new HashSet<int>();

        public bool Update(BotSceneSnapshot snapshot)
        {
            _scratch.Clear();

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
                _scratch.Add(snapshot.VisibleObjects[i].StableId);

            bool changed = !_hasBaseline
                || _visibleObjectIds.Count != _scratch.Count
                || !_visibleObjectIds.SetEquals(_scratch);

            if (!changed)
                return false;

            _visibleObjectIds.Clear();
            foreach (int stableId in _scratch)
                _visibleObjectIds.Add(stableId);

            _hasBaseline = true;
            return true;
        }

        public void Reset()
        {
            _hasBaseline = false;
            _visibleObjectIds.Clear();
            _scratch.Clear();
        }
    }
}
