using System;
using DeliveryRun.UI.Run;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class HudCorePanelsCoordinator
    {
        private HudGaugePanel _gaugePanel;
        private HudTrackPlayerPanel _trackPlayerPanel;
        private HudPausePanel _pausePanel;
        private Sprite _circleRingSprite;

        internal bool IsPauseMenuOpen => _pausePanel != null && _pausePanel.IsOpen;

        internal void BuildGaugeUi(RunHudView view, float fuel01)
        {
            EnsureGaugePanel();
            _gaugePanel.BuildIfNeeded(view, _circleRingSprite);
            _gaugePanel.SetFuel(fuel01);
        }

        internal void SetSpeed(float speedKmh)
        {
            _gaugePanel?.SetSpeed(speedKmh);
        }

        internal void SetFuel(float fuel01)
        {
            _gaugePanel?.SetFuel(fuel01);
        }

        internal void BuildTrackUi(RunHudView view)
        {
            if (_trackPlayerPanel == null)
            {
                _trackPlayerPanel = new HudTrackPlayerPanel();
            }

            _trackPlayerPanel.BuildIfNeeded(view);
        }

        internal void RefreshTrack(string[] pickedTrackNames)
        {
            _trackPlayerPanel?.Refresh(pickedTrackNames);
        }

        internal void BuildPauseUi(RunHudView view, Action onBackLobbyRequested, Action onUiClick)
        {
            if (_pausePanel == null)
            {
                _pausePanel = new HudPausePanel();
            }

            _pausePanel.BuildIfNeeded(view, onBackLobbyRequested, onUiClick);
        }

        internal void ClosePause(bool restore)
        {
            _pausePanel?.Close(restore);
        }

        internal void Cleanup()
        {
            _pausePanel?.Cleanup();
            _pausePanel = null;

            _gaugePanel?.Cleanup();
            _gaugePanel = null;

            _trackPlayerPanel?.Cleanup();
            _trackPlayerPanel = null;

            DestroyCircleRing();
        }

        private void EnsureGaugePanel()
        {
            if (_gaugePanel == null)
            {
                _gaugePanel = new HudGaugePanel();
            }

            if (_circleRingSprite == null)
            {
                _circleRingSprite = HudUiFactory.CreateCircleRingSprite(128, 3f);
            }
        }

        private void DestroyCircleRing()
        {
            if (_circleRingSprite == null)
            {
                return;
            }

            Texture2D ringTexture = _circleRingSprite.texture;
            Object.Destroy(_circleRingSprite);
            _circleRingSprite = null;
            if (ringTexture != null)
            {
                Object.Destroy(ringTexture);
            }
        }
    }
}
