using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace SolmangoCLI.Objects;

public class KeyPair
{
    [JsonProperty("public_key")]
    public string PublicKey { get; set; } = null!;

    [JsonProperty("private_key")]
    public string PrivateKey { get; set; } = null!;
}