using System;
using System.Collections.Generic;
using DeliveryRun.Music;

namespace DeliveryRun.Managers.Core
{
    public sealed class MusicLibraryService
    {
        private static readonly MusicSynergySO[] EmptySynergyArray = Array.Empty<MusicSynergySO>();

        private readonly Dictionary<string, MusicTrackSO> _tracksById = new Dictionary<string, MusicTrackSO>(64);
        private readonly Dictionary<string, MusicGenreSO> _genresById = new Dictionary<string, MusicGenreSO>(16);
        private readonly Dictionary<string, List<MusicSynergySO>> _synergiesByGenreId =
            new Dictionary<string, List<MusicSynergySO>>(16);

        public MusicDatabaseSO Db { get; }

        public MusicLibraryService(MusicDatabaseSO db)
        {
            Db = db;
            IndexDatabase();
        }

        public bool TryGetTrack(string id, out MusicTrackSO track)
        {
            if (string.IsNullOrEmpty(id))
            {
                track = null;
                return false;
            }

            return _tracksById.TryGetValue(id, out track);
        }

        public bool TryGetGenre(string id, out MusicGenreSO genre)
        {
            if (string.IsNullOrEmpty(id))
            {
                genre = null;
                return false;
            }

            return _genresById.TryGetValue(id, out genre);
        }

        public MusicSynergySO GetBestSynergyForGenre(MusicGenreSO genre, int pickedCount)
        {
            if (genre == null || pickedCount <= 0)
            {
                return null;
            }

            List<MusicSynergySO> list;
            if (!_synergiesByGenreId.TryGetValue(genre.GenreId, out list) || list == null)
            {
                return null;
            }

            MusicSynergySO best = null;
            int bestRequiredCount = -1;

            for (int i = 0; i < list.Count; i++)
            {
                MusicSynergySO synergy = list[i];
                if (synergy == null)
                {
                    continue;
                }

                int required = synergy.RequiredCount;
                if (required <= 0 || required > pickedCount)
                {
                    continue;
                }

                if (required <= bestRequiredCount)
                {
                    continue;
                }

                bestRequiredCount = required;
                best = synergy;
            }

            return best;
        }

        public IEnumerable<MusicSynergySO> GetSynergiesForGenre(MusicGenreSO genre)
        {
            if (genre == null)
            {
                return EmptySynergyArray;
            }

            List<MusicSynergySO> list;
            if (_synergiesByGenreId.TryGetValue(genre.GenreId, out list) && list != null)
            {
                return list;
            }

            return EmptySynergyArray;
        }

        private void IndexDatabase()
        {
            _tracksById.Clear();
            _genresById.Clear();
            _synergiesByGenreId.Clear();

            if (Db == null)
            {
                return;
            }

            if (Db.Genres != null)
            {
                for (int i = 0; i < Db.Genres.Length; i++)
                {
                    MusicGenreSO genre = Db.Genres[i];
                    if (genre == null || string.IsNullOrEmpty(genre.GenreId))
                    {
                        continue;
                    }

                    _genresById[genre.GenreId] = genre;
                }
            }

            if (Db.Tracks != null)
            {
                for (int i = 0; i < Db.Tracks.Length; i++)
                {
                    MusicTrackSO track = Db.Tracks[i];
                    if (track == null || string.IsNullOrEmpty(track.TrackId))
                    {
                        continue;
                    }

                    _tracksById[track.TrackId] = track;
                }
            }

            if (Db.Synergies != null)
            {
                for (int i = 0; i < Db.Synergies.Length; i++)
                {
                    MusicSynergySO synergy = Db.Synergies[i];
                    if (synergy == null || synergy.Genre == null || string.IsNullOrEmpty(synergy.Genre.GenreId))
                    {
                        continue;
                    }

                    string genreId = synergy.Genre.GenreId;
                    List<MusicSynergySO> list;
                    if (!_synergiesByGenreId.TryGetValue(genreId, out list))
                    {
                        list = new List<MusicSynergySO>(2);
                        _synergiesByGenreId.Add(genreId, list);
                    }

                    list.Add(synergy);
                }
            }
        }
    }
}
