using DeliveryRun;
using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    public sealed class SoundManager : SubManagerBase
    {
        private const string LobbyBgmKey = "audio/bgm/lobby_default";
        private const string RunBgmKey = "audio/bgm/run_default";
        private const string LoadingBgmKey = "audio/bgm/loading_default";
        private const string DeliverySuccessSfxKey = "audio/sfx/delivery_success";
        private const string DeliveryFailSfxKey = "audio/sfx/delivery_fail";
        private const string LastOrderSfxKey = "audio/sfx/last_order_start";
        private const string RatingDepletedSfxKey = "audio/sfx/rating_depleted";
        private const string MusicChoiceOpenSfxKey = "audio/sfx/music_choice_open";
        private const string MusicChoiceApplySfxKey = "audio/sfx/music_choice_apply";

        private AudioManager _audioManager;
        private string _currentBgmKey;

        public override string Name => nameof(SoundManager);
        public override int InitOrder => 25;

        public string CurrentBgmKey => _currentBgmKey;

        protected override void OnInitialize()
        {
            _audioManager = Services.GetRequired<AudioManager>();

            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<DeliveryOrderCompleted>(Events, OnDeliveryOrderCompleted);
            Subs.Add<DeliveryOrderFailed>(Events, OnDeliveryOrderFailed);
            Subs.Add<RunSessionLastOrderStarted>(Events, OnRunSessionLastOrderStarted);
            Subs.Add<RatingDepleted>(Events, OnRatingDepleted);
            Subs.Add<MusicChoiceRequested>(Events, OnMusicChoiceRequested);
            Subs.Add<MusicChoiceApplied>(Events, OnMusicChoiceApplied);
        }

        protected override void OnShutdown()
        {
            StopBgm();
            _audioManager = null;
            _currentBgmKey = null;
        }

        public void PlayBgm(string bgmKey)
        {
            if (string.IsNullOrEmpty(bgmKey))
            {
                Debug.LogError("[SoundManager] BGM key is null or empty.");
                return;
            }

            _currentBgmKey = bgmKey;
            if (_audioManager != null)
            {
                _audioManager.PlayBgm(bgmKey);
            }
        }

        public void StopBgm()
        {
            if (_audioManager != null)
            {
                _audioManager.StopBgm();
            }

            _currentBgmKey = null;
        }

        public void PlaySfx(string sfxKey)
        {
            if (string.IsNullOrEmpty(sfxKey))
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[SoundManager] PlaySfx (stub): " + sfxKey);
#endif
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.LoadingScene)
            {
                PlayBgm(LoadingBgmKey);
            }
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            if (evt.SceneName == SceneNames.LobbyScene)
            {
                PlayBgm(LobbyBgmKey);
                return;
            }

            if (evt.SceneName == SceneNames.RunScene)
            {
                PlayBgm(RunBgmKey);
            }
        }

        private void OnDeliveryOrderCompleted(DeliveryOrderCompleted evt)
        {
            PlaySfx(DeliverySuccessSfxKey);
        }

        private void OnDeliveryOrderFailed(DeliveryOrderFailed evt)
        {
            PlaySfx(DeliveryFailSfxKey);
        }

        private void OnRunSessionLastOrderStarted(RunSessionLastOrderStarted evt)
        {
            PlaySfx(LastOrderSfxKey);
        }

        private void OnRatingDepleted(RatingDepleted evt)
        {
            PlaySfx(RatingDepletedSfxKey);
        }

        private void OnMusicChoiceRequested(MusicChoiceRequested evt)
        {
            PlaySfx(MusicChoiceOpenSfxKey);
        }

        private void OnMusicChoiceApplied(MusicChoiceApplied evt)
        {
            PlaySfx(MusicChoiceApplySfxKey);
        }
    }
}
