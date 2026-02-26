using UnityEngine;

namespace DeliveryRun.Music
{
    [CreateAssetMenu(fileName = "MusicDatabase", menuName = "DeliveryRun/Music/Database", order = 130)]
    public sealed class MusicDatabaseSO : ScriptableObject
    {
        public MusicGenreSO[] Genres;
        public MusicTrackSO[] Tracks;
        public MusicSynergySO[] Synergies;
    }
}
