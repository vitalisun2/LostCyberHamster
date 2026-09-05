using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    public class IntroScreenController : ScreenController
    {
        private VisualElement _IntroImage => _contentRoot.Q<VisualElement>("skin-image");
        private Button _buttonSkip => _contentRoot.Q<Button>("skip-btn");
        

        public IntroScreenController(UIDocument uiDocument) : base(uiDocument)
        {
        }


        protected override ScreenEnum _screenAssetName => ScreenEnum.IntroScreen;

        protected override void BindView()
        {
            
        }


        protected override void OnSubscribeToEvents()
        {
            _buttonSkip?.RegisterCallback<ClickEvent>(OnClickBtnSkip);
        }

        private async void OnClickBtnSkip(ClickEvent evt)
        {
        }


        protected override void OnUnsubscribeFromEvents()
        {
            _buttonSkip?.UnregisterCallback<ClickEvent>(OnClickBtnSkip);
        }
    }
}