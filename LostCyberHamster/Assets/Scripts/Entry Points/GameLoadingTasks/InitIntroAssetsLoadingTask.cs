using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.System;
using LoadingTasks;
using UnityEngine;

namespace Assets.Scripts.Entry_Points.GameLoadingTasks
{
    [Serializable]
    public class InitIntroAssetsLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация интро";

        [SerializeReference]
        private List<ILoadingTask> _children = new();
        public List<ILoadingTask> Children => _children;

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            LevelController.Instance.Init((GameManager)bundle["gameManager"]);

            LevelController.Instance.LevelData.IntroObject = (GameObject)bundle["introObject"];
            var hasIntroSprites = await LevelController.Instance.LoadIntroData();

            if (hasIntroSprites)
            {
                LevelController.Instance.LevelData.GameManager.StartIntro();
            }
            // Если интро нет — НЕ вызываем StartGame() здесь.
            // GameEntryPoint вызовет StartGame() после завершения ВСЕХ загрузочных задач.
        }
    }
}
