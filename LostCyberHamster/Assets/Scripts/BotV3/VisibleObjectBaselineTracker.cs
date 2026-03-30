using System;
using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Отслеживает смену набора видимых obstacle ids, чтобы runtime мог триггерить replan только по факту изменений.
    /// Стреляет OnBaselineChanged при каждом изменении набора.
    /// </summary>
    internal class VisibleObjectBaselineTracker
    {
        private bool _hasBaseline;
        private readonly HashSet<int> _visibleObjectIds = new HashSet<int>();
        private readonly HashSet<int> _scratch = new HashSet<int>();

        public event Action OnBaselineChanged;

        /// <summary>
        /// Сравнивает текущий набор видимых id с baseline и стреляет OnBaselineChanged при изменении.
        /// </summary>
        public void Update(BotSceneSnapshot snapshot)
        {
            // Собрать текущий набор id
            _scratch.Clear();

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
                _scratch.Add(snapshot.VisibleObjects[i].StableId);

            // Сравнить с baseline
            bool changed = !_hasBaseline
                || _visibleObjectIds.Count != _scratch.Count
                || !_visibleObjectIds.SetEquals(_scratch);

            if (!changed)
                return;

            // Обновить baseline и уведомить подписчиков
            _visibleObjectIds.Clear();
            foreach (int stableId in _scratch)
                _visibleObjectIds.Add(stableId);

            _hasBaseline = true;
            OnBaselineChanged?.Invoke();
        }

        public void Reset()
        {
            _hasBaseline = false;
            _visibleObjectIds.Clear();
            _scratch.Clear();
        }
    }
}
