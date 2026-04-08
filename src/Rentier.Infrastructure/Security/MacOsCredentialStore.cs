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
        return Task.Run<Result<VoidResult, Error>>(() =>
        {
            // -U: update if exists (upsert semantics)
            var (exitCode, _, stderr) = RunSecurity(
                $"add-generic-password -a \"{AccountName}\" -s \"{key}\" -w \"{secret}\" -U");

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
        return Task.Run<Result<string, Error>>(() =>
        {
            var (exitCode, stdout, stderr) = RunSecurity(
                $"find-generic-password -a \"{AccountName}\" -s \"{key}\" -w");

            if (exitCode == 0)
                return Result<string, Error>.Success(stdout.Trim());

            if (exitCode == ExitCodeNotFound)
                return Result<string, Error>.Failure(Error.CredentialNotFound(key));

            return Result<string, Error>.Failure(
                Error.CredentialWriteFailed(
                    $"security find-generic-password failed (exit {exitCode}): {stderr.Trim()}"));
        }, ct);
    }

    public Task<Result<VoidResult, Error>> DeleteCredentialAsync(
        string key, CancellationToken ct = default)
    {
        return Task.Run<Result<VoidResult, Error>>(() =>
        {
            var (exitCode, _, stderr) = RunSecurity(
                $"delete-generic-password -a \"{AccountName}\" -s \"{key}\"");

            // Idempotent: treat "not found" as success
            if (exitCode == 0 || exitCode == ExitCodeNotFound)
                return Result<VoidResult, Error>.Success(VoidResult.Value);

            return Result<VoidResult, Error>.Failure(
                Error.CredentialDeleteFailed(
                    $"security delete-generic-password failed (exit {exitCode}): {stderr.Trim()}"));
        }, ct);
    }

    private static (int ExitCode, string Stdout, string Stderr) RunSecurity(string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = SecurityBinary,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout, stderr);
    }
}
