// Copyright Siamango

using Newtonsoft.Json;
using Solnet.Rpc;
using System;

namespace SolmangoCLI;

[Serializable]
public partial class Options
{
    [JsonProperty("preferences")]
    public Preferences PreferencesOptions { get; private set; }

    [JsonProperty("email")]
    public Email EmailOptions { get; private set; }

    [JsonProperty("debug")]
    public Debug DebugOptions { get; private set; }

    public Options()
    {
        PreferencesOptions = new Preferences();
        EmailOptions = new Email();
        DebugOptions = new Debug();
    }

    public bool Verify() => PreferencesOptions.Verify();

    [Serializable]
    public class Preferences
    {
        [JsonProperty("default_cluster")]
        public string DefaultCluster { get; private set; }

        [JsonProperty("fund_privatekey")]
        public string FundPrivateKey { get; private set; }

        [JsonProperty("fund_publickey")]
        public string FundPublicKey { get; private set; }

        public Preferences()
        {
            DefaultCluster = Cluster.DevNet.ToString();
        }

        public bool Verify() => DefaultCluster != null && FundPrivateKey is not null && FundPrivateKey != string.Empty && FundPublicKey is not null && FundPublicKey != string.Empty;
    }

    [Serializable]
    public class Email
    {
        [JsonProperty("enable")]
        public bool Enable { get; private set; }

        [JsonProperty("project_email_password")]
        public string ProjectEmailPassword { get; private set; }

        [JsonProperty("project_email")]
        public string ProjectEmail { get; private set; }

        public Email()
        {
            Enable = false;
            ProjectEmailPassword = string.Empty;
            ProjectEmail = string.Empty;
        }
    }
}