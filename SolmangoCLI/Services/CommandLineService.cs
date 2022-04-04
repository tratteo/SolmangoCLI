using HandierCli;
using Microsoft.Extensions.Logging;
using SolmangoCLI.Statics;
using System;
using System.Threading.Tasks;

namespace SolmangoCLI.Services;

public class CommandLineService : ICoreRunner
{
    private readonly IServiceProvider services;

    private readonly ILogger<CommandLineService> logger;

    public CommandLine Cli { get; private set; } = null!;

    public CommandLineService(IServiceProvider services, ILogger<CommandLineService> logger)
    {
        this.services = services;
        this.logger = logger;
        BuildCli();
    }

    public Task Run() => Cli.Run();

    private void BuildCli()
    {
        Cli = CommandLine.Factory().ExitOn("exit", "quit").OnUnrecognized(cmd => Logger.ConsoleInstance.LogError($"{cmd} not recognized")).Build();
        Cli.Register(Command.Factory("scrape")
        .Description("Scrape NFTs collections by name, symbol and updateAuthority")
        .ArgumentsHandler(
            ArgumentsHandler.Factory()
            .Positional("out file name")
            .Keyed("-n", "collection name")
            .Keyed("-s", "collection symbol")
            .Keyed("-u", "update authority"))
        .AddAsync(async (handler) => await CommandsHandler.ScrapeCommand(handler, services, logger)));

        Cli.Register(Command.Factory("help")
            .Description("display the available commands")
            .InhibitHelp()
            .Add((handler) => Console.WriteLine(Cli.Print())));

        Cli.Register(Command.Factory("get-holders")
            .Description("Retrive holders list using the mint list")
            .ArgumentsHandler(
            ArgumentsHandler.Factory()
            .Positional("The minting list path")
            .Positional("the path where to save the result"))
            .AddAsync(async (handler) => await CommandsHandler.RetriveHolders(handler, services, logger)));

        Cli.Register(Command.Factory("distribute-tokens")
            .Description("Distribute the given mint to a list of addresses stored in a dictionary")
            .ArgumentsHandler(ArgumentsHandler.Factory()
            .Positional("The path to the source wallet keypair file")
            .Positional("The token mint to distribute")
            .Positional("The path to the address dictionary.json"))
            .AddAsync(async (handler) => await CommandsHandler.DistributeTokens(handler, services, logger)));

        Cli.Register(Command.Factory("generate-keypair")
            .Description("generate a keypair.json to use on the solana CLI")
            .ArgumentsHandler(ArgumentsHandler.Factory()
            .Positional("The private key in base 58")
            .Positional("The public key in base 58")
            .Positional("The path to save the keypair in byte[] format"))
            .Add((handler) => CommandsHandler.GenerateKeyPairFromBase58Keys(handler, services, logger)));
    }
}