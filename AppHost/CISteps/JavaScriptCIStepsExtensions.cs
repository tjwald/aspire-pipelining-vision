using Aspire.Hosting.JavaScript;
using Aspire.Hosting.Pipelines;

namespace AppHost.CISteps;

public static class JavaScriptCIStepsExtensions
{
    extension(IResourceBuilder<JavaScriptAppResource> builder)
    {
        public IResourceBuilder<JavaScriptAppResource> WithInstallationStep() => builder.WithInstallationStep([]);

        public IResourceBuilder<JavaScriptAppResource> WithInstallationStep(string[] args)
        {
            return builder.WithPipelineStepFactory(factoryContext =>
            {
                var resource = factoryContext.Resource;

                return new PipelineStep
                {
                    Name = $"install-{resource.Name}",
                    Action = async ctx => await CLIHelper.RunProcess(resource.PackageManager, "install", resource.WorkingDirectory, ctx.Logger),
                    RequiredBySteps = ["install"]
                };
            });
        }
        
        public IResourceBuilder<JavaScriptAppResource> WithLintingStep()
        {
            return builder.WithPipelineStepFactory(factoryContext =>
            {
                var resource = factoryContext.Resource; // this is no longer a JavaScriptAppResource in pipeline execution.
                
                var frontendDir = resource.WorkingDirectory;
                string packageManager = resource.PackageManager;
                return new PipelineStep
                {
                    Name = $"lint-{resource.Name}",
                    Action = async ctx =>
                    {
                        // prefer the resource's package-manager annotation when available (fallback: lockfile detection)

                        string pkgArgs = packageManager switch
                        {
                            "yarn" => "lint",
                            "pnpm" or "npm" or _ => "run lint --silent",
                        };

                        await CLIHelper.RunProcess(packageManager, pkgArgs, frontendDir, ctx.Logger);
                    },
                    RequiredBySteps = ["lint"],
                    DependsOnSteps = [$"install-{resource.Name}"]
                };
            });
        }
    }
}