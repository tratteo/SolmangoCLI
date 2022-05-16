using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SolmangoCLI.Settings;
using System;

namespace SolmangoCLI.Statics;

public class SolanaEndPointManager
{
    public SolanaEndPointManager(IServiceProvider services)
    {
        var connectionOption = services.GetRequiredService<IOptionsMonitor<ConnectionSettings>>();
        EndPoint = connectionOption.CurrentValue.ClusterEndpoint.ToLower() switch
        {
            "m" => Cluster.MainNet,
            "d" => Cluster.DevNet,
            "c" => Cluster.CustomEndPoint,
            _ => Cluster.DevNet
        };
    }
    public string EndPoint { get; private set; }

    public class Cluster
    {
        public static string DevNet { get => "https://api.devnet.solana.com"; }
        public static string MainNet { get => "https://api.mainnet-beta.solana.com"; }

        public static string CustomEndPoint{ get => "https://ssc-dao.genesysgo.net/"; }
    }

}
