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
                Image rowImage = _upgradeRowImages[i];
                Text stateLabel = _upgradeStateTexts[i];
                Text buttonLabel = _upgradeButtons[i].GetComponentInChildren<Text>();
                if (level >= 3)
                {
                    _upgradeRows[i].text = UpgradeLabels[i] + " Lv." + level + " (MAX)";
                    _upgradeRows[i].color = new Color(0.05f, 0.06f, 0.08f, 1f);
                    _upgradeButtons[i].interactable = false;
                    if (buttonLabel != null)
                    {
                        buttonLabel.text = "MAX";
                    }

                    if (stateLabel != null)
                    {
                        stateLabel.text = "MAX";
                        stateLabel.color = new Color(0.12f, 0.36f, 0.74f, 1f);
                    }

                    if (rowImage != null)
                    {
                        rowImage.color = new Color(0.84f, 0.91f, 1f, 0.96f);
                    }

                    SetUpgradeButtonVisual(_upgradeButtons[i], false, true);
                }
                else
                {
                    int next = level + 1;
                    int price = UpgradePrice(next);
                    _upgradeRows[i].text = UpgradeLabels[i] + " Lv." + level + " -> Lv." + next + "   Cost $" + price;
                    _upgradeRows[i].color = new Color(0.05f, 0.06f, 0.08f, 1f);
                    bool canBuy = _meta.TotalCash >= price;
                    _upgradeButtons[i].interactable = canBuy;
                    if (buttonLabel != null)
                    {
                        buttonLabel.text = canBuy ? "UPGRADE" : "LOCKED";
                        buttonLabel.color = canBuy
                            ? new Color(0.08f, 0.08f, 0.1f, 1f)
                            : new Color(0.28f, 0.12f, 0.12f, 1f);
                    }

                    if (stateLabel != null)
                    {
                        stateLabel.text = canBuy ? "BUYABLE" : "NEED $" + price;
                        stateLabel.color = canBuy
                            ? new Color(0.11f, 0.47f, 0.12f, 1f)
                            : new Color(0.62f, 0.17f, 0.17f, 1f);
                    }

                    if (rowImage != null)
                    {
                        rowImage.color = canBuy
                            ? new Color(0.87f, 0.97f, 0.88f, 0.96f)
                            : new Color(0.98f, 0.88f, 0.86f, 0.96f);
                    }

                    SetUpgradeButtonVisual(_upgradeButtons[i], canBuy, false);
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
                bool unlockable = !unlocked && CanUnlockRegionNow(regionId);

                _regionItemButtons[i].interactable = true;
                _regionItemTexts[i].text =
                    (inspected ? "> " : "  ") +
                    LobbyRegionMetaUtil.FormatRegionName(regionId) +
                    (selected ? " *" : string.Empty);

                SetRegionItemVisual(i, unlocked, selected, inspected, unlockable);
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
                    _upgradeRows[i].color = new Color(0.05f, 0.06f, 0.08f, 1f);
                }

                if (_upgradeButtons[i] != null)
                {
                    _upgradeButtons[i].interactable = false;
                    SetUpgradeButtonVisual(_upgradeButtons[i], false, false);
                }

                if (_upgradeStateTexts[i] != null)
                {
                    _upgradeStateTexts[i].text = "N/A";
                    _upgradeStateTexts[i].color = new Color(0.25f, 0.25f, 0.3f, 1f);
                }

                if (_upgradeRowImages[i] != null)
                {
                    _upgradeRowImages[i].color = new Color(0.9f, 0.91f, 0.94f, 0.96f);
                }
            }

            if (_regionText != null)
            {
                _regionText.text = "Region List";
            }

            for (int i = 0; i < _regionItemButtons.Length; i++)
            {
                if (_regionItemTexts[i] != null)
                {
                    _regionItemTexts[i].text = LobbyRegionMetaUtil.FormatRegionName(RegionIds[i]);
                    _regionItemTexts[i].color = new Color(0.08f, 0.08f, 0.1f, 1f);
                }

                if (_regionItemStateTexts[i] != null)
                {
                    _regionItemStateTexts[i].text = "N/A";
                    _regionItemStateTexts[i].color = new Color(0.25f, 0.25f, 0.3f, 1f);
                }

                if (_regionItemIcons[i] != null)
                {
                    _regionItemIcons[i].enabled = false;
                }
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

        private void SetRegionItemVisual(int index, bool unlocked, bool selected, bool inspected, bool unlockable)
        {
            Button button = _regionItemButtons[index];
            Text label = _regionItemTexts[index];
            if (button == null || label == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            Image icon = _regionItemIcons[index];
            Text stateText = _regionItemStateTexts[index];
            bool focused = selected || inspected;

            string stateLabel;
            Sprite stateSprite;
            Color stateColor;
            Color backgroundColor;
            Color labelColor;

            if (focused)
            {
                stateLabel = "SELECTED";
                stateSprite = _regionSelectedIconSprite != null ? _regionSelectedIconSprite : _regionOpenIconSprite;
                stateColor = Color.white;
                backgroundColor = new Color(0.2f, 0.44f, 0.86f, 0.98f);
                labelColor = Color.white;
            }
            else if (unlocked)
            {
                stateLabel = "OPEN";
                stateSprite = _regionOpenIconSprite;
                stateColor = new Color(0.11f, 0.47f, 0.12f, 1f);
                backgroundColor = new Color(0.91f, 0.97f, 0.91f, 1f);
                labelColor = new Color(0.08f, 0.09f, 0.1f, 1f);
            }
            else if (unlockable)
            {
                stateLabel = "UNLOCKABLE";
                stateSprite = _regionUnlockableIconSprite;
                stateColor = new Color(0.7f, 0.52f, 0.05f, 1f);
                backgroundColor = new Color(1f, 0.95f, 0.85f, 1f);
                labelColor = new Color(0.08f, 0.09f, 0.1f, 1f);
            }
            else
            {
                stateLabel = "LOCKED";
                stateSprite = _regionLockedIconSprite;
                stateColor = new Color(0.62f, 0.17f, 0.17f, 1f);
                backgroundColor = new Color(0.88f, 0.89f, 0.92f, 1f);
                labelColor = new Color(0.16f, 0.16f, 0.18f, 1f);
            }

            if (image != null)
            {
                image.color = backgroundColor;
            }

            label.color = labelColor;

            if (icon != null)
            {
                icon.enabled = stateSprite != null;
                icon.sprite = stateSprite;
                icon.color = focused ? Color.white : stateColor;
            }

            if (stateText != null)
            {
                stateText.text = stateLabel;
                stateText.color = focused ? Color.white : stateColor;
            }
        }

        private bool CanUnlockRegionNow(string regionId)
        {
            if (_meta == null || string.IsNullOrEmpty(regionId) || _meta.IsRegionUnlocked(regionId))
            {
                return false;
            }

            RegionUnlockRule rule;
            if (!RegionProgressionCatalog.TryGetRule(regionId, out rule))
            {
                return true;
            }

            if (!_meta.IsRegionUnlocked(rule.PreviousRegionId))
            {
                return false;
            }

            if (_meta.GetBestRunCash(rule.PreviousRegionId) < rule.RequiredRunCash)
            {
                return false;
            }

            if (_meta.GetBestRunRating(rule.PreviousRegionId) < rule.RequiredRunRating)
            {
                return false;
            }

            return _meta.TotalCash >= rule.UnlockCost;
        }

        private static void SetUpgradeButtonVisual(Button button, bool canBuy, bool isMax)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            if (isMax)
            {
                image.color = new Color(0.86f, 0.92f, 1f, 1f);
                return;
            }

            image.color = canBuy
                ? Color.white
                : new Color(0.92f, 0.84f, 0.84f, 1f);
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
