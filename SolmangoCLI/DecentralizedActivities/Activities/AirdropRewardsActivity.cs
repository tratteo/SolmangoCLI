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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace SolmangoCLI.DecentralizedActivities;

public class AirdropRewardsActivity : DecentralizedActivity
{
    public const string ID = "airdrop_rewards_activity";
    private readonly Account creatorAccount;
    private readonly ImmutableList<string> mints;

    public Dictionary<string, ulong> Rewards { get; private set; }

    public AirdropRewardsActivity(ImmutableList<string> mints, IConfiguration configuration) : base(ID, configuration)
    {
        creatorAccount = new Account(configuration.GetSection("Keys:CreatorPrivate").Get<string>(), configuration.GetSection("Keys:CreatorPublic").Get<string>());
        this.mints = mints;
        Rewards = new Dictionary<string, ulong>();
    }

    public override async Task<ActivityResult> Execute(DateTime executionDate, IRpcScheduler rpcScheduler, IRpcClient rpcClient, ILogger logger = null, IProgress<ExecutionProgress> progress = null)
    {
        ClusterSnapshot clusterSnapshot = await GetClusterSnapshot(rpcScheduler, rpcClient, logger);

        Rewards.Clear();
        if (mints.Count == 0)
        {
            return ActivityResult.Factory(Id, executionDate, true,
                ("rewards", 0),
                ("lamports_per_mint", 0),
                ("rewards_percentage", 0),//TODO change
                ("owners_number", 0),
                ("total_fees", 0));
        }
        ulong rewardsLamports = 0, lamportsPerMint = 0, totalFees = 0;
        var failure = false;

        progress?.Report(new ExecutionProgress(Id, GetActivityStepCount(), new ExecutionProgress.Step(2, "Building owners dictionary snapshot", 0F)));
        Progress<float> ownersProgress = new Progress<float>(p => progress?.Report(new ExecutionProgress(Id, GetActivityStepCount(), new ExecutionProgress.Step(1, "Building owners dictionary snapshot", p))));

        var oneOfOwners = rpcScheduler.Schedule(() => Solmango.GetOwnersByCollection(rpcClient, mints, ownersProgress));
        if (oneOfOwners.TryPickT1(out var saturatedEx, out var dictToken))
        {
            logger?.LogError($"Scheduler saturated, fatal error");
            return ActivityResult.Failure(Id, executionDate);
        }
        // Got the token for the dictionary job
        if ((await dictToken).TryPickT1(out var solmangoEx, out var owners))
        {
            logger?.LogError($"Unable to retrieve owners dict {solmangoEx.Message}");
            return ActivityResult.Failure(Id, executionDate);
        }
        // Got the owners dictionary
        totalFees = clusterSnapshot.FeesInfo.FeeCalculator.LamportsPerSignature * (ulong)owners.Count;
        var oneOfBalance = rpcScheduler.Schedule(() => rpcClient.GetBalanceAsync(creatorAccount.PublicKey));
        if (oneOfBalance.TryPickT1(out saturatedEx, out var balanceToken))
        {
            logger?.LogError($"Scheduler saturated, fatal error");
            return ActivityResult.Failure(Id, executionDate);
        }

        // Got the balance token
        var balanceResponse = await balanceToken;
        if (!balanceResponse.WasRequestSuccessfullyHandled)
        {
            return ActivityResult.Failure(Id, executionDate);
        }
        rewardsLamports = balanceResponse.Result.Value - totalFees;
        lamportsPerMint = rewardsLamports / (ulong)mints.Count;
        logger?.LogInformation($"\n- rewards: {rewardsLamports}\n- lamports per mint: {lamportsPerMint}\n- rewards percentage: {0}%");
        progress?.Report(new ExecutionProgress(Id, GetActivityStepCount(), new ExecutionProgress.Step(3, "Executing rewards transactions", 0F)));
        if (configuration.GetSection("Debug:InhibitTransactions").Get<bool>() && !failure)
        {
            return ActivityResult.Factory(Id, executionDate, true,
                ("rewards", rewardsLamports),
                ("lamports_per_mint", lamportsPerMint),
                ("rewards_percentage", 0),
                ("owners_number", owners.Count),
                ("total_fees", totalFees));
        }

        int current = 0;
        foreach (KeyValuePair<string, List<string>> pair in owners)
        {
            var currentAmount = lamportsPerMint * (ulong)pair.Value.Count;
            PublicKey destination = new(pair.Key);

            var oneOfTx = rpcScheduler.Schedule(() => rpcClient.SendTransactionAsync(new TransactionBuilder()
                .SetRecentBlockHash(clusterSnapshot.BlockHash.Blockhash)
                .SetFeePayer(creatorAccount)
                .AddInstruction(SystemProgram.Transfer(creatorAccount.PublicKey, destination, currentAmount))
                .Build(creatorAccount)));

            if (oneOfTx.TryPickT1(out saturatedEx, out var txToken))
            {
                logger?.LogError($"Scheduler saturated, fatal error");
                failure = true;
            }
            // Got the transaction
            var txResponse = await txToken;
            if (txResponse.WasRequestSuccessfullyHandled)
            {
                //logger?.LogInfo($"Sent {currentAmount} to {destination.Key}");
                Rewards.Add(destination.Key, currentAmount);
            }
            else
            {
                logger?.LogError($"Unable to execute transaction, error[{txResponse.ServerErrorCode}]: {txResponse.Reason}");
                failure = true;
            }
            progress?.Report(new ExecutionProgress(Id, GetActivityStepCount(), new ExecutionProgress.Step(3, "Executing rewards transactions", ((float)++current) / owners.Count)));
        }

        return ActivityResult.Factory(Id, executionDate, true,
                ("rewards", rewardsLamports),
                ("lamports_per_mint", lamportsPerMint),
                ("rewards_percentage", 0),
                ("owners_number", owners.Count),
                ("total_fees", totalFees));
    }

    public override int GetActivityStepCount() => 3;
}