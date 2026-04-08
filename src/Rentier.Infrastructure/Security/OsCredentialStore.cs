using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Rentier.Application.Interfaces;

namespace Rentier.Infrastructure.Security;

/// <summary>
/// Windows Credential Manager implementation for secure credential storage.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class OsCredentialStore : ICredentialStore
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
    private static extern void CredFreeW(IntPtr credentialPtr);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDeleteW(string target, uint type, uint reservedFlag);

    public Task SaveCredentialAsync(string key, string secret, CancellationToken ct = default)
    {
        return Task.Run(() =>
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
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }, ct);
    }

    public Task<string?> GetCredentialAsync(string key, CancellationToken ct = default)
    {
        return Task.Run<string?>(() =>
        {
            if (!CredReadW(key, CRED_TYPE_GENERIC, 0, out IntPtr ptr))
            {
                if (Marshal.GetLastPInvokeError() == ERROR_NOT_FOUND)
                    return null;
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            try
            {
                var cred = Marshal.PtrToStructure<CREDENTIALW>(ptr);
                byte[] blob = new byte[cred.CredentialBlobSize];
                Marshal.Copy(cred.CredentialBlob, blob, 0, blob.Length);
                return Encoding.UTF8.GetString(blob);
            }
            finally
            {
                CredFreeW(ptr);
            }
        }, ct);
    }

    public Task DeleteCredentialAsync(string key, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (!CredDeleteW(key, CRED_TYPE_GENERIC, 0))
            {
                if (Marshal.GetLastPInvokeError() != ERROR_NOT_FOUND)
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
        }, ct);
    }
}

