using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (CoreRoot.Instance == null)
            {
                GameObject coreRootObject = new GameObject("CoreRoot");
                coreRootObject.AddComponent<CoreRoot>();
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Object.FindFirstObjectByType<DebugOverlay>() == null)
            {
                GameObject overlayObject = new GameObject("DebugOverlay");
                overlayObject.AddComponent<DebugOverlay>();
                Object.DontDestroyOnLoad(overlayObject);
            }
#endif
        }
    }
}
