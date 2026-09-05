using System;
using System.Threading.Tasks;

namespace GameAds
{
    /// <summary>Граница SDK; каждый listener принадлежит одному запросу.</summary>
    public interface IRewardedAdProvider
    {
        bool IsSupported { get; }
        bool IsInitialized { get; }
        bool HasLoadedAd { get; }
        Task InitializeAsync();
        void Load(Action loaded, Action<string> failed);
        void Show(Action started, Action<bool> completed, Action<string> failed);
    }
}
