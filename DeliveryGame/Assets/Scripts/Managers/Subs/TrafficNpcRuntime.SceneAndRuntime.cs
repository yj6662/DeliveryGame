using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class TrafficNpcRuntime
    {
        private void HandleSceneChanged(string sceneName, bool force)
        {
            bool isRun = SceneNames.IsRunSceneLike(sceneName);
            if (!isRun)
            {
                _isRunScene = false;
                if (force)
                {
                    ClearRuntime();
                }

                return;
            }

            bool enteredNow = !_isRunScene;
            _isRunScene = true;
            EnsureRuntimeRoot();

            if (!enteredNow && !force)
            {
                return;
            }

            _spawnAccum = 0f;
            _playerRegionPollAccum = 0f;
            _currentPlayerRegionId = string.Empty;
            _playerTransform = null;
            _missingPlayerLogged = false;

            EnsureRuntimeControllers();
            RebuildSpawnCandidates();
            _spawner?.ValidateStatesAgainstNetwork();
        }

        private void EnsureRuntimeRoot()
        {
            if (_runtimeRoot != null)
            {
                return;
            }

            GameObject root = GameObject.Find("TrafficNpcRuntimeRoot");
            if (root == null)
            {
                root = new GameObject("TrafficNpcRuntimeRoot");
            }

            _runtimeRoot = root.transform;
        }

        private Transform GetRuntimeRoot()
        {
            EnsureRuntimeRoot();
            return _runtimeRoot;
        }

        private void EnsureRuntimeControllers()
        {
            if (_network == null)
            {
                return;
            }

            bool networkChanged = !ReferenceEquals(_boundNetwork, _network);
            if (networkChanged || _spawnCandidates == null || _spawner == null)
            {
                _spawnCandidates = new SpawnCandidateService(_network);
                _spawner = new NpcSpawner(
                    _network,
                    _states,
                    _spawnCandidates,
                    NextInt,
                    () => _currentPlayerRegionId,
                    GetRuntimeRoot,
                    () => _catalog);

                _boundNetwork = _network;
                _boundSignals = null;
                _planner = null;
                _movement = null;
            }

            bool signalChanged = !ReferenceEquals(_boundSignals, _signals);
            if (signalChanged || _planner == null || _movement == null)
            {
                _planner = new NpcBehaviorPlanner(_network, _signals, _states, Next01);
                _movement = new NpcMovementController(_network, _signals, _states, _planner, _spawner);
                _boundSignals = _signals;
            }
        }

        private void RebuildSpawnCandidates()
        {
            _spawnCandidates?.RebuildSpawnCandidates(_currentPlayerRegionId);
        }

        private void ClearRuntime()
        {
            for (int i = 0; i < _states.Count; i++)
            {
                NpcRuntimeState state = _states[i];
                if (state != null && state.Transform != null)
                {
                    Object.Destroy(state.Transform.gameObject);
                }
            }

            _states.Clear();
            _spawnAccum = 0f;

            if (_runtimeRoot != null)
            {
                Object.Destroy(_runtimeRoot.gameObject);
                _runtimeRoot = null;
            }

            _spawnCandidates?.Clear();
            _spawnCandidates = null;
            _spawner = null;
            _planner = null;
            _movement = null;
            _boundNetwork = null;
            _boundSignals = null;

            _playerRegionPollAccum = 0f;
            _currentPlayerRegionId = string.Empty;
            _playerTransform = null;
            _missingPlayerLogged = false;
        }

        private float Next01()
        {
            _rngState = (_rngState * 1664525u) + 1013904223u;
            return (_rngState & 0x00FFFFFFu) / 16777215f;
        }

        private int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 1)
            {
                return 0;
            }

            int value = Mathf.FloorToInt(Next01() * maxExclusive);
            if (value >= maxExclusive)
            {
                value = maxExclusive - 1;
            }

            return value;
        }
    }
}
