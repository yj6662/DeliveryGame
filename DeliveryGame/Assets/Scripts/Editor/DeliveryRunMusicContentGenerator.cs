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
            new GenreSeed("hiphop", "Hip Hop", "Street Sprint", "Fast and aggressive driving, higher spill risk.", "move_speed_mul", "speed"),
            new GenreSeed("ballad", "Ballad", "Warm Delivery", "Stable driving with stronger temperature retention.", "food_temp_decay_mul", "stability"),
            new GenreSeed("edm", "EDM", "Nitro Pulse", "Burst speed and quicker order tempo.", "move_speed_mul", "tempo"),
            new GenreSeed("jazz", "Jazz", "Smart Route", "Longer decision windows with balanced reward growth.", "offer_accept_ttl_mul", "route"),
            new GenreSeed("lofi", "Lo-Fi", "Soft Handling", "Lower spill growth and safer delivery handling.", "spill_gain_mul", "safe"),
            new GenreSeed("rock", "Rock", "Momentum Brake", "Powerful braking and control momentum.", "bike_brake_mul", "control"),
            new GenreSeed("classic", "Classic", "Precision Guard", "High control and calm handling at lower top pace.", "bike_grip_mul", "precision"),
            new GenreSeed("disco", "Funk / Disco", "Multi Drop Groove", "More frequent order opportunities and cash flow.", "offer_respawn_delay_mul", "multi")
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
            var expectedGenrePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var expectedTrackPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var expectedSynergyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < GenreSeeds.Length; i++)
            {
                GenreSeed seed = GenreSeeds[i];
                expectedGenrePaths.Add(GenreFolder + "/" + seed.GenreId + ".asset");
                expectedTrackPaths.Add(TrackFolder + "/" + seed.GenreId + "_c1.asset");
                expectedTrackPaths.Add(TrackFolder + "/" + seed.GenreId + "_c2.asset");
                expectedTrackPaths.Add(TrackFolder + "/" + seed.GenreId + "_c3.asset");
                expectedTrackPaths.Add(TrackFolder + "/" + seed.GenreId + "_r1.asset");
                expectedTrackPaths.Add(TrackFolder + "/" + seed.GenreId + "_r2.asset");
                expectedTrackPaths.Add(TrackFolder + "/" + seed.GenreId + "_e1.asset");
                expectedSynergyPaths.Add(SynergyFolder + "/" + seed.GenreId + "_duo.asset");
                expectedSynergyPaths.Add(SynergyFolder + "/" + seed.GenreId + "_trio.asset");

                MusicGenreSO genre = LoadOrCreateGenre(seed);
                genres.Add(genre);

                CreateOrUpdateTracksForGenre(genre, tracks);
                CreateOrUpdateSynergyForGenre(genre, synergies);
            }

            CleanupFolderExcept(GenreFolder, "t:MusicGenreSO", expectedGenrePaths);
            CleanupFolderExcept(TrackFolder, "t:MusicTrackSO", expectedTrackPaths);
            CleanupFolderExcept(SynergyFolder, "t:MusicSynergySO", expectedSynergyPaths);

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
            MusicModifierDef[] modifiers = BuildTrackModifiers(genre.GenreId, tier);
            track.DisplayName = BuildTrackName(genre, tier, tierIndex, modifiers);
            track.Tier = tier;
            track.Genre = genre;
            track.Description = BuildTrackDescription(genre, tier);
            track.Modifiers = modifiers;

            EditorUtility.SetDirty(track);
            outTracks.Add(track);
        }

        private static string BuildTrackName(MusicGenreSO genre, MusicTier tier, int tierIndex, MusicModifierDef[] modifiers)
        {
            string genreId = genre != null ? genre.GenreId : string.Empty;
            int slotIndex = GetTrackSlotIndex(tier, tierIndex);
            string curatedTitle = GetCuratedTrackTitle(genreId, slotIndex);
            if (!string.IsNullOrEmpty(curatedTitle))
            {
                return curatedTitle;
            }

            string buffTag = ResolvePrimaryBuffTag(modifiers);
            string tail = ResolveTrackTailWord(tier, tierIndex);
            return buffTag + " " + tail;
        }

        private static int GetTrackSlotIndex(MusicTier tier, int tierIndex)
        {
            if (tier == MusicTier.Common)
            {
                if (tierIndex <= 1) return 0;
                if (tierIndex == 2) return 1;
                return 2;
            }

            if (tier == MusicTier.Rare)
            {
                if (tierIndex <= 1) return 3;
                return 4;
            }

            return 5;
        }

        private static string GetCuratedTrackTitle(string genreId, int slotIndex)
        {
            if (string.IsNullOrEmpty(genreId))
            {
                return null;
            }

            if (genreId == "hiphop")
            {
                if (slotIndex == 0) return "Back Alley Kick";
                if (slotIndex == 1) return "Concrete Pulse";
                if (slotIndex == 2) return "Redline Cipher";
                if (slotIndex == 3) return "Corner Cut Anthem";
                if (slotIndex == 4) return "Nitro Sidewalk";
                return "City Heat Drop";
            }

            if (genreId == "ballad")
            {
                if (slotIndex == 0) return "Warm Window";
                if (slotIndex == 1) return "Steamlight";
                if (slotIndex == 2) return "Golden Box";
                if (slotIndex == 3) return "Hold the Flavor";
                if (slotIndex == 4) return "Late Night Thermos";
                return "Slow Burn Promise";
            }

            if (genreId == "edm")
            {
                if (slotIndex == 0) return "Neon Overpass";
                if (slotIndex == 1) return "Afterburn Sequence";
                if (slotIndex == 2) return "Signal Rush";
                if (slotIndex == 3) return "Midnight Slipstream";
                if (slotIndex == 4) return "Pulse Dispatch";
                return "Voltage Overdrive";
            }

            if (genreId == "jazz")
            {
                if (slotIndex == 0) return "Blue Route";
                if (slotIndex == 1) return "Downtown Swing";
                if (slotIndex == 2) return "Greenlight Waltz";
                if (slotIndex == 3) return "Bonus Boulevard";
                if (slotIndex == 4) return "Late Shift Solo";
                return "Crescent Detour";
            }

            if (genreId == "lofi")
            {
                if (slotIndex == 0) return "Rainy Delivery";
                if (slotIndex == 1) return "Soft Corner";
                if (slotIndex == 2) return "Cup Holder Dream";
                if (slotIndex == 3) return "Quiet Lane";
                if (slotIndex == 4) return "Steady Hands";
                return "No-Spill Reverie";
            }

            if (genreId == "rock")
            {
                if (slotIndex == 0) return "Brake Line Riot";
                if (slotIndex == 1) return "Iron Turn";
                if (slotIndex == 2) return "Gripbreaker";
                if (slotIndex == 3) return "Asphalt Thunder";
                if (slotIndex == 4) return "Clutch Echo";
                return "Terminal Roar";
            }

            if (genreId == "classic")
            {
                if (slotIndex == 0) return "Ivory Lane";
                if (slotIndex == 1) return "Measured Turn";
                if (slotIndex == 2) return "Silver Overture";
                if (slotIndex == 3) return "Patience Sonata";
                if (slotIndex == 4) return "Old Town Minuet";
                return "Royal Chicane";
            }

            if (genreId == "disco")
            {
                if (slotIndex == 0) return "Mirrorball Courier";
                if (slotIndex == 1) return "Saturday Route";
                if (slotIndex == 2) return "Golden Receipt";
                if (slotIndex == 3) return "Turbo Glitter";
                if (slotIndex == 4) return "Cashline Boogie";
                return "Last Drop Fever";
            }

            return null;
        }

        private static string ResolvePrimaryBuffTag(MusicModifierDef[] modifiers)
        {
            if (modifiers == null || modifiers.Length <= 0)
            {
                return "Flow";
            }

            int bestIndex = 0;
            float bestScore = -1f;
            for (int i = 0; i < modifiers.Length; i++)
            {
                MusicModifierDef mod = modifiers[i];
                float score = mod.Mode == MusicModifierMode.Mul
                    ? Mathf.Abs(mod.Value - 1f)
                    : Mathf.Abs(mod.Value);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            string key = modifiers[bestIndex].StatKey ?? string.Empty;
            if (key == "move_speed_mul") return "Rush";
            if (key == "bike_grip_mul") return "Grip";
            if (key == "bike_brake_mul") return "Brake";
            if (key == "reward_mul") return "Payout";
            if (key == "food_temp_decay_mul") return "Heatguard";
            if (key == "spill_gain_mul")
            {
                return modifiers[bestIndex].Value < 1f ? "Stability" : "Risk";
            }
            if (key == "offer_accept_ttl_mul") return "Window";
            if (key == "offer_respawn_delay_mul") return "Dispatch";
            if (key == "fuel_drain_mul") return "Efficiency";
            return "Flow";
        }

        private static string ResolveTrackTailWord(MusicTier tier, int tierIndex)
        {
            if (tier == MusicTier.Common)
            {
                if (tierIndex <= 1) return "Pulse";
                if (tierIndex == 2) return "Drive";
                return "Line";
            }

            if (tier == MusicTier.Rare)
            {
                if (tierIndex <= 1) return "Surge";
                return "Mode";
            }

            return "Overdrive";
        }

        private static string BuildTrackDescription(MusicGenreSO genre, MusicTier tier)
        {
            return genre.DisplayName + " vibe track. Tier: " + tier + ". Theme: " + genre.ThemeTitle;
        }

        private static MusicModifierDef[] BuildTrackModifiers(string genreId, MusicTier tier)
        {
            if (genreId == "hiphop")
            {
                return new[]
                {
                    Mul("move_speed_mul", TierValue(tier, 1.10f, 1.16f, 1.24f)),
                    Mul("spill_gain_mul", TierValue(tier, 1.08f, 1.14f, 1.22f))
                };
            }

            if (genreId == "ballad")
            {
                return new[]
                {
                    Mul("food_temp_decay_mul", TierValue(tier, 0.90f, 0.82f, 0.74f)),
                    Mul("spill_gain_mul", TierValue(tier, 0.92f, 0.86f, 0.80f)),
                    Mul("move_speed_mul", TierValue(tier, 0.98f, 0.96f, 0.94f))
                };
            }

            if (genreId == "edm")
            {
                return new[]
                {
                    Mul("move_speed_mul", TierValue(tier, 1.12f, 1.20f, 1.30f)),
                    Mul("offer_respawn_delay_mul", TierValue(tier, 0.90f, 0.82f, 0.74f)),
                    Mul("spill_gain_mul", TierValue(tier, 1.06f, 1.12f, 1.18f))
                };
            }

            if (genreId == "jazz")
            {
                return new[]
                {
                    Mul("offer_accept_ttl_mul", TierValue(tier, 1.10f, 1.18f, 1.28f)),
                    Mul("reward_mul", TierValue(tier, 1.05f, 1.10f, 1.16f)),
                    Mul("bike_brake_mul", TierValue(tier, 1.06f, 1.12f, 1.20f))
                };
            }

            if (genreId == "lofi")
            {
                return new[]
                {
                    Mul("spill_gain_mul", TierValue(tier, 0.88f, 0.80f, 0.72f)),
                    Mul("food_temp_decay_mul", TierValue(tier, 0.94f, 0.88f, 0.82f))
                };
            }

            if (genreId == "rock")
            {
                return new[]
                {
                    Mul("bike_brake_mul", TierValue(tier, 1.10f, 1.18f, 1.28f)),
                    Mul("bike_grip_mul", TierValue(tier, 1.06f, 1.12f, 1.18f))
                };
            }

            if (genreId == "classic")
            {
                return new[]
                {
                    Mul("bike_grip_mul", TierValue(tier, 1.10f, 1.18f, 1.28f)),
                    Mul("offer_accept_ttl_mul", TierValue(tier, 1.06f, 1.12f, 1.20f)),
                    Mul("move_speed_mul", TierValue(tier, 0.97f, 0.95f, 0.93f))
                };
            }

            if (genreId == "disco")
            {
                return new[]
                {
                    Mul("offer_respawn_delay_mul", TierValue(tier, 0.90f, 0.82f, 0.74f)),
                    Mul("reward_mul", TierValue(tier, 1.08f, 1.14f, 1.22f)),
                    Mul("move_speed_mul", TierValue(tier, 1.04f, 1.08f, 1.14f))
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
            bool duo = requiredCount == 2;
            string genreId = genre != null ? genre.GenreId : string.Empty;

            if (genreId == "hiphop")
            {
                return duo
                    ? new[] { Mul("move_speed_mul", 1.08f), Mul("spill_gain_mul", 1.05f) }
                    : new[] { Mul("move_speed_mul", 1.15f), Mul("spill_gain_mul", 1.12f) };
            }

            if (genreId == "ballad")
            {
                return duo
                    ? new[] { Mul("food_temp_decay_mul", 0.88f), Mul("spill_gain_mul", 0.90f) }
                    : new[] { Mul("food_temp_decay_mul", 0.78f), Mul("spill_gain_mul", 0.82f) };
            }

            if (genreId == "edm")
            {
                return duo
                    ? new[] { Mul("move_speed_mul", 1.10f), Mul("offer_respawn_delay_mul", 0.88f) }
                    : new[] { Mul("move_speed_mul", 1.20f), Mul("offer_respawn_delay_mul", 0.75f), Mul("spill_gain_mul", 1.08f) };
            }

            if (genreId == "jazz")
            {
                return duo
                    ? new[] { Mul("offer_accept_ttl_mul", 1.12f), Mul("reward_mul", 1.08f) }
                    : new[] { Mul("offer_accept_ttl_mul", 1.25f), Mul("reward_mul", 1.15f) };
            }

            if (genreId == "lofi")
            {
                return duo
                    ? new[] { Mul("spill_gain_mul", 0.82f) }
                    : new[] { Mul("spill_gain_mul", 0.70f), Mul("food_temp_decay_mul", 0.85f) };
            }

            if (genreId == "rock")
            {
                return duo
                    ? new[] { Mul("bike_brake_mul", 1.12f) }
                    : new[] { Mul("bike_brake_mul", 1.22f), Mul("bike_grip_mul", 1.12f) };
            }

            if (genreId == "classic")
            {
                return duo
                    ? new[] { Mul("bike_grip_mul", 1.12f), Mul("offer_accept_ttl_mul", 1.08f) }
                    : new[] { Mul("bike_grip_mul", 1.22f), Mul("offer_accept_ttl_mul", 1.16f), Mul("move_speed_mul", 0.95f) };
            }

            if (genreId == "disco")
            {
                return duo
                    ? new[] { Mul("reward_mul", 1.10f), Mul("offer_respawn_delay_mul", 0.85f) }
                    : new[] { Mul("reward_mul", 1.22f), Mul("offer_respawn_delay_mul", 0.72f) };
            }

            float value = duo ? 1.08f : 1.15f;
            return new[] { Mul(genre.PrimaryStatKey, value) };
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

        private static void CleanupFolderExcept(string folderPath, string searchFilter, HashSet<string> keepPaths)
        {
            string[] guids = AssetDatabase.FindAssets(searchFilter, new[] { folderPath });
            for (int i = 0; i < guids.Length; i++)
            {
                string guid = guids[i];
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (keepPaths.Contains(path))
                {
                    continue;
                }

                if (AssetDatabase.DeleteAsset(path))
                {
                    Debug.Log("[MusicGen] Deleted legacy asset: " + path);
                }
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
