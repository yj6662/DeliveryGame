using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryRun.Managers.Subs
{
    public sealed class SceneRouter : SubManagerBase
    {
        public override string Name => nameof(SceneRouter);
        public override int InitOrder => 10;

        public AsyncOperation LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[SceneRouter] Scene name is null or empty.");
                return null;
            }

            return SceneManager.LoadSceneAsync(sceneName, mode);
        }
    }
}
