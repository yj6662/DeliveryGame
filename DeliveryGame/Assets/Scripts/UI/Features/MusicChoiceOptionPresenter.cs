using DeliveryRun.Music;
using DeliveryRun.Managers.Core;

namespace DeliveryRun.UI.Features
{
    internal readonly struct MusicChoiceOptionViewData
    {
        internal MusicChoiceOptionViewData(string title, string sub, string detail, int tierCode, bool immediateSynergy)
        {
            Title = title;
            Sub = sub;
            Detail = detail;
            TierCode = tierCode;
            ImmediateSynergy = immediateSynergy;
        }

        internal string Title { get; }
        internal string Sub { get; }
        internal string Detail { get; }
        internal int TierCode { get; }
        internal bool ImmediateSynergy { get; }
    }

    internal sealed class MusicChoiceOptionPresenter
    {
        private readonly string[] _pickedGenreByChoice;

        internal MusicChoiceOptionPresenter(string[] pickedGenreByChoice)
        {
            _pickedGenreByChoice = pickedGenreByChoice;
        }

        internal MusicChoiceOptionViewData BuildOptionViewData(
            int optionIndex,
            int optionCount,
            string[] currentTrackIds,
            int openChoiceIndex,
            MusicLibraryService library)
        {
            string title = "Unknown Track";
            string sub = "Unknown Genre";
            string detail = "Theme: -";
            int tierCode = 0;
            bool immediateSynergy = false;

            if (optionIndex < 0 || optionIndex >= optionCount || currentTrackIds == null || library == null)
            {
                return new MusicChoiceOptionViewData(title, sub, detail, tierCode, immediateSynergy);
            }

            string trackId = currentTrackIds[optionIndex];
            if (string.IsNullOrEmpty(trackId))
            {
                return new MusicChoiceOptionViewData(title, sub, detail, tierCode, immediateSynergy);
            }

            MusicTrackSO track;
            if (!library.TryGetTrack(trackId, out track) || track == null)
            {
                return new MusicChoiceOptionViewData(title, sub, detail, tierCode, immediateSynergy);
            }

            title = NormalizeTrackCardTitle(string.IsNullOrEmpty(track.DisplayName) ? trackId : track.DisplayName);
            tierCode = ToTierCode(track.Tier);

            string genreName = "Unknown";
            if (track.Genre != null && !string.IsNullOrEmpty(track.Genre.DisplayName))
            {
                genreName = track.Genre.DisplayName;
            }

            sub = genreName + " | " + track.Tier.ToString().ToUpperInvariant();
            detail = BuildDetailText(track, library);
            immediateSynergy = WouldActivateSynergyNow(track.Genre, library, openChoiceIndex);

            return new MusicChoiceOptionViewData(title, sub, detail, tierCode, immediateSynergy);
        }

        internal string ResolveGenreIdForOption(
            int optionIndex,
            int optionCount,
            string[] currentTrackIds,
            MusicLibraryService library)
        {
            if (optionIndex < 0 || optionIndex >= optionCount || currentTrackIds == null || library == null)
            {
                return string.Empty;
            }

            string trackId = currentTrackIds[optionIndex];
            if (string.IsNullOrEmpty(trackId))
            {
                return string.Empty;
            }

            MusicTrackSO track;
            if (!library.TryGetTrack(trackId, out track) || track == null || track.Genre == null)
            {
                return string.Empty;
            }

            return string.IsNullOrEmpty(track.Genre.GenreId) ? string.Empty : track.Genre.GenreId;
        }

        private static int ToTierCode(MusicTier tier)
        {
            if (tier == MusicTier.Rare) return 1;
            if (tier == MusicTier.Epic) return 2;
            return 0;
        }

        private static string BuildDetailText(MusicTrackSO track, MusicLibraryService library)
        {
            if (track == null)
            {
                return "Theme: -";
            }

            string themeTitle = "Theme: -";
            string synergyLine = "Synergy: -";
            if (track.Genre != null)
            {
                string theme = string.IsNullOrEmpty(track.Genre.ThemeTitle) ? track.Genre.DisplayName : track.Genre.ThemeTitle;
                themeTitle = "Theme: " + theme;
                synergyLine = BuildSynergyLine(track.Genre, library);
            }

            string buffLine = BuildModifierSummary(track.Modifiers);
            return themeTitle + "\n" + synergyLine + "\n" + buffLine;
        }

