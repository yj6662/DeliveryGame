using System;
using DeliveryRun.Managers.Core;
using DeliveryRun.Music;
using DomainRunChoiceConstants = DeliveryRun.Delivery.RunSession.RunChoiceConstants;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class MusicDraftRuntime
    {
        private const int ChoiceCount = DomainRunChoiceConstants.ChoiceCount;
        private const int CommonWeight = 70;
        private const int RareWeight = 25;
        private const int EpicWeight = 5;

        private readonly ServiceRegistry _services;
        private readonly MusicTrackSO[] _tracks;
        private readonly string[] _pickedTrackIds = new string[ChoiceCount];
        private readonly string[] _draftTrackIds = new string[ChoiceCount];
        private readonly string[] _draftGenreIds = new string[ChoiceCount];

        private ModifierStackService _stack;
        private System.Random _rng;

        private int[] _commonTrackIndices;
        private int[] _rareTrackIndices;
        private int[] _epicTrackIndices;
        private int _commonTrackCount;
        private int _rareTrackCount;
        private int _epicTrackCount;

        private bool _runActive;

        internal MusicDraftRuntime(MusicTrackSO[] tracks, ServiceRegistry services, ModifierStackService stack)
        {
            _tracks = tracks ?? Array.Empty<MusicTrackSO>();
            _services = services;
            _stack = stack;
            BuildTierIndices();
            _rng = new System.Random(unchecked((int)DateTime.UtcNow.Ticks));
            _runActive = false;
            ClearPickedTracks();
            ClearDraftTracks();
        }

        internal void OnRunStarted()
        {
            _runActive = true;
            _rng = new System.Random(unchecked((int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF)));
            ClearPickedTracks();
            ClearDraftTracks();
        }

        internal void OnRunEnded()
        {
            _runActive = false;
            ClearPickedTracks();
            ClearDraftTracks();
        }

        internal void RecordSelectedTrack(int choiceIndex, string trackId)
        {
            if (choiceIndex < 0 || choiceIndex >= ChoiceCount)
            {
                return;
            }

            _pickedTrackIds[choiceIndex] = trackId;
        }

        internal bool TryGenerateDraft(int choiceIndex, out string trackId0, out string trackId1, out string trackId2)
        {
            trackId0 = null;
            trackId1 = null;
            trackId2 = null;

            if (!_runActive || choiceIndex < 0 || choiceIndex >= ChoiceCount)
            {
                return false;
            }

            GenerateDraft();
            trackId0 = _draftTrackIds[0];
            trackId1 = _draftTrackIds[1];
            trackId2 = _draftTrackIds[2];
            return true;
        }

        private void ClearPickedTracks()
        {
            for (int i = 0; i < _pickedTrackIds.Length; i++)
            {
                _pickedTrackIds[i] = null;
            }
        }

        private void ClearDraftTracks()
        {
            for (int i = 0; i < ChoiceCount; i++)
            {
                _draftTrackIds[i] = null;
                _draftGenreIds[i] = null;
            }
        }
    }
}
