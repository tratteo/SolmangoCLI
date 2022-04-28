using System.Threading;
using System.Threading.Tasks;

namespace SolmangoCLI.Services;

public interface IRunner
{
    Task RunAsync(CancellationToken token);
}