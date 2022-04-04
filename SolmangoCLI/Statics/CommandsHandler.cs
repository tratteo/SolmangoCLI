using BetterHaveIt;
using HandierCli;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OneOf;
using SolmangoCLI.Objects;
using SolmangoCLI.Settings;
using SolmangoNET;
using SolmangoNET.Exceptions;
using SolmangoNET.Rpc;
using Solnet.KeyStore;
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
            if (hashList != null && hashList.Count > 0)
            {
                ConsoleProgressBar progressBar = new ConsoleProgressBar(50);
                var res = await SolmangoNET.Solmango.GetOwnersByCollection(rpcClient, hashList, progressBar);
                if (res.TryPickT1(out var ex, out var owners))
                {
                    logger.LogError(ex.Reason);
                    progressBar.Dispose();
                    return false;
                }
                progressBar.Dispose();
                var json = JsonConvert.SerializeObject(owners, Formatting.Indented);
                File.WriteAllText(handler.GetPositional(1), json);
                int sum = 0;
                foreach (var pair in owners)
                {
                    sum += pair.Value.Count;
                }

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
        var connectionOption = services.GetRequiredService<IOptionsMonitor<ConnectionSettings>>();
        var rpcClient = ClientFactory.GetClient(connectionOption.CurrentValue.ClusterEndpoint);
        ConsoleProgressBar progressBar = new ConsoleProgressBar(50);
        int sum = 0;
        List<string> failedAddresses = new List<string>();
        try
        {
            var key = File.ReadAllText(handler.GetPositional(0));
            //gets keys
            var keys = JsonConvert.DeserializeObject<KeyPair>(key);
            Account sender;
            if (keys is not null)
                sender = new Account(keys!.PrivateKey, keys.PublicKey);
            else
            {
                logger.LogError("Couldn't Parse keypair.json");
                return false;
            }
            //gets dictionary
            var str = File.ReadAllText(handler.GetPositional(2));
            var dic = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(str);
            int i = 1;
            if (dic is not null)
            {
                foreach (var pair in dic)
                {
                    var res = await SendSplToken(rpcClient, sender, pair.Key, handler.GetPositional(1), (ulong)pair.Value.Count);
                    if (res.TryPickT1(out var ex, out var success))
                    {
                        logger.LogError(ex.ToString());
                        return false;
                    }
                    else
                    {
                        if (success)
                        {
                            sum += pair.Value.Count;
                        }
                        else
                        {
                            failedAddresses.Add(pair.Key);
                        }
                    }
                    i++;
                    progressBar.Report((float)i / dic.Count);
                }
            }
            else
            {
                logger.LogError("Couldn't parse the dictionary.json");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex.ToString());
            return false;
        }
        finally
        {
            progressBar.Dispose();
            if (failedAddresses.Count > 0)
            {
                logger.LogError("Sent {sum} Tokens but Failed to send tokens to these addresses: \n {addresses}", sum, String.Join("\n", failedAddresses));
            }
            else
            {
                logger.LogInformation(" sent {sum} Tokens ", sum);
            }
        }
    }

    public static async Task<string?> TryGetAssociatedTokenAccount(IRpcClient rpcClient, string address, string tokenMint)
    {
        var res = await rpcClient.GetTokenAccountsByOwnerAsync(address, tokenMint);
        return res.Result is null ? null : res.Result.Value is not null && res.Result.Value.Count > 0 ? res.Result.Value[0].PublicKey : null;
    }

    /// <summary>
    ///   Sends a custom SPL token. Create the address on the receiver account if does not exists.
    /// </summary>
    /// <param name="rpcClient"> </param>
    /// <param name="toPublicKey"> </param>
    /// <param name="fromAccount"> </param>
    /// <param name="tokenMint"> </param>
    /// <param name="amount"> </param>
    /// <returns> </returns>
    public static async Task<OneOf<bool, SolmangoRpcException>> SendSplToken(IRpcClient rpcClient, Account fromAccount, string toPublicKey, string tokenMint, ulong amount)
    {
        var blockHash = await rpcClient.GetLatestBlockHashAsync();
        var rentExemptionAmmount = await rpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize);
        var first = TryGetAssociatedTokenAccount(rpcClient, toPublicKey, tokenMint);
        var second = TryGetAssociatedTokenAccount(rpcClient, fromAccount.PublicKey, tokenMint);
        await Task.WhenAll(first, second);

        var associatedAccount = first.Result;
        var sourceTokenAccount = second.Result;
        if (sourceTokenAccount is null) return false;
        byte[] transaction;
        if (associatedAccount is not null)
        {
            transaction = new TransactionBuilder().SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(fromAccount)
                .AddInstruction(TokenProgram.Transfer(new PublicKey(sourceTokenAccount),
                new PublicKey(associatedAccount),
                amount.ToLamports(),
                fromAccount.PublicKey))
                .Build(fromAccount);
        }
        else
        {
            var newAccKeypair = new Account();
            transaction = new TransactionBuilder().SetRecentBlockHash(blockHash.Result.Value.Blockhash).
                SetFeePayer(fromAccount).
                AddInstruction(
                SystemProgram.CreateAccount(
                    fromAccount.PublicKey,
                    newAccKeypair.PublicKey,
                    rentExemptionAmmount.Result,
                    TokenProgram.TokenAccountDataSize,
                    TokenProgram.ProgramIdKey)).
                AddInstruction(
                TokenProgram.InitializeAccount(
                    newAccKeypair.PublicKey,
                    new PublicKey(tokenMint),
                    new PublicKey(toPublicKey))).
                AddInstruction(TokenProgram.Transfer(new PublicKey(sourceTokenAccount),
                    newAccKeypair.PublicKey,
                    amount.ToLamports(),
                    fromAccount.PublicKey))
                .Build(new List<Account>()
                {
                        fromAccount,
                        newAccKeypair
                });
        }
        var res = await rpcClient.SendTransactionAsync(Convert.ToBase64String(transaction));
        return !res.WasRequestSuccessfullyHandled ? new SolmangoRpcException(res.Reason, res.ServerErrorCode) : true;
    }
}