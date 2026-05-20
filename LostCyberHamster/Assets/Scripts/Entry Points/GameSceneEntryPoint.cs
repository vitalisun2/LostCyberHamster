using System;
using System.Threading.Tasks;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Vues.GameCore;
using Zenject;

namespace Assets.Scripts.Entry_Points
{
    public class GameSceneEntryPoint : MonoBehaviour
    {
        [SerializeField]
        private UIDocument _uiDocument;

        private UIManager _uiManager;

        private async Task Awake()
        {
           _uiManager = new UIManager(new IScreenController[]
            {
                new GameScreenController(_uiDocument),
                new PauseModalController(_uiDocument),
                new WinModalController(_uiDocument),
                new LoseModalController(_uiDocument),
                new IntroScreenController(_uiDocument)
            });

            var gameController = _uiManager.GetController<GameScreenController>();
            gameController.SetSuperJumpAction(() => { });
            gameController.SetJumpAction(() => { });
            gameController.SetUltraAction(() => { });
            gameController.SetTapAction(() => { });

            var winScreenController = _uiManager.GetController<WinModalController>();

            var pauseModalController = _uiManager.GetController<PauseModalController>();
            pauseModalController.SetResumeAction(() => {
               UIManager.OnModalShow(ScreenEnum.LoseModal);
                });
            pauseModalController.SetRestartAction(() => { });
            pauseModalController.SetExitAction(() =>
            {
               winScreenController.SetParamsForInit("Моя игра", "Она мне принадлежит и таким же ...", 2);
                UIManager.OnModalShow(ScreenEnum.WinModal);
            });


            await _uiManager.LoadScreenAsync(ScreenEnum.IntroScreen);
       }

        private void OnEnable()
        {
            _uiManager?.SubscribeToEvents();
        }

        private void OnDisable()
        {
            _uiManager?.UnsubscribeFromEvents();
        }
    }
}
