namespace SolmangoCLI.Settings;

public class ConnectionSettings
{
    public const string Position = "Connection";

    //https://api.devnet.solana.com
    //https://ssc-dao.genesysgo.net/
    //distribute-tokens Brt35yayL8TLTRgVgDzbjYLncu1WL9QBm8ynashuz8TY  C:\Users\nclmd\Desktop\sol-tests\snapshot\gen0_holders.json
    public string ClusterEndpoint { get; set; } = null!;
}