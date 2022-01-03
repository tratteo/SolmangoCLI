// Copyright Siamango

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SolmangoNET;
using SolmangoNET.Rpc;
using Solnet.Rpc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SolmangoCLI.DecentralizedActivities;

public abstract class DecentralizedActivity : IEquatable<DecentralizedActivity>
{
    protected readonly IConfiguration configuration;

    public string Id { get; private set; }

    public DecentralizedActivity(string id, IConfiguration configuration)
    {
        Id = id;
        this.configuration = configuration;
    }

    public virtual int GetActivityStepCount() => 1;

    public abstract Task<ActivityResult> Execute(DateTime executionDate, IRpcScheduler rpcScheduler, IRpcClient rpcClient, ILogger logger = null, IProgress<ExecutionProgress> progress = null);

    public async Task<ClusterSnapshot> GetClusterSnapshot(IRpcScheduler rpcScheduler, IRpcClient rpcClient, ILogger logger = null)
    {
        var oneOfCluster = rpcScheduler.Schedule(() => Solmango.GetClusterSnapshot(rpcClient));
        if (oneOfCluster.TryPickT1(out var saturatedEx, out var clusterToken))
        {
            logger?.LogError($"Scheduler saturated, fatal error");
            return null;
        }
        var oneOfSnapshot = await clusterToken;
        if (oneOfSnapshot.TryPickT1(out var solmangoEx, out var clusterSnapshot))
        {
            logger?.LogError($"Unable to retrieve cluster snapshot, RPC error: {saturatedEx.Message}");
            return null;
        }
        return clusterSnapshot;
    }

    public bool Equals(DecentralizedActivity other) => Id.Equals(other.Id);

    public override bool Equals(object obj) => Equals(obj as DecentralizedActivity);

    public override int GetHashCode() => base.GetHashCode();

    public struct ActivityResult : IEquatable<ActivityResult>
    {
        public string Id { get; private set; }

        public DateTime ExecutionDate { get; private set; }

        public bool Success { get; private set; }

        public Dictionary<string, object> Params { get; private set; }

        public static ActivityResult Failure(string id, DateTime executionDate)
        {
            return new ActivityResult()
            {
                Id = id,
                ExecutionDate = executionDate,
                Success = false,
                Params = new Dictionary<string, object>()
            };
        }

        public static ActivityResult Factory(string id, DateTime executionDate, bool success, params (string, object)[] parameters)
        {
            ActivityResult res = new ActivityResult()
            {
                Id = id,
                ExecutionDate = executionDate,
                Success = success,
                Params = new Dictionary<string, object>()
            };
            foreach (var elem in parameters)
            {
                res.Params.TryAdd(elem.Item1, elem.Item2);
            }
            return res;
        }

        public string AsJson()
        {
            StringBuilder jsonBuilder = new();
            using JsonWriter writer = new JsonTextWriter(new StringWriter(jsonBuilder))
            {
                Formatting = Formatting.Indented
            };
            writer.WriteStartObject();
            writer.WritePropertyName("activity"); writer.WriteValue(Id);
            writer.WritePropertyName("date"); writer.WriteValue(ExecutionDate);
            foreach (var pair in Params)
            {
                writer.WritePropertyName(pair.Key); writer.WriteValue(pair.Value);
            }
            writer.WriteEndObject();
            return jsonBuilder.ToString();
        }

        public bool Equals(ActivityResult other)
        {
            if (!(Id.Equals(other.Id) && ExecutionDate.Ticks.Equals(other.ExecutionDate.Ticks) && Success.Equals(other.Success))) return false;
            if (Params.Count != other.Params.Count) return false;
            foreach (var pair in Params)
            {
                if (!other.Params.TryGetValue(pair.Key, out var value)) return false;
                if (!pair.Value.Equals(value)) return false;
            }
            return true;
        }
    }
}