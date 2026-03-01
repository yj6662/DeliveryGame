using DeliveryRun.Managers.Core;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class NpcSpawner
    {
        internal void ValidateStatesAgainstNetwork()
        {
            if (_network == null)
            {
                return;
            }

            for (int i = 0; i < _states.Count; i++)
            {
                NpcRuntimeState state = _states[i];
                if (state == null)
                {
                    continue;
                }

                TrafficLaneData lane;
                if (_network.TryGetLane(state.LaneIndex, out lane))
                {
                    if (state.DistanceOnLane < lane.Length + 0.01f)
                    {
                        continue;
                    }
                }

                ReassignToRandomLane(state);
            }
        }

        internal void CleanupDestroyedStates()
        {
            for (int i = _states.Count - 1; i >= 0; i--)
            {
                NpcRuntimeState state = _states[i];
                if (state != null && state.Transform != null)
                {
                    continue;
                }

                _states.RemoveAt(i);
            }
        }
    }
}
