using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.System;
using LoadingTasks;
using UnityEngine;

namespace Assets.Scripts.Entry_Points.GameLoadingTasks
{
    /// <summary>Помещает подготовленную композицию фонов на нижний край дороги.</summary>
    [Serializable]
    public sealed class InitEnvironmentLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация окружения";
        [SerializeReference] private List<ILoadingTask> _children = new();
        public List<ILoadingTask> Children => _children;

        /// <summary>Создаёт окружение и передаёт ему владение загруженным префабом.</summary>
        public Task LoadAsync(Dictionary<string, object> bundle)
        {
            // Принимаем lease из загрузчика ровно один раз.
            var lease = LevelController.Instance.LevelData.TakeEnvironmentLease();
            if (lease == null || !lease.IsActive || lease.Value == null)
            {
                lease?.Dispose();
                throw new InvalidOperationException("Префаб окружения не загружен перед созданием сцены.");
            }
            GameObject instance = null;

            // Начало префаба совпадает с фиксированным мировым нижним краем дороги.
            try
            {
                var root = (EnvironmentRoot)bundle["environmentRoot"];
                instance = UnityEngine.Object.Instantiate(lease.Value,
                    new Vector3(0f, Consts.RoadBottomYPos, 0f), Quaternion.identity, root.transform);
                var environment = instance.GetComponent<LocationEnvironment>();
                if (environment == null)
                    throw new InvalidOperationException("У префаба окружения отсутствует LocationEnvironment.");
                environment.Initialize((GameManager)bundle["gameManager"], (Camera)bundle["gameCamera"], lease);
            }
            catch
            {
                if (instance != null)
                    UnityEngine.Object.Destroy(instance);
                lease.Dispose();
                throw;
            }
            return Task.CompletedTask;
        }
    }
}
