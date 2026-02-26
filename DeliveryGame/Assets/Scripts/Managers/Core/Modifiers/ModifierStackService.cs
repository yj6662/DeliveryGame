using System.Collections.Generic;
using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed class ModifierStackService
    {
        private readonly Dictionary<string, RunModifier> _modsBySource = new Dictionary<string, RunModifier>(8);
        private readonly Dictionary<RunStatId, float> _mulByStat = new Dictionary<RunStatId, float>(4);
        private readonly Dictionary<RunStatId, float> _addByStat = new Dictionary<RunStatId, float>(4);

        public void AddOrReplace(in RunModifier mod)
        {
            if (string.IsNullOrEmpty(mod.SourceId))
            {
                Debug.LogWarning("[ModifierStackService] Ignored modifier with empty SourceId.");
                return;
            }

            if (_modsBySource.ContainsKey(mod.SourceId))
            {
                _modsBySource.Remove(mod.SourceId);
            }

            _modsBySource.Add(mod.SourceId, mod);
            RebuildTotalsNonAlloc();
        }

        public bool RemoveSource(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId))
            {
                return false;
            }

            bool removed = _modsBySource.Remove(sourceId);
            if (!removed)
            {
                return false;
            }

            RebuildTotalsNonAlloc();
            return true;
        }

        public void ClearAll()
        {
            if (_modsBySource.Count == 0)
            {
                _mulByStat.Clear();
                _addByStat.Clear();
                return;
            }

            _modsBySource.Clear();
            _mulByStat.Clear();
            _addByStat.Clear();
        }

        public float GetMul(RunStatId stat)
        {
            float mul;
            if (_mulByStat.TryGetValue(stat, out mul))
            {
                return mul;
            }

            return 1f;
        }

        public float GetAdd(RunStatId stat)
        {
            float add;
            if (_addByStat.TryGetValue(stat, out add))
            {
                return add;
            }

            return 0f;
        }

        public float Evaluate(RunStatId stat, float baseValue)
        {
            float add = GetAdd(stat);
            float mul = GetMul(stat);
            return (baseValue + add) * mul;
        }

        private void RebuildTotalsNonAlloc()
        {
            _mulByStat.Clear();
            _addByStat.Clear();

            foreach (KeyValuePair<string, RunModifier> pair in _modsBySource)
            {
                RunModifier mod = pair.Value;
                if (mod.Mode == ModifierMode.Mul)
                {
                    float currentMul;
                    if (!_mulByStat.TryGetValue(mod.Stat, out currentMul))
                    {
                        currentMul = 1f;
                    }

                    _mulByStat[mod.Stat] = currentMul * mod.Value;
                    continue;
                }

                float currentAdd;
                if (!_addByStat.TryGetValue(mod.Stat, out currentAdd))
                {
                    currentAdd = 0f;
                }

                _addByStat[mod.Stat] = currentAdd + mod.Value;
            }
        }
    }
}
