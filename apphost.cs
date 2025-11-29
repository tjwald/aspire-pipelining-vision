#:sdk Aspire.AppHost.Sdk@13.0.1
#:package Aspire.Hosting.JavaScript@13.0.1
#:package Aspire.Hosting.Python@13.0.1
#:package Aspire.Hosting.Redis@13.0.1

#pragma warning disable ASPIREPIPELINES001

using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Python;
using Aspire.Hosting.JavaScript;

var builder = DistributedApplication.CreateBuilder(args)
    .WithInstallation()
    .WithLinting();


var cache = builder.AddRedis("cache");

var app = builder.AddUvicornApp("app", "./app", "main:app")
    .WithUv()
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WaitFor(cache)
    .WithHttpHealthCheck("/health")
    .WithUvInstallationStep("./app")
    .WithUvLintingSteps([
        ("ruff", ["check"]),
        ("mypy", ["."])
    ]);

var frontend = builder.AddViteApp("frontend", "./frontend")
    .WithReference(app)
    .WaitFor(app)
    .WithInstallationStep()
    .WithLintingStep();

app.PublishWithContainerFiles(frontend, "./static");


builder.Build().Run();


public static class InstallationsExtensions
{
    public static IDistributedApplicationBuilder WithInstallation(this IDistributedApplicationBuilder builder)
    {
        builder.Pipeline.AddStep("install", context =>
        {
            context.Logger.LogInformation("Installation step completed successfully.");
            return Task.CompletedTask;
        });

        return builder;
    }

    public static IResourceBuilder<PythonAppResource> WithUvInstallationStep(this IResourceBuilder<PythonAppResource> builder, string workingDirectory) => builder.WithUvInstallationStep(workingDirectory, []);

    public static IResourceBuilder<PythonAppResource> WithUvInstallationStep(this IResourceBuilder<PythonAppResource> builder, string workingDirectory, string[] args)
    {
        return builder.WithPipelineStepFactory(factoryContext =>
        {
            var resource = factoryContext.Resource;
            var logger = factoryContext.PipelineContext.Logger;
            return new PipelineStep
                {
                    Name = $"install-{resource.Name}",
                    Action = async (ctx) => await LintingExtensions.RunProcess("uv", string.Join(" ", ["sync", ..args]), workingDirectory, logger),
                    RequiredBySteps = ["install"]
                };
        });
    }

    public static IResourceBuilder<JavaScriptAppResource> WithInstallationStep(this IResourceBuilder<JavaScriptAppResource> builder) => builder.WithInstallationStep([]);

    public static IResourceBuilder<JavaScriptAppResource> WithInstallationStep(this IResourceBuilder<JavaScriptAppResource> builder, string[] args)
    {
        return builder.WithPipelineStepFactory(factoryContext =>
        {
            var resource = factoryContext.Resource;
            var logger = factoryContext.PipelineContext.Logger;

            if (!resource.TryGetLastAnnotation<ExecutableAnnotation>(out var execAnnotation) || string.IsNullOrEmpty(execAnnotation.WorkingDirectory))
            {
                logger.LogWarning("Could not find working directory for {resourceName}", resource.Name);
                throw new InvalidOperationException($"Could not find working directory for {resource.Name}");
            }
                
            var frontendDir = execAnnotation.WorkingDirectory;
            string packageManager = "npm";
            if (resource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var packageManagerAnnotation))
            {
                packageManager = packageManagerAnnotation.ExecutableName;
            }

            return new PipelineStep
                {
                    Name = $"install-{resource.Name}",
                    Action = async (ctx) => await LintingExtensions.RunProcess(packageManager, "install", frontendDir, logger),
                    RequiredBySteps = ["install"]
                };
        });
    }
}


public static class LintingExtensions
{
    public static IDistributedApplicationBuilder WithLinting(this IDistributedApplicationBuilder builder)
    {
        builder.Pipeline.AddStep("lint", context =>
        {
            context.Logger.LogInformation("Linting finished successfully.");
            return Task.CompletedTask;
        });
        return builder;
    }
    
    public static IResourceBuilder<PythonAppResource> WithUvLintingSteps(this IResourceBuilder<PythonAppResource> builder, IEnumerable<(string command, List<string> args)> lintingCommands)
    {
        return builder.WithLintingSteps(lintingCommands.Select<(string command, List<string> args), (string name, string command, List<string> args)>(lintCommand => (lintCommand.command, "uv", ["run", lintCommand.command, ..lintCommand.args])));
    }
    
