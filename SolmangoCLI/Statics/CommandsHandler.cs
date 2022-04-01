using BetterHaveIt;
using HandierCli;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SolmangoCLI.DecentralizedActivities;
using SolmangoCLI.Objects;
using SolmangoCLI.Services;
using SolmangoNET;
using SolmangoNET.Rpc;
using Solnet.Programs;
using Solnet.Programs.Models.TokenProgram;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Core.Http;
using Solnet.Rpc.Messages;
using Solnet.Rpc.Models;
using Solnet.Wallet;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace SolmangoCLI.Statics;

public static class CommandsHandler
{
    public static async Task ScrapeCommand(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        IConfiguration configuration = services.GetService<IConfiguration>();
        IRpcScheduler rpcScheduler = services.GetService<IRpcScheduler>();
        handler.GetKeyed("-n", out var name);
        handler.GetKeyed("-s", out var symbol);
        handler.GetKeyed("-u", out var updateAuthority);
        if ((name == null || name == string.Empty) && (symbol == null || symbol == string.Empty) && (updateAuthority == null || updateAuthority == string.Empty)) return;
        if (!ParseCluster(handler, configuration, logger, out var cluster)) return;

        IRpcClient rpcClient = ClientFactory.GetClient(cluster);
        logger.LogInformation($"Scraping on {cluster} with parameters: name: {name} | symbol: {symbol} | updateAuthority: {updateAuthority}");
        var oneOfMints = rpcScheduler.Schedule(() => Solmango.ScrapeCollectionMints(rpcClient, name, symbol, updateAuthority != string.Empty ? new PublicKey(updateAuthority) : null));
        if (oneOfMints.TryPickT1(out var saturatedEx, out var token))
        {
            logger.LogError($"Rpc scheduler saturated");
            return;
        }
        var oneOf = await ConsoleAwaiter.Factory().Frames(8, "|", "/", "-", "\\").Info("Scraping collection ").Build().Await(Task.Run(async () => await token));
        if (oneOf.TryPickT1(out var solmangoEx, out var mints))
        {
            logger.LogError($"Rpc error scraping collection: {solmangoEx.Message}");
            return;
        }
        CollectionData collectionData = new()
        {
            UpdateAuthority = updateAuthority,
            Name = name,
            Symbol = symbol,
            Mints = ImmutableList.CreateRange(from e in mints select e.Item1)
        };
        var (path, fileName) = PathExtensions.Split(handler.GetPositional(0));
        logger.LogInformation($"Found {mints.Count} mints, result -> {path + fileName}");
        Serializer.SerializeJson(path, fileName, collectionData);
    }

