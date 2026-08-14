using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.Common;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.GameEngine.Skins;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.System;
using LoadingTasks;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Scripts.Entry_Points.GameLoadingTasks
{
    [Serializable]
    public class InitCharacterLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Создание персонажа";

        [SerializeReference]
        private List<ILoadingTask> _children = new();
        public List<ILoadingTask> Children => _children;

        private EnvironmentRoot _environmentRoot;
        private Hamster _characterPrefab;

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            _characterPrefab = (Hamster)bundle["characterPrefab"];
            _environmentRoot = (EnvironmentRoot)bundle["environmentRoot"];

            await CreateHamsterAsync();
        }

        private async Task CreateHamsterAsync()
        {
            // Создаём персонажа до подключения runtime забега.
            var hamster = GameObject.Instantiate(_characterPrefab,
                new Vector3(Consts.HamsterXPos, Consts.HamsterYPos, 0), Quaternion.identity, _environmentRoot.transform);

            try
            {
                // Собираем runtime-зависимости персонажа до регистрации gameplay listeners.
                await ConfigureSuperAttackAsync(hamster);
                (SkinVisualRuntime normalVisual, SkinVisualRuntime skateboardVisual) =
                    await SkinVisualRuntimeFactory.CreateSelectedAsync(hamster);
                hamster.ConfigureSkinVisuals(normalVisual, skateboardVisual);

                // Подключаем только постоянные gameplay listeners после сборки visual.
                AddGameListeners(hamster);
                LevelController.Instance.LevelData.Hamster = hamster;
            }
            catch
            {
                // Уничтожение Hamster освобождает уже созданные super attack и skin leases.
                GameObject.Destroy(hamster.gameObject);
                throw;
            }
        }

        private static async Task ConfigureSuperAttackAsync(Hamster hamster)
        {
            int? activeSuperAttackId =
                SuperAttackService.ActiveSuperAttackId;
            if (!activeSuperAttackId.HasValue ||
                !SuperAttackService.TryGet(
                    activeSuperAttackId.Value,
                    out SuperAttackData data))
            {
                hamster.ConfigureSuperAttack(null);
                return;
            }

            ISuperAttackRuntime runtime =
                await SuperAttackFactory.CreateAsync(data);
            hamster.ConfigureSuperAttack(runtime);
        }

        private void AddGameListeners(Hamster hamster)
        {
            var gameManager = LevelController.Instance.LevelData.GameManager;
            var listeners = hamster.gameObject.GetComponentsInChildren<Listeners.IGameListener>();

            foreach (var listener in listeners)
            {
                gameManager.AddListener(listener);
            }

            // If game already started (e.g. test level without intro), fire OnStart for late listeners
            if (gameManager.State == GameState.PLAYING)
            {
                foreach (var listener in listeners)
                {
                    if (listener is Listeners.IGameStartListener startListener)
                        startListener.OnStart();
                }
            }
            else if (gameManager.State == GameState.INTRO)
            {
                foreach (var listener in listeners)
                {
                    if (listener is Listeners.IGameIntroListener introListener)
                        introListener.OnIntro();
                }
            }
        }

    }
}
