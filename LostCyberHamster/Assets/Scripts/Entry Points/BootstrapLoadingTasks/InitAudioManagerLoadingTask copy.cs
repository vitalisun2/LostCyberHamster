using System.Collections.Generic;
using System.Threading.Tasks;
using Vues.GameCore;
using LoadingTasks;
using GameManagement;
using UnityEngine.AddressableAssets;
using UnityEngine;
using Assets.Scripts.System;

namespace Assets.Scripts.Entry_Points.BootstrapLoadingTasks
{
    public class InitAudioManagerLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация аудио менеджера";

        public List<ILoadingTask> Children { get; }

        public InitAudioManagerLoadingTask()
        {
        }

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            var loadHandle = Addressables.LoadAssetAsync<AudioClip>($"music_test1");

            loadHandle.Completed += h =>
            {
                var clip = h.Result;
                AudioManager.FadeInMusic(clip, 2500);
            };


            //AudioManager.FadeInMusic(clip, 2500);
            AudioManager.SetMusicVolume(GameDataManager.Settings.MusicVolume);
            AudioManager.SetSfxVolume(GameDataManager.Settings.SfxVolume);
            VibrationManager.EnableVibration = GameDataManager.Settings.EnableVibration;
        }

    }

    //init skins loading task
}
