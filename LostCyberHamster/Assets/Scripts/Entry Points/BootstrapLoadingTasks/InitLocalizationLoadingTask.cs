using System.Collections.Generic;
using System.Threading.Tasks;
using GameManagement;
using LoadingTasks;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Scripts.Entry_Points.BootstrapLoadingTasks
{
    internal sealed class InitLocalizationLoadingTask: ILoadingTaskSequence
    {
        public string Name { get; } = "Загрузка локализации";
        public List<ILoadingTask> Children { get; }


        public InitLocalizationLoadingTask()
        {
        }

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            if (GameDataManager.Settings.Language == (int)SystemLanguage.Unknown){
                var language = Application.systemLanguage;
                await LocalizationManager.SetLanguageAsync(language);
                GameDataManager.Settings.Language = (int)language;
                return;
            }
            
            await LocalizationManager.SetLanguageAsync((SystemLanguage)GameDataManager.Settings.Language);
        }
    }
}