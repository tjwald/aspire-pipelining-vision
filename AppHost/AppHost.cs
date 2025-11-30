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