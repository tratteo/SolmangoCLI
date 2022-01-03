// Copyright Siamango

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SolmangoNET.Rpc;
using Solnet.Rpc;
using System;
using System.Threading.Tasks;

namespace SolmangoCLI.DecentralizedActivities;

internal class DummyActivity : DecentralizedActivity
{
    public const string ID = "dummy";
    private readonly int dummyCycles;

    public DummyActivity(IConfiguration configuration, int dummyCycles = 100) : base(ID, configuration)
    {
        this.dummyCycles = dummyCycles;
    }

    public override async Task<ActivityResult> Execute(DateTime executionDate, IRpcScheduler rpcScheduler, IRpcClient rpcClient, ILogger logger = null, IProgress<ExecutionProgress> progress = null)
    {
        for (int i = 0; i < dummyCycles; i++)
        {
            await Task.Delay(50);
            progress?.Report(new ExecutionProgress(Id, GetActivityStepCount(), new ExecutionProgress.Step(1, "Dummy cycle", (float)i / dummyCycles)));
        }
        progress?.Report(new ExecutionProgress(Id, GetActivityStepCount(), new ExecutionProgress.Step(1, "Dummy cycle", 1)));
        return ActivityResult.Factory(Id, executionDate, true, Array.Empty<(string, object)>());
    }
}