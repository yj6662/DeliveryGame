using System;
using System.Collections.Generic;
using DeliveryRun.Music;
using UnityEditor;
using UnityEngine;

namespace DeliveryRun.Editor
{
    public static class DeliveryRunMusicContentGenerator
    {
        private const string GenreFolder = "Assets/Data/Music/Genres";
        private const string TrackFolder = "Assets/Data/Music/Tracks";
        private const string SynergyFolder = "Assets/Data/Music/Synergies";
        private const string DatabasePath = "Assets/Resources/Bootstrap/MusicDatabase.asset";

        private static readonly GenreSeed[] GenreSeeds =
        {
            new GenreSeed("edm", "EDM", "Nitro Surge", "High-energy acceleration tracks.", "move_speed_mul", "speed"),
            new GenreSeed("hiphop", "Hip Hop", "Street Bonus", "Reward-focused street rhythm.", "reward_mul", "reward"),
            new GenreSeed("rock", "Rock", "Brake Burst", "Hard stop control with power riffs.", "bike_brake_mul", "control"),
            new GenreSeed("pop", "Pop", "City Drive", "Balanced speed and bonus flow.", "move_speed_mul", "balanced"),
            new GenreSeed("lofi", "Lo-Fi", "Calm Grip", "Stable handling with smooth pace.", "bike_grip_mul", "stability"),
            new GenreSeed("metal", "Metal", "Risk Engine", "Extreme speed with handling risk.", "move_speed_mul", "risk"),
            new GenreSeed("jazz", "Jazz", "Precision Line", "Control-oriented grip and brake harmony.", "bike_grip_mul", "control"),
            new GenreSeed("classic", "Classic", "Safe Tempo", "Safer handling with reduced pace.", "bike_grip_mul", "stability")
        };

        [MenuItem("Tools/DeliveryRun/Generate Music Content")]
        public static void GenerateAll()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Music");
            EnsureFolder(GenreFolder);
            EnsureFolder(TrackFolder);
            EnsureFolder(SynergyFolder);
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Bootstrap");

            var genres = new List<MusicGenreSO>(8);
            var tracks = new List<MusicTrackSO>(48);
            var synergies = new List<MusicSynergySO>(16);

            for (int i = 0; i < GenreSeeds.Length; i++)
            {
                GenreSeed seed = GenreSeeds[i];
                MusicGenreSO genre = LoadOrCreateGenre(seed);
                genres.Add(genre);

                CreateOrUpdateTracksForGenre(genre, tracks);
                CreateOrUpdateSynergyForGenre(genre, synergies);
            }

            MusicDatabaseSO db = LoadOrCreateAsset<MusicDatabaseSO>(DatabasePath);
            db.Genres = genres.ToArray();
            db.Tracks = tracks.ToArray();
            db.Synergies = synergies.ToArray();
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[MusicGen] Genres: " + genres.Count + ", Tracks: " + tracks.Count + ", Synergies: " + synergies.Count);
            Debug.Log("[MusicGen] Database: " + DatabasePath);
        }

        private static MusicGenreSO LoadOrCreateGenre(GenreSeed seed)
        {
            string path = GenreFolder + "/" + seed.GenreId + ".asset";
            MusicGenreSO genre = LoadOrCreateAsset<MusicGenreSO>(path);
            genre.GenreId = seed.GenreId;
            genre.DisplayName = seed.DisplayName;
            genre.ThemeTitle = seed.ThemeTitle;
            genre.ThemeDescription = seed.ThemeDescription;
            genre.PrimaryStatKey = seed.PrimaryStatKey;
            genre.Tag = seed.Tag;
            EditorUtility.SetDirty(genre);
            return genre;
        }

        private static void CreateOrUpdateTracksForGenre(MusicGenreSO genre, List<MusicTrackSO> outTracks)
        {
            CreateOrUpdateTrack(genre, MusicTier.Common, 1, outTracks);
            CreateOrUpdateTrack(genre, MusicTier.Common, 2, outTracks);
            CreateOrUpdateTrack(genre, MusicTier.Common, 3, outTracks);
            CreateOrUpdateTrack(genre, MusicTier.Rare, 1, outTracks);
            CreateOrUpdateTrack(genre, MusicTier.Rare, 2, outTracks);
            CreateOrUpdateTrack(genre, MusicTier.Epic, 1, outTracks);
        }

        private static void CreateOrUpdateTrack(MusicGenreSO genre, MusicTier tier, int tierIndex, List<MusicTrackSO> outTracks)
        {
            string suffix;
            if (tier == MusicTier.Common)
            {
                suffix = "c" + tierIndex;
            }
            else if (tier == MusicTier.Rare)
            {
                suffix = "r" + tierIndex;
            }
            else
            {
                suffix = "e" + tierIndex;
            }

            string trackId = genre.GenreId + "_" + suffix;
            string path = TrackFolder + "/" + trackId + ".asset";
            MusicTrackSO track = LoadOrCreateAsset<MusicTrackSO>(path);

            track.TrackId = trackId;
            track.DisplayName = BuildTrackName(genre.DisplayName, tier, tierIndex);
            track.Tier = tier;
            track.Genre = genre;
            track.Description = BuildTrackDescription(genre, tier);
            track.Modifiers = BuildTrackModifiers(genre.GenreId, tier);

            EditorUtility.SetDirty(track);
            outTracks.Add(track);
        }

