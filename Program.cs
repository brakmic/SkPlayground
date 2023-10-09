﻿using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using SkPlayground.Plugins;

namespace SkPlayground;
class Program
{
  private static IConfiguration? Configuration { get; set; }

  public static async Task Main(string[] args)
  {
    var builder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.plugins.json", optional: false, reloadOnChange: true);

    Configuration = builder.Build();

    var fileOption = new Option<FileInfo>(
        new[] { "--input", "-i" },
        "Path to the input file to be processed");

    var functionOption = new Option<string>(
        new[] { "--function", "-f" },
        "The function to be executed.");

    var rootCommand = new RootCommand
        {
            fileOption,
            functionOption
        };

    rootCommand.SetHandler(
        //Run,
        RunWithActionPlanner,
        //RunWithSequentialPlanner,
        fileOption, functionOption
    );

    await rootCommand.InvokeAsync(args);
  }

  private static async Task Run(FileInfo file, string function)
  {
    var kernelSettings = KernelSettings.LoadSettings();

    using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
    {
      builder
              .SetMinimumLevel(kernelSettings.LogLevel ?? LogLevel.Warning)
              .AddConsole()
              .AddDebug();
    });

    IKernel kernel = new KernelBuilder()
        .WithLoggerFactory(loggerFactory)
        .WithCompletionService(kernelSettings)
        .Build();

    if (kernelSettings.EndpointType == EndpointTypes.TextCompletion)
    {
      var rootDirectory = Configuration!.GetSection("SkillSettings:Root").Get<string>();
      var pluginDirectories = Configuration.GetSection("SkillSettings:Plugins").Get<string[]>();

      var skillsRoot = Path.Combine(Directory.GetCurrentDirectory(), rootDirectory!);
      var skillImport = kernel.ImportSemanticSkillFromDirectory(skillsRoot, pluginDirectories!);

      string description = await File.ReadAllTextAsync(file.FullName);
      var context = new ContextVariables();

      string key = "input";
      context.Set(key, description);

      var result = await kernel.RunAsync(context, skillImport[function]);
      Console.WriteLine(result);
    }
  }
  private static async Task RunWithActionPlanner(FileInfo file, string function)
  {
    var kernelSettings = KernelSettings.LoadSettings();

    using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
    {
      builder
              .SetMinimumLevel(kernelSettings.LogLevel ?? LogLevel.Warning)
              .AddConsole()
              .AddDebug();
    });

    IKernel kernel = new KernelBuilder()
        .WithLoggerFactory(loggerFactory)
        .WithCompletionService(kernelSettings)
        .Build();

    if (kernelSettings.EndpointType == EndpointTypes.TextCompletion)
    {

      var rootDirectory = Configuration!.GetSection("SkillSettings:Root").Get<string>();
      var pluginDirectories = Configuration.GetSection("SkillSettings:Plugins").Get<string[]>();

      var skillsRoot = Path.Combine(Directory.GetCurrentDirectory(), rootDirectory!);
      var skillImport = kernel.ImportSemanticSkillFromDirectory(skillsRoot, pluginDirectories!);

      var httpPlugin = kernel.ImportSkill(new HttpPlugin(), nameof(HttpPlugin));

      var planner = new ActionPlanner(kernel);
      var ask = await File.ReadAllTextAsync(file.FullName);
      var plan = await planner.CreatePlanAsync(ask);

      Console.WriteLine($"\nPLAN:\n{plan.ToSafePlanString()}");

      var result = await plan.InvokeAsync();

      Console.WriteLine($"\nRESULT: {result}");
    }
  }

  private static async Task RunWithSequentialPlanner(FileInfo file, string function)
  {
    var kernelSettings = KernelSettings.LoadSettings();

    using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
    {
      builder
              .SetMinimumLevel(kernelSettings.LogLevel ?? LogLevel.Warning)
              .AddConsole()
              .AddDebug();
    });

    IKernel kernel = new KernelBuilder()
        .WithLoggerFactory(loggerFactory)
        .WithCompletionService(kernelSettings)
        .Build();

    if (kernelSettings.EndpointType == EndpointTypes.TextCompletion)
    {
      var rootDirectory = Configuration!.GetSection("SkillSettings:Root").Get<string>();
      var pluginDirectories = Configuration.GetSection("SkillSettings:Plugins").Get<string[]>();

      var skillsRoot = Path.Combine(Directory.GetCurrentDirectory(), rootDirectory!);
      var skillImport = kernel.ImportSemanticSkillFromDirectory(skillsRoot, pluginDirectories!);
      var keyGenPlugin = kernel.ImportSkill(new KeyAndCertGenerator(), nameof(KeyAndCertGenerator));
      var secretsPlugin = kernel.ImportSkill(new SecretYamlUpdater(), nameof(SecretYamlUpdater));

      var planner = new SequentialPlanner(kernel);
      var ask = await File.ReadAllTextAsync(file.FullName);
      var plan = await planner.CreatePlanAsync(ask);

      Console.WriteLine($"\nPLAN:\n{plan.ToSafePlanString()}");

      var result = await plan.InvokeAsync();

      Console.WriteLine($"\nRESULT: {result}");
    }
  }
}
