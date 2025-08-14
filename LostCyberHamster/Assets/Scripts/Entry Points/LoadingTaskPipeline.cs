using System;
using LoadingTasks;
using UnityEngine;

namespace Assets.Scripts.Entry_Points
{
    [CreateAssetMenu(fileName = "LoadingTaskPipeline", menuName = "Loading/Task Pipeline")]
    public class LoadingTaskPipeline : ScriptableObject
    {
        [Tooltip("The root task from which all loading begins.")]
        [SerializeReference]
        public ILoadingTask rootTask;

        // Helper property to cast rootTask to ILoadingTask
        public ILoadingTask Root => rootTask;
    }

}
