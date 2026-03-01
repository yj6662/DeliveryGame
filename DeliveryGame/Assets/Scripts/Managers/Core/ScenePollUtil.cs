namespace DeliveryRun.Managers.Core
{
    internal static class ScenePollUtil
    {
        internal static bool ShouldPoll(ref float accum, float intervalSeconds, float unscaledDeltaTime)
        {
            if (intervalSeconds <= 0f)
            {
                return true;
            }

            float dt = unscaledDeltaTime;
            if (dt < 0f)
            {
                dt = 0f;
            }

            accum += dt;
            if (accum < intervalSeconds)
            {
                return false;
            }

            accum = 0f;
            return true;
        }
    }
}
