using System.Collections.Generic;

namespace AssetInventory
{
    internal enum PackageTileStatusTone
    {
        Accent,
        Danger,
        Success,
        Warning
    }

    internal static class PackageTileStatusModel
    {
        private static readonly PackageTileStatus[] StatusPriority =
        {
            PackageTileStatus.StoreStatus,
            PackageTileStatus.UpdateAvailable,
            PackageTileStatus.Outdated,
            PackageTileStatus.NoIndex,
            PackageTileStatus.Excluded,
            PackageTileStatus.InProject,
            PackageTileStatus.Downloaded,
            PackageTileStatus.Indexed,
            PackageTileStatus.AICaptions,
            PackageTileStatus.SemanticIndex,
            PackageTileStatus.CodeIndex,
            PackageTileStatus.Backup,
            PackageTileStatus.KeepCached
        };

        private static readonly string[] StatusLabels =
        {
            "Store Status",
            "Update Available",
            "Outdated",
            "No Index",
            "Excluded",
            "In Project",
            "Downloaded",
            "Indexed",
            "AI Captions",
            "Semantic Index",
            "Code Index",
            "Backup",
            "Keep Cached"
        };

        internal static IReadOnlyList<PackageTileStatus> PriorityOrder => StatusPriority;
        internal static IReadOnlyList<string> SelectorLabels => StatusLabels;

        internal static PackageTileStatus NormalizeMask(int mask)
        {
            return (PackageTileStatus)(mask & (int)PackageTileStatus.All);
        }

        internal static bool IsSelected(PackageTileStatus selectedStatuses, PackageTileStatus status)
        {
            return (selectedStatuses & status) != 0;
        }

        internal static bool IsActive(PackageTileStatus status, AssetInfo info, List<AssetInfo> allAssets)
        {
            if (info == null) return false;

            switch (status)
            {
                case PackageTileStatus.StoreStatus:
                    return info.IsDeprecated || info.IsAbandoned;
                case PackageTileStatus.UpdateAvailable:
                    return info.IsUpdateAvailable(allAssets, false) || info.WasOutdated;
                case PackageTileStatus.Outdated:
                    return info.CurrentSubState == Asset.SubState.Outdated;
                case PackageTileStatus.NoIndex:
                    return info.NoIndex;
                case PackageTileStatus.Excluded:
                    return info.Exclude;
                case PackageTileStatus.InProject:
                    return info.InProject;
                case PackageTileStatus.Downloaded:
                    return info.IsDownloaded;
                case PackageTileStatus.Indexed:
                    return info.IsIndexed;
                case PackageTileStatus.AICaptions:
                    return info.UseAI;
                case PackageTileStatus.SemanticIndex:
                    return info.IsSemanticIndexEnabled;
                case PackageTileStatus.CodeIndex:
                    return info.IsCodeIndexEnabled;
                case PackageTileStatus.Backup:
                    return info.Backup;
                case PackageTileStatus.KeepCached:
                    return info.KeepExtracted;
                default:
                    return false;
            }
        }

        internal static string GetTileLabel(PackageTileStatus status, AssetInfo info)
        {
            switch (status)
            {
                case PackageTileStatus.StoreStatus:
                    return info?.IsAbandoned == true ? "Disabled" : "Deprecated";
                case PackageTileStatus.UpdateAvailable:
                    return "Update";
                case PackageTileStatus.Outdated:
                    return "Outdated";
                case PackageTileStatus.NoIndex:
                    return "No Index";
                case PackageTileStatus.Excluded:
                    return "Excluded";
                case PackageTileStatus.InProject:
                    return "In Project";
                case PackageTileStatus.Downloaded:
                    return "Downloaded";
                case PackageTileStatus.Indexed:
                    return "Indexed";
                case PackageTileStatus.AICaptions:
                    return "AI";
                case PackageTileStatus.SemanticIndex:
                    return "Semantic";
                case PackageTileStatus.CodeIndex:
                    return "Code";
                case PackageTileStatus.Backup:
                    return "Backup";
                case PackageTileStatus.KeepCached:
                    return "Keep Cached";
                default:
                    return string.Empty;
            }
        }

        internal static string GetTooltip(PackageTileStatus status, AssetInfo info)
        {
            switch (status)
            {
                case PackageTileStatus.StoreStatus:
                    return info?.IsAbandoned == true
                        ? "This package is disabled by Unity."
                        : "This package is deprecated in the Unity Asset Store.";
                case PackageTileStatus.UpdateAvailable:
                    return "A newer package version is available.";
                case PackageTileStatus.Outdated:
                    return "The downloaded package cache is outdated.";
                case PackageTileStatus.NoIndex:
                    return "Future indexing is disabled for this package.";
                case PackageTileStatus.Excluded:
                    return "This package is excluded from indexing and search results.";
                case PackageTileStatus.InProject:
                    return "Content from this package is present in the current project.";
                case PackageTileStatus.Downloaded:
                    return "This package is available locally.";
                case PackageTileStatus.Indexed:
                    return "This package has indexed files.";
                case PackageTileStatus.AICaptions:
                    return "This package is enabled for AI caption creation.";
                case PackageTileStatus.SemanticIndex:
                    return "This package is enabled for semantic indexing.";
                case PackageTileStatus.CodeIndex:
                    return "This package is enabled for code indexing.";
                case PackageTileStatus.Backup:
                    return "Automatic backups are enabled for this package.";
                case PackageTileStatus.KeepCached:
                    return "This package is configured to stay extracted in the cache.";
                default:
                    return string.Empty;
            }
        }

        internal static PackageTileStatusTone GetTone(PackageTileStatus status)
        {
            switch (status)
            {
                case PackageTileStatus.StoreStatus:
                case PackageTileStatus.NoIndex:
                case PackageTileStatus.Excluded:
                    return PackageTileStatusTone.Danger;
                case PackageTileStatus.UpdateAvailable:
                case PackageTileStatus.Outdated:
                    return PackageTileStatusTone.Warning;
                case PackageTileStatus.InProject:
                case PackageTileStatus.Downloaded:
                case PackageTileStatus.Indexed:
                    return PackageTileStatusTone.Success;
                default:
                    return PackageTileStatusTone.Accent;
            }
        }

    }
}
