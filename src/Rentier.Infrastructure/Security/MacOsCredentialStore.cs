using System.Diagnostics;
using System.Runtime.Versioning;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;

namespace Rentier.Infrastructure.Security;

/// <summary>
/// macOS Keychain implementation via the <c>security</c> CLI.
/// </summary>
[SupportedOSPlatform("osx")]
public sealed class MacOsCredentialStore : ICredentialStore
{
    private const string SecurityBinary = "security";
    private const string AccountName = "Rentier";
    // macOS security CLI exit code for "item not found"
    private const int ExitCodeNotFound = 44;

    public Task<Result<VoidResult, Error>> SaveCredentialAsync(
        string key, string secret, CancellationToken ct = default)
    {
        return Task.Run<Result<VoidResult, Error>>(async () =>
        {
            // ArgumentList prevents shell injection — each argument is passed verbatim.
            // Password is piped via stdin (not passed as -w <value>) to keep it out of `ps aux`.
            var (exitCode, _, stderr) = await RunSecurityAsync(
                ["add-generic-password", "-a", AccountName, "-s", key, "-U", "-w"],
                stdinInput: secret);

            return exitCode == 0
                ? Result<VoidResult, Error>.Success(VoidResult.Value)
                : Result<VoidResult, Error>.Failure(
                    Error.CredentialWriteFailed(
                        $"security add-generic-password failed (exit {exitCode}): {stderr.Trim()}"));
        }, ct);
    }

    public Task<Result<string, Error>> GetCredentialAsync(
        string key, CancellationToken ct = default)
    {
        return Task.Run<Result<string, Error>>(async () =>
        {
            var (exitCode, stdout, stderr) = await RunSecurityAsync(
                ["find-generic-password", "-a", AccountName, "-s", key, "-w"]);

            if (exitCode == 0)
                return Result<string, Error>.Success(stdout.Trim());

            if (exitCode == ExitCodeNotFound)
                return Result<string, Error>.Failure(Error.CredentialNotFound(key));

            return Result<string, Error>.Failure(
                Error.CredentialReadFailed(
                    $"security find-generic-password failed (exit {exitCode}): {stderr.Trim()}"));
        }, ct);
    }

    public Task<Result<VoidResult, Error>> DeleteCredentialAsync(
        string key, CancellationToken ct = default)
    {
        return Task.Run<Result<VoidResult, Error>>(async () =>
        {
            var (exitCode, _, stderr) = await RunSecurityAsync(
                ["delete-generic-password", "-a", AccountName, "-s", key]);

            // Idempotent: treat "not found" as success
            if (exitCode == 0 || exitCode == ExitCodeNotFound)
                return Result<VoidResult, Error>.Success(VoidResult.Value);

            return Result<VoidResult, Error>.Failure(
                Error.CredentialDeleteFailed(
                    $"security delete-generic-password failed (exit {exitCode}): {stderr.Trim()}"));
        }, ct);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunSecurityAsync(
        string[] args, string? stdinInput = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = SecurityBinary,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdinInput is not null,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // Write secret via stdin to avoid exposing it in process arguments
        if (stdinInput is not null)
        {
            await process.StandardInput.WriteLineAsync(stdinInput);
            process.StandardInput.Close();
        }

        // Read stdout and stderr concurrently to prevent deadlock when either buffer fills
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync();

        return (process.ExitCode, await stdoutTask, await stderrTask);
    }
}
