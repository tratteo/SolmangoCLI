// Copyright Siamango

using HandierCli;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SolmangoCLI.Services;
using SolmangoNET.Rpc;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SolmangoCLI;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.Title = "Solmango cli";
        Logger.ConsoleInstance.LogInfo("----- SOLMANGO CLI -----\n\n", ConsoleColor.DarkCyan);

        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((config) =>
                config.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", true, true)
                .AddEnvironmentVariables())
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<CommandLineService>();
                services.AddSingleton<IActivityProvider, ActivityProvider>();
                services.AddSingleton<IRpcScheduler, BasicRpcScheduler>((services) =>
                {
                    var scheduler = new BasicRpcScheduler(100);
                    scheduler.Start();
                    return scheduler;
                });
            })
            .Build();
        var core = host.Services.GetService<CommandLineService>();
        if (core != null)
        {
            await core.Run();
        }
    }
}