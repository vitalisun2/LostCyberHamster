using Assets.Scripts.Entry_Points.BootstrapLoadingTasks;
using LoadingTasks;
using UnityEngine;
using Zenject;

namespace Assets.Scripts.Installers
{
    public class BootstrapSceneInstaller : MonoInstaller
    {

        public override void InstallBindings()
        {
            Container.Bind<ILoadingTask>().To<InitGameRepositoryLoadingTask>().AsTransient();
            //Container.Bind<ILoadingTask>().To<LoadAddressablesLoadingTask>().AsTransient();
            Container.Bind<ILoadingTask>().To<InitAnalyticsManagerLoadingTask>().AsTransient();
            Container.Bind<ILoadingTask>().To<InitLocalizationLoadingTask>().AsTransient();
            Container.Bind<ILoadingTask>().To<InitAudioManagerLoadingTask>().AsTransient();
            Container.Bind<ILoadingTask>().To<InitSkinsLoadingTask>().AsTransient();
            Container.Bind<ILoadingTask>().To<InitLocationsListLoadingTask>().AsTransient();
            Container.Bind<ILoadingTask>().To<LoadMainMenuLoadingTask>().AsTransient();
        }
    }
}
