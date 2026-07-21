using Assets.Scripts.Account;
using GameManagement.CloudSave;
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
            Container.Bind<IAccountAuthenticationGateway>()
                .To<UnityAccountAuthenticationGateway>()
                .AsSingle();
            Container.Bind<IUnityPlayerAccountGateway>()
                .To<UnityPlayerAccountGateway>()
                .AsSingle();
            Container.Bind<AccountService>().AsSingle();
            Container.Bind<ICloudSaveGateway>()
                .To<UnityCloudSaveGateway>()
                .AsSingle();
            Container.Bind<CloudSyncService>().AsSingle().NonLazy();
            Container.Bind<ExistingAccountRestoreCoordinator>().AsSingle();
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
