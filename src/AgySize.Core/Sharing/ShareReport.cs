using AgySize.Core.Models;
using AgySize.Core.Reporting;

namespace AgySize.Core.Sharing;

public static class ShareReport
{
    /// <summary>Liste les partages de la machine (Windows uniquement) et produit le rapport PDF client des partages.</summary>
    public static IReadOnlyList<ShareCoverage> Write(ScanResult result, string filePath, int oldFileThresholdDays = 365)
    {
        IReadOnlyList<NetworkShare> localShares = OperatingSystem.IsWindows()
            ? ShareEnumerator.GetLocalShares()
            : Array.Empty<NetworkShare>();

        var coverage = ShareMatcher.Match(result, localShares);
        SharesPdfReportWriter.WriteReport(result, coverage, filePath, oldFileThresholdDays);
        return coverage;
    }
}
