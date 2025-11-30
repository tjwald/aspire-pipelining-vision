using Microsoft.Extensions.Logging;

namespace AppHost.CISteps;

public static class CIStepExtensions
{
    extension(IDistributedApplicationBuilder builder)
    {
        public IDistributedApplicationBuilder WithInstallation()
        {
            builder.Pipeline.AddStep(WellKnownCIStepNames.Install, context =>
            {
                context.Logger.LogInformation("Installation step completed successfully.");
                return Task.CompletedTask;
            });

            return builder;
        }
        
        public IDistributedApplicationBuilder WithLinting()
        {
            builder.Pipeline.AddStep(WellKnownCIStepNames.Lint, context =>
            {
                context.Logger.LogInformation("Linting finished successfully.");
                return Task.CompletedTask;
            });
            return builder;
        }
        
        public IDistributedApplicationBuilder WithTesting()
        {
            builder.Pipeline.AddStep(WellKnownCIStepNames.Test, context =>
            {
                context.Logger.LogInformation("Testing finished successfully.");
                return Task.CompletedTask;
            });
            return builder;
        }

        public IDistributedApplicationBuilder WithCISteps()
        {
            return builder
                .WithInstallation()
                .WithLinting()
                .WithTesting();
        }
    }
}