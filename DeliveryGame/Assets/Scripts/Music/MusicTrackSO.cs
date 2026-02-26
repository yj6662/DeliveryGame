using UnityEngine;

namespace DeliveryRun.Music
{
    [CreateAssetMenu(fileName = "MusicTrack", menuName = "DeliveryRun/Music/Track", order = 110)]
    public sealed class MusicTrackSO : ScriptableObject
    {
        public string TrackId;
        public string DisplayName;
        public MusicTier Tier;
        public MusicGenreSO Genre;

        [TextArea]
        public string Description;

        public MusicModifierDef[] Modifiers;
    }
}