    public static IResourceBuilder<PythonAppResource> WithLintingSteps(this IResourceBuilder<PythonAppResource> builder, IEnumerable<(string name, string command, List<string> args)> lintingCommands)
    {
        return builder.WithPipelineStepFactory(factoryContext =>
        {
            var logger = factoryContext.PipelineContext.Logger;            
            var resource = factoryContext.Resource;
            if (!resource.TryGetLastAnnotation<ExecutableAnnotation>(out var execAnnotation) || string.IsNullOrEmpty(execAnnotation.WorkingDirectory))
            {
                logger?.LogWarning("Could not find working directory for {resourceName}", resource.Name);
                throw new InvalidOperationException($"Could not find working directory for {resource.Name}");
            }
        
            var appDir = execAnnotation.WorkingDirectory;
            return Task.FromResult<IEnumerable<PipelineStep>>([
                new PipelineStep
                {
                    Name = $"lint-{resource.Name}",
                    Action = async (ctx) =>
                    {
                        logger.LogInformation("Linting for {resourceName} completed successfully", resource.Name);
                    },
                    RequiredBySteps = ["lint"],
                    DependsOnSteps = [$"install-{resource.Name}"]
                },
                ..lintingCommands.Select(lintCommand => new PipelineStep
                {
                    Name = $"lint-{lintCommand.name}-{resource.Name}",
                    Action = async (ctx) => await RunProcess(lintCommand.command, string.Join(" ", lintCommand.args), appDir, logger),
                    RequiredBySteps = [$"lint-{resource.Name}"]
                })
            ]);
        });
    }

    public static IResourceBuilder<JavaScriptAppResource> WithLintingStep(this IResourceBuilder<JavaScriptAppResource> builder)
    {
        return builder.WithPipelineStepFactory(factoryContext =>
        {
            var logger = factoryContext.PipelineContext.Logger;
            var resource = factoryContext.Resource;

            if (!resource.TryGetLastAnnotation<ExecutableAnnotation>(out var execAnnotation) || string.IsNullOrEmpty(execAnnotation.WorkingDirectory))
            {
                logger.LogWarning("Could not find working directory for {resourceName}", resource.Name);
                throw new InvalidOperationException($"Could not find working directory for {resource.Name}");
            }
                
            var frontendDir = execAnnotation.WorkingDirectory;
            string packageManager = "npm";
            if (resource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var packageManagerAnnotation))
            {
                packageManager = packageManagerAnnotation.ExecutableName;
            }
            return new PipelineStep
            {
                Name = $"lint-{resource.Name}",
                Action = async (ctx) =>
                {
                    // prefer the resource's package-manager annotation when available (fallback: lockfile detection)

                    string pkgArgs = packageManager switch
                    {
                        "yarn" => "lint",
                        "pnpm" or "npm" or _ => "run lint --silent",
                    };

                    await RunProcess(packageManager, pkgArgs, frontendDir, logger);
                },
                RequiredBySteps = ["lint"],
                DependsOnSteps = [$"install-{resource.Name}"]
            };
        });
    }
    
    /// Helper to run external processes and stream output into pipeline logs
    public static async Task RunProcess(string fileName, string args, string workingDir, ILogger? logger = null)
    {
        logger ??= LoggerFactory.Create(b => b.AddConsole()).CreateLogger("aspire-lint");

        ProcessStartInfo psi;

        // On Windows, use cmd /c to execute commands through the shell for better PATH resolution
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            psi = new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = $"/c {fileName} {args}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDir
            };
        }
        else
        {
            psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDir
            };
        }

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start '{fileName} {args}'");

        var outTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await proc.StandardOutput.ReadLineAsync()) != null)
            {
                logger.LogInformation("{Line}", line);
            }
        });

        var errTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await proc.StandardError.ReadLineAsync()) != null)
            {
                logger.LogError("{Line}", line);
            }
        });

        await Task.WhenAll(outTask, errTask, proc.WaitForExitAsync());

        if (proc.ExitCode != 0)
        {
            throw new Exception($"Process '{fileName} {args}' failed with exit code {proc.ExitCode}");
        }
    }
}