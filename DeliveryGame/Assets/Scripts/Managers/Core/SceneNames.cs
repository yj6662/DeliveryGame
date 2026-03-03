namespace DeliveryRun
{
    public static class SceneNames
    {
        public const string CoreScene = "CoreScene";
        public const string StartScene = "StartScene";
        public const string LobbyScene = "LobbyScene";
        public const string RunScene = "RunScene";
        public const string LoadingScene = "LoadingScene";

        public static bool IsRunSceneLike(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            return sceneName == RunScene ||
                   sceneName.StartsWith(RunScene + "_", System.StringComparison.Ordinal);
        }

        // Legacy aliases kept to avoid noisy callsite churn.
        public const string Core = CoreScene;
        public const string Start = StartScene;
        public const string Lobby = LobbyScene;
        public const string Run = RunScene;
        public const string Loading = LoadingScene;
    }
}
