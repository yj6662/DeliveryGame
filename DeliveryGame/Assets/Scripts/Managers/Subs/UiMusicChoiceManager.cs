using DeliveryRun;
using DeliveryRun.Delivery.RunSession;
using DeliveryRun.Managers.Core;
using DeliveryRun.UI;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryRun.Managers.Subs
{
    public sealed class UiMusicChoiceManager : SubManagerBase
    {
        private const float ScenePollInterval = 0.25f;

        private UiPrefabCatalogSO _catalog;
        private MusicSelectionModalView _modal;
        private bool _isOpen;
        private int _choiceIndexOpen;
        private float _scenePollElapsed;
        private bool _fallbackPauseCaptured;
        private float _fallbackSavedTimeScale;

        public override string Name => nameof(UiMusicChoiceManager);
        public override int InitOrder => 35;

        protected override void OnInitialize()
        {
            _catalog = UiPrefabCatalogLoader.LoadOrNull();
            _choiceIndexOpen = -1;

            Subs.Add<RunChoicePointReached>(Events, OnRunChoicePointReached);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            if (!_isOpen)
            {
                return;
            }

            _scenePollElapsed += unscaledDeltaTime;
            if (_scenePollElapsed < ScenePollInterval)
            {
                return;
            }

            _scenePollElapsed = 0f;
            if (SceneManager.GetActiveScene().name != SceneNames.RunScene)
            {
                HideModalAndResume();
            }
        }

        protected override void OnShutdown()
        {
            HideModalAndResume();
            RestoreFallbackTimeScale();
        }

        private void OnRunChoicePointReached(RunChoicePointReached evt)
        {
            if (evt.Index != 0 || _isOpen)
            {
                return;
            }

            if (SceneManager.GetActiveScene().name != SceneNames.RunScene)
            {
                return;
            }

            ShowModal(evt.Index);
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To != SceneNames.RunScene)
            {
                HideModalAndResume();
            }
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            if (evt.SceneName != SceneNames.RunScene)
            {
                HideModalAndResume();
            }
        }

        private void ShowModal(int choiceIndex)
        {
            if (_catalog == null)
            {
                _catalog = UiPrefabCatalogLoader.LoadOrNull();
            }

            if (_catalog == null || _catalog.MusicSelectionModalPrefab == null)
            {
                Debug.LogError("[UiMusicChoiceManager] MusicSelectionModalPrefab is not assigned.");
                return;
            }

            PauseGameplayOnly();

            GameObject modalObject = Object.Instantiate(_catalog.MusicSelectionModalPrefab);
            _modal = modalObject.GetComponent<MusicSelectionModalView>();
            if (_modal == null)
            {
                Debug.LogError("[UiMusicChoiceManager] MusicSelectionModal prefab missing MusicSelectionModalView.");
                Object.Destroy(modalObject);
                ResumeGameplayOnly();
                return;
            }

            _choiceIndexOpen = choiceIndex;
            _isOpen = true;
            _scenePollElapsed = 0f;

            _modal.SetOption(0, "Nitro Beat", "Speed Up", "Synergy: Sprint");
            _modal.SetOption(1, "Chill Cruise", "Stability", "Synergy: Safe");
            _modal.SetOption(2, "Risk Bass", "High Risk", "Synergy: Bonus");
            _modal.Show(OnSelected);
        }

        private void OnSelected(int optionIndex)
        {
            Events.Publish(new MusicChoiceSelected
            {
                ChoiceIndex = _choiceIndexOpen,
                OptionIndex = optionIndex
            });

            HideModal();
            ResumeGameplayOnly();
            _isOpen = false;
            _choiceIndexOpen = -1;
        }

        private void HideModalAndResume()
        {
            if (!_isOpen && _modal == null)
            {
                return;
            }

            HideModal();
            ResumeGameplayOnly();
            _isOpen = false;
            _choiceIndexOpen = -1;
        }

        private void HideModal()
        {
            if (_modal == null)
            {
                return;
            }

            _modal.Hide();
            Object.Destroy(_modal.gameObject);
            _modal = null;
        }

        private void PauseGameplayOnly()
        {
            RunSessionManager runSessionManager;
            if (Services.TryGet(out runSessionManager) && runSessionManager != null)
            {
                runSessionManager.EnterPauseForChoice();
                return;
            }

            CaptureFallbackTimeScale();
            Time.timeScale = 0f;
        }

        private void ResumeGameplayOnly()
        {
            RunSessionManager runSessionManager;
            if (Services.TryGet(out runSessionManager) && runSessionManager != null)
            {
                runSessionManager.ResumeFromChoice();
                RestoreFallbackTimeScale();
                return;
            }

            RestoreFallbackTimeScale();
        }

        private void CaptureFallbackTimeScale()
        {
            if (_fallbackPauseCaptured)
            {
                return;
            }

            _fallbackSavedTimeScale = Time.timeScale;
            _fallbackPauseCaptured = true;
        }

        private void RestoreFallbackTimeScale()
        {
            if (!_fallbackPauseCaptured)
            {
                return;
            }

            Time.timeScale = _fallbackSavedTimeScale;
            _fallbackSavedTimeScale = 0f;
            _fallbackPauseCaptured = false;
        }
    }
}
