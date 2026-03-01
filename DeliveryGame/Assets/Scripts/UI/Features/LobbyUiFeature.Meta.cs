using DeliveryRun.Managers.Core;

namespace DeliveryRun.UI.Features
{
    internal sealed partial class LobbyUiFeature
    {
        private void RefreshLobbyMetaTexts()
        {
            if (_lobbyMetaCashText == null || _lobbyMetaRegionText == null || _lobbyMetaSelectedRegionText == null || _lobbyMetaUpgradeText == null || _lobbyMetaUnlockText == null)
            {
                return;
            }

            EnsureLobbyMetaService();
            if (_lobbyMetaService == null)
            {
                _lobbyMetaCashText.text = "Total Cash: $0";
                _lobbyMetaRegionText.text = "Sectors: 1 / " + LobbyRegionIds.Length;
                _lobbyMetaSelectedRegionText.text = "Selected: CENTRAL";
                _lobbyMetaUpgradeText.text = "Upgrades: SPD L0 | TRN L0 | ACC L0 | LUCK L0";
                _lobbyMetaUnlockText.text = "Unlock info unavailable.";
                _lobbyPendingUnlockRegionId = null;
                if (_lobbyUnlockRegionButton != null) _lobbyUnlockRegionButton.interactable = false;
                if (_lobbyUnlockRegionButtonLabel != null) _lobbyUnlockRegionButtonLabel.text = "UNLOCK REGION";
                return;
            }

            int unlockedCount = 0;
            for (int i = 0; i < LobbyRegionIds.Length; i++)
            {
                if (_lobbyMetaService.IsRegionUnlocked(LobbyRegionIds[i]))
                {
                    unlockedCount++;
                }
            }

            _lobbyMetaCashText.text = "Total Cash: $" + _lobbyMetaService.TotalCash;
            _lobbyMetaRegionText.text = "Sectors: " + unlockedCount + " / " + LobbyRegionIds.Length;
            _lobbyMetaSelectedRegionText.text = "Selected: " + LobbyRegionMetaUtil.FormatRegionName(_lobbyMetaService.SelectedRegionId);
            _lobbyMetaUpgradeText.text =
                "Upgrades: SPD L" + _lobbyMetaService.GetUpgradeLevel(LobbyUpgradeIds[0]) +
                " | TRN L" + _lobbyMetaService.GetUpgradeLevel(LobbyUpgradeIds[1]) +
                " | ACC L" + _lobbyMetaService.GetUpgradeLevel(LobbyUpgradeIds[2]) +
                " | LUCK L" + _lobbyMetaService.GetUpgradeLevel(LobbyUpgradeIds[3]);

            LobbyRegionUnlockSnapshot unlockSnapshot;
            if (!LobbyRegionMetaUtil.TryBuildNextUnlockSnapshot(_lobbyMetaService, out unlockSnapshot))
            {
                _lobbyMetaUnlockText.text = "All sectors unlocked.";
                if (_lobbyUnlockRegionButton != null) _lobbyUnlockRegionButton.interactable = false;
                if (_lobbyUnlockRegionButtonLabel != null) _lobbyUnlockRegionButtonLabel.text = "ALL UNLOCKED";
                _lobbyPendingUnlockRegionId = null;
                return;
            }

            _lobbyPendingUnlockRegionId = unlockSnapshot.RegionId;

            if (!unlockSnapshot.PreviousRegionUnlocked)
            {
                _lobbyMetaUnlockText.text =
                    "Unlock " + LobbyRegionMetaUtil.FormatRegionName(unlockSnapshot.RegionId) +
                    ": clear " + LobbyRegionMetaUtil.FormatRegionName(unlockSnapshot.PreviousRegionId) + " first.";
            }
            else
            {
                _lobbyMetaUnlockText.text =
                    "Unlock " + LobbyRegionMetaUtil.FormatRegionName(unlockSnapshot.RegionId) +
                    " | Prev best $" + unlockSnapshot.BestRunCash + " (need $" + unlockSnapshot.RequiredRunCash + ")" +
                    " | R " + unlockSnapshot.BestRunRating.ToString("0.0") + " (need " + unlockSnapshot.RequiredRunRating.ToString("0.0") + ")" +
                    " | Cost $" + unlockSnapshot.UnlockCost;
            }

            if (_lobbyUnlockRegionButton != null)
            {
                _lobbyUnlockRegionButton.interactable = unlockSnapshot.CanUnlock;
            }

            if (_lobbyUnlockRegionButtonLabel != null)
            {
                if (unlockSnapshot.CanUnlock)
                {
                    _lobbyUnlockRegionButtonLabel.text = "UNLOCK " + LobbyRegionMetaUtil.FormatRegionName(unlockSnapshot.RegionId);
                }
                else if (!unlockSnapshot.CanAfford)
                {
                    _lobbyUnlockRegionButtonLabel.text = "NEED CASH $" + unlockSnapshot.UnlockCost;
                }
                else
                {
                    _lobbyUnlockRegionButtonLabel.text = "REQUIREMENTS LOCKED";
                }
            }
        }

        private void EnsureLobbyMetaService()
        {
            if (_lobbyMetaService != null)
            {
                return;
            }

            Services.TryGet(out _lobbyMetaService);
        }
    }
}
