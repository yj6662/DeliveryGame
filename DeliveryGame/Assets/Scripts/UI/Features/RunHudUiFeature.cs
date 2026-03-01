using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Managers.Core;
using DeliveryRun.Managers.Subs;
using DeliveryRun.Music;
using System.Collections.Generic;
using DeliveryRun.UI.Run;
using UnityEngine;
using DomainRunChoiceConstants = DeliveryRun.Delivery.RunSession.RunChoiceConstants;


namespace DeliveryRun.UI.Features
{
    internal sealed partial class RunHudUiFeature : UiFeatureBase
    {
        protected override void OnInitialize()
        {
            InitializeRunHudModule();
        }
        protected override void OnTick(float unscaledDeltaTime)
        {
            TickRunHudModule(unscaledDeltaTime);
        }
        protected override void OnShutdown()
        {
            ShutdownRunHudModule();
        }

        private const float ScenePollInterval = 0.25f;
        private const float UiRefreshInterval = 0.1f;
        private const float MinimapRefreshInterval = 0.05f;

        private const int ChoiceSlots = DomainRunChoiceConstants.ChoiceCount;
        private const int MaxTrackedOrders = 8;

        private readonly string[] _pickedGenreByChoice = new string[ChoiceSlots];
        private readonly string[] _pickedTrackNameByChoice = new string[ChoiceSlots];
        private readonly HudOfferPhoneCoordinator _phoneCoordinator = new HudOfferPhoneCoordinator(MaxTrackedOrders);
        private readonly HudCorePanelsCoordinator _corePanelsCoordinator = new HudCorePanelsCoordinator();
        private readonly HudWorldOverlayCoordinator _worldOverlayCoordinator = new HudWorldOverlayCoordinator();
        private readonly Dictionary<string, float> _completedOrderQualityByOffer = new Dictionary<string, float>(MaxTrackedOrders);

        private MusicLibraryService _musicLibrary;
        private ModifierStackService _modifierStack;
        private OrderFlowManager _orderFlowManager;

        private GameObject _hudInstance;
        private RunHudView _view;
        private bool _hudLoadRequested;
        private bool _isRunScene;
        private bool _musicChoiceModalOpen;

        private float _scenePollElapsed;
        private float _uiRefreshElapsed;
        private float _minimapRefreshElapsed;
        private int _lastWholeSecond;

        private float _speedMultiplier;
        private float _currentSpeedKmh;

        private MotorbikeController _player;

        private float _fuel01 = 1f;

        private int _sessionBalance;

        private HudStatusDisplay _statusDisplay;

        internal bool IsPauseMenuOpen => _corePanelsCoordinator.IsPauseMenuOpen;

    }
}
