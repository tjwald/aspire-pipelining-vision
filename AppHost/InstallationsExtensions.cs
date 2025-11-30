using AppHost;
using Aspire.Hosting.JavaScript;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Python;
using Microsoft.Extensions.Logging;

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

    extension(IResourceBuilder<PythonAppResource> builder)
    {
        public IResourceBuilder<PythonAppResource> WithUvInstallationStep() => builder.WithUvInstallationStep([]);

        public IResourceBuilder<PythonAppResource> WithUvInstallationStep(string[] args)
        {
            return builder.WithPipelineStepFactory(factoryContext =>
            {
                var resource = factoryContext.Resource;
                var logger = factoryContext.PipelineContext.Logger;
        
                return new PipelineStep
                {
                    Name = $"install-{resource.Name}",
                    Action = async (ctx) => await CLIHelper.RunProcess("uv", string.Join(" ", ["sync", ..args]), resource.WorkingDirectory, logger),
                    RequiredBySteps = ["install"]
                };
            });
        }
    }

    extension(IResourceBuilder<JavaScriptAppResource> builder)
    {
        public IResourceBuilder<JavaScriptAppResource> WithInstallationStep() => builder.WithInstallationStep([]);

        public IResourceBuilder<JavaScriptAppResource> WithInstallationStep(string[] args)
        {
            return builder.WithPipelineStepFactory(factoryContext =>
            {
                var resource = factoryContext.Resource;
                var logger = factoryContext.PipelineContext.Logger;

                return new PipelineStep
                {
                    Name = $"install-{resource.Name}",
                    Action = async (ctx) => await CLIHelper.RunProcess(resource.PackageManager, "install", resource.WorkingDirectory, logger),
                    RequiredBySteps = ["install"]
                };
            });
        }
    }
}