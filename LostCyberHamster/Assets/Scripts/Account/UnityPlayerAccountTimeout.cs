using System;
using System.Threading.Tasks;

namespace LostCyberHamster.Account
{
    /// <summary>
    /// Реализует рабочий таймаут через системный планировщик задач.
    /// </summary>
    internal sealed class UnityPlayerAccountTimeout : IUnityPlayerAccountTimeout
    {
        /// <summary>
        /// Возвращает задачу, завершающуюся после заданного интервала.
        /// </summary>
        public Task WaitAsync(TimeSpan timeout)
        {
            return Task.Delay(timeout);
        }
    }
}
