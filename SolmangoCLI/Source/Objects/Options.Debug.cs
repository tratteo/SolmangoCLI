// Copyright Siamango

using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace SolmangoCLI;

public partial class Options
{
    [Serializable]
    public class Debug
    {
        [JsonProperty("verbose")]
        public bool Verbose { get; init; }

        [JsonProperty("inhibit_transactions")]
        public bool InhibitTransactions { get; init; }

        [JsonProperty("force_email")]
        public bool ForceEmail { get; init; }

        public bool Verify(List<string> warnings = null, List<string> errors = null) => true;
    }
}