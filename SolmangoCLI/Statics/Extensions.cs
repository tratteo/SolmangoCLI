using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SolmangoCLI.Services;
using System.Threading;
using System.Threading.Tasks;

namespace SolmangoCLI.Statics;

public static class Extensions
{
    public static double ToSOL(this ulong lamports) => lamports / 1_000_000_000D;

    public static ulong ToLamports(this ulong sol) => sol * 1_000_000_000;

    public static void AddOfflineRunner<T>(this IServiceCollection services) where T : class, IRunner
    {
        services.AddSingleton<IRunner, T>();
    }

    public static Task RunOfflineAsync(this WebApplication app, CancellationToken token)
    {
        var runner = app.Services.GetRequiredService<IRunner>();
        return runner.RunAsync(token);
    }
}