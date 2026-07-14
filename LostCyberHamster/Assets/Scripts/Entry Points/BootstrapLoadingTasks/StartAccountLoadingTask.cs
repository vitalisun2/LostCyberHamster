using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using LoadingTasks;

namespace Assets.Scripts.Entry_Points.BootstrapLoadingTasks
{
    public sealed class StartAccountLoadingTask : ILoadingTask
    {
        private readonly AccountService _accountService;

        public string Name => "Запуск аккаунта";

        public List<ILoadingTask> Children { get; } = new();

        public StartAccountLoadingTask(AccountService accountService)
        {
            _accountService = accountService;
        }

        /// <summary>
        /// Запускает фоновое определение аккаунта, не задерживая bootstrap.
        /// </summary>
        public Task LoadAsync(Dictionary<string, object> bundle)
        {
            _accountService.Start();
            return Task.CompletedTask;
        }
    }
}
