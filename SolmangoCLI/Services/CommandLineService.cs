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
    }
}