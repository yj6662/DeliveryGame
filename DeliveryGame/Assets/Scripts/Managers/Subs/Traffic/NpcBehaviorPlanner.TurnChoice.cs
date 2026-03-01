using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class NpcBehaviorPlanner
    {
        internal int PickNextLane(TrafficLaneData currentLane, int currentLaneIndex, out TurnKind chosenTurn)
        {
            chosenTurn = TurnKind.Straight;

            int optionCount = _network.GetNextLaneCount(currentLaneIndex);
            if (optionCount <= 0)
            {
                return -1;
            }

            int straightLane = -1;
            int rightLane = -1;
            int leftLane = -1;
            int fallbackLane = -1;
            float straightBest = float.NegativeInfinity;
            float rightBest = float.NegativeInfinity;
            float leftBest = float.NegativeInfinity;
            float fallbackBest = float.NegativeInfinity;

            for (int option = 0; option < optionCount; option++)
            {
                int candidateIndex;
                if (!_network.TryGetNextLane(currentLaneIndex, option, out candidateIndex))
                {
                    continue;
                }

                TrafficLaneData candidate;
                if (!_network.TryGetLane(candidateIndex, out candidate))
                {
                    continue;
                }

                if (candidate.EndNodeId == currentLane.StartNodeId && optionCount > 1)
                {
                    continue;
                }

                TurnKind turnKind = ClassifyTurn(currentLane.Forward, candidate.Forward);
                float dot = Vector3.Dot(currentLane.Forward, candidate.Forward);
                float score = dot * 2.25f + ((_next01() - 0.5f) * 0.25f);

                if (turnKind == TurnKind.Straight)
                {
                    if (score > straightBest)
                    {
                        straightBest = score;
                        straightLane = candidateIndex;
                    }
                }
                else if (turnKind == TurnKind.Right)
                {
                    if (score > rightBest)
                    {
                        rightBest = score;
                        rightLane = candidateIndex;
                    }
                }
                else if (turnKind == TurnKind.Left)
                {
                    if (score > leftBest)
                    {
                        leftBest = score;
                        leftLane = candidateIndex;
                    }
                }
                else
                {
                    if (score > fallbackBest)
                    {
                        fallbackBest = score;
                        fallbackLane = candidateIndex;
                    }
                }
            }

            float roll = _next01();
            if (straightLane >= 0 && roll < TurnStraightWeight)
            {
                chosenTurn = TurnKind.Straight;
                return straightLane;
            }

            if (rightLane >= 0 && roll < (TurnStraightWeight + TurnRightWeight))
            {
                chosenTurn = TurnKind.Right;
                return rightLane;
            }

            if (leftLane >= 0 && roll < (TurnStraightWeight + TurnRightWeight + TurnLeftWeight))
            {
                chosenTurn = TurnKind.Left;
                return leftLane;
            }

            if (straightLane >= 0)
            {
                chosenTurn = TurnKind.Straight;
                return straightLane;
            }

            if (rightLane >= 0)
            {
                chosenTurn = TurnKind.Right;
                return rightLane;
            }

            if (leftLane >= 0)
            {
                chosenTurn = TurnKind.Left;
                return leftLane;
            }

            if (fallbackLane >= 0)
            {
                chosenTurn = TurnKind.UTurn;
                return fallbackLane;
            }

            int fallback;
            if (_network.TryGetNextLane(currentLaneIndex, 0, out fallback))
            {
                chosenTurn = TurnKind.Straight;
                return fallback;
            }

            return -1;
        }

        internal static TurnKind ClassifyTurn(Vector3 fromForward, Vector3 toForward)
        {
            float dot = Vector3.Dot(fromForward, toForward);
            if (dot >= 0.78f)
            {
                return TurnKind.Straight;
            }

            if (dot <= -0.22f)
            {
                return TurnKind.UTurn;
            }

            float crossY = Vector3.Cross(fromForward, toForward).y;
            if (crossY < 0f)
            {
                return TurnKind.Right;
            }

            return TurnKind.Left;
        }

        internal static float GetTurnSpeedFactor(TurnKind turnKind)
        {
            if (turnKind == TurnKind.Right)
            {
                return 0.74f;
            }

            if (turnKind == TurnKind.Left)
            {
                return 0.64f;
            }

            if (turnKind == TurnKind.UTurn)
            {
                return 0.52f;
            }

            return 1f;
        }
    }
}
