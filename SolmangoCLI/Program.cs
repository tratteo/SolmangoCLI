// Copyright Siamango

using HandierCli;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SolmangoCLI.Services;
using SolmangoCLI.Settings;
using SolmangoCLI.Statics;
using SolmangoNET.Rpc;
using System;

Console.Title = "Solmango cli";
Logger.ConsoleInstance.LogInfo("----- SOLMANGO CLI -----\n\n", ConsoleColor.DarkCyan);

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ConnectionSettings>(builder.Configuration.GetSection(ConnectionSettings.Position));
builder.Services.Configure<PathSettings>(builder.Configuration.GetSection(PathSettings.Position));
builder.Services.AddOfflineRunner<CommandLineService>();

builder.Services.AddSingleton<IRpcScheduler, BasicRpcScheduler>((services) =>
{
    var scheduler = new BasicRpcScheduler(1250);
    scheduler.Start();
    return scheduler;
});
var app = builder.Build();
await app.RunOfflineAsync(System.Threading.CancellationToken.None);