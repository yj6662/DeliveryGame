using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    public sealed class AudioManager : SubManagerBase
    {
        private string _currentBgmKey;

        public override string Name => nameof(AudioManager);
        public override int InitOrder => 20;

        protected override void OnInitialize()
        {
            _currentBgmKey = null;
        }

        protected override void OnShutdown()
        {
            StopBgm();
        }

        public void PlayBgm(string bgmKey)
        {
            if (string.IsNullOrEmpty(bgmKey))
            {
                Debug.LogError("[AudioManager] BGM key is null or empty.");
                return;
            }

            _currentBgmKey = bgmKey;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[AudioManager] PlayBgm (stub): " + bgmKey);
#endif
        }

        public void StopBgm()
        {
            if (string.IsNullOrEmpty(_currentBgmKey))
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[AudioManager] StopBgm (stub): " + _currentBgmKey);
#endif

            _currentBgmKey = null;
        }

        public string GetCurrentBgmKey()
        {
            return _currentBgmKey;
        }
    }
}
