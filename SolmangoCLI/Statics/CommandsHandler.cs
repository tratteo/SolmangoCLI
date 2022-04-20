using BetterHaveIt;
using HandierCli;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SolmangoCLI.Objects;
using SolmangoCLI.Settings;
using SolmangoNET;
using SolmangoNET.Rpc;
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
        var connectionSettings = services.GetRequiredService<IOptionsMonitor<ConnectionSettings>>();
        handler.GetKeyed("-n", out var name);
        handler.GetKeyed("-s", out var symbol);
        handler.GetKeyed("-u", out var updateAuthority);

        var rpcClient = ClientFactory.GetClient(connectionSettings.CurrentValue.ClusterEndpoint);
        logger.LogInformation("Scraping on {endpoint} with parameters: name: {name} | symbol: {symbol} | updateAuthority: {updateAuthority}", connectionSettings.CurrentValue.ClusterEndpoint, name, symbol, updateAuthority);
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
        try
        {
            var keypair = new Account(handler.GetPositional(0), handler.GetPositional(1));
            PrivateKey privateKey = new PrivateKey(handler.GetPositional(0));
            var intarray = privateKey.KeyBytes.Select(k => (int)k).ToArray();
            Serializer.SerializeJson(handler.GetPositional(1), intarray, false, new JsonSerializerSettings() { Formatting = Formatting.None });
            logger.LogInformation("File generated correctly");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("Exception: {ex}", ex.ToString());
            return false;
        }
    }

    public static async Task<bool> GetTokenSupply(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        var connectionOption = services.GetRequiredService<IOptionsMonitor<ConnectionSettings>>();
        var rpcClient = ClientFactory.GetClient(connectionOption.CurrentValue.ClusterEndpoint);
        try
        {
            var res = await rpcClient.GetTokenSupplyAsync(handler.GetPositional(0));
            var supply = res.Result.Value;
            logger.LogInformation("Supply: {supply} Decimals: {decimal}", supply.AmountDouble, supply.Decimals);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
            return false;
        }
    }

    public static async Task<bool> RetriveHolders(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        var connectionOption = services.GetRequiredService<IOptionsMonitor<ConnectionSettings>>();
        var rpcClient = ClientFactory.GetClient(connectionOption.CurrentValue.ClusterEndpoint);
        try
        {
            Serializer.DeserializeJson<ImmutableList<string>>(handler.GetPositional(0), out var hashList);
            if (hashList is null || hashList.Count <= 0)
            {
                logger.LogError("Couldn't find the hash list path or the file is empty");
                return false;
            }
            var progressBar = new ConsoleProgressBar(50);
            var res = await Solmango.GetOwnersByCollection(rpcClient, hashList, progressBar);
            if (res.TryPickT1(out var ex, out var owners))
            {
                logger.LogError("Rpc exception: {ex}", ex.Reason);
                progressBar.Dispose();
                return false;
            }
            progressBar.Dispose();
            Serializer.SerializeJson(handler.GetPositional(1), owners, true, new JsonSerializerSettings() { Formatting = Formatting.Indented });
            var sum = 0;
            foreach (var pair in owners)
            {
                sum += pair.Value.Count;
            }

            logger.LogInformation("Holders count: {holders}\nMints count: {mints}", owners.Keys.Count, sum);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("Exception {ex}: ", ex.Message);
            return false;
        }
    }
    //TODO add send spl token 
    public static async Task<bool> DistributeTokens(ArgumentsHandler handler, IServiceProvider services, ILogger logger)
    {
        var connectionOption = services.GetRequiredService<IOptionsMonitor<ConnectionSettings>>();
        var rpcClient = ClientFactory.GetClient(connectionOption.CurrentValue.ClusterEndpoint);
        var progressBar = new ConsoleProgressBar(50);
        var sum = 0;
        var failedAddresses = new Dictionary<string, List<string>>();
        try
        {
            if (!Serializer.DeserializeJson<KeyPair>(handler.GetPositional(0), out var keys) || keys is null)
            {
                logger.LogError("Couldn't Parse {keypair}", Path.GetFileName(handler.GetPositional(0)));
                return false;
            }

            var sender = new Account(keys.PrivateKey, keys.PublicKey);

            if (!Serializer.DeserializeJson<Dictionary<string, List<string>>>(handler.GetPositional(2), out var dic) || dic is null)
            {
                logger.LogError("Couldn't parse {dictionary}", Path.GetFileName(handler.GetPositional(2)));
                return false;
            }

            var progressCount = 1;
            foreach (var pair in dic)
            {
                var res = await Solmango.SendSplToken(rpcClient, sender, pair.Key, handler.GetPositional(1), (ulong)pair.Value.Count);
                if (res.TryPickT1(out var ex, out var success))
                {
                    logger.LogError("Rpc exception {ex}", ex.ToString());
                    failedAddresses.Add(pair.Key, pair.Value);
                    continue;
                }
                else
                {
                    if (success)
                    {
                        sum += pair.Value.Count;
                    }
                    else
                    {
                        failedAddresses.Add(pair.Key, pair.Value);
                    }
                }
                progressCount++;
                progressBar.Report((float)progressCount / dic.Count);
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("Exception: {ex}", ex.ToString());
            return false;
        }
        finally
        {
            progressBar.Dispose();
            // the failed addresses
            if (failedAddresses.Count > 0)
            {
                logger.LogError("Sent {sum} Tokens but Failed to send tokens to these addresses: \n {addresses}", sum, string.Join("\n", failedAddresses.Keys));
                var path = Path.GetDirectoryName(handler.GetPositional(2)) + "\\failedAddressesLog.json";
                Serializer.SerializeJson(path!, failedAddresses);
                logger.LogInformation("Failed delivery addresses dictionary saved at : {path}", path);
            }
            else
            {
                logger.LogInformation("Sent {sum} Tokens ", sum);
            }
        }
    }
}