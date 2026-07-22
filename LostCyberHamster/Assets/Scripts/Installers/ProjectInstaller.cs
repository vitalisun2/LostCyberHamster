using Assets.Scripts.Account;
using GameManagement.CloudSave;
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
            // Регистрируем шлюзы и сервис управления аккаунтом игрока.
            Container.Bind<IAccountAuthenticationGateway>()
                .To<UnityAccountAuthenticationGateway>()
                .AsSingle();
            Container.Bind<IUnityPlayerAccountGateway>()
                .To<UnityPlayerAccountGateway>()
                .AsSingle();
            Container.Bind<AccountService>().AsSingle();

            // Регистрируем шлюз и сервисы облачной синхронизации.
            Container.Bind<ICloudSaveGateway>()
                .To<UnityCloudSaveGateway>()
                .AsSingle();
            Container.Bind<SnapshotUploadService>().AsSingle();
            Container.Bind<CloudSaveConflictService>().AsSingle();
            Container.Bind<CloudSyncService>().AsSingle().NonLazy();
            Container.Bind<ExistingAccountRestoreCoordinator>().AsSingle();
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
