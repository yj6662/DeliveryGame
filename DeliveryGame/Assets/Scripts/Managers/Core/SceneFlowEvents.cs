namespace DeliveryRun.Managers.Core
{
    public struct BootToStartRequested
    {
    }

    public struct BootToLobbyRequested
    {
    }

    public struct StartRunRequested
    {
    }

    public struct ReturnToLobbyRequested
    {
    }

    public struct SceneTransitionStarted
    {
        public string From;
        public string To;
    }

    public struct SceneTransitionProgress
    {
        public float Progress;
        public string Phase;
    }

    public struct SceneTransitionCompleted
    {
        public string SceneName;
    }
}
