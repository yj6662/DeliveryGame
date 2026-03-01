using System.Text;
using DeliveryRun.Managers.Core;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class LobbyHubRuntime
    {
        private static readonly string[] RegionIds = MetaProgressionConstants.RegionIds;
        private static readonly string[] UpgradeLabels = MetaProgressionConstants.UpgradeLabels;

        private void RefreshMeta()
        {
            if (_metaText == null)
            {
                return;
            }

            if (_meta == null)
            {
                _services.TryGet(out _meta);
            }

            if (_meta == null)
            {
                _metaText.text = "Meta unavailable";
                return;
            }

            _metaText.text =
                "Total Cash: $" + _meta.TotalCash +
                "\nSelected Region: " + LobbyRegionMetaUtil.FormatRegionName(_meta.SelectedRegionId);
            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                if (_upgradeRows[i] == null || _upgradeButtons[i] == null)
                {
                    continue;
                }

                int level = _meta.GetUpgradeLevel(UpgradeIds[i]);
                if (level >= 3)
                {
                    _upgradeRows[i].text = UpgradeLabels[i] + " Lv." + level + " (MAX)";
                    _upgradeButtons[i].interactable = false;
                }
                else
                {
                    int price = UpgradePrice(level + 1);
                    _upgradeRows[i].text = UpgradeLabels[i] + " Lv." + level + " -> Lv." + (level + 1) + "  $" + price;
                    _upgradeButtons[i].interactable = _meta.TotalCash >= price;
                }
            }

            if (_regionText != null)
            {
                var regionBuilder = new StringBuilder(512);
                regionBuilder.Append("Unlocked Regions\n");
                for (int i = 0; i < RegionIds.Length; i++)
                {
                    string regionId = RegionIds[i];
                    bool unlocked = _meta.IsRegionUnlocked(regionId);
                    regionBuilder.Append(unlocked ? "[OPEN] " : "[LOCK] ");
                    if (string.Equals(regionId, _meta.SelectedRegionId, System.StringComparison.Ordinal))
                    {
                        regionBuilder.Append("* ");
                    }

                    regionBuilder.Append(LobbyRegionMetaUtil.FormatRegionName(regionId));
                    regionBuilder.Append('\n');
                }

                LobbyRegionUnlockSnapshot unlockSnapshot;
                if (!LobbyRegionMetaUtil.TryBuildNextUnlockSnapshot(_meta, out unlockSnapshot))
                {
                    regionBuilder.Append("\nAll regions unlocked.");
                    _regionText.text = regionBuilder.ToString();
                    if (_unlockButton != null) _unlockButton.interactable = false;
                    if (_unlockButtonText != null) _unlockButtonText.text = "ALL";
                    _pendingUnlockRegionId = null;
                }
                else
                {
                    _pendingUnlockRegionId = unlockSnapshot.RegionId;
                    regionBuilder.Append("\nNext Unlock: ");
                    regionBuilder.Append(LobbyRegionMetaUtil.FormatRegionName(unlockSnapshot.RegionId));
                    regionBuilder.Append("\nNeed previous: ");
                    regionBuilder.Append(LobbyRegionMetaUtil.FormatRegionName(unlockSnapshot.PreviousRegionId));
                    regionBuilder.Append("\nBest Cash: $");
                    regionBuilder.Append(unlockSnapshot.BestRunCash);
                    regionBuilder.Append(" / $");
                    regionBuilder.Append(unlockSnapshot.RequiredRunCash);
                    regionBuilder.Append("\nBest Rating: ");
                    regionBuilder.Append(unlockSnapshot.BestRunRating.ToString("0.0"));
                    regionBuilder.Append(" / ");
                    regionBuilder.Append(unlockSnapshot.RequiredRunRating.ToString("0.0"));
                    regionBuilder.Append("\nUnlock Cost: $");
                    regionBuilder.Append(unlockSnapshot.UnlockCost);

                    _regionText.text = regionBuilder.ToString();
                    if (_unlockButton != null) _unlockButton.interactable = unlockSnapshot.CanUnlock;
                    if (_unlockButtonText != null) _unlockButtonText.text = unlockSnapshot.CanUnlock ? "UNLOCK" : "LOCKED";
                }
            }
        }

        private static int UpgradePrice(int targetLevel)
        {
            if (targetLevel <= 1) return 1200;
            if (targetLevel == 2) return 2800;
            return 5600;
        }
    }
}
