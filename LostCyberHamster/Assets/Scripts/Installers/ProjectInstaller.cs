using Assets.Scripts.Account;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Assets.Scripts.DevTools;
#endif
using Zenject;

namespace Assets.Scripts.Installers
{
    public class ProjectInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<AccountService>().AsSingle();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Container.Bind<DevToolsMenuOverlay>()
                .FromNewComponentOnNewGameObject()
                .WithGameObjectName("[DevToolsMenu]")
                .AsSingle()
                .NonLazy();
#endif
        }
    }
}
