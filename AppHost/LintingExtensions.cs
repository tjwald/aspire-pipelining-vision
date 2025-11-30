using System.Diagnostics;
using System.Runtime.InteropServices;
using AppHost;
using Aspire.Hosting.JavaScript;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Python;
using Microsoft.Extensions.Logging;

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
    
    extension(IResourceBuilder<PythonAppResource> builder)
    {
        public IResourceBuilder<PythonAppResource> WithUvLintingSteps(IEnumerable<(string command, List<string> args)> lintingCommands)
        {
            return builder.WithLintingSteps(lintingCommands.Select<(string command, List<string> args), (string name, string command, List<string> args)>(lintCommand => (lintCommand.command, "uv", ["run", lintCommand.command, ..lintCommand.args])));
        }

        public IResourceBuilder<PythonAppResource> WithLintingSteps(IEnumerable<(string name, string command, List<string> args)> lintingCommands)
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
                        Action = async (ctx) => await CLIHelper.RunProcess(lintCommand.command, string.Join(" ", lintCommand.args), appDir, logger),
                        RequiredBySteps = [$"lint-{resource.Name}"]
                    })
                ]);
            });
        }
    }

    extension(IResourceBuilder<JavaScriptAppResource> builder)
    {
        public IResourceBuilder<JavaScriptAppResource> WithLintingStep()
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

                        await CLIHelper.RunProcess(packageManager, pkgArgs, frontendDir, logger);
                    },
                    RequiredBySteps = ["lint"],
                    DependsOnSteps = [$"install-{resource.Name}"]
                };
            });
        }
    }
}