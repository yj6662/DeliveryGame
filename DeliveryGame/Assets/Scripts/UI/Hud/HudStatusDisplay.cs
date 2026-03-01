using System;
using System.Text;
using DeliveryRun.Managers.Core;
using DeliveryRun.Music;
using DeliveryRun.UI.Run;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class HudStatusDisplay
    {
        private const string DefaultCashLabel = "RUN CASH: $0 (+$0)";
        private const string DefaultBuffLabel = "BUFF: -";
        private const string DefaultSynergyLabel = "SYNERGY: -";

        private readonly StringBuilder _builder = new StringBuilder(256);
        private string _buffLine = DefaultBuffLabel;
        private string _synergyLine = DefaultSynergyLabel;
        private string _refuelLine;

        internal void Reset()
        {
            _buffLine = DefaultBuffLabel;
            _synergyLine = DefaultSynergyLabel;
            _refuelLine = null;
        }

        internal void SetRefuelState(bool isRefueling, float fuelPerSecond, float costPerSecond)
        {
            if (isRefueling)
            {
                _refuelLine = "REFUEL: +" + fuelPerSecond.ToString("0") + "/s  -$" + costPerSecond.ToString("0") + "/s";
            }
            else
            {
                _refuelLine = null;
            }
        }

        internal void RebuildBuffLine(ModifierStackService modifierStack, float speedMultiplier)
        {
            if (modifierStack == null)
            {
                float speedPct = (speedMultiplier - 1f) * 100f;
                _buffLine = Mathf.Abs(speedPct) < 0.01f ? DefaultBuffLabel : "BUFF: SPD " + speedPct.ToString("+0;-0") + "%";
                return;
            }

            _builder.Clear();
            _builder.Append("BUFF:");
            int appended = 0;
            appended += AppendBuffMul(_builder, "SPD", modifierStack.GetMul(RunStatId.PlayerMoveSpeedMultiplier), appended);
            appended += AppendBuffMul(_builder, "GRIP", modifierStack.GetMul(RunStatId.BikeLateralGripMultiplier), appended);
            appended += AppendBuffMul(_builder, "BRAKE", modifierStack.GetMul(RunStatId.BikeBrakeForceMultiplier), appended);
            appended += AppendBuffMul(_builder, "REWARD", modifierStack.GetMul(RunStatId.RewardMultiplier), appended);
            appended += AppendBuffMul(_builder, "TEMP", modifierStack.GetMul(RunStatId.FoodTemperatureDecayMultiplier), appended);
            appended += AppendBuffMul(_builder, "SPILL", modifierStack.GetMul(RunStatId.FoodSpillGainMultiplier), appended);
            appended += AppendBuffMul(_builder, "TTL", modifierStack.GetMul(RunStatId.OfferAcceptTtlMultiplier), appended);
            appended += AppendBuffMul(_builder, "RESPAWN", modifierStack.GetMul(RunStatId.OfferRespawnDelayMultiplier), appended);

            _buffLine = appended <= 0 ? DefaultBuffLabel : _builder.ToString();
        }

        internal void RebuildSynergyLine(MusicLibraryService musicLibrary, string[] pickedGenreByChoice)
        {
            if (musicLibrary == null || pickedGenreByChoice == null || pickedGenreByChoice.Length <= 0)
            {
                _synergyLine = DefaultSynergyLabel;
                return;
            }

            _builder.Clear();
            int appended = 0;
            int choiceSlots = pickedGenreByChoice.Length;
            string[] unique = new string[choiceSlots];
            int[] counts = new int[choiceSlots];
            int uniqueCount = 0;

            for (int i = 0; i < choiceSlots; i++)
            {
                string genreId = pickedGenreByChoice[i];
                if (string.IsNullOrEmpty(genreId))
                {
                    continue;
                }

                int found = -1;
                for (int u = 0; u < uniqueCount; u++)
                {
                    if (string.Equals(unique[u], genreId, StringComparison.Ordinal))
                    {
                        found = u;
                        break;
                    }
                }

                if (found >= 0)
                {
                    counts[found]++;
                }
                else
                {
                    unique[uniqueCount] = genreId;
                    counts[uniqueCount] = 1;
                    uniqueCount++;
                }
            }

            for (int i = 0; i < uniqueCount; i++)
            {
                if (counts[i] < 2)
                {
                    continue;
                }

                MusicGenreSO genre;
                if (!musicLibrary.TryGetGenre(unique[i], out genre) || genre == null)
                {
                    continue;
                }

                MusicSynergySO synergy = musicLibrary.GetBestSynergyForGenre(genre, counts[i]);
                if (synergy == null)
                {
                    continue;
                }

                if (appended > 0)
                {
                    _builder.Append(" / ");
                }

                _builder.Append(string.IsNullOrEmpty(synergy.DisplayName) ? genre.DisplayName : synergy.DisplayName);
                appended++;
            }

            _synergyLine = appended > 0 ? "SYNERGY: " + _builder : DefaultSynergyLabel;
        }

        internal void ApplyStatus(RunHudView view)
        {
            if (view == null)
            {
                return;
            }

            _builder.Clear();
            _builder.Append(_buffLine);
            _builder.Append('\n');
            _builder.Append(_synergyLine);
            if (!string.IsNullOrEmpty(_refuelLine))
            {
                _builder.Append('\n');
                _builder.Append(_refuelLine);
            }

            view.SetNowPlaying(_builder.ToString());
        }

        internal void ApplyCash(RunHudView view, int sessionBalance, int sessionBonus)
        {
            if (view == null)
            {
                return;
            }

            if (sessionBalance == 0 && sessionBonus == 0)
            {
                view.SetCash(DefaultCashLabel);
                return;
            }

            string bonus = sessionBonus >= 0 ? "+$" + sessionBonus.ToString("N0") : "-$" + Mathf.Abs(sessionBonus).ToString("N0");
            view.SetCash("RUN CASH: $" + sessionBalance.ToString("N0") + " (" + bonus + ")");
        }

        private static int AppendBuffMul(StringBuilder builder, string label, float mul, int appendedCount)
        {
            float pct = (mul - 1f) * 100f;
            if (Mathf.Abs(pct) < 0.01f)
            {
                return 0;
            }

            if (appendedCount > 0)
            {
                builder.Append(" |");
            }

            builder.Append(' ')
                .Append(label)
                .Append(' ')
                .Append(pct.ToString("+0;-0"))
                .Append('%');
            return 1;
        }
    }
}
