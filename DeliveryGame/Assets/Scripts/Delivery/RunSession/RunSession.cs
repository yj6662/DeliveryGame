namespace DeliveryRun.Delivery.RunSession
{
    public sealed class RunSession
    {
        public const int Choice0 = 1 << 0;
        public const int Choice1 = 1 << 1;
        public const int Choice2 = 1 << 2;
        public const int LastOrder = 1 << 3;
        public const int TimeExpired = 1 << 4;

        private const int ExpectedChoiceCount = 3;

        private readonly float _durationSeconds;
        private readonly float[] _choiceTimesSeconds;
        private readonly float _lastOrderStartSeconds;

        private RunSessionState _state;
        private float _elapsedSeconds;
        private RunEndReason? _endReason;
        private int _firedFlags;

        public RunSessionState State => _state;
        public float DurationSeconds => _durationSeconds;
        public float ElapsedSeconds => _elapsedSeconds;

        public float RemainingSeconds
        {
            get
            {
                float remaining = _durationSeconds - _elapsedSeconds;
                return remaining > 0f ? remaining : 0f;
            }
        }

        public RunEndReason? EndReason => _endReason;

        public RunSession(float durationSeconds, float[] choiceTimesSeconds, float lastOrderStartSeconds)
        {
            _durationSeconds = durationSeconds > 0f ? durationSeconds : 0f;
            _lastOrderStartSeconds = lastOrderStartSeconds;
            _choiceTimesSeconds = new float[ExpectedChoiceCount];

            if (choiceTimesSeconds != null && choiceTimesSeconds.Length >= ExpectedChoiceCount)
            {
                _choiceTimesSeconds[0] = choiceTimesSeconds[0];
                _choiceTimesSeconds[1] = choiceTimesSeconds[1];
                _choiceTimesSeconds[2] = choiceTimesSeconds[2];
            }
            else
            {
                _choiceTimesSeconds[0] = 0f;
                _choiceTimesSeconds[1] = 180f;
                _choiceTimesSeconds[2] = 300f;
            }

            _state = RunSessionState.Ready;
            _elapsedSeconds = 0f;
            _endReason = null;
            _firedFlags = 0;
        }

        public bool Start()
        {
            if (_state != RunSessionState.Ready)
            {
                return false;
            }

            _elapsedSeconds = 0f;
            _endReason = null;
            _firedFlags = 0;
            _state = RunSessionState.Running;
            return true;
        }

        public bool EnterPauseForChoice()
        {
            if (_state != RunSessionState.Running)
            {
                return false;
            }

            _state = RunSessionState.PauseForChoice;
            return true;
        }

        public bool ResumeFromChoice()
        {
            if (_state != RunSessionState.PauseForChoice)
            {
                return false;
            }

            _state = RunSessionState.Running;
            return true;
        }

        public bool End(RunEndReason reason)
        {
            if (_state == RunSessionState.Ended)
            {
                return false;
            }

            _state = RunSessionState.Ended;
            _endReason = reason;
            return true;
        }

        public int Tick(float unscaledDt)
        {
            if (_state != RunSessionState.Running && _state != RunSessionState.PauseForChoice)
            {
                return 0;
            }

            float dt = unscaledDt;
            if (dt < 0f)
            {
                dt = 0f;
            }

            _elapsedSeconds += dt;

            int fired = 0;
            fired |= TryFireChoiceFlag(0, Choice0);
            fired |= TryFireChoiceFlag(1, Choice1);
            fired |= TryFireChoiceFlag(2, Choice2);
            fired |= TryFireLastOrderFlag();

            if (_elapsedSeconds >= _durationSeconds)
            {
                if (End(RunEndReason.TimeExpired))
                {
                    _firedFlags |= TimeExpired;
                    fired |= TimeExpired;
                }
            }

            return fired;
        }

        private int TryFireChoiceFlag(int choiceIndex, int flag)
        {
            if ((_firedFlags & flag) != 0)
            {
                return 0;
            }

            if (_elapsedSeconds < _choiceTimesSeconds[choiceIndex])
            {
                return 0;
            }

            _firedFlags |= flag;
            return flag;
        }

        private int TryFireLastOrderFlag()
        {
            if ((_firedFlags & LastOrder) != 0)
            {
                return 0;
            }

            if (_elapsedSeconds < _lastOrderStartSeconds)
            {
                return 0;
            }

            _firedFlags |= LastOrder;
            return LastOrder;
        }
    }
}
