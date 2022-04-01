using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolmangoCLI.Objects;

internal class HoldersAirdrops
{
    [JsonProperty("holders_addresses")]
    public List<string> Holders { get; set; }
}