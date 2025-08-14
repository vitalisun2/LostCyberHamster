using System.Collections.Generic;
using System.Threading.Tasks;

namespace LoadingTasks
{
    public interface ILoadingTask
    {
        string Name { get; }
        Task LoadAsync(Dictionary<string, object> bundle);
        List<ILoadingTask> Children { get; }
    }
}
