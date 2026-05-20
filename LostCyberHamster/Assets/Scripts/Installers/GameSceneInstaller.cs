using Assets.Scripts.Entry_Points.GameLoadingTasks;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.System;
using LoadingTasks;
using UnityEngine;
using Vues.GameCore;
using Zenject;

namespace Assets.Scripts.Installers
{
    public class GameSceneInstaller : MonoInstaller
    {
        [SerializeField] private GameManager _gameManager;
        [SerializeField] private GameObject _introObject;
        [SerializeField] private Hamster _hamsterPrefab;
        [SerializeField] private EnvironmentRoot _environmentRoot;
        [SerializeField] private UiRoot _uiRoot;

        public override void InstallBindings()
        {
            Container.Bind<GameManager>().FromInstance(_gameManager).AsSingle();
            Container.Bind<GameObject>().FromInstance(_introObject).AsSingle();
            Container.Bind<Hamster>().FromInstance(_hamsterPrefab).AsSingle();
            Container.Bind<EnvironmentRoot>().FromInstance(_environmentRoot).AsSingle();
            Container.Bind<UiRoot>().FromInstance(_uiRoot).AsSingle();
        }

    }
}
