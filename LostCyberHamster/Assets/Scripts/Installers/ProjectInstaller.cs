using Assets.Scripts.Account;
using Assets.Scripts.Online;
using GameAds;
using GameManagement.CloudSave;
using GameManagement.CloudSave.Gateway;
using GameManagement.CloudSave.Version;
using GameManagement.Leaderboard;
using GameManagement;
using LostCyberHamster.UI;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Assets.Scripts.DevTools;
#endif
using Zenject;

namespace Assets.Scripts.Installers
{
    /// <summary>
    /// Регистрирует проектные сервисы аккаунта, облачных сохранений и инструментов разработки.
    /// </summary>
    public class ProjectInstaller : MonoInstaller
    {
        /// <summary>
        /// Настраивает зависимости, общие для всех сцен проекта.
        /// </summary>
        public override void InstallBindings()
        {
            var network = GameNetworkFacade.Instance;
            Container.Bind<GameNetworkFacade>().FromInstance(network).AsSingle();
            Container.Bind<IRewardedAdProvider>().FromInstance(network).AsSingle();
            // Регистрируем шлюзы и сервис управления аккаунтом игрока.
            Container.Bind<IAccountAuthenticationGateway>()
                .FromInstance(network)
                .AsSingle();
            Container.Bind<IUnityPlayerAccountGateway>()
                .FromInstance(network)
                .AsSingle();
            Container.BindInterfacesAndSelfTo<AccountService>().AsSingle();
            Container.Bind<ExistingAccountRestoreCoordinator>().AsSingle();

            // Подключаем облачную синхронизацию.
            Container.Bind<ICloudSaveGateway>()
                .FromInstance(network)
                .AsSingle();
            Container.Bind<ICloudSaveVersionStore>()
                .To<CloudSaveVersionStore>()
                .AsSingle();
            Container.Bind<SnapshotService>().AsSingle();
            Container.Bind<ConflictService>().AsSingle();
            Container.BindInterfacesAndSelfTo<CloudSyncService>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<WeeklyLeaderboardCoordinator>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<LeaderboardReadService>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<ProfileOwnershipService>().AsSingle().NonLazy();
            Container.Bind<LocalSaveFeedback>().FromNewComponentOnNewGameObject()
                .WithGameObjectName("[LocalSaveFeedback]").AsSingle().NonLazy();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Добавляем меню тестовых инструментов только в редакторе и development-сборках.
            Container.Bind<DevToolsMenuOverlay>()
                .FromNewComponentOnNewGameObject()
                .WithGameObjectName("[DevToolsMenu]")
                .AsSingle()
                .NonLazy();
#endif
        }
    }
}
