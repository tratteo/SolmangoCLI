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

        Cli.Register(Command.Factory("airdrop")
            .Description("test")
            .AddAsync(async (handler) => await CommandsHandler.AirdropToHolders(logger, "BSMhJs3m3BAS1A83oSheF32JAYnCo3tPeohZrRHcya5o", "4R9JiktMdTyxgX2ezd7iiscaRMHXsKeh3VM99e9eepoK", new Solnet.Wallet.Account("118NUZLXcRt8TyJo21TXA4C9BrTuXEBFfKgFtk7vSgHuuJ4iz2rKfmyeHGz45SoktgkTpdXss1KkNPqecx8NFNk", "EX2teQbpUAYjwcfmup9mJabqXgNLyVB1bhmFMyj7imXz"), "A48RTo99QPH9qGyqSWhPPykViPwF9msJtX77TnufgMcK")));

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
            .Positional("The address dictionary to distribute to"))
            .AddAsync(async (handler) => await CommandsHandler.DistributeTokens(handler, services, logger)));
    }
}