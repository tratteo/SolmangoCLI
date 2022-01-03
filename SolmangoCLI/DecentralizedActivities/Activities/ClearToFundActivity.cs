// Copyright Siamango

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SolmangoNET;
using SolmangoNET.Rpc;
using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Wallet;
using System;
using System.Threading.Tasks;

namespace SolmangoCLI.DecentralizedActivities;

public class ClearToFundActivity : DecentralizedActivity
{
    public const string ID = "clear_to_fund_activity";
    private readonly Account creatorAccount;
    private readonly PublicKey fundPublickey;

    public ClearToFundActivity(IConfiguration configuration) : base(ID, configuration)
    {
        creatorAccount = new Account(configuration.GetSection("Keys:CreatorPrivate").Get<string>(), configuration.GetSection("Keys:CreatorPublic").Get<string>());
        fundPublickey = new PublicKey(configuration.GetSection("Keys:FundPublic").Get<string>());
    }

    public override async Task<ActivityResult> Execute(DateTime executionDate, IRpcScheduler rpcScheduler, IRpcClient rpcClient, ILogger logger = null, IProgress<ExecutionProgress> progress = null)
    {
        ClusterSnapshot clusterSnapshot = await GetClusterSnapshot(rpcScheduler, rpcClient, logger);
        ulong floorAmount = 0;
        progress?.Report(new ExecutionProgress(Id, GetActivityStepCount(), new ExecutionProgress.Step(1, "Retrieving creator account balance", 1F)));
        var oneOfBalance = rpcScheduler.Schedule(() => rpcClient.GetBalanceAsync(creatorAccount.PublicKey));
        if (oneOfBalance.TryPickT1(out var saturatedEx, out var balanceToken))
        {
            logger?.LogError($"Scheduler saturated, fatal error");
            return ActivityResult.Failure(Id, executionDate);
        }
        var balanceResponse = await balanceToken;
        if (!balanceResponse.WasRequestSuccessfullyHandled)
        {
            return ActivityResult.Failure(Id, executionDate);
        }
        // Got the balance
        floorAmount = balanceResponse.Result.Value - clusterSnapshot.FeesInfo.FeeCalculator.LamportsPerSignature;
        if (configuration.GetSection("Debug:InhibitTransactions").Get<bool>())
        {
            logger?.LogInformation($"Cleared account sending {floorAmount} to fund account {fundPublickey.Key}");
            return ActivityResult.Factory(Id, executionDate, true, ("amount", floorAmount));
        }

        progress?.Report(new ExecutionProgress(Id, GetActivityStepCount(), new ExecutionProgress.Step(2, "Sending balance to fund account", 1F)));

        var oneOfTx = rpcScheduler.Schedule(() => rpcClient.SendTransactionAsync(new TransactionBuilder()
            .SetRecentBlockHash(clusterSnapshot.BlockHash.Blockhash)
            .SetFeePayer(creatorAccount)
            .AddInstruction(SystemProgram.Transfer(creatorAccount.PublicKey, fundPublickey, floorAmount))
            .Build(creatorAccount)));
        if (oneOfTx.TryPickT1(out saturatedEx, out var txToken))
        {
            logger?.LogError($"Scheduler saturated, fatal error");
            return ActivityResult.Failure(Id, executionDate);
        }
        // Got the transaction token
        var txResponse = await txToken;
        if (txResponse.WasRequestSuccessfullyHandled)
        {
            logger?.LogInformation($"Cleared account sending {floorAmount} to fund account {fundPublickey}");
        }
        else
        {
            logger?.LogError($"Unable to send transaction to fund, error[{txResponse.ServerErrorCode}]: {txResponse.Reason}");
            return ActivityResult.Failure(Id, executionDate);
        }

        return ActivityResult.Factory(Id, executionDate, true, ("amount", floorAmount));
    }

    public override int GetActivityStepCount() => 2;
}