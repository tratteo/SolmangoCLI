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

    public CommandLineService(IServiceProvider services, ILogger<CommandLineService> logger, IConfiguration configuration)
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
    }
}