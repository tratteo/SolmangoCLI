namespace SolmangoCLI.Settings;

public class ConnectionSettings
{
    public const string Position = "Connection";

    //https://api.devnet.solana.com
    //https://ssc-dao.genesysgo.net/

    public string ClusterEndpoint { get; set; } = null!;
}