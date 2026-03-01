using System.Collections.Generic;
using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class RunModifierRuntime
    {
        private void RemoveTrackSources(int choiceIndex)
        {
            if (choiceIndex < 0 || choiceIndex >= ChoiceCount)
            {
                return;
            }

            List<string> sourceIds = _trackSourceIdsByChoice[choiceIndex];
            for (int i = 0; i < sourceIds.Count; i++)
            {
                _stack.RemoveSource(sourceIds[i]);
            }

            sourceIds.Clear();
        }

        private void RemoveAllSynergySources()
        {
            for (int i = 0; i < _activeSynergySourceIds.Count; i++)
            {
                _stack.RemoveSource(_activeSynergySourceIds[i]);
            }

            _activeSynergySourceIds.Clear();
        }

        private void ClearAllModifiers(string sourceId, bool publishCleared)
        {
            for (int i = 0; i < ChoiceCount; i++)
            {
                RemoveTrackSources(i);
                _selectedTrackIds[i] = null;
                _selectedGenreIds[i] = null;
            }

            RemoveAllSynergySources();
            _stack.ClearAll();

            if (publishCleared)
            {
                _events.Publish(new RunModifiersCleared());
            }

            PublishStateChanged(sourceId);
        }

        private void ResetSelectionState()
        {
            for (int i = 0; i < ChoiceCount; i++)
            {
                _selectedTrackIds[i] = null;
                _selectedGenreIds[i] = null;
                _trackSourceIdsByChoice[i].Clear();
            }

            _activeSynergySourceIds.Clear();
        }

        private void PublishStateChanged(string sourceId)
        {
            PublishSpeedMulIfChanged(sourceId);
            _events.Publish(new RunModifiersChanged { SourceId = sourceId });
        }

        private void PublishSpeedMulIfChanged(string sourceId)
        {
            float current = _stack.GetMul(RunStatId.PlayerMoveSpeedMultiplier);
            if (Mathf.Abs(current - _lastPublishedSpeedMul) < Epsilon)
            {
                return;
            }

            _lastPublishedSpeedMul = current;
            _events.Publish(new PlayerMoveSpeedMultiplierChanged
            {
                Multiplier = current,
                SourceId = sourceId
            });
        }
    }
}
