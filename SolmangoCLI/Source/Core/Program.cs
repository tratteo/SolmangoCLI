// Copyright Siamango

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
    private static Options options;
    private static IRpcScheduler rpcScheduler;

    public static void InitializeCache()
    {
        if (!Serializer.DeserializeJson(string.Empty, OPTIONS_FILE, out options))
        {
            options = new Options();
            Serializer.SerializeJson(string.Empty, OPTIONS_FILE, options);
            Logger.ConsoleInstance.LogWarning("Options file not found, created at: " + OPTIONS_FILE + ", compile it");
        }
        if (!options.Verify())
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
        await BuildCli().Run();
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
        .AddAsync(async (handler) => await CommandsHandler.ScrapeCommand(handler, options, rpcScheduler, cli.Logger)));

        cli.Register(Command.Factory("distribute-dividends")
            .Description("Distribute dividends to share holders")
            .ArgumentsHandler(
                ArgumentsHandler.Factory()
                .Positional("holders file")
                .Keyed("-d", "Solana cluster")
                .Keyed("-o", "output file name"))
            .AddAsync(async (handler) => await CommandsHandler.DistributeDividends(handler, options, rpcScheduler, cli.Logger)));

        cli.Register(Command.Factory("help")
            .Description("display the available commands")
            .InhibitHelp()
            .Add((handler) => Console.WriteLine(cli.Print())));

        return cli;
    }
}