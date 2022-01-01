using Newtonsoft.Json;
using Solnet.Wallet;
using System;

namespace SolmangoCLI;

[Serializable]
public class ShareHolder
{
    [JsonProperty("name")]
    public string Name { get; init; }

    [JsonProperty("surname")]
    public string Surname { get; init; }

    [JsonProperty("email")]
    public string Email { get; init; }

    [JsonProperty("address")]
    public string Address { get; init; }

    [JsonProperty("shares")]
    public double SharesPercentage { get; init; }

    public override string? ToString() => $"{Name} {Surname}\nEmail: {Email}\nAddress: {Address}\nShares: {SharesPercentage}";
}