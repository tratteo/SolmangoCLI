using HandierCli;
using SolmangoNET;
using SolmangoNET.Rpc;
using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Wallet;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace SolmangoCLI;

public static class CommandsHandler
{
    public static async Task ScrapeCommand(ArgumentsHandler handler, Options options, IRpcScheduler rpcScheduler, Logger logger)
    {
        handler.GetKeyed("-n", out var name);
        handler.GetKeyed("-s", out var symbol);
        handler.GetKeyed("-u", out var updateAuthority);
        Cluster cluster = Cluster.DevNet;
        try
        {
            cluster = Enum.Parse<Cluster>(options.PreferencesOptions.DefaultCluster);
        }
        catch (Exception)
        {
            logger.LogError("Unable to parse RPC client from options file.");
            return;
        }
        if (handler.GetKeyed("-d", out var network))
        {
            try
            {
                cluster = Enum.Parse<Cluster>(network);
            }
            catch (Exception)
            {
                logger.LogError("Unable to parse RPC client, defaulting to MainNet");
                cluster = Cluster.MainNet;
            }
        }
        IRpcClient rpcClient = ClientFactory.GetClient(cluster);
        logger.LogInfo($"Scraping on {cluster} with parameters: name: {name} | symbol: {symbol} | updateAuthority: {updateAuthority}");
        var oneOfMints = rpcScheduler.Schedule(() => Solmango.ScrapeCollectionMints(rpcClient, name, symbol, updateAuthority != string.Empty ? new PublicKey(updateAuthority) : null));
        if (oneOfMints.TryPickT1(out var saturatedEx, out var token))
        {
            logger.LogError($"Rpc scheduler saturated");
            return;
        }
        var oneOf = await Program.CONSOLE_AWAITER.Info("Scraping collection ").Build().Await(Task.Run(async () => await token));
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
        var outName = handler.GetPositional(0) + ".json";
        logger.LogInfo($"Found {mints.Count} mints, result -> {Paths.REPORTS_FOLDER_PATH + outName}");
        Serializer.SerializeJson(Paths.REPORTS_FOLDER_PATH, outName, collectionData);
    }

    public static async Task DistributeDividends(ArgumentsHandler handler, Options options, IRpcScheduler rpcScheduler, Logger logger)
    {
        Account fundAccount = new Account(options.PreferencesOptions.FundPrivateKey, options.PreferencesOptions.FundPublicKey);
        string holderFileName = handler.GetPositional(0);
        if (!Serializer.DeserializeJson(string.Empty, holderFileName, out Dividends holders))
        {
            logger.LogError($"Unable to find holders file: {holderFileName}");
            return;
        }
        Cluster cluster = Cluster.DevNet;
        try
        {
            cluster = Enum.Parse<Cluster>(options.PreferencesOptions.DefaultCluster);
        }
        catch (Exception)
        {
            logger.LogError("Unable to parse RPC client from options file.");
            return;
        }
        if (handler.GetKeyed("-d", out var network))
        {
            try
            {
                cluster = Enum.Parse<Cluster>(network);
            }
            catch (Exception)
            {
                logger.LogError("Unable to parse RPC client, defaulting to MainNet");
                cluster = Cluster.MainNet;
            }
        }
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
        logger.LogInfo($"Fund balance: {balance.ToSOL()}\nTotal estimated fees: {totalFees.ToSOL()} SOL\nPer share: {amountPerShare.ToSOL()} SOL", ConsoleColor.DarkMagenta);
        var smtpClient = new SmtpClient()
        {
            Host = "ssl0.ovh.net",
            Port = 587,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = new NetworkCredential(options.EmailOptions.ProjectEmail, options.EmailOptions.ProjectEmailPassword),
            EnableSsl = true
        };
        Task[] smtpTasks = new Task[holders.HoldersArray.Length];
        for (var i = 0; i < holders.HoldersArray.Length; i++)
        {
            ShareHolder holder = holders.HoldersArray[i];
            ulong dividend = (ulong)(holder.SharesPercentage * amountPerShare);
            logger.LogInfo($"\nShare holder", ConsoleColor.DarkCyan);
            logger.LogInfo($"{holder}\nProfits: {dividend.ToSOL()} SOL");

            PublicKey destination = new(holder.Address);
            if (options.DebugOptions.InhibitTransactions)
            {
                logger.LogInfo($"Sent {dividend.ToSOL()} SOL", ConsoleColor.Green);
                successCount++;
                if (options.DebugOptions.ForceEmail)
                {
                    smtpTasks[i] = smtpClient.SendMailAsync(MailTemplates.Dividend(options.EmailOptions.ProjectEmail, holder, dividend.ToSOL().ToString()));
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
                logger.LogInfo($"Sent {dividend.ToSOL()} SOL", ConsoleColor.Green);
                if (options.EmailOptions.Enable)
                {
                    smtpTasks[i] = smtpClient.SendMailAsync(MailTemplates.Dividend(options.EmailOptions.ProjectEmail, holder, dividend.ToSOL().ToString()));
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

        logger.LogInfo($"\nDividends distribution completed with {holders.HoldersArray.Length - successCount} failures");
    }
}