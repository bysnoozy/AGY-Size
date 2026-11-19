using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace AgySize.Core.Sharing;

/// <summary>
/// Liste les partages de dossiers de la machine locale (API Windows NetShareEnum), sans les partages
/// administratifs (C$, ADMIN$, IPC$...), d'imprimantes ni les partages techniques d'un contrôleur de
/// domaine (SYSVOL, NETLOGON, print$). Les partages masqués créés par un utilisateur (ex. "Compta$")
/// sont conservés.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ShareEnumerator
{
    private const uint StypeMask = 0xFF;
    private const uint StypeDiskTree = 0;
    private const uint StypeSpecial = 0x80000000;
    private const int MaxPreferredLength = -1;
    private const int NerrSuccess = 0;
    private const int ErrorMoreData = 234;

    private static readonly HashSet<string> TechnicalShares = new(StringComparer.OrdinalIgnoreCase)
    {
        "print$", "SYSVOL", "NETLOGON",
    };

    public static IReadOnlyList<NetworkShare> GetLocalShares()
    {
        var machine = Environment.MachineName;
        var shares = new List<NetworkShare>();
        var resume = 0;
        int status;

        do
        {
            status = NetShareEnum(null, 2, out var buffer, MaxPreferredLength, out var read, out _, ref resume);
            if (status != NerrSuccess && status != ErrorMoreData)
            {
                throw new InvalidOperationException($"Impossible de lister les partages réseau (code {status}).");
            }

            try
            {
                var size = Marshal.SizeOf<ShareInfo2>();
                for (var i = 0; i < read; i++)
                {
                    var info = Marshal.PtrToStructure<ShareInfo2>(buffer + i * size);
                    if ((info.Type & StypeMask) != StypeDiskTree || (info.Type & StypeSpecial) != 0
                        || string.IsNullOrEmpty(info.Path) || TechnicalShares.Contains(info.NetName))
                    {
                        continue;
                    }

                    shares.Add(new NetworkShare(
                        info.NetName,
                        $@"\\{machine}\{info.NetName}",
                        info.Path,
                        string.IsNullOrWhiteSpace(info.Remark) ? null : info.Remark));
                }
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    NetApiBufferFree(buffer);
                }
            }
        }
        while (status == ErrorMoreData);

        return shares.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShareInfo2
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string NetName;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Remark;
        public uint Permissions;
        public uint MaxUses;
        public uint CurrentUses;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Path;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Password;
    }

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetShareEnum(
        string? serverName, int level, out IntPtr buffer, int preferredMaxLength,
        out int entriesRead, out int totalEntries, ref int resumeHandle);

    [DllImport("Netapi32.dll")]
    private static extern int NetApiBufferFree(IntPtr buffer);
}
