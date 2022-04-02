using BetterHaveIt;
using HandierCli;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SolmangoCLI.Settings;
using SolmangoNET;
using SolmangoNET.Rpc;
using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Core.Http;
using Solnet.Wallet;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SolmangoCLI.Statics;

public static class CommandsHandler
{
    public static async Task ScrapeCommand(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var rpcScheduler = services.GetRequiredService<IRpcScheduler>();
        var connectionSettings = services.GetRequiredService<IOptionsMonitor<ConnectionSettings>>();
        handler.GetKeyed("-n", out var name);
        handler.GetKeyed("-s", out var symbol);
        handler.GetKeyed("-u", out var updateAuthority);

        var rpcClient = ClientFactory.GetClient(connectionSettings.CurrentValue.ClusterEndpoint);
        logger.LogInformation($"Scraping on {connectionSettings.CurrentValue.ClusterEndpoint} with parameters: name: {name} | symbol: {symbol} | updateAuthority: {updateAuthority}");
        var oneOfMints = rpcScheduler.Schedule(() => Solmango.ScrapeCollectionMints(rpcClient, name, symbol, updateAuthority is not null ? new PublicKey(updateAuthority) : null));
        if (oneOfMints.TryPickT1(out var saturatedEx, out var token))
        {
            logger.LogError($"Rpc scheduler saturated");
            return;
        }
        var oneOf = await ConsoleAwaiter
            .Factory()
            .Frames(8, "|", "/", "-", "\\").Info("Scraping collection ")
            .Build()
            .Await(Task.Run(async () => await token));
        if (oneOf.TryPickT1(out var solmangoEx, out var mints))
        {
            logger.LogError($"Rpc error scraping collection: {solmangoEx.Reason}");
            return;
        }

        var (path, fileName) = PathExtensions.Split(handler.GetPositional(0));
        logger.LogInformation($"Found {mints.Count} mints, result -> {path + fileName}");
        Serializer.SerializeJson(path, fileName, ImmutableList.CreateRange(from e in mints select e.Item1));
    }

    public static async Task<bool> RetriveHolders(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        var connectionOption = services.GetRequiredService<IOptionsMonitor<ConnectionSettings>>();
        var rpcClient = ClientFactory.GetClient(connectionOption.CurrentValue.ClusterEndpoint);
        try
        {
            var hash = File.ReadAllText(handler.GetPositional(0));
            var hashList = JsonConvert.DeserializeObject<ImmutableList<string>>(hash);
            ConsoleProgressBar progressBar = new ConsoleProgressBar(50);
            if (hashList != null && hashList.Count > 0)
            {
                var res = await SolmangoNET.Solmango.GetOwnersByCollection(rpcClient, hashList, progressBar);
                if (res.TryPickT1(out var ex, out var owners))
                {
                    logger.LogError(ex.Reason);
                    return false;
                }
                var json = JsonConvert.SerializeObject(owners, Formatting.Indented);
                File.WriteAllText(handler.GetPositional(1), json);
                int sum = 0;
                foreach (var pair in owners)
                {
                    sum += pair.Value.Count;
                }
                progressBar.Dispose();
                logger.LogInformation("Found {holders} holders after performing scraping on {sum} mints ", owners.Keys.Count, sum);
                return true;
            }
            else
            {
                logger.LogError("Couldn't find the hash list path or the file is empty");
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            return false;
        }
    }

    public static async Task<bool> DistributeTokens(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        return true;
    }

    public static async Task<RequestResult<string>> AirdropToHolders(ILogger logger, string sourceTokenAccount, string toWalletAccount, Account sourceAccountOwner, string tokenMint, ulong ammount = 1)
    {
        var activeRpcClient = ClientFactory.GetClient("https://api.devnet.solana.com");
        var blockHash = await activeRpcClient.GetRecentBlockHashAsync();
        var rentExemptionAmmount = await activeRpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize);

        var lortAccounts = await GetOwnedTokenAccounts(toWalletAccount, tokenMint, "", activeRpcClient);
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
                    rentExemptionAmmount.Result,
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
}