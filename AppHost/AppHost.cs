using AppHost.CISteps;

var builder = DistributedApplication.CreateBuilder(args)
    .WithCISteps()   // out of the box
    .AddUvPythonSetup(uvVersion: "0.9.11");

var cache = builder.AddRedis("cache");

var app = builder.AddUvicornApp("app", "../app", "app.main:app")
    .WithUv(args: ["sync", "--all-groups"]) // args passed here so that dev doesn't uninstall and reinstall dev, test groups when switching from run to lint/test
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WaitFor(cache)
    .WithHttpHealthCheck("/health")
    .WithUvInstallationStep(["--all-groups"])
    .WithUvLintingSteps([
        ("ruff", ["check"]),  // could support having a --fix in the linting step augment these commands
        ("mypy", ["."])
    ])
    .WithUvTestingStep([
        ("unit", "pytest", ["-v", "tests/unit"])
    ]);

var frontend = builder.AddViteApp("frontend", "../frontend")
    .WithReference(app)
    .WaitFor(app)
    .WithInstallationStep()  // Should exist by default but allow overriding with customization
    .WithLintingStep();

app.PublishWithContainerFiles(frontend, "./static");


builder.Build().Run();