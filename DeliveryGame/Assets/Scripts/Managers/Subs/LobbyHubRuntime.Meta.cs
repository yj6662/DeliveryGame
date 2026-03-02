using System;
using System.Text;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.UI;

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
                ApplyMetaUnavailableState();
                return;
            }

            int unlockedCount = 0;
            for (int i = 0; i < RegionIds.Length; i++)
            {
                if (_meta.IsRegionUnlocked(RegionIds[i]))
                {
                    unlockedCount++;
                }
            }

            _metaText.text =
                "Total Cash: $" + _meta.TotalCash +
                "\nSelected Region: " + LobbyRegionMetaUtil.FormatRegionName(_meta.SelectedRegionId) +
                "\nUnlocked Regions: " + unlockedCount + " / " + RegionIds.Length;

            if (_homeSummaryText != null)
            {
                _homeSummaryText.text =
                    "Current Region: " + LobbyRegionMetaUtil.FormatRegionName(_meta.SelectedRegionId) +
                    "\nUpgrades Owned: " + BuildUpgradeSummary() +
                    "\n\nUse [GARAGE] to purchase upgrades and [REGION] to inspect unlock requirements.";
            }

            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                if (_upgradeRows[i] == null || _upgradeButtons[i] == null)
                {
                    continue;
                }

                int level = _meta.GetUpgradeLevel(UpgradeIds[i]);
                Text buttonLabel = _upgradeButtons[i].GetComponentInChildren<Text>();
                if (level >= 3)
                {
                    _upgradeRows[i].text = UpgradeLabels[i] + " Lv." + level + " (MAX)";
                    _upgradeButtons[i].interactable = false;
                    if (buttonLabel != null)
                    {
                        buttonLabel.text = "MAX";
                    }
                }
                else
                {
                    int next = level + 1;
                    int price = UpgradePrice(next);
                    _upgradeRows[i].text = UpgradeLabels[i] + " Lv." + level + " -> Lv." + next + "   Cost $" + price;
                    bool canBuy = _meta.TotalCash >= price;
                    _upgradeButtons[i].interactable = canBuy;
                    if (buttonLabel != null)
                    {
                        buttonLabel.text = canBuy ? "UPGRADE" : "NEED $" + price;
                    }
                }
            }

            if (string.IsNullOrEmpty(_inspectedRegionId) || !IsKnownRegion(_inspectedRegionId))
            {
                _inspectedRegionId = _meta.SelectedRegionId;
            }

            if (_regionText != null)
            {
                _regionText.text =
                    "Region List\nCurrent: " + LobbyRegionMetaUtil.FormatRegionName(_meta.SelectedRegionId);
            }

            for (int i = 0; i < RegionIds.Length; i++)
            {
                if (_regionItemButtons[i] == null || _regionItemTexts[i] == null)
                {
                    continue;
                }

                string regionId = RegionIds[i];
                bool unlocked = _meta.IsRegionUnlocked(regionId);
                bool selected = string.Equals(regionId, _meta.SelectedRegionId, StringComparison.Ordinal);
                bool inspected = string.Equals(regionId, _inspectedRegionId, StringComparison.Ordinal);

                _regionItemButtons[i].interactable = true;
                _regionItemTexts[i].text =
                    (inspected ? "> " : "  ") +
                    (unlocked ? "[OPEN] " : "[LOCK] ") +
                    LobbyRegionMetaUtil.FormatRegionName(regionId) +
                    (selected ? " *" : string.Empty);

                SetRegionItemVisual(i, unlocked, selected, inspected);
            }

            bool inspectedUnlocked;
            bool canUnlockInspected;
            string detailText = BuildRegionDetailText(_inspectedRegionId, out inspectedUnlocked, out canUnlockInspected);
            if (_regionDetailText != null)
            {
                _regionDetailText.text = detailText;
            }

            if (_unlockButton != null)
            {
                _unlockButton.interactable = canUnlockInspected;
            }

            if (_unlockButtonText != null)
            {
                if (inspectedUnlocked)
                {
                    _unlockButtonText.text = "OPENED";
                }
                else if (canUnlockInspected)
                {
                    _unlockButtonText.text = "UNLOCK";
                }
                else
                {
                    _unlockButtonText.text = "LOCKED";
                }
            }

            if (_nextRegionButton != null)
            {
                _nextRegionButton.interactable = RegionIds.Length > 1;
            }
        }

        private void ApplyMetaUnavailableState()
        {
            _metaText.text = "Meta unavailable";

            if (_homeSummaryText != null)
            {
                _homeSummaryText.text = "Meta data is not loaded.";
            }

            for (int i = 0; i < _upgradeRows.Length; i++)
            {
                if (_upgradeRows[i] != null)
                {
                    _upgradeRows[i].text = UpgradeLabels[i] + " Lv.0";
                }

                if (_upgradeButtons[i] != null)
                {
                    _upgradeButtons[i].interactable = false;
                }
            }

            if (_regionText != null)
            {
                _regionText.text = "Region List";
            }

            if (_regionDetailText != null)
            {
                _regionDetailText.text = "Region detail unavailable.";
            }

            if (_unlockButton != null)
            {
                _unlockButton.interactable = false;
            }

            if (_unlockButtonText != null)
            {
                _unlockButtonText.text = "LOCKED";
            }

            _pendingUnlockRegionId = null;
        }

        private string BuildUpgradeSummary()
        {
            if (_meta == null)
            {
                return "None";
            }

            var builder = new StringBuilder(80);
            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(" | ");
                }

                builder.Append(UpgradeLabels[i]);
                builder.Append(" L");
                builder.Append(_meta.GetUpgradeLevel(UpgradeIds[i]));
            }

            return builder.ToString();
        }

        private string BuildRegionDetailText(string regionId, out bool isUnlocked, out bool canUnlock)
        {
            isUnlocked = false;
            canUnlock = false;
            _pendingUnlockRegionId = null;

            if (_meta == null)
            {
                return "Meta unavailable.";
            }

            if (string.IsNullOrEmpty(regionId) || !IsKnownRegion(regionId))
            {
                regionId = _meta.SelectedRegionId;
            }

            isUnlocked = _meta.IsRegionUnlocked(regionId);

            var builder = new StringBuilder(420);
            builder.Append(LobbyRegionMetaUtil.FormatRegionName(regionId));
            if (string.Equals(regionId, _meta.SelectedRegionId, StringComparison.Ordinal))
            {
                builder.Append("  [CURRENT]");
            }

            builder.Append("\nStatus: ");
            builder.Append(isUnlocked ? "OPEN" : "LOCKED");

            RegionUnlockRule rule;
            if (!RegionProgressionCatalog.TryGetRule(regionId, out rule))
            {
                builder.Append("\n\nStarter region. No unlock conditions.");
                return builder.ToString();
            }

            int bestRunCash = _meta.GetBestRunCash(rule.PreviousRegionId);
            float bestRunRating = _meta.GetBestRunRating(rule.PreviousRegionId);
            bool previousRegionUnlocked = _meta.IsRegionUnlocked(rule.PreviousRegionId);
            bool meetsPerformance =
                previousRegionUnlocked &&
                bestRunCash >= rule.RequiredRunCash &&
                bestRunRating >= rule.RequiredRunRating;
            bool canAfford = _meta.TotalCash >= rule.UnlockCost;
            canUnlock = !isUnlocked && meetsPerformance && canAfford;

            builder.Append("\n\nUnlock Conditions");
            builder.Append("\n- Previous Region Open: ");
            builder.Append(previousRegionUnlocked ? "YES" : "NO");
            builder.Append(" (");
            builder.Append(LobbyRegionMetaUtil.FormatRegionName(rule.PreviousRegionId));
            builder.Append(")");
            builder.Append("\n- Best Cash: $");
            builder.Append(bestRunCash);
            builder.Append(" / $");
            builder.Append(rule.RequiredRunCash);
            builder.Append("\n- Best Rating: ");
            builder.Append(bestRunRating.ToString("0.0"));
            builder.Append(" / ");
            builder.Append(rule.RequiredRunRating.ToString("0.0"));
            builder.Append("\n- Unlock Cost: $");
            builder.Append(rule.UnlockCost);

            builder.Append("\n\nResult: ");
            if (isUnlocked)
            {
                builder.Append("Already unlocked.");
            }
            else if (!previousRegionUnlocked)
            {
                builder.Append("Need previous region unlock first.");
            }
            else if (!meetsPerformance)
            {
                builder.Append("Need better run results.");
            }
            else if (!canAfford)
            {
                builder.Append("Need more cash.");
            }
            else
            {
                builder.Append("Ready to unlock now.");
                _pendingUnlockRegionId = regionId;
            }

            return builder.ToString();
        }

        private void SetRegionItemVisual(int index, bool unlocked, bool selected, bool inspected)
        {
            Button button = _regionItemButtons[index];
            Text label = _regionItemTexts[index];
            if (button == null || label == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                if (inspected)
                {
                    image.color = new Color(0.2f, 0.44f, 0.86f, 0.98f);
                }
                else if (selected)
                {
                    image.color = new Color(0.3f, 0.62f, 0.34f, 0.98f);
                }
                else if (unlocked)
                {
                    image.color = new Color(0.94f, 0.95f, 0.99f, 1f);
                }
                else
                {
                    image.color = new Color(0.8f, 0.82f, 0.86f, 1f);
                }
            }

            label.color = (inspected || selected)
                ? Color.white
                : new Color(0.08f, 0.08f, 0.1f, 1f);
        }

        private static bool IsKnownRegion(string regionId)
        {
            if (string.IsNullOrEmpty(regionId))
            {
                return false;
            }

            for (int i = 0; i < RegionIds.Length; i++)
            {
                if (string.Equals(RegionIds[i], regionId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static int UpgradePrice(int targetLevel)
        {
            if (targetLevel <= 1) return 1200;
            if (targetLevel == 2) return 2800;
            return 5600;
        }
    }
}
