using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace CodeCompanionDesktop.Credentials;

public sealed class WindowsCredentialStore
{
    public const string ElevenLabsApiKeyTarget = "CodeCompanionDesktop/ElevenLabsApiKey";

    private const int ErrorNotFound = 1168;
    private const int CredentialPersistLocalMachine = 2;
    private const uint CredentialTypeGeneric = 1;

    public void SaveSecret(string targetName, string userName, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        var secretBytes = Encoding.Unicode.GetBytes(secret);
        var secretPointer = Marshal.AllocCoTaskMem(secretBytes.Length);

        try
        {
            Marshal.Copy(secretBytes, 0, secretPointer, secretBytes.Length);

            var credential = new NativeCredential
            {
                Type = CredentialTypeGeneric,
                TargetName = targetName,
                CredentialBlobSize = (uint)secretBytes.Length,
                CredentialBlob = secretPointer,
                Persist = CredentialPersistLocalMachine,
                UserName = userName
            };

            if (!CredWrite(ref credential, 0))
            {
                throw CreateLastWin32Exception("Failed to save credential.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
            Marshal.Copy(secretBytes, 0, secretPointer, secretBytes.Length);
            Marshal.FreeCoTaskMem(secretPointer);
        }
    }

    public string? ReadSecret(string targetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);

        if (!CredRead(targetName, CredentialTypeGeneric, 0, out var credentialPointer))
        {
            var errorCode = Marshal.GetLastWin32Error();
            if (errorCode == ErrorNotFound)
            {
                return null;
            }

            throw new Win32Exception(errorCode, "Failed to read credential.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return string.Empty;
            }

            var secretBytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, secretBytes, 0, secretBytes.Length);

            try
            {
                return Encoding.Unicode.GetString(secretBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secretBytes);
            }
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public bool DeleteSecret(string targetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);

        if (CredDelete(targetName, CredentialTypeGeneric, 0))
        {
            return true;
        }

        var errorCode = Marshal.GetLastWin32Error();
        if (errorCode == ErrorNotFound)
        {
            return false;
        }

        throw new Win32Exception(errorCode, "Failed to delete credential.");
    }

    private static Win32Exception CreateLastWin32Exception(string message)
    {
        return new Win32Exception(Marshal.GetLastWin32Error(), message);
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(
        string targetName,
        uint type,
        uint flags,
        out IntPtr credentialPointer);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string targetName, uint type, uint flags);

    [DllImport("Advapi32.dll", SetLastError = false)]
    private static extern void CredFree(IntPtr credentialPointer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }
}
