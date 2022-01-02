// Copyright Siamango

using BetterHaveIt.Repositories;
using HandierCli;
using SolmangoNET.Rpc;
using Solnet.Rpc;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace SolmangoCLI;

internal class Program
{
    public const string OPTIONS_FILE = "options.json";
    public const string HOLDERS_FILE = "holders.json";

    public static readonly ConsoleAwaiter.Builder CONSOLE_AWAITER = ConsoleAwaiter.Factory().Frames(8, "|", "/", "-", "\\");
    private static RepositoryJson<Options> optionsRepository;
    private static IRpcScheduler rpcScheduler;

    public static void InitializeCache()
    {
        try
        {
            optionsRepository = new RepositoryJson<Options>($"./{OPTIONS_FILE}");
        }
        catch (Exception e)
        {
            Logger.ConsoleInstance.LogError(e.ToString());
            Serializer.SerializeJson(string.Empty, OPTIONS_FILE, new Options());
            Logger.ConsoleInstance.LogWarning("Options file not found, created at: " + OPTIONS_FILE + ", compile it");
            optionsRepository = new RepositoryJson<Options>($"./{OPTIONS_FILE}");
        }

        if (!optionsRepository.Data.Verify())
        {
            Logger.ConsoleInstance.LogError("Options file contains errors");
            Console.ReadKey();
            Environment.Exit(1);
        }
    }

    private static async Task Main(string[] args)
    {
        Console.Title = "Solmango cli";
        Logger.ConsoleInstance.LogInfo("----- SOLMANGO CLI -----\n\n", ConsoleColor.DarkCyan);
        InitializeCache();
        rpcScheduler = new BasicRpcScheduler(1000);
        rpcScheduler.Start();
        CommandLine cli = BuildCli();
        optionsRepository.OnHotReloadTry += (success) =>
        {
            if (success)
            {
                cli.Logger.LogInfo("Hot reloaded options");
            }
            else
            {
                cli.Logger.LogError("Unable to hot reload options, contains errors");
            }
        };
        await cli.Run();
    }

    private static CommandLine BuildCli()
    {
        CommandLine cli = CommandLine.Factory().ExitOn("exit", "quit").OnUnrecognized(cmd => Logger.ConsoleInstance.LogError($"{cmd} not recognized")).Build();
        cli.Register(Command.Factory("scrape")
        .Description("Scrape NFTs collections by name, symbol and candyMachineId")
        .ArgumentsHandler(
            ArgumentsHandler.Factory()
            .Positional("out file name")
            .Keyed("-n", "collection name")
            .Keyed("-s", "collection symbol")
            .Keyed("-u", "candy machine id")
            .Keyed("-d", "Solana cluster"))
        .AddAsync(async (handler) => await CommandsHandler.ScrapeCommand(handler, optionsRepository.Data, rpcScheduler, cli.Logger)));

        cli.Register(Command.Factory("distribute-dividends")
            .Description("Distribute dividends to share holders")
            .ArgumentsHandler(
                ArgumentsHandler.Factory()
                .Positional("holders file")
                .Keyed("-d", "Solana cluster")
                .Keyed("-o", "output file name"))
            .AddAsync(async (handler) => await CommandsHandler.DistributeDividends(handler, optionsRepository.Data, rpcScheduler, cli.Logger)));

        cli.Register(Command.Factory("help")
            .Description("display the available commands")
            .InhibitHelp()
            .Add((handler) => Console.WriteLine(cli.Print())));

        return cli;
    }
}