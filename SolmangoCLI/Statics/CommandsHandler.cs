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
using Solnet.Programs.Utilities;
using Solnet.Rpc;
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

        handler.GetKeyed("-n", out var name);
        handler.GetKeyed("-s", out var symbol);
        handler.GetKeyed("-u", out var updateAuthority);

        var rpcClient = services.GetRpcClient();
        logger.LogInformation("Scraping on {endpoint} with parameters: name: {name} | symbol: {symbol} | updateAuthority: {updateAuthority}", services.GetEndPoint(), name, symbol, updateAuthority);
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
            logger.LogError("Rpc error scraping collection: {reason}", solmangoEx.Reason);
            return;
        }

        logger.LogInformation("Found {count} mints, result -> {path}", mints.Count, handler.GetPositional(0));
        Serializer.SerializeJson(handler.GetPositional(0), ImmutableList.CreateRange(from e in mints select e.Item1));
    }

    public static bool GenerateKeyPairFromBase58Keys(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        var privateKey = handler.GetPositional(0);
        var path = handler.GetPositional(1);
        var readable = handler.HasFlag("/r");
        try
        {
            var pKey = new PrivateKey(privateKey);
            if (!readable)
            {
                var intarray = pKey.KeyBytes.Select(k => (int)k).ToArray();
                Serializer.SerializeJson(path, intarray, false, new JsonSerializerSettings() { Formatting = Formatting.None });
            }
            else
            {
                var account = new Account(pKey.KeyBytes, pKey.KeyBytes[32..]);
                Serializer.SerializeJson(path,
                    account.PrivateKey.Key,
                    false,
                    new JsonSerializerSettings() { Formatting = Formatting.Indented });
            }
            logger.LogInformation("File generated correctly");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("Fatal exception: {ex}", ex.ToString());
            return false;
        }
    }

    public static async void VerifyCliAccount(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        if (!services.TryGetCliAccount(out var account))
        {
            logger.LogError("Unable to find/deserialize CLI account");
            return;
        }
        var rpcClient = services.GetRpcClient();
        if (account is not null)
        {
            var res = await rpcClient.GetBalanceAsync(account.PublicKey);
            if (!res.WasRequestSuccessfullyHandled)
            {
                logger.LogError("Account not valid, check that it represents an existing account");
            }
            else
            {
                logger.LogInformation("CLI account valid\nPublicKey: {p}\nBalance: {b}", account.PublicKey, res.Result.Value.ToSOL());
            }
        }
    }

    public static async void VerifyCliAccount(IServiceProvider services, ILogger logger)
    {
        if (!services.TryGetCliAccount(out var account))
        {
            logger.LogInformation("Unable to find/deserialize CLI account");
            return;
        }
        var rpcClient = services.GetRpcClient();
        if (account is not null)
        {
            var res = await rpcClient.GetBalanceAsync(account.PublicKey);
            if (!res.WasRequestSuccessfullyHandled)
            {
                logger.LogError("Account not valid, check that it represents an existing account");
            }
            else
            {
                logger.LogInformation("\nEndPoint: {e}\nCLI account valid\nPublicKey: {p}\nBalance: {b}",services.GetEndPoint(), account.PublicKey, res.Result.Value.ToSOL());
            }
        }
        return;
    }

    public static async Task<bool> GetTokenSupply(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        var mint = handler.GetPositional(0);
        var rpcClient = services.GetRpcClient();
        var res = await rpcClient.GetTokenSupplyAsync(mint);
        if (!res.WasRequestSuccessfullyHandled || res.Result is null) return false;
        var supply = res.Result.Value;
        logger.LogInformation("Supply: {supply}, decimals: {decimal}", supply.AmountDouble, supply.Decimals);
        return true;
    }

    public static async Task<bool> RetriveHolders(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        var hashListPath = handler.GetPositional(0);
        var path = handler.GetPositional(1);
        var amountOnly = handler.HasFlag("/amount-only");
        var rpcClient = services.GetRpcClient();
        try
        {
            Serializer.DeserializeJson<ImmutableList<string>>(hashListPath, out var hashList);
            if (hashList is null || hashList.Count <= 0)
            {
                logger.LogError("Couldn't find the hash list path or the file is empty");
                return false;
            }
            var progressBar = new ConsoleProgressBar(50);
            var res = Solmango.GetOwnersByCollectionBatch(rpcClient, hashList, progressBar);
            if (res.TryPickT1(out var ex, out var owners))
            {
                logger.LogError("Rpc exception: {ex}", ex.Reason);
                progressBar.Dispose();
                return false;
            }
            progressBar.Dispose();
            if (amountOnly)
            {
                var amountOnlyDic = owners.ToDictionary(pair => pair.Key, pair => (ulong)pair.Value.Count);
                Serializer.SerializeJson(path, amountOnlyDic, true, new JsonSerializerSettings() { Formatting = Formatting.Indented });
            }
            else
            {
                Serializer.SerializeJson(path, owners, true, new JsonSerializerSettings() { Formatting = Formatting.Indented });
            }
            var sum = 0;
            foreach (var pair in owners)
            {
                sum += pair.Value.Count;
            }

            logger.LogInformation("Holders count: {holders}\nMints count: {mints}", owners.Keys.Count, sum);

            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("Exception {ex}: ", ex.Message);
            return false;
        }
    }

    public static async Task<bool> SendSplToken(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        if (!services.TryGetCliAccount(out var sender)) return false;
        var receiver = handler.GetPositional(0);
        var mint = handler.GetPositional(1);
        if (!double.TryParse(handler.GetPositional(2), out var amount))
        {
            logger.LogError("Couldn't parse this amount: {amount}", handler.GetPositional(2));
            return false;
        }
        var rpcClient = services.GetRpcClient();

        var res = await Solmango.SendSplToken(rpcClient, sender, receiver, mint, amount);
        if (res.TryPickT1(out var ex, out var success))
        {
            logger.LogError("Rpc exception {ex}", ex.ToString());
        }
        else
        {
            //TODO check if the transaction is confirmed by getting the latest blockhash and see
            logger.LogInformation("Successfully sent {amount} to {receiver}", amount, receiver);
        }
        return true;
    }

    public static async Task<bool> GetHoldersTokenBalance(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        var mint = handler.GetPositional(0);
        var path = handler.GetPositional(1);
        var rpcClient = services.GetRpcClient();
        var progressBar = new ConsoleProgressBar(50);
        var progressCount = 0;

        ulong tokenScraped = 0;
        var holders = new Dictionary<string, ulong>();

        var holder = await Solmango.GetSplTokenHolders(rpcClient, mint);
        if (holder.TryPickT1(out var ex, out var res))
        {
            logger.LogError("Rpc error: {ex}", ex.ToString());
            return false;
        }

        foreach (var pair in res)
        {
            progressCount++;
            progressBar.Report((float)progressCount / res.Count);

            var dataBytes = Convert.FromBase64String(pair.Account.Data[0]);
            var owner = ((ReadOnlySpan<byte>)dataBytes).GetPubKey(32);
            var amount = ((ReadOnlySpan<byte>)dataBytes).GetU64(64);

            if (amount <= 0) continue;
            if (holders.TryGetValue(owner, out var _))
            {
                holders[owner] += amount;
            }
            else
            {
                holders.Add(owner, amount);
            }

            tokenScraped += amount;
        }
        progressBar.Dispose();
        Serializer.SerializeJson(path, holders, true, new JsonSerializerSettings() { Formatting = Formatting.Indented });
        logger.LogInformation("Found {amount} holders for a total of {amount} tokens", holders.Count, tokenScraped);
        logger.LogInformation("Saved dictionary -> {}", path);

        return true;
    }

    public static async Task<bool> DistributeTokensToHoldersDictionary(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        var mint = handler.GetPositional(0);
        var path = handler.GetPositional(1);
        var skip = handler.HasFlag("/s");

        var connectionOption = services.GetRequiredService<IOptionsMonitor<ConnectionSettings>>();
        var rpcClient = services.GetRpcClient();
        var rpcScheduler = services.GetRequiredService<IRpcScheduler>();
        var progressBar = new ConsoleProgressBar(50);
        var sum = 0UL;
        var failedAddresses = new Dictionary<string, ulong>();
        var skipCount = 0;

        if (!services.TryGetCliAccount(out var sender)) return false;
        try
        {
            if (!Serializer.DeserializeJson<Dictionary<string, ulong>>(path, out var dic) || dic is null)
            {
                logger.LogError("Couldn't parse {dictionary}", Path.GetFileName(handler.GetPositional(1)));
                return false;
            }

            var progressCount = 0;
            foreach (var pair in dic)
            {
                progressCount++;
                progressBar.Report((float)progressCount / dic.Count);
                if (skip)
                {
                    var ata = await Solmango.GetAssociatedTokenAccount(rpcClient, pair.Key, mint);
                    if (ata is not null)
                    {
                        var balance = rpcClient.GetTokenAccountBalance(ata);
                        if (balance is not null && balance.Result.Value.AmountUlong > 0)
                        {
                            skipCount++;
                            continue;
                        }
                    }
                }
                var result = rpcScheduler.Schedule(() => Solmango.SendSplToken(rpcClient, sender, pair.Key, mint, pair.Value));
                if (result.TryPickT1(out var satEx, out var job))
                {
                    logger.LogError("Saturation exception {ex}", satEx.ToString());
                    failedAddresses.Add(pair.Key, pair.Value);
                    continue;
                }
                var res = await job;
                if (res.TryPickT1(out var ex, out var success))
                {
                    logger.LogError("Rpc exception {ex}", ex.ToString());
                    failedAddresses.Add(pair.Key, pair.Value);
                    continue;
                }
                else
                {
                    if (success is not null)
                    {
                        sum += pair.Value;
                    }
                    else
                    {
                        failedAddresses.Add(pair.Key, pair.Value);
                    }
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("Fatal exception: {ex}", ex.ToString());
            return false;
        }
        finally
        {
            progressBar.Dispose();
            if (skip)
            {
                logger.LogInformation("Skipped {skipCount} addresses", skipCount);
            }
            if (failedAddresses.Count > 0)
            {
                logger.LogError("Sent {sum} tokens but failed to send tokens to these addresses: \n {addresses}", sum, string.Join("\n", failedAddresses.Keys));
                var failPath = Path.GetDirectoryName(path) + Path.AltDirectorySeparatorChar + "failedAddressesLog.json";
                Serializer.SerializeJson(path, failedAddresses);
                logger.LogInformation("Failed addresses -> {path}", path);
            }
            else
            {
                logger.LogInformation("Sent {sum} tokens ", sum);
            }
        }
    }

    public static async Task<bool> DistributeTokensToHoldersDictionaryBatch(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        var mint = handler.GetPositional(0);
        var path = handler.GetPositional(1);
        var skip = handler.HasFlag("/s");

        var connectionOption = services.GetRequiredService<IOptionsMonitor<ConnectionSettings>>();
        var walletPath = services.GetRequiredService<IOptionsMonitor<PathSettings>>();
        var rpcClient = services.GetRpcClient();

        var batcher = new SolanaRpcBatchWithCallbacks(rpcClient);
        batcher.AutoExecute(Solnet.Rpc.Types.BatchAutoExecuteMode.ExecuteWithCallbackFailures, 20);//reducing batch size in order to prevent blockhash to become obsolete
        var progressBar = new ConsoleProgressBar(50);
        var sum = 0UL;
        var failedAddresses = new Dictionary<string, ulong>();
        var skipCount = 0;

        if (!services.TryGetCliAccount(out var sender)) return false;
        try
        {
            if (!Serializer.DeserializeJson<Dictionary<string, ulong>>(path, out var dic) || dic is null)
            {
                logger.LogError("Couldn't parse {dictionary}", Path.GetFileName(handler.GetPositional(1)));
                return false;
            }

            var progressCount = 0;
            foreach (var pair in dic)
            {
                progressCount++;
                progressBar.Report((float)progressCount / dic.Count);
                if (skip)
                {
                    var ata = await Solmango.GetAssociatedTokenAccount(rpcClient, pair.Key, mint);
                    if (ata is not null)
                    {
                        var balance = await rpcClient.GetTokenAccountBalanceAsync(ata);
                        if (balance is not null && balance.Result.Value.AmountUlong > 0)
                        {
                            skipCount++;
                            continue;
                        }
                    }
                }
                var result = await Solmango.BuildSendSplTokenTransaction(rpcClient, sender, pair.Key, mint, pair.Value);
                if (result.TryPickT1(out var rpcEx, out var transaction))
                {
                    logger.LogError("Rpc Exception in building the transaction", rpcEx.ToString());
                    failedAddresses.Add(pair.Key, pair.Value);
                    continue;
                }

                batcher.SendTransaction(transaction, false, Solnet.Rpc.Types.Commitment.Finalized, (signature, ex) =>
                  {
                      if (ex is not null)
                      {
                          logger.LogError(ex.Message);
                          failedAddresses.Add(pair.Key, pair.Value);
                      }
                      else
                      {
                          sum += pair.Value;
                      }
                  });
            }
            batcher.Flush();

            return failedAddresses.Count <= 0;
        }
        catch (Exception ex)
        {
            logger.LogError("Fatal exception: {ex}", ex.ToString());
            return false;
        }
        finally
        {
            progressBar.Dispose();
            if (skip)
            {
                logger.LogInformation("Skipped {skipCount} addresses", skipCount);
            }
            if (failedAddresses.Count > 0)
            {
                logger.LogError("Sent {sum} tokens but failed to send tokens to these addresses: \n {addresses}", sum, string.Join("\n", failedAddresses.Keys));
                var failPath = Path.GetDirectoryName(path) + Path.AltDirectorySeparatorChar + "failedAddressesLog.json";

                Serializer.SerializeJson(failPath, failedAddresses);

                logger.LogInformation("Failed addresses -> {path}", failPath);
            }
            else
            {
                logger.LogInformation("Sent {sum} tokens ", sum);
            }
        }
    }
}