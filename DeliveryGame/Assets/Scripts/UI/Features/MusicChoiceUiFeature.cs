using DeliveryRun;
using DeliveryRun.Managers.Core;
using DeliveryRun.Managers.Subs;
using DeliveryRun.Music;
using DeliveryRun.UI;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.SceneManagement;
using DomainRunChoiceConstants = DeliveryRun.Delivery.RunSession.RunChoiceConstants;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;


namespace DeliveryRun.UI.Features
{
    internal sealed partial class MusicChoiceUiFeature : UiFeatureBase
    {
        protected override void OnInitialize()
        {
            InitializeMusicChoiceModule();
        }
        protected override void OnTick(float unscaledDeltaTime)
        {
            TickMusicChoiceModule(unscaledDeltaTime);
        }
        protected override void OnShutdown()
        {
            ShutdownMusicChoiceModule();
        }

        private const int MusicChoiceOptionCount = DomainRunChoiceConstants.ChoiceCount;
        private const float MusicChoiceScenePollInterval = 0.25f;

        private readonly string[] _musicChoiceCurrentTrackIds = new string[MusicChoiceOptionCount];
        private readonly string[] _musicChoicePickedGenreByChoice = new string[MusicChoiceOptionCount];

        private MusicLibraryService _musicChoiceLibrary;
        private MusicChoiceOptionPresenter _musicChoiceOptionPresenter;

        private GameObject _musicChoiceModalInstance;
        private MusicSelectionModalView _musicChoiceModal;
        private bool _musicChoiceIsOpen;
        private bool _musicChoiceModalLoadRequested;
        private int _musicChoiceChoiceIndexOpen;
        private float _musicChoiceScenePollElapsed;
        private bool _musicChoiceModalOpenPublished;

        private bool _musicChoiceFallbackPauseCaptured;
        private float _musicChoiceFallbackSavedTimeScale;

        private void InitializeMusicChoiceModule()
        {
            Services.TryGet(out _musicChoiceLibrary);
            _musicChoiceOptionPresenter = new MusicChoiceOptionPresenter(_musicChoicePickedGenreByChoice);

            _musicChoiceChoiceIndexOpen = -1;
            ClearMusicChoiceDraftCache();
            ClearMusicChoicePickedGenres();

            Subs.Add<MusicDraftGenerated>(Events, OnMusicChoiceDraftGenerated);
            Subs.Add<MusicChoiceAutoResolved>(Events, OnMusicChoiceAutoResolved);
            Subs.Add<DomainRunSessionStarted>(Events, OnMusicChoiceRunSessionStarted);
            Subs.Add<SceneTransitionStarted>(Events, OnMusicChoiceSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnMusicChoiceSceneTransitionCompleted);
        }

        private void TickMusicChoiceModule(float unscaledDeltaTime)
        {
            if (!_musicChoiceIsOpen)
            {
                return;
            }

            if (!ScenePollUtil.ShouldPoll(ref _musicChoiceScenePollElapsed, MusicChoiceScenePollInterval, unscaledDeltaTime))
            {
                return;
            }

            if (SceneManager.GetActiveScene().name != SceneNames.RunScene)
            {
                HideMusicChoiceModalAndResume();
            }
        }

        private void ShutdownMusicChoiceModule()
        {
            HideMusicChoiceModalAndResume();
            PublishMusicChoiceModalState(false);
            RestoreMusicChoiceFallbackTimeScale();
            ClearMusicChoicePickedGenres();
        }

        private void OnMusicChoiceRunSessionStarted(DomainRunSessionStarted evt)
        {
            ClearMusicChoicePickedGenres();
            _musicChoiceChoiceIndexOpen = -1;
            ClearMusicChoiceDraftCache();
        }

        private void OnMusicChoiceDraftGenerated(MusicDraftGenerated evt)
        {
            if (_musicChoiceIsOpen)
            {
                Debug.LogWarning("[UiMusicChoiceManager] Received draft while modal is already open. Ignored.");
                return;
            }

            if (SceneManager.GetActiveScene().name != SceneNames.RunScene)
            {
                return;
            }

            _musicChoiceChoiceIndexOpen = evt.ChoiceIndex;
            _musicChoiceCurrentTrackIds[0] = evt.TrackId0;
            _musicChoiceCurrentTrackIds[1] = evt.TrackId1;
            _musicChoiceCurrentTrackIds[2] = evt.TrackId2;

            ShowMusicChoiceModal();
        }

        private void OnMusicChoiceAutoResolved(MusicChoiceAutoResolved evt)
        {
            if (!_musicChoiceIsOpen || evt.ChoiceIndex != _musicChoiceChoiceIndexOpen)
            {
                return;
            }

            if (_musicChoiceChoiceIndexOpen >= 0 && _musicChoiceChoiceIndexOpen < _musicChoicePickedGenreByChoice.Length)
            {
                _musicChoicePickedGenreByChoice[_musicChoiceChoiceIndexOpen] = evt.GenreId;
            }

            HideMusicChoiceModalAndResume();
        }

        private void OnMusicChoiceSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To != SceneNames.RunScene)
            {
                HideMusicChoiceModalAndResume();
            }
        }

        private void OnMusicChoiceSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            if (evt.SceneName != SceneNames.RunScene)
            {
                HideMusicChoiceModalAndResume();
            }
        }

    }
}
