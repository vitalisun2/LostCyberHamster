using System;
using System.Threading.Tasks;

namespace LostCyberHamster.Account
{
    /// <summary>
    /// Определяет заменяемую границу ожидания для детерминированной проверки таймаутов gateway.
    /// </summary>
    internal interface IUnityPlayerAccountTimeout
    {
        /// <summary>
        /// Возвращает задачу, завершающуюся после заданного интервала.
        /// </summary>
        Task WaitAsync(TimeSpan timeout);
    }
}