        private static string BuildSynergyLine(MusicGenreSO genre, MusicLibraryService library)
        {
            if (genre == null || library == null)
            {
                return "Synergy: -";
            }

            string duoName = null;
            string trioName = null;
            foreach (MusicSynergySO synergy in library.GetSynergiesForGenre(genre))
            {
                if (synergy == null)
                {
                    continue;
                }

                if (synergy.RequiredCount == 2)
                {
                    duoName = string.IsNullOrEmpty(synergy.DisplayName) ? "Duo" : synergy.DisplayName;
                }
                else if (synergy.RequiredCount == 3)
                {
                    trioName = string.IsNullOrEmpty(synergy.DisplayName) ? "Trio" : synergy.DisplayName;
                }
            }

            if (string.IsNullOrEmpty(duoName) && string.IsNullOrEmpty(trioName))
            {
                return "Synergy: -";
            }

            if (!string.IsNullOrEmpty(duoName) && !string.IsNullOrEmpty(trioName))
            {
                return "Synergy: " + duoName + " / " + trioName;
            }

            return "Synergy: " + (!string.IsNullOrEmpty(duoName) ? duoName : trioName);
        }

        private bool WouldActivateSynergyNow(MusicGenreSO genre, MusicLibraryService library, int skipChoiceIndex)
        {
            if (genre == null || library == null || string.IsNullOrEmpty(genre.GenreId))
            {
                return false;
            }

            int existingCount = CountPickedGenre(genre.GenreId, skipChoiceIndex);
            MusicSynergySO before = library.GetBestSynergyForGenre(genre, existingCount);
            MusicSynergySO after = library.GetBestSynergyForGenre(genre, existingCount + 1);
            if (after == null)
            {
                return false;
            }

            if (before == null)
            {
                return true;
            }

            if (after.RequiredCount > before.RequiredCount)
            {
                return true;
            }

            return !string.Equals(after.SynergyId, before.SynergyId, System.StringComparison.Ordinal);
        }

        private int CountPickedGenre(string genreId, int skipChoiceIndex)
        {
            if (string.IsNullOrEmpty(genreId) || _pickedGenreByChoice == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < _pickedGenreByChoice.Length; i++)
            {
                if (i == skipChoiceIndex)
                {
                    continue;
                }

                if (string.Equals(_pickedGenreByChoice[i], genreId, System.StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static string NormalizeTrackCardTitle(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            string title = raw.Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (title.Length <= 0)
            {
                return string.Empty;
            }

            int end = title.Length - 1;
            while (end >= 0 && char.IsDigit(title[end]))
            {
                end--;
            }

            if (end < title.Length - 1)
            {
                while (end >= 0 && char.IsWhiteSpace(title[end]))
                {
                    end--;
                }

                if (end >= 0)
                {
                    title = title.Substring(0, end + 1).TrimEnd();
                }
            }

            return title;
        }

        private static string BuildModifierSummary(MusicModifierDef[] modifiers)
        {
            if (modifiers == null || modifiers.Length == 0)
            {
                return "BUFF: -";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder(96);
            builder.Append("BUFF: ");
            int appended = 0;
            for (int i = 0; i < modifiers.Length; i++)
            {
                MusicModifierDef modifier = modifiers[i];
                string label = ToShortStatLabel(modifier.StatKey);
                if (string.IsNullOrEmpty(label))
                {
                    continue;
                }

                if (appended > 0)
                {
                    builder.Append(", ");
                }

                if (modifier.Mode == MusicModifierMode.Mul)
                {
                    float pct = (modifier.Value - 1f) * 100f;
                    builder.Append(label).Append(' ').Append(pct.ToString("+0;-0")).Append('%');
                }
                else
                {
                    builder.Append(label).Append(' ').Append(modifier.Value.ToString("+0.##;-0.##"));
                }
                appended++;
            }

            if (appended <= 0)
            {
                return "BUFF: -";
            }

            return builder.ToString();
        }

        private static string ToShortStatLabel(string statKey)
        {
            if (string.IsNullOrEmpty(statKey))
            {
                return null;
            }

            if (string.Equals(statKey, "move_speed_mul", System.StringComparison.Ordinal)) return "SPD";
            if (string.Equals(statKey, "bike_grip_mul", System.StringComparison.Ordinal)) return "GRIP";
            if (string.Equals(statKey, "bike_brake_mul", System.StringComparison.Ordinal)) return "BRAKE";
            if (string.Equals(statKey, "reward_mul", System.StringComparison.Ordinal)) return "REWARD";
            if (string.Equals(statKey, "food_temp_decay_mul", System.StringComparison.Ordinal) ||
                string.Equals(statKey, "temp_decay_mul", System.StringComparison.Ordinal)) return "TEMP";
            if (string.Equals(statKey, "spill_gain_mul", System.StringComparison.Ordinal)) return "SPILL";
            if (string.Equals(statKey, "offer_accept_ttl_mul", System.StringComparison.Ordinal)) return "TTL";
            if (string.Equals(statKey, "offer_respawn_delay_mul", System.StringComparison.Ordinal) ||
                string.Equals(statKey, "offer_interval_mul", System.StringComparison.Ordinal)) return "RESPAWN";
            return statKey;
        }
    }
}
