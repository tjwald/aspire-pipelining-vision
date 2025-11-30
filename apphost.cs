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
    .WithLinting()
    .WithTesting();


var cache = builder.AddRedis("cache");

var app = builder.AddUvicornApp("app", "./app", "app.main:app")
    .WithUv(args: ["sync", "--all-groups"])
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WaitFor(cache)
    .WithHttpHealthCheck("/health")
    .WithUvInstallationStep(["--all-groups"])
    .WithUvLintingSteps([
        ("ruff", ["check"]),
        ("mypy", ["."])
    ])
    .WithUvTestingStep([
        ("unit", "pytest", ["-v", "tests/unit"])
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

    public static IResourceBuilder<PythonAppResource> WithUvInstallationStep(this IResourceBuilder<PythonAppResource> builder) => builder.WithUvInstallationStep([]);

    public static IResourceBuilder<PythonAppResource> WithUvInstallationStep(this IResourceBuilder<PythonAppResource> builder, string[] args)
    {
        return builder.WithPipelineStepFactory(factoryContext =>
        {
            var resource = factoryContext.Resource;
            var logger = factoryContext.PipelineContext.Logger;
        
            return new PipelineStep
                {
                    Name = $"install-{resource.Name}",
                    Action = async (ctx) => await LintingExtensions.RunProcess("uv", string.Join(" ", ["sync", ..args]), resource.WorkingDirectory, logger),
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

            return new PipelineStep
                {
                    Name = $"install-{resource.Name}",
                    Action = async (ctx) => await LintingExtensions.RunProcess(resource.PackageManager, "install", resource.WorkingDirectory, logger),
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
        
            var appDir = resource.WorkingDirectory;
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
            var resource = factoryContext.Resource; // this is no longer a JavaScriptAppResource in pipeline execution.
                
            var frontendDir = resource.WorkingDirectory;
            string packageManager = resource.PackageManager;
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

public static class TestingExtensions
{
    public static IDistributedApplicationBuilder WithTesting(this IDistributedApplicationBuilder builder)
    {
        builder.Pipeline.AddStep("test", context =>
        {
            context.Logger.LogInformation("Testing finished successfully.");
            return Task.CompletedTask;
        });
        return builder;
    }

    public static IResourceBuilder<PythonAppResource> WithUvTestingStep(this IResourceBuilder<PythonAppResource> builder, IEnumerable<(string name, string command, List<string> args)> testingCommands)
    {
        return builder.WithPipelineStepFactory(factoryContext =>
        {
            var logger = factoryContext.PipelineContext.Logger;            
            var resource = factoryContext.Resource;
        
            var appDir = resource.WorkingDirectory;
            return Task.FromResult<IEnumerable<PipelineStep>>([
                new PipelineStep
                {
                    Name = $"test-{resource.Name}",
                    Action = async (ctx) =>
                    {
                        logger.LogInformation("Testing for {resourceName} completed successfully", resource.Name);
                    },
                    RequiredBySteps = ["test"]
                },
                ..testingCommands.Select(testCommand =>
                new PipelineStep
                {
                    Name = $"test-{testCommand.name}-{resource.Name}",
                    Action = async (ctx) => await LintingExtensions.RunProcess("uv", string.Join(" ", ["run", testCommand.command, ..testCommand.args]), appDir, logger),
                    DependsOnSteps = [$"install-{resource.Name}"],
                    RequiredBySteps = [$"test-{resource.Name}"]
                })
            ]);
        });
    }
}

internal static class ResourceExtensions
{
    extension(IResource resource)
    {
        /// Get Working Directory from ExecutableAnnotation
        public string WorkingDirectory
        {
            get 
            {
                if (resource.TryGetLastAnnotation<ExecutableAnnotation>(out var execAnnotation))
                {
                    return execAnnotation.WorkingDirectory;
                }
                throw new InvalidOperationException($"Could not find working directory for {resource.Name}");
            }
        }

        public string PackageManager
        {
            get 
            {
                if (resource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var packageManagerAnnotation))
                {
                    return packageManagerAnnotation.ExecutableName;
                }
                throw new InvalidOperationException($"Could not find package manager for {resource.Name}");
            }
        }
    }
}