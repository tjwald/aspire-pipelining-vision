using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace AppHost.CISteps;

internal static class CLIHelper
{
    public static async Task<string> RunProcessWithOutput(string fileName, string args, string workingDir)
    {
        var psi = GetProcessStartInfo(fileName, args, workingDir);
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start '{fileName} {args}'");
        await proc.WaitForExitAsync();
         if (proc.ExitCode != 0)
        {
            throw new Exception($"Process '{fileName} {args}' failed with exit code {proc.ExitCode}");
        }
        return await proc.StandardOutput.ReadToEndAsync();
    }
    
    /// Helper to run external processes and stream output into pipeline logs
    public static async Task RunProcess(string fileName, string args, string workingDir, ILogger? logger = null)
    {
        logger ??= LoggerFactory.Create(b => b.AddConsole()).CreateLogger("aspire-lint");

        var psi = GetProcessStartInfo(fileName, args, workingDir);

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start '{fileName} {args}'");

        var outTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await proc.StandardOutput.ReadLineAsync()) != null)
            {
                logger.LogInformation("{Line}", line);
            }
        });

        var errTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await proc.StandardError.ReadLineAsync()) != null)
            {
                logger.LogError("{Line}", line);
            }
        });

        await Task.WhenAll(outTask, errTask, proc.WaitForExitAsync());

        if (proc.ExitCode != 0)
        {
            throw new Exception($"Process '{fileName} {args}' failed with exit code {proc.ExitCode}");
        }
    }

    private static ProcessStartInfo GetProcessStartInfo(string fileName, string args, string workingDir)
    {
        ProcessStartInfo psi;

        // On Windows, use cmd /c to execute commands through the shell for better PATH resolution
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            psi = new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = $"/c {fileName} {args}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDir
            };
        }
        else
        {
            psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDir
            };
        }

        return psi;
    }
}