using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SolmangoCLI.Settings;
using System;

namespace SolmangoCLI.Statics;

public class SolanaEndPointManager
{
    public string EndPoint { get; private set; }

    public SolanaEndPointManager(IServiceProvider services)
    {
        var connectionOption = services.GetRequiredService<IOptionsMonitor<ConnectionSettings>>();
        EndPoint = GetEndPoint(connectionOption.CurrentValue.ClusterEndpoint);
    }

    public void ChangeEndPoint(string name)
    {
        EndPoint = GetEndPoint(name);
    }

    private static string GetEndPoint(string name)
    {
        return name switch
        {
            "m" => Cluster.MainNet,
            "d" => Cluster.DevNet,
            "c" => Cluster.CustomEndPoint,
            _ => Cluster.DevNet
        };
    }

    public class Cluster
    {
        public static string DevNet { get => "https://api.devnet.solana.com"; }

        public static string MainNet { get => "https://api.mainnet-beta.solana.com"; }

        public static string CustomEndPoint { get => "https://ssc-dao.genesysgo.net/"; }
    }
}