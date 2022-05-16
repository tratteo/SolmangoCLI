using BetterHaveIt;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SolmangoCLI.Services;
using SolmangoCLI.Settings;
using Solnet.Rpc;
using Solnet.Wallet;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SolmangoCLI.Statics;

public static class Extensions
{
    public static double ToSOL(this ulong lamports) => lamports / 1_000_000_000D;

    public static ulong ToLamports(this ulong sol) => sol * 1_000_000_000;

    public static IServiceCollection AddOfflineRunner<T>(this IServiceCollection services) where T : class, IRunner => _ = services.AddSingleton<IRunner, T>();

    public static IRpcClient GetRpcClient(this IServiceProvider services)
    {
        var connectionOption = services.GetRequiredService<SolanaEndPointManager>();
        var rpcClient = ClientFactory.GetClient(connectionOption.EndPoint);
        return rpcClient;
    }

    public static string GetEndPoint(this IServiceProvider services)
    {
        var connectionOption = services.GetRequiredService<SolanaEndPointManager>();
        return connectionOption.EndPoint;
    }

    public static bool TryGetCliAccount(this IServiceProvider services, out Account account)
    {
        var pathOptions = services.GetRequiredService<IOptionsMonitor<PathSettings>>();
        var factory = services.GetRequiredService<ILoggerFactory>();
        var logger = factory.CreateLogger("CLI account");
        try
        {
            if (Serializer.DeserializeJson<string>(pathOptions.CurrentValue.Wallet, out var privateKey) && privateKey is not null)
            {
                var pKey = new PrivateKey(privateKey);
                account = new Account(pKey.KeyBytes, pKey.KeyBytes[32..]);
                return true;
            }
        }
        catch (Exception) { }

        try
        {
            if (Serializer.DeserializeJson<byte[]>(pathOptions.CurrentValue.Wallet, out var bytes) && bytes is not null && bytes.Length == 64)
            {
                account = new Account(bytes, bytes[32..]);
                return true;
            }
        }
        catch (Exception)
        {
            account = null!;
            logger.LogError("Unable to correctly load keypair at {path}", pathOptions.CurrentValue.Wallet);
            return false;
        }

        account = null!;
        logger.LogError("Unable to correctly load keypair at {path}", pathOptions.CurrentValue.Wallet);
        return false;
    }

    public static Task RunOfflineAsync(this WebApplication app, CancellationToken token)
    {
        var runner = app.Services.GetRequiredService<IRunner>();
        return runner.RunAsync(token);
    }
}