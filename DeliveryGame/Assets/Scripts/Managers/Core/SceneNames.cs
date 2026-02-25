namespace DeliveryRun
{
    public static class SceneNames
    {
        public const string CoreScene = "CoreScene";
        public const string LobbyScene = "LobbyScene";
        public const string RunScene = "RunScene";
        public const string LoadingScene = "LoadingScene";

        // Legacy aliases kept to avoid noisy callsite churn.
        public const string Core = CoreScene;
        public const string Lobby = LobbyScene;
        public const string Run = RunScene;
        public const string Loading = LoadingScene;
    }
}
