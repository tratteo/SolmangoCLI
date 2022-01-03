using Newtonsoft.Json;
using System;

namespace SolmangoCLI;

[Serializable]
public class Dividends
{
    [JsonProperty("holders")]
    public ShareHolder[] HoldersArray { get; init; }
}