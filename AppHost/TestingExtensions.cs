using AppHost;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Python;
using Microsoft.Extensions.Logging;

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
                        Action = async (ctx) => await CLIHelper.RunProcess("uv", string.Join(" ", ["run", testCommand.command, ..testCommand.args]), appDir, logger),
                        DependsOnSteps = [$"install-{resource.Name}"],
                        RequiredBySteps = [$"test-{resource.Name}"]
                    })
            ]);
        });
    }
}