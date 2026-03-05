using System.Collections;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryRun.UI.Lobby
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LobbyUIRootView))]
    public sealed class LobbyUIController : MonoBehaviour
    {
        [SerializeField] private LobbyUIRootView view;

        private bool _listenersBound;
        private bool _transitionInProgress;

        private void Awake()
        {
            if (view == null)
            {
                view = GetComponent<LobbyUIRootView>();
            }
        }

        private void OnEnable()
        {
            BindListeners();
            if (view != null)
            {
                view.ShowPanel(LobbyPanel.MainMenu);
            }
        }

        private void OnDisable()
        {
            UnbindListeners();
        }

        public void SetView(LobbyUIRootView rootView)
        {
            view = rootView;
        }

        private void BindListeners()
        {
            if (_listenersBound || view == null)
            {
                return;
            }

            AddListener(view.DeliveryStartButton, OnDeliveryStartClicked);
            AddListener(view.RegionUnlockButton, OnRegionUnlockClicked);
            AddListener(view.UpgradeButton, OnUpgradeClicked);
            AddListener(view.ExitButton, OnExitClicked);
            AddListener(view.RegionUnlockBackButton, OnBackToMainClicked);
            AddListener(view.UpgradeBackButton, OnBackToMainClicked);

            _listenersBound = true;
        }

        private void UnbindListeners()
        {
            if (!_listenersBound || view == null)
            {
                return;
            }

            RemoveListener(view.DeliveryStartButton, OnDeliveryStartClicked);
            RemoveListener(view.RegionUnlockButton, OnRegionUnlockClicked);
            RemoveListener(view.UpgradeButton, OnUpgradeClicked);
            RemoveListener(view.ExitButton, OnExitClicked);
            RemoveListener(view.RegionUnlockBackButton, OnBackToMainClicked);
            RemoveListener(view.UpgradeBackButton, OnBackToMainClicked);

            _listenersBound = false;
        }

        private static void AddListener(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void RemoveListener(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
        }

        private void OnDeliveryStartClicked()
        {
            if (_transitionInProgress)
            {
                return;
            }

            if (TryRequestSceneFlowStart())
            {
                Debug.Log("[LobbyUIController] StartRunRequested published via SceneFlow.");
                return;
            }

            StartCoroutine(FallbackStartRunRoutine());
        }

        private void OnRegionUnlockClicked()
        {
            if (view == null)
            {
                return;
            }

            view.ShowPanel(LobbyPanel.RegionUnlock);
        }

        private void OnUpgradeClicked()
        {
            if (view == null)
            {
                return;
            }

            view.ShowPanel(LobbyPanel.Upgrade);
        }

        private void OnBackToMainClicked()
        {
            if (view == null)
            {
                return;
            }

            view.ShowPanel(LobbyPanel.MainMenu);
        }

        private static void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static bool TryRequestSceneFlowStart()
        {
            CoreRoot root = CoreRoot.Instance;
            if (root == null || root.Events == null)
            {
                return false;
            }

            root.Events.Publish(new StartRunRequested());
            return true;
        }

        private IEnumerator FallbackStartRunRoutine()
        {
            _transitionInProgress = true;

            string runScene = ResolveLoadableScene(SceneNames.RunScene, "RunScene");
            if (string.IsNullOrEmpty(runScene))
            {
                Debug.LogError("[LobbyUIController] Run scene not found for fallback transition.");
                _transitionInProgress = false;
                yield break;
            }

            string loadingScene = ResolveLoadableScene(SceneNames.LoadingScene, "LoadingScene");
            if (!string.IsNullOrEmpty(loadingScene))
            {
                AsyncOperation loadLoading = SceneManager.LoadSceneAsync(loadingScene, LoadSceneMode.Single);
                if (loadLoading != null)
                {
                    while (!loadLoading.isDone)
                    {
                        yield return null;
                    }
                }
            }

            AsyncOperation loadRun = SceneManager.LoadSceneAsync(runScene, LoadSceneMode.Single);
            if (loadRun != null)
            {
                while (!loadRun.isDone)
                {
                    yield return null;
                }
            }

            _transitionInProgress = false;
        }

        private static string ResolveLoadableScene(params string[] candidates)
        {
            if (candidates == null)
            {
                return null;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                string sceneName = candidates[i];
                if (!string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName))
                {
                    return sceneName;
                }
            }

            return null;
        }
    }
}
