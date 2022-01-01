using Newtonsoft.Json;
using System;
using System.Collections.Immutable;

namespace SolmangoCLI;

[Serializable]
internal class CollectionData
{
    [JsonProperty("name")]
    public string Name { get; init; }

    [JsonProperty("symbol")]
    public string Symbol { get; init; }

    [JsonProperty("update_authority")]
    public string UpdateAuthority { get; init; }

    [JsonProperty("mints")]
    public ImmutableList<string> Mints { get; init; }

    public CollectionData()
    {
        Name = string.Empty;
        Symbol = string.Empty;
        UpdateAuthority = string.Empty;
        Mints = ImmutableList.Create<string>();
    }
}