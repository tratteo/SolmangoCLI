using HandierCli;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolmangoCLI.Statics;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SolmangoCLI.Services;

public class CommandLineService : IRunner
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

    public Task RunAsync(CancellationToken cancellationToken)
    {
        return Cli.Run();
    }

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
            .Positional("The path where to save the result"))
            .AddAsync(async (handler) => await CommandsHandler.RetriveHolders(handler, services, logger)));

        Cli.Register(Command.Factory("distribute-tokens")
            .Description("Distribute the given mint to a list of addresses stored in a dictionary")
            .ArgumentsHandler(ArgumentsHandler.Factory()
            .Positional("The token mint to distribute")
            .Positional("The path to the address dictionary.json")
            .Flag("/s", " Skip sending to addresses who already hold the token"))
            .AddAsync(async (handler) => await CommandsHandler.DistributeTokens(handler, services, logger)));

        Cli.Register(Command.Factory("generate-keypair")
            .Description("Generate a keypair.json to use on the solana CLI")
            .ArgumentsHandler(ArgumentsHandler.Factory()
            .Positional("The keypair in base 58")
            .Positional("The path to save the keypair in byte[] format"))
            .Add((handler) => CommandsHandler.GenerateKeyPairFromBase58Keys(handler, services, logger)));

        Cli.Register(Command.Factory("token-supply")
            .Description("Get the supply and decimals of a given token")
            .ArgumentsHandler(ArgumentsHandler.Factory()
            .Positional("the token mint"))
            .AddAsync(async (handler) => await CommandsHandler.GetTokenSupply(handler, services, logger)));

        Cli.Register(Command.Factory("send-spl-token")
            .Description("Send an spl-token to an address")
            .ArgumentsHandler(ArgumentsHandler.Factory()
            .Positional("The receiver address")
            .Positional("The token mint address")
            .Positional("The amount to send in <double> format"))
            .AddAsync(async (handler) => await CommandsHandler.SendSplToken(handler, services, logger)));
    }
}