    public static async Task DistributeDividends(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        IConfiguration configuration = services.GetService<IConfiguration>();
        IRpcScheduler rpcScheduler = services.GetService<IRpcScheduler>();
        Account fundAccount = new Account(configuration.GetSection("Keys:FundPrivate").Get<string>(), configuration.GetSection("Keys:FundPublic").Get<string>());
        string holderFileName = handler.GetPositional(0);
        if (!Serializer.DeserializeJson(string.Empty, holderFileName, out Dividends holders))
        {
            logger.LogError($"Unable to find holders file: {holderFileName}");
            return;
        }
        if (!ParseCluster(handler, configuration, logger, out var cluster)) return;
        IRpcClient rpcClient = ClientFactory.GetClient(cluster);

        var oneOfBalance = rpcScheduler.Schedule(() => rpcClient.GetBalanceAsync(fundAccount.PublicKey));
        if (oneOfBalance.TryPickT1(out var saturatedEx, out var balanceTok))
        {
            logger.LogError($"Rpc scheduler saturated");
            return;
        }
        var balanceRes = await balanceTok;
        if (!balanceRes.WasRequestSuccessfullyHandled)
        {
            logger.LogError($"Unable to retrieve balance");
            return;
        }
        var oneOfCluster = rpcScheduler.Schedule(() => Solmango.GetClusterSnapshot(rpcClient));
        if (oneOfCluster.TryPickT1(out saturatedEx, out var clusterToken))
        {
            logger.LogError($"Scheduler saturated, fatal error");
            return;
        }
        var oneOfSnapshot = await clusterToken;
        if (oneOfSnapshot.TryPickT1(out var solmangoEx, out var clusterSnapshot))
        {
            logger.LogError($"Unable to retrieve cluster snapshot, RPC error: {saturatedEx.Message}");
            return;
        }
        ulong totalFees = clusterSnapshot.FeesInfo.FeeCalculator.LamportsPerSignature * (ulong)holders.HoldersArray.Length;

        ulong balance = balanceRes.Result.Value;
        ulong amountPerShare = (ulong)((balance - totalFees) / 100F);
        int successCount = 0;
        logger.LogInformation($"Fund balance: {balance.ToSOL()}\nTotal estimated fees: {totalFees.ToSOL()} SOL\nPer share: {amountPerShare.ToSOL()} SOL", ConsoleColor.DarkMagenta);
        var smtpClient = new SmtpClient()
        {
            Host = "ssl0.ovh.net",
            Port = 587,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = new NetworkCredential(configuration.GetSection("Preferences:Email").Get<string>(), configuration.GetSection("Preferences:EmailPassword").Get<string>()),
            EnableSsl = true
        };
        Task[] smtpTasks = new Task[holders.HoldersArray.Length];
        for (var i = 0; i < holders.HoldersArray.Length; i++)
        {
            ShareHolder holder = holders.HoldersArray[i];
            ulong dividend = (ulong)(holder.SharesPercentage * amountPerShare);
            logger.LogInformation($"\nShare holder", ConsoleColor.DarkCyan);
            logger.LogInformation($"{holder}\nProfits: {dividend.ToSOL()} SOL");

            PublicKey destination = new(holder.Address);
            if (configuration.GetSection("Debug:InhibitTransactions").Get<bool>())
            {
                logger.LogInformation($"Sent {dividend.ToSOL()} SOL", ConsoleColor.Green);
                successCount++;
                if (configuration.GetSection("Preferences:EnableEmail").Get<bool>())
                {
                    smtpTasks[i] = smtpClient.SendMailAsync(MailTemplates.Dividend(configuration.GetSection("Preferences:Email").Get<string>(), holder, dividend.ToSOL().ToString()));
                }
                continue;
            }

            var oneOfTx = rpcScheduler.Schedule(() => rpcClient.SendTransactionAsync(new TransactionBuilder()
                .SetRecentBlockHash(clusterSnapshot.BlockHash.Blockhash)
                .SetFeePayer(fundAccount)
                .AddInstruction(SystemProgram.Transfer(fundAccount.PublicKey, destination, dividend))
                .Build(fundAccount)));

            if (oneOfTx.TryPickT1(out saturatedEx, out var txToken))
            {
                logger?.LogError($"Scheduler saturated, fatal error");
            }
            // Got the transaction
            var txResponse = await txToken;
            if (txResponse.WasRequestSuccessfullyHandled)
            {
                logger.LogInformation($"Sent {dividend.ToSOL()} SOL", ConsoleColor.Green);
                if (configuration.GetSection("Preferences:EnableEmail").Get<bool>())
                {
                    smtpTasks[i] = smtpClient.SendMailAsync(MailTemplates.Dividend(configuration.GetSection("Preferences:Email").Get<string>(), holder, dividend.ToSOL().ToString()));
                }
                successCount++;
            }
            else
            {
                logger?.LogError($"Unable to execute transaction, error[{txResponse.ServerErrorCode}]: {txResponse.Reason}");
            }
        }
        await ConsoleAwaiter.Factory().Info("Sending emails to share holders ").Frames(8, "|", "/", "-", "\\").Build()
            .Await(Task.WhenAll(smtpTasks.Where(t => t is not null)));

        logger.LogInformation($"\nDividends distribution completed with {holders.HoldersArray.Length - successCount} failures");
    }

    public static async Task ExecuteActivity(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        IActivityProvider activityProvider = services.GetService<IActivityProvider>();
        IRpcScheduler rpcScheduler = services.GetService<IRpcScheduler>();
        CommandLineService commandLineService = services.GetService<CommandLineService>();
        string activityId = handler.GetPositional(0);
        DecentralizedActivity activity = activityProvider.GetActivity(activityId);
        if (activity == null)
        {
            logger.LogError($"Unable to find activity with id {activityId}");
            return;
        }
        Progress<ExecutionProgress> prog = new Progress<ExecutionProgress>((prog) =>
        {
            logger.LogInformation($"Activity {activityId}, step {prog.CurrentStep.Number}/{prog.StepCount}, {prog.CurrentStep.Description} at {prog.CurrentStep.Progress * 100F:0.0}%", false);
        });

        await activity.Execute(DateTime.Now, rpcScheduler, ClientFactory.GetClient(Cluster.DevNet), logger, prog);
        //commandLineService.Cli.Logger.LogInfo(Environment.NewLine);
    }

    public static async Task AirdddropToHolders(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        IConfiguration configuration = services.GetService<IConfiguration>();
        //if (!ParseCluster(handler, configuration, logger, out var cluster)) return;
        IRpcClient rpcClient = ClientFactory.GetClient("https://api.devnet.solana.com");
        //var hashList = handler.GetPositional(0);
        //var holderList = handler.GetPositional(1);
        //Account fundAccount = new Account(configuration.GetSection("Keys:FundPrivate").Get<string>(), configuration.GetSection("Keys:FundPublic").Get<string>());
        Account fundAccount = new Account("118NUZLXcRt8TyJo21TXA4C9BrTuXEBFfKgFtk7vSgHuuJ4iz2rKfmyeHGz45SoktgkTpdXss1KkNPqecx8NFNk", "EX2teQbpUAYjwcfmup9mJabqXgNLyVB1bhmFMyj7imXz");
    }

    public static async Task<RequestResult<string>> AirdropToHolders(ILogger logger, string sourceTokenAccount, string toWalletAccount, Account sourceAccountOwner, string tokenMint, ulong ammount = 1)
    {
        IRpcClient activeRpcClient = ClientFactory.GetClient("https://api.devnet.solana.com");
        RequestResult<ResponseValue<BlockHash>> blockHash = await activeRpcClient.GetRecentBlockHashAsync();
        RequestResult<ulong> rentExemptionAmmount = await activeRpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize);

        List<Solnet.Rpc.Models.TokenAccount> lortAccounts = await GetOwnedTokenAccounts(toWalletAccount, tokenMint, "", activeRpcClient);
        byte[] transaction;
        if (lortAccounts != null && lortAccounts.Count > 0)
        {
            transaction = new TransactionBuilder().SetRecentBlockHash(blockHash.Result.Value.Blockhash).
                AddInstruction(TokenProgram.Transfer(new PublicKey(sourceTokenAccount),
                new PublicKey(lortAccounts[0].ToString()),
                ammount,
                sourceAccountOwner.PublicKey))
                .Build(sourceAccountOwner);
        }
        else
        {
            var newAccKeypair = new Account();
            transaction = new TransactionBuilder().SetRecentBlockHash(blockHash.Result.Value.Blockhash).
                SetFeePayer(sourceAccountOwner).
                AddInstruction(
                SystemProgram.CreateAccount(
                    sourceAccountOwner.PublicKey,
                    newAccKeypair.PublicKey,
                    (ulong)rentExemptionAmmount.Result,
                    TokenProgram.TokenAccountDataSize,
                    TokenProgram.ProgramIdKey)).
                AddInstruction(
                TokenProgram.InitializeAccount(
                    newAccKeypair.PublicKey,
                    new PublicKey(tokenMint),
                    new PublicKey(toWalletAccount))).
                AddInstruction(TokenProgram.Transfer(new PublicKey(sourceTokenAccount),
                    newAccKeypair.PublicKey,
                    ammount,
                    sourceAccountOwner.PublicKey))
                .Build(new List<Account>()
                {
                        sourceAccountOwner,
                        newAccKeypair
                });
        }
        var tx = await activeRpcClient.SendTransactionAsync(Convert.ToBase64String(transaction));
        logger.LogInformation(tx.WasSuccessful.ToString());
        return tx;
    }

    public static async Task<List<Solnet.Rpc.Models.TokenAccount>> GetOwnedTokenAccounts(string walletPubKey, string tokenMintPubKey, string tokenProgramPublicKey, IRpcClient activeRpcClient)
    {
        var result = await activeRpcClient.GetTokenAccountsByOwnerAsync(walletPubKey, tokenMintPubKey, tokenProgramPublicKey);
        if (result.Result != null && result.Result.Value != null)
        {
            return result.Result.Value;
        }
        return null;
    }

    private static bool ParseCluster(ArgumentsHandler handler, IConfiguration configuration, ILogger logger, out Cluster cluster)
    {
        cluster = Cluster.DevNet;
        if (handler.GetKeyed("-d", out var network))
        {
            try
            {
                cluster = Enum.Parse<Cluster>(network);
            }
            catch (Exception ex)
            {
                logger.LogError($"Unable to parse RPC client: {ex}");
                try
                {
                    cluster = Enum.Parse<Cluster>(configuration.GetSection("Preferences:DefaultCluster").Get<string>());
                }
                catch (Exception e)
                {
                    logger.LogError($"Unable to parse RPC client from options file: {e.ToString()}");
                    return false;
                }
            }
        }
        else
        {
            try
            {
                cluster = Enum.Parse<Cluster>(configuration.GetSection("Preferences:DefaultCluster").Get<string>());
            }
            catch (Exception e)
            {
                logger.LogError($"Unable to parse RPC client from options file: {e}");
                return false;
            }
        }
        return true;
    }
}