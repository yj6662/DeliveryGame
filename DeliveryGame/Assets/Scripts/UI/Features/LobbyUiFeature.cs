using DeliveryRun.Managers.Core;
using DeliveryRun.Managers.Subs;
using DeliveryRun.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;


namespace DeliveryRun.UI.Features
{
    internal sealed partial class LobbyUiFeature : UiFeatureBase
    {
        protected override void OnInitialize()
        {
            InitializeLobbyModule();
        }
        protected override void OnTick(float unscaledDeltaTime)
        {
            TickLobbyModule(unscaledDeltaTime);
        }
        protected override void OnShutdown()
        {
            ShutdownLobbyModule();
        }

        private const float LobbyScenePollInterval = 0.25f;
        private static readonly string[] LobbyRegionIds = MetaProgressionConstants.RegionIds;
        private static readonly string[] LobbyUpgradeIds = MetaProgressionConstants.UpgradeIds;

        private AudioManager _lobbyAudioManager;
        private MetaProgressionService _lobbyMetaService;

        private GameObject _lobbyRoot;
        private GameObject _lobbySettingsPanel;
        private Slider _lobbyBgmSlider;
        private Slider _lobbyUiSlider;
        private Text _lobbyMetaCashText;
        private Text _lobbyMetaRegionText;
        private Text _lobbyMetaSelectedRegionText;
        private Text _lobbyMetaUpgradeText;
        private Text _lobbyMetaUnlockText;
        private Button _lobbyUnlockRegionButton;
        private Text _lobbyUnlockRegionButtonLabel;
        private string _lobbyPendingUnlockRegionId;

        private bool _lobbyModuleIsLobbyScene;
        private float _lobbyScenePollElapsed;
        private bool _lobbyModuleSuppressedByHub;

        private Camera _lobbyModuleCamera;
        private bool _lobbyModuleCreatedCamera;

        private void InitializeLobbyModule()
        {
            LobbyHubManager lobbyHubManager;
            if (Services.TryGet(out lobbyHubManager) && lobbyHubManager != null)
            {
                _lobbyModuleSuppressedByHub = true;
                return;
            }

            _lobbyModuleSuppressedByHub = false;
            Services.TryGet(out _lobbyAudioManager);
            Services.TryGet(out _lobbyMetaService);

            Subs.Add<SceneTransitionStarted>(Events, OnLobbySceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnLobbySceneTransitionCompleted);
            Subs.Add<MetaBalanceChanged>(Events, OnLobbyMetaBalanceChanged);
            Subs.Add<RegionUnlocked>(Events, OnLobbyRegionUnlocked);
            Subs.Add<RegionUnlockStatusChanged>(Events, OnLobbyRegionUnlockStatusChanged);
            Subs.Add<RegionUnlockFailed>(Events, OnLobbyRegionUnlockFailed);
            Subs.Add<SelectedRegionChanged>(Events, OnLobbySelectedRegionChanged);
            Subs.Add<PermanentUpgradeChanged>(Events, OnLobbyPermanentUpgradeChanged);

            HandleLobbySceneChanged(SceneManager.GetActiveScene().name);
        }

        private void TickLobbyModule(float unscaledDeltaTime)
        {
            if (_lobbyModuleSuppressedByHub)
            {
                return;
            }

            if (!ScenePollUtil.ShouldPoll(ref _lobbyScenePollElapsed, LobbyScenePollInterval, unscaledDeltaTime))
            {
                return;
            }

            HandleLobbySceneChanged(SceneManager.GetActiveScene().name);
        }

        private void ShutdownLobbyModule()
        {
            if (_lobbyModuleSuppressedByHub)
            {
                _lobbyModuleSuppressedByHub = false;
                return;
            }

            DestroyLobbyModuleUi();
            CleanupLobbyModuleCamera();
        }

        private void OnLobbySceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.LobbyScene)
            {
                return;
            }

            _lobbyModuleIsLobbyScene = false;
            DestroyLobbyModuleUi();
            CleanupLobbyModuleCamera();
        }

        private void OnLobbySceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleLobbySceneChanged(evt.SceneName);
        }

        private void OnLobbyMetaBalanceChanged(MetaBalanceChanged evt)
        {
            RefreshLobbyMetaTexts();
        }

        private void OnLobbyRegionUnlocked(RegionUnlocked evt)
        {
            RefreshLobbyMetaTexts();
        }

        private void OnLobbySelectedRegionChanged(SelectedRegionChanged evt)
        {
            RefreshLobbyMetaTexts();
        }

        private void OnLobbyRegionUnlockStatusChanged(RegionUnlockStatusChanged evt)
        {
            RefreshLobbyMetaTexts();
        }

        private void OnLobbyRegionUnlockFailed(RegionUnlockFailed evt)
        {
            RefreshLobbyMetaTexts();
        }

        private void OnLobbyPermanentUpgradeChanged(PermanentUpgradeChanged evt)
        {
            RefreshLobbyMetaTexts();
        }

        private void HandleLobbySceneChanged(string sceneName)
        {
            bool shouldShow = sceneName == SceneNames.LobbyScene;
            if (_lobbyModuleIsLobbyScene == shouldShow)
            {
                if (shouldShow)
                {
                    EnsureLobbyModuleUi();
                    EnsureLobbyModuleCamera();
                }

                return;
            }

            _lobbyModuleIsLobbyScene = shouldShow;
            if (_lobbyModuleIsLobbyScene)
            {
                EnsureLobbyModuleUi();
                EnsureLobbyModuleCamera();
                return;
            }

            DestroyLobbyModuleUi();
            CleanupLobbyModuleCamera();
        }

        private void EnsureLobbyModuleCamera()
        {
            if (_lobbyModuleCamera != null)
            {
                return;
            }

            Camera existingMain = Camera.main;
            if (existingMain != null)
            {
                _lobbyModuleCamera = existingMain;
                _lobbyModuleCreatedCamera = false;
                return;
            }

            Camera anyCamera = Object.FindAnyObjectByType<Camera>();
            if (anyCamera != null)
            {
                _lobbyModuleCamera = anyCamera;
                if (!_lobbyModuleCamera.CompareTag("MainCamera"))
                {
                    _lobbyModuleCamera.tag = "MainCamera";
                }
                _lobbyModuleCreatedCamera = false;
                return;
            }

            GameObject cameraObject = new GameObject("LobbyCamera");
            _lobbyModuleCamera = cameraObject.AddComponent<Camera>();
            _lobbyModuleCamera.clearFlags = CameraClearFlags.SolidColor;
            _lobbyModuleCamera.backgroundColor = new Color(0.08f, 0.11f, 0.15f, 1f);
            _lobbyModuleCamera.fieldOfView = 54f;
            _lobbyModuleCamera.nearClipPlane = 0.01f;
            _lobbyModuleCamera.farClipPlane = 600f;

            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 9f, -12f);
            cameraObject.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
            cameraObject.AddComponent<AudioListener>();
            _lobbyModuleCreatedCamera = true;
        }

        private void CleanupLobbyModuleCamera()
        {
            if (_lobbyModuleCamera == null)
            {
                _lobbyModuleCreatedCamera = false;
                return;
            }

            if (_lobbyModuleCreatedCamera)
            {
                Object.Destroy(_lobbyModuleCamera.gameObject);
            }

            _lobbyModuleCamera = null;
            _lobbyModuleCreatedCamera = false;
        }
    }
}
