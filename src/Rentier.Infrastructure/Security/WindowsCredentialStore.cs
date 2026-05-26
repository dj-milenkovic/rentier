using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;

namespace Rentier.Infrastructure.Security;

/// <summary>
/// Windows Credential Manager implementation for secure credential storage.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialStore : ICredentialStore
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIALW
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public uint LastWrittenLow;
        public uint LastWrittenHigh;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
    }

    private const uint CRED_TYPE_GENERIC = 1u;
    private const uint CRED_PERSIST_LOCAL_MACHINE = 2u;
    private const int ERROR_NOT_FOUND = 1168;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWriteW(ref CREDENTIALW credential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredReadW(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr credentialPtr);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDeleteW(string target, uint type, uint reservedFlag);

    public Task<Result<VoidResult, Error>> SaveCredentialAsync(
        string key, string secret, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(key))
            return Task.FromResult(
                Result<VoidResult, Error>.Failure(
                    Error.CredentialWriteFailed("Key must not be empty")));

        if (string.IsNullOrEmpty(secret))
            return Task.FromResult(
                Result<VoidResult, Error>.Failure(
                    Error.CredentialWriteFailed("Secret must not be empty")));

        return Task.Run<Result<VoidResult, Error>>(() =>
        {
            byte[] blob = Encoding.UTF8.GetBytes(secret);
            IntPtr ptr = Marshal.AllocHGlobal(blob.Length);
            try
            {
                Marshal.Copy(blob, 0, ptr, blob.Length);
                var cred = new CREDENTIALW
                {
                    Type = CRED_TYPE_GENERIC,
                    TargetName = key,
                    CredentialBlobSize = (uint)blob.Length,
                    CredentialBlob = ptr,
                    Persist = CRED_PERSIST_LOCAL_MACHINE,
                    UserName = key
                };
                if (!CredWriteW(ref cred, 0))
                {
                    var ex = new Win32Exception(Marshal.GetLastPInvokeError());
                    return Result<VoidResult, Error>.Failure(
                        Error.CredentialWriteFailed(ex.Message));
                }
                return Result<VoidResult, Error>.Success(VoidResult.Value);
            }
            finally
            {
                // Zero the blob before freeing to avoid leaving the secret in unmanaged memory
                Array.Clear(blob, 0, blob.Length);
                Marshal.FreeHGlobal(ptr);
            }
        }, ct);
    }

    public Task<Result<string, Error>> GetCredentialAsync(
        string key, CancellationToken ct = default)
    {
        return Task.Run<Result<string, Error>>(() =>
        {
            if (!CredReadW(key, CRED_TYPE_GENERIC, 0, out IntPtr ptr))
            {
                var err = Marshal.GetLastPInvokeError();
                return err == ERROR_NOT_FOUND
                    ? Result<string, Error>.Failure(Error.CredentialNotFound(key))
                    : Result<string, Error>.Failure(
                        Error.CredentialReadFailed(new Win32Exception(err).Message));
            }

            try
            {
                var cred = Marshal.PtrToStructure<CREDENTIALW>(ptr);
                byte[] blob = new byte[cred.CredentialBlobSize];
                Marshal.Copy(cred.CredentialBlob, blob, 0, blob.Length);
                var secret = Encoding.UTF8.GetString(blob);
                // Zero the blob to avoid leaving the secret in managed memory
                Array.Clear(blob, 0, blob.Length);
                return Result<string, Error>.Success(secret);
            }
            finally
            {
                CredFree(ptr);
            }
        }, ct);
    }

    public Task<Result<VoidResult, Error>> DeleteCredentialAsync(
        string key, CancellationToken ct = default)
    {
        return Task.Run<Result<VoidResult, Error>>(() =>
        {
            if (!CredDeleteW(key, CRED_TYPE_GENERIC, 0))
            {
                var err = Marshal.GetLastPInvokeError();
                if (err == ERROR_NOT_FOUND)
                    return Result<VoidResult, Error>.Success(VoidResult.Value); // idempotent
                return Result<VoidResult, Error>.Failure(
                    Error.CredentialDeleteFailed(new Win32Exception(err).Message));
            }
            return Result<VoidResult, Error>.Success(VoidResult.Value);
        }, ct);
    }
}
