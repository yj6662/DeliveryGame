using DeliveryRun.Delivery.Input;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Managers.Core;
using DeliveryRun.Managers.Subs;
using DeliveryRun.Music;
using DeliveryRun.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.UI.Features
{
    internal sealed partial class RunHudUiFeature
    {
        private void HandleOfferAcceptInput()
        {
            if (!RuntimeInput.WasOfferAcceptPressedThisFrame())
            {
                return;
            }

            string offerId;
            if (!_phoneCoordinator.TryConsumeOfferAccept(_musicChoiceModalOpen, out offerId))
            {
                return;
            }

            TryPlayUiClick();
            Events.Publish(new AcceptOfferRequested { OfferId = offerId });
        }

        private void RefreshTimeLabel()
        {
            if (_view == null)
            {
                return;
            }

            RunSessionManager runSessionManager;
            if (!Services.TryGet(out runSessionManager) || runSessionManager == null)
            {
                _view.SetTimeLabel("Time: --:--");
                return;
            }

            int wholeSeconds = Mathf.CeilToInt(runSessionManager.RemainingSeconds);
            if (wholeSeconds < 0)
            {
                wholeSeconds = 0;
            }

            if (wholeSeconds == _lastWholeSecond)
            {
                return;
            }

            _lastWholeSecond = wholeSeconds;
            int minutes = wholeSeconds / 60;
            int seconds = wholeSeconds - minutes * 60;
            _view.SetTimeLabel("Time: " + minutes.ToString("00") + ":" + seconds.ToString("00"));
        }

        private void RefreshStatusHud()
        {
            if (_view == null)
            {
                return;
            }

            EnsurePlayerReference();

            int speedKmh = 0;
            if (_player != null)
            {
                speedKmh = Mathf.RoundToInt(Mathf.Max(0f, _player.CurrentSpeed) * 3.6f);
            }

            _currentSpeedKmh = speedKmh;
            _corePanelsCoordinator.SetSpeed(_currentSpeedKmh);
            RebuildBuffLineFromModifiers();
            ApplyStatusLines();
        }

        private void ApplyStatusLines()
        {
            _statusDisplay?.ApplyStatus(_view);
        }

        private void RebuildBuffLineFromModifiers()
        {
            if (_modifierStack == null)
            {
                Services.TryGet(out _modifierStack);
            }

            _statusDisplay?.RebuildBuffLine(_modifierStack, _speedMultiplier);
        }

        private string ResolveTrackName(MusicChoiceSelected evt)
        {
            if (_musicLibrary == null)
            {
                Services.TryGet(out _musicLibrary);
            }

            if (_musicLibrary != null && !string.IsNullOrEmpty(evt.TrackId))
            {
                MusicTrackSO track;
                if (_musicLibrary.TryGetTrack(evt.TrackId, out track) && track != null && !string.IsNullOrEmpty(track.DisplayName))
                {
                    return track.DisplayName;
                }
            }

            if (evt.OptionIndex == 0) return "NITRO BEAT";
            if (evt.OptionIndex == 1) return "CHILL CRUISE";
            if (evt.OptionIndex == 2) return "RISK BASS";
            return "UNKNOWN";
        }

        private void RebuildSynergyLine()
        {
            if (_musicLibrary == null)
            {
                Services.TryGet(out _musicLibrary);
            }

            _statusDisplay?.RebuildSynergyLine(_musicLibrary, _pickedGenreByChoice);
        }

        private void ApplyCashLabel()
        {
            _statusDisplay?.ApplyCash(_view, _sessionBalance, _phoneCoordinator.SessionBonus);
        }

        private void BuildPhoneUi()
        {
            _phoneCoordinator.BuildUiIfNeeded(_view);
        }

        private void UpdatePhonePanel(float dt)
        {
            _phoneCoordinator.UpdateUiLayout(dt);
        }

        private void RebuildPhoneTextIfNeeded(bool force = false)
        {
            _phoneCoordinator.RefreshUiText(force);
        }

        private void RefreshTrackPlayerText()
        {
            _corePanelsCoordinator.RefreshTrack(_pickedTrackNameByChoice);
        }

        private void BuildWorldOrderTimerUi()
        {
            _worldOverlayCoordinator.BuildUiIfNeeded(_view);
        }

        private void RefreshWorldOrderTimerUi()
        {
            EnsurePlayerReference();
            EnsureOrderFlowManager();
            _worldOverlayCoordinator.RefreshWorldOrderTimer(_isRunScene, _orderFlowManager, _player);
        }

        private void UpdateWorldOrderTimerTransform()
        {
            EnsurePlayerReference();
            _worldOverlayCoordinator.UpdateWorldOrderTimerTransform(_isRunScene, _player);
        }

        private void UpdateWorldRewardPopupTransform()
        {
            EnsurePlayerReference();
            _worldOverlayCoordinator.UpdateWorldRewardPopupTransform(_isRunScene, _player);
        }

        private void BuildMinimapUi()
        {
            _worldOverlayCoordinator.BuildUiIfNeeded(_view);
        }

        private void TryPlayUiClick()
        {
            Ui.PlayUiClick();
        }

        private void EnsurePlayerReference()
        {
            if (_player == null)
            {
                _player = Object.FindAnyObjectByType<MotorbikeController>();
            }
        }

        private void EnsureOrderFlowManager()
        {
            if (_orderFlowManager == null)
            {
                Services.TryGet(out _orderFlowManager);
            }
        }

        private void ResetState()
        {
            _currentSpeedKmh = 0f;
            _fuel01 = 1f;
            if (_statusDisplay == null)
            {
                _statusDisplay = new HudStatusDisplay();
            }

            _statusDisplay.Reset();
            _speedMultiplier = 1f;
            _phoneCoordinator.ResetAll();
            _completedOrderQualityByOffer.Clear();
            _sessionBalance = 0;
            _lastWholeSecond = int.MinValue;
            _worldOverlayCoordinator.ResetRuntimeState();
            ClearMusicChoiceSelection();
        }

        private void ClearMusicChoiceSelection()
        {
            for (int i = 0; i < ChoiceSlots; i++)
            {
                _pickedGenreByChoice[i] = null;
                _pickedTrackNameByChoice[i] = null;
            }
        }
    }
}
