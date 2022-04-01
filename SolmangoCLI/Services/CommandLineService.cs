using HandierCli;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SolmangoCLI.Statics;
using System;
using System.Threading.Tasks;

namespace SolmangoCLI.Services;

public class CommandLineService : ICoreRunner
{
    private readonly IServiceProvider services;

    private readonly ILogger<CommandLineService> logger;

    public CommandLine Cli { get; private set; }

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
            .Keyed("-u", "update authority")
            .Keyed("-d", "Solana cluster"))
        .AddAsync(async (handler) => await CommandsHandler.ScrapeCommand(handler, services, logger)));

        Cli.Register(Command.Factory("dividends")
            .Description("Distribute dividends to share holders")
            .ArgumentsHandler(
                ArgumentsHandler.Factory()
                .Positional("holders file")
                .Keyed("-d", "Solana cluster")
                .Keyed("-o", "output file name"))
            .AddAsync(async (handler) => await CommandsHandler.DistributeDividends(handler, services, logger)));

        Cli.Register(Command.Factory("activity")
            .Description("execute an activity")
            .ArgumentsHandler(ArgumentsHandler.Factory().Positional("activity id").Keyed("-d", "Solana cluster"))
            .AddAsync(async (handler) => await CommandsHandler.ExecuteActivity(handler, services, logger)));

        Cli.Register(Command.Factory("help")
            .Description("display the available commands")
            .InhibitHelp()
            .Add((handler) => Console.WriteLine(Cli.Print())));

        Cli.Register(Command.Factory("airdrop")
            .Description("test")
            .AddAsync(async (handler) => await CommandsHandler.AirdropToHolders(logger, "BSMhJs3m3BAS1A83oSheF32JAYnCo3tPeohZrRHcya5o", "4R9JiktMdTyxgX2ezd7iiscaRMHXsKeh3VM99e9eepoK", new Solnet.Wallet.Account("118NUZLXcRt8TyJo21TXA4C9BrTuXEBFfKgFtk7vSgHuuJ4iz2rKfmyeHGz45SoktgkTpdXss1KkNPqecx8NFNk", "EX2teQbpUAYjwcfmup9mJabqXgNLyVB1bhmFMyj7imXz"), "A48RTo99QPH9qGyqSWhPPykViPwF9msJtX77TnufgMcK")));
    }
}