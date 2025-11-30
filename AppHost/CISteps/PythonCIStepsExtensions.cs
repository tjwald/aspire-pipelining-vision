using System.Runtime.InteropServices;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Python;
using Microsoft.Extensions.Logging;

namespace AppHost.CISteps;

public static class PythonCIStepsExtensions
{
    extension(IDistributedApplicationBuilder builder)
    {
        public IDistributedApplicationBuilder AddUvPythonSetup(string? uvVersion = null)
        {
            builder.Pipeline.AddStep($"{WellKnownCIStepNames.Setup}-uv-python", async stepContext =>
            {
                try
                {
                    var output = await CLIHelper.RunProcessWithOutput("uv", "--version", ".");
                    if (uvVersion is not null)
                    {
                        var uvVersionParts = output.Split();
                        if (uvVersionParts[1] != uvVersion)
                        {
                            stepContext.Logger.LogWarning("Already installed uv with version: {uvVersion}. Changing existing versions not supported yet", uvVersionParts[1]);
                        }
                        else
                        {
                            stepContext.Logger.LogInformation("Already installed uv with version: {uvVersion}", uvVersionParts[1]);
                        }
                        return;
                    }
                }
                catch (Exception e)
                {
                    stepContext.Logger.LogInformation("uv not installed - installing version: {uvVersion}", uvVersion ?? "latest");
                }

                string command, args;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    command = "powershell";
                    if (uvVersion is not null)
                    {
                        args = $"-ExecutionPolicy ByPass -c \"irm https://astral.sh/uv/{uvVersion}/install.ps1 | iex\"";
                    }
                    else
                    {
                        args = "-ExecutionPolicy ByPass -c \"irm https://astral.sh/uv/install.ps1 | iex\"";
                    }
                }
                else
                {
                    command = "curl";
                    if (uvVersion is not null)
                    {
                        args = $"-LsSf https://astral.sh/uv/{uvVersion}/install.sh | sh";
                    }
                    else
                    {
                        args = "-LsSf https://astral.sh/uv/install.sh | sh";
                    }
                }

                try
                {
                    await CLIHelper.RunProcess(command, args, ".", stepContext.Logger);
                }
                catch (Exception e)
                {
                    stepContext.Logger.LogError(e, "Couldn't install uv with version: {uvVersion}", uvVersion ?? "latest");
                    throw;
                }
                
            }, requiredBy: WellKnownCIStepNames.Setup);
            return builder;
        }
    }
    
    extension(IResourceBuilder<PythonAppResource> builder)
    {
        public IResourceBuilder<PythonAppResource> WithUvInstallationStep() => builder.WithUvInstallationStep([]);

        public IResourceBuilder<PythonAppResource> WithUvInstallationStep(string[] args)
        {
            return builder.WithPipelineStepFactory(factoryContext =>
            {
                var resource = factoryContext.Resource;
        
                return new PipelineStep
                {
                    Name = $"{WellKnownCIStepNames.Install}-{resource.Name}",
                    Action = async ctx => await CLIHelper.RunProcess("uv", string.Join(" ", ["sync", ..args]), resource.WorkingDirectory, ctx.Logger),
                    RequiredBySteps = [WellKnownCIStepNames.Install]
                };
            });
        }
        
        public IResourceBuilder<PythonAppResource> WithUvLintingSteps(IEnumerable<(string command, List<string> args)> lintingCommands)
        {
            return builder.WithLintingSteps(lintingCommands.Select<(string command, List<string> args), (string name, string command, List<string> args)>(lintCommand => (lintCommand.command, "uv", ["run", lintCommand.command, ..lintCommand.args])));
        }

        public IResourceBuilder<PythonAppResource> WithLintingSteps(IEnumerable<(string name, string command, List<string> args)> lintingCommands)
        {
            return builder.WithPipelineStepFactory(factoryContext =>
            {
                var resource = factoryContext.Resource;
        
                var appDir = resource.WorkingDirectory;
                return Task.FromResult<IEnumerable<PipelineStep>>([
                    new PipelineStep
                    {
                        Name = $"{WellKnownCIStepNames.Lint}-{resource.Name}",
                        Action = async ctx =>
                        {
                            ctx.Logger.LogInformation("Linting for {resourceName} completed successfully", resource.Name);
                        },
                        RequiredBySteps = [WellKnownCIStepNames.Lint],
                        DependsOnSteps = [$"{WellKnownCIStepNames.Install}-{resource.Name}"]
                    },
                    ..lintingCommands.Select(lintCommand => new PipelineStep
                    {
                        Name = $"{WellKnownCIStepNames.Lint}-{lintCommand.name}-{resource.Name}",
                        Action = async ctx => await CLIHelper.RunProcess(lintCommand.command, string.Join(" ", lintCommand.args), appDir, ctx.Logger),
                        RequiredBySteps = [$"{WellKnownCIStepNames.Lint}-{resource.Name}"]
                    })
                ]);
            });
        }
        
        public IResourceBuilder<PythonAppResource> WithUvTestingStep(IEnumerable<(string name, string command, List<string> args)> testingCommands)
        {
            return builder.WithPipelineStepFactory(factoryContext =>
            {
                var logger = factoryContext.PipelineContext.Logger;            
                var resource = factoryContext.Resource;
        
                var appDir = resource.WorkingDirectory;
                return Task.FromResult<IEnumerable<PipelineStep>>([
                    new PipelineStep
                    {
                        Name = $"{WellKnownCIStepNames.Test}-{resource.Name}",
                        Action = ctx =>
                        {
                            ctx.Logger.LogInformation("Testing for {resourceName} completed successfully", resource.Name);
                            return Task.CompletedTask;
                        },
                        RequiredBySteps = [WellKnownCIStepNames.Test]
                    },
                    ..testingCommands.Select(testCommand =>
                        new PipelineStep
                        {
                            Name = $"{WellKnownCIStepNames.Test}-{testCommand.name}-{resource.Name}",
                            Action = ctx => CLIHelper.RunProcess("uv", string.Join(" ", ["run", testCommand.command, ..testCommand.args]), appDir, ctx.Logger),
                            DependsOnSteps = [$"{WellKnownCIStepNames.Install}-{resource.Name}"],
                            RequiredBySteps = [$"{WellKnownCIStepNames.Test}-{resource.Name}"]
                        })
                ]);
            });
        }
    }
}