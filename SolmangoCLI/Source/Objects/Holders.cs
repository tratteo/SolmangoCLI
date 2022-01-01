using Newtonsoft.Json;
using System;

namespace SolmangoCLI;

[Serializable]
public class Holders
{
    [JsonProperty("holders")]
    public ShareHolder[] HoldersArray { get; init; }
}