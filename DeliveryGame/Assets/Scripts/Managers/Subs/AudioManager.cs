using DeliveryRun.Managers.Core;
using DeliveryRun.UI;
using UnityEngine;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class AudioManager : SubManagerBase
    {
        private static bool DisableBgmPlaybackTemporarily = true;

        private GameObject _audioHost;
        private AudioSource _bgmSource;
        private AudioSource _uiSource;
        private AddressablesService _addressables;
        private UiPrefabCatalogSO _catalog;
        private string _currentBgmKey;
        private bool _bgmMutedLogPrinted;
        private float _bgmVolume = 0.65f;
        private float _uiVolume = 0.9f;

        public override string Name => nameof(AudioManager);
        public override int InitOrder => 20;

        protected override void OnInitialize()
        {
            _catalog = UiPrefabCatalogLoader.LoadOrNull();
            Services.TryGet(out _addressables);

            _audioHost = new GameObject("AudioHost");
            Object.DontDestroyOnLoad(_audioHost);

            _bgmSource = _audioHost.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.volume = _bgmVolume;

            _uiSource = _audioHost.AddComponent<AudioSource>();
            _uiSource.loop = false;
            _uiSource.playOnAwake = false;
            _uiSource.volume = _uiVolume;

            _currentBgmKey = null;
            _bgmMutedLogPrinted = false;

            Subs.Add<DomainRunSessionStarted>(Events, OnRunSessionStarted);
        }

        protected override void OnShutdown()
        {
            StopBgm();

            if (_addressables != null)
            {
                _addressables.ReleaseAllCached();
            }

            if (_audioHost != null)
            {
                Object.Destroy(_audioHost);
            }

            _audioHost = null;
            _bgmSource = null;
            _uiSource = null;
            _addressables = null;
            _catalog = null;
            _currentBgmKey = null;
            _bgmMutedLogPrinted = false;
        }

        public float GetBgmVolume01()
        {
            return _bgmVolume;
        }

        public float GetUiVolume01()
        {
            return _uiVolume;
        }

        public void SetBgmVolume01(float value)
        {
            _bgmVolume = Mathf.Clamp01(value);
            if (_bgmSource != null)
            {
                _bgmSource.volume = _bgmVolume;
            }
        }

        public void SetUiVolume01(float value)
        {
            _uiVolume = Mathf.Clamp01(value);
            if (_uiSource != null)
            {
                _uiSource.volume = _uiVolume;
            }
        }

        public void PlayBgm(string bgmKey)
        {
            PlayTestBgm(bgmKey);
        }

        public void PlayTestBgm(string key)
        {
            if (DisableBgmPlaybackTemporarily)
            {
                if (!_bgmMutedLogPrinted)
                {
                    _bgmMutedLogPrinted = true;
                    Debug.Log("[AudioManager] BGM playback is temporarily muted.");
                }

                StopBgm();
                return;
            }

            string resolvedKey = key;
            string testBgmKey = _catalog != null ? _catalog.TestBgmKey : null;
            if (!string.IsNullOrEmpty(testBgmKey))
            {
                if (string.IsNullOrEmpty(resolvedKey) ||
                    resolvedKey == "audio/bgm/run_default" ||
                    resolvedKey == "audio/bgm/lobby_default" ||
                    resolvedKey == "audio/bgm/loading_default")
                {
                    resolvedKey = testBgmKey;
                }
            }

            if (string.IsNullOrEmpty(resolvedKey))
            {
                resolvedKey = testBgmKey;
            }

            if (string.IsNullOrEmpty(resolvedKey))
            {
                Debug.LogError("[AudioManager] Test BGM key is null or empty.");
                return;
            }

            _currentBgmKey = resolvedKey;
            if (_addressables == null || !_addressables.IsAvailable)
            {
                Debug.LogError("[AudioManager] Addressables unavailable. Cannot play BGM key=" + resolvedKey);
                return;
            }

            _addressables.LoadAudioClip(resolvedKey, clip =>
            {
                if (clip == null || _bgmSource == null)
                {
                    return;
                }

                if (!string.Equals(_currentBgmKey, resolvedKey, System.StringComparison.Ordinal))
                {
                    return;
                }

                _bgmSource.clip = clip;
                _bgmSource.Play();
            });
        }

        public void StopBgm()
        {
            if (_bgmSource != null && _bgmSource.isPlaying)
            {
                _bgmSource.Stop();
            }

            if (_bgmSource != null)
            {
                _bgmSource.clip = null;
            }

            _currentBgmKey = null;
        }

        public void PlayUiClick(string key)
        {
            string resolvedKey = key;
            if (string.IsNullOrEmpty(resolvedKey))
            {
                resolvedKey = _catalog != null ? _catalog.UiClickKey : null;
            }

            if (string.IsNullOrEmpty(resolvedKey))
            {
                Debug.LogError("[AudioManager] UI click key is null or empty.");
                return;
            }

            if (_addressables == null || !_addressables.IsAvailable)
            {
                Debug.LogError("[AudioManager] Addressables unavailable. Cannot play UI click key=" + resolvedKey);
                return;
            }

            _addressables.LoadAudioClip(resolvedKey, clip =>
            {
                if (clip == null || _uiSource == null)
                {
                    return;
                }

                _uiSource.PlayOneShot(clip);
            });
        }

        public string GetCurrentBgmKey()
        {
            return _currentBgmKey;
        }

        private void OnRunSessionStarted(DomainRunSessionStarted evt)
        {
            if (DisableBgmPlaybackTemporarily)
            {
                return;
            }

            string bgmKey = _catalog != null ? _catalog.TestBgmKey : null;
            PlayTestBgm(bgmKey);
        }
    }
}