        private static string BuildTrackName(string genreDisplayName, MusicTier tier, int tierIndex)
        {
            string tierText = tier == MusicTier.Common ? "Common" : tier == MusicTier.Rare ? "Rare" : "Epic";
            return genreDisplayName + " " + tierText + " " + tierIndex;
        }

        private static string BuildTrackDescription(MusicGenreSO genre, MusicTier tier)
        {
            return genre.DisplayName + " vibe track. Tier: " + tier + ". Theme: " + genre.ThemeTitle;
        }

        private static MusicModifierDef[] BuildTrackModifiers(string genreId, MusicTier tier)
        {
            if (genreId == "edm")
            {
                return new[]
                {
                    Mul("move_speed_mul", TierValue(tier, 1.10f, 1.18f, 1.30f))
                };
            }

            if (genreId == "hiphop")
            {
                return new[]
                {
                    Mul("reward_mul", TierValue(tier, 1.12f, 1.22f, 1.40f))
                };
            }

            if (genreId == "rock")
            {
                return new[]
                {
                    Mul("bike_brake_mul", TierValue(tier, 1.10f, 1.18f, 1.30f))
                };
            }

            if (genreId == "pop")
            {
                return new[]
                {
                    Mul("move_speed_mul", TierValue(tier, 1.08f, 1.14f, 1.22f)),
                    Mul("reward_mul", TierValue(tier, 1.05f, 1.10f, 1.18f))
                };
            }

            if (genreId == "lofi")
            {
                return new[]
                {
                    Mul("bike_grip_mul", TierValue(tier, 1.10f, 1.18f, 1.30f))
                };
            }

            if (genreId == "metal")
            {
                return new[]
                {
                    Mul("move_speed_mul", TierValue(tier, 1.20f, 1.32f, 1.45f)),
                    Mul("bike_grip_mul", TierValue(tier, 0.92f, 0.88f, 0.84f))
                };
            }

            if (genreId == "jazz")
            {
                return new[]
                {
                    Mul("bike_grip_mul", TierValue(tier, 1.10f, 1.16f, 1.24f)),
                    Mul("bike_brake_mul", TierValue(tier, 1.08f, 1.12f, 1.18f))
                };
            }

            if (genreId == "classic")
            {
                return new[]
                {
                    Mul("bike_grip_mul", TierValue(tier, 1.12f, 1.20f, 1.30f)),
                    Mul("move_speed_mul", TierValue(tier, 0.98f, 0.96f, 0.94f))
                };
            }

            return new[]
            {
                Mul("move_speed_mul", 1.0f)
            };
        }

        private static void CreateOrUpdateSynergyForGenre(MusicGenreSO genre, List<MusicSynergySO> outSynergies)
        {
            CreateOrUpdateSynergy(genre, "duo", 2, outSynergies);
            CreateOrUpdateSynergy(genre, "trio", 3, outSynergies);
        }

        private static void CreateOrUpdateSynergy(MusicGenreSO genre, string stageId, int requiredCount, List<MusicSynergySO> outSynergies)
        {
            string synergyId = genre.GenreId + "_" + stageId;
            string path = SynergyFolder + "/" + synergyId + ".asset";

            MusicSynergySO synergy = LoadOrCreateAsset<MusicSynergySO>(path);
            synergy.SynergyId = synergyId;
            synergy.DisplayName = genre.DisplayName + " " + (requiredCount == 2 ? "Duo" : "Trio");
            synergy.Genre = genre;
            synergy.RequiredCount = requiredCount;
            synergy.Description = requiredCount == 2
                ? "Two picks of " + genre.DisplayName + " unlocked."
                : "Three picks of " + genre.DisplayName + " unlocked.";

            synergy.Modifiers = BuildSynergyModifiers(genre, requiredCount);
            EditorUtility.SetDirty(synergy);
            outSynergies.Add(synergy);
        }

        private static MusicModifierDef[] BuildSynergyModifiers(MusicGenreSO genre, int requiredCount)
        {
            if (genre.GenreId == "metal")
            {
                if (requiredCount == 2)
                {
                    return new[]
                    {
                        Mul("move_speed_mul", 1.10f),
                        Mul("bike_grip_mul", 0.95f)
                    };
                }

                return new[]
                {
                    Mul("move_speed_mul", 1.18f),
                    Mul("bike_grip_mul", 0.92f)
                };
            }

            float value = requiredCount == 2 ? 1.08f : 1.15f;
            return new[]
            {
                Mul(genre.PrimaryStatKey, value)
            };
        }

        private static float TierValue(MusicTier tier, float common, float rare, float epic)
        {
            if (tier == MusicTier.Common)
            {
                return common;
            }

            if (tier == MusicTier.Rare)
            {
                return rare;
            }

            return epic;
        }

        private static MusicModifierDef Mul(string statKey, float value)
        {
            return new MusicModifierDef
            {
                StatKey = statKey,
                Mode = MusicModifierMode.Mul,
                Value = value
            };
        }

        private static T LoadOrCreateAsset<T>(string assetPath) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private struct GenreSeed
        {
            public readonly string GenreId;
            public readonly string DisplayName;
            public readonly string ThemeTitle;
            public readonly string ThemeDescription;
            public readonly string PrimaryStatKey;
            public readonly string Tag;

            public GenreSeed(
                string genreId,
                string displayName,
                string themeTitle,
                string themeDescription,
                string primaryStatKey,
                string tag)
            {
                GenreId = genreId;
                DisplayName = displayName;
                ThemeTitle = themeTitle;
                ThemeDescription = themeDescription;
                PrimaryStatKey = primaryStatKey;
                Tag = tag;
            }
        }
    }
}
