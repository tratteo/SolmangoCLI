// Copyright Siamango

using HandierCli;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SolmangoCLI.Services;
using SolmangoCLI.Settings;
using SolmangoNET.Rpc;
using System;

Console.Title = "Solmango cli";
Logger.ConsoleInstance.LogInfo("----- SOLMANGO CLI -----\n\n", ConsoleColor.DarkCyan);

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ConnectionSettings>(builder.Configuration.GetSection(ConnectionSettings.Position));
builder.Services.AddSingleton<ICoreRunner, CommandLineService>();
builder.Services.AddSingleton<IRpcScheduler, BasicRpcScheduler>((services) =>
{
    var scheduler = new BasicRpcScheduler(100);
    scheduler.Start();
    return scheduler;
});
var app = builder.Build();
var core = app.Services.GetRequiredService<ICoreRunner>();
await core.Run();