using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Python;
using Microsoft.Extensions.Logging;

namespace AppHost.CISteps;

public static class PythonCIStepsExtensions
{
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
                    Name = $"install-{resource.Name}",
                    Action = async ctx => await CLIHelper.RunProcess("uv", string.Join(" ", ["sync", ..args]), resource.WorkingDirectory, ctx.Logger),
                    RequiredBySteps = ["install"]
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
                        Name = $"lint-{resource.Name}",
                        Action = async ctx =>
                        {
                            ctx.Logger.LogInformation("Linting for {resourceName} completed successfully", resource.Name);
                        },
                        RequiredBySteps = ["lint"],
                        DependsOnSteps = [$"install-{resource.Name}"]
                    },
                    ..lintingCommands.Select(lintCommand => new PipelineStep
                    {
                        Name = $"lint-{lintCommand.name}-{resource.Name}",
                        Action = async ctx => await CLIHelper.RunProcess(lintCommand.command, string.Join(" ", lintCommand.args), appDir, ctx.Logger),
                        RequiredBySteps = [$"lint-{resource.Name}"]
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
                            Action = async (ctx) => await CLIHelper.RunProcess("uv", string.Join(" ", ["run", testCommand.command, ..testCommand.args]), appDir, logger),
                            DependsOnSteps = [$"install-{resource.Name}"],
                            RequiredBySteps = [$"test-{resource.Name}"]
                        })
                ]);
            });
        }
    }
}