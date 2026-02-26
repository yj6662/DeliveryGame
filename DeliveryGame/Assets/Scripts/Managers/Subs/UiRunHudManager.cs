using DeliveryRun;
using DeliveryRun.Managers.Core;
using DeliveryRun.UI;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryRun.Managers.Subs
{
    public sealed class UiRunHudManager : SubManagerBase
    {
        private const float ScenePollInterval = 0.25f;
        private const float TimeUpdateInterval = 0.1f;

        private UiPrefabCatalogSO _catalog;
        private GameObject _hudInstance;
        private RunHudView _view;
        private bool _isRunScene;
        private float _scenePollElapsed;
        private float _timeUpdateElapsed;
        private int _lastWholeSecond;
        private string _nowPlayingLabel;

        public override string Name => nameof(UiRunHudManager);
        public override int InitOrder => 36;

        protected override void OnInitialize()
        {
            _catalog = UiPrefabCatalogLoader.LoadOrNull();
            _nowPlayingLabel = "NOW PLAYING: -";
            _lastWholeSecond = int.MinValue;

            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<MusicChoiceSelected>(Events, OnMusicChoiceSelected);

            HandleSceneChanged(SceneManager.GetActiveScene().name);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            _scenePollElapsed += unscaledDeltaTime;
            if (_scenePollElapsed >= ScenePollInterval)
            {
                _scenePollElapsed = 0f;
                HandleSceneChanged(SceneManager.GetActiveScene().name);
            }

            if (!_isRunScene)
            {
                return;
            }

            EnsureHudExists();
            if (_view == null)
            {
                return;
            }

            _timeUpdateElapsed += unscaledDeltaTime;
            if (_timeUpdateElapsed < TimeUpdateInterval)
            {
                return;
            }

            _timeUpdateElapsed = 0f;
            RefreshTimeLabel();
        }

        protected override void OnShutdown()
        {
            DestroyHud();
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To != SceneNames.RunScene)
            {
                DestroyHud();
                _isRunScene = false;
            }
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleSceneChanged(evt.SceneName);
        }

        private void OnMusicChoiceSelected(MusicChoiceSelected evt)
        {
            _nowPlayingLabel = "NOW PLAYING: " + ToTrackName(evt.OptionIndex);
            if (_view != null)
            {
                _view.SetNowPlaying(_nowPlayingLabel);
            }
        }

        private void HandleSceneChanged(string sceneName)
        {
            bool shouldBeRunScene = sceneName == SceneNames.RunScene;
            if (_isRunScene == shouldBeRunScene)
            {
                if (_isRunScene)
                {
                    EnsureHudExists();
                }

                return;
            }

            _isRunScene = shouldBeRunScene;
            if (_isRunScene)
            {
                EnsureHudExists();
                return;
            }

            DestroyHud();
        }

        private void EnsureHudExists()
        {
            if (_view != null)
            {
                return;
            }

            if (_catalog == null)
            {
                _catalog = UiPrefabCatalogLoader.LoadOrNull();
                if (_catalog == null)
                {
                    return;
                }
            }

            if (_catalog.RunHudPrefab == null)
            {
                Debug.LogError("[UiRunHudManager] RunHudPrefab is null in UiPrefabCatalog.");
                return;
            }

            _hudInstance = Object.Instantiate(_catalog.RunHudPrefab);
            _view = _hudInstance.GetComponent<RunHudView>();
            if (_view == null)
            {
                Debug.LogError("[UiRunHudManager] RunHUD prefab missing RunHudView component.");
                Object.Destroy(_hudInstance);
                _hudInstance = null;
                return;
            }

            _view.SetNowPlaying(_nowPlayingLabel);
            _lastWholeSecond = int.MinValue;
            _timeUpdateElapsed = TimeUpdateInterval;
            RefreshTimeLabel();
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

            float remainingSeconds = runSessionManager.RemainingSeconds;
            int wholeSeconds = Mathf.CeilToInt(remainingSeconds);
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
            int seconds = wholeSeconds - (minutes * 60);
            _view.SetTimeLabel("Time: " + minutes.ToString("00") + ":" + seconds.ToString("00"));
        }

        private void DestroyHud()
        {
            if (_view != null)
            {
                Object.Destroy(_view.gameObject);
            }

            _view = null;
            _hudInstance = null;
            _lastWholeSecond = int.MinValue;
            _timeUpdateElapsed = 0f;
        }

        private static string ToTrackName(int optionIndex)
        {
            if (optionIndex == 0)
            {
                return "NITRO BEAT";
            }

            if (optionIndex == 1)
            {
                return "CHILL CRUISE";
            }

            if (optionIndex == 2)
            {
                return "RISK BASS";
            }

            return "UNKNOWN";
        }
    }
}
