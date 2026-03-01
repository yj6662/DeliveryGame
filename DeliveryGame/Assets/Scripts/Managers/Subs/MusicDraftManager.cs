using System;
using DeliveryRun.Managers.Core;
using DeliveryRun.Music;
using UnityEngine;
using DomainRunChoiceConstants = DeliveryRun.Delivery.RunSession.RunChoiceConstants;
using DomainRunChoicePointReached = DeliveryRun.Delivery.RunSession.RunChoicePointReached;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class MusicDraftManager : SubManagerBase
    {
        private const int ChoiceCount = DomainRunChoiceConstants.ChoiceCount;

        private MusicDraftRuntime _runtime;

        public override string Name => nameof(MusicDraftManager);
        public override int InitOrder => 34;

        protected override void OnInitialize()
        {
            MusicDatabaseSO database = Resources.Load<MusicDatabaseSO>("Bootstrap/MusicDatabase");
            if (database == null)
            {
                Debug.LogError("[MusicDraftManager] Missing MusicDatabase at Resources/Bootstrap/MusicDatabase.");
                return;
            }

            MusicTrackSO[] tracks = database.Tracks != null ? database.Tracks : Array.Empty<MusicTrackSO>();
            if (tracks.Length == 0)
            {
                Debug.LogError("[MusicDraftManager] MusicDatabase has no tracks.");
                return;
            }

            MusicLibraryService library = new MusicLibraryService(database);
            Services.Register(library);
            Services.TryGet(out ModifierStackService stack);

            _runtime = new MusicDraftRuntime(tracks, Services, stack);

            Subs.Add<DomainRunSessionStarted>(Events, OnRunStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunEnded);
            Subs.Add<DomainRunChoicePointReached>(Events, OnChoicePointReached);
            Subs.Add<MusicChoiceSelected>(Events, OnMusicChoiceSelected);
        }

        private void OnRunStarted(DomainRunSessionStarted evt)
        {
            _runtime?.OnRunStarted();
        }

        private void OnRunEnded(DomainRunSessionEnded evt)
        {
            _runtime?.OnRunEnded();
        }

        private void OnMusicChoiceSelected(MusicChoiceSelected evt)
        {
            if (evt.ChoiceIndex < 0 || evt.ChoiceIndex >= ChoiceCount || _runtime == null)
            {
                return;
            }

            _runtime.RecordSelectedTrack(evt.ChoiceIndex, evt.TrackId);
        }

        private void OnChoicePointReached(DomainRunChoicePointReached evt)
        {
            if (evt.Index < 0 || evt.Index >= ChoiceCount || _runtime == null)
            {
                return;
            }

            if (!_runtime.TryGenerateDraft(evt.Index, out string trackId0, out string trackId1, out string trackId2))
            {
                return;
            }

            Events.Publish(new MusicDraftGenerated
            {
                ChoiceIndex = evt.Index,
                TrackId0 = trackId0,
                TrackId1 = trackId1,
                TrackId2 = trackId2
            });
        }
    }
}
