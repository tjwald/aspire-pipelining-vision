# Aspire Linting Support

When working in a polyglot multi-service application, having standard way to add and run mono repo actions is critical for developer onboarding, AI agent efficeincy and maintainability.

Using the aspire pipelines you can add custom commands to run on all supported services of your application.

In this example repo, I am adding linting support, but this could easily be used for testing, dependency scans & audit, and more.


# Usage

You can run: 
```bash
aspire do lint
```

and you will see this output:
![img.png](docs/images/img.png)