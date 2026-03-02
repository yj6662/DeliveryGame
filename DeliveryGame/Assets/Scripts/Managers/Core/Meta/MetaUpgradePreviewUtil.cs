using System;
using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public static class MetaUpgradePreviewUtil
    {
        // Matches current MotorbikeController serialized defaults.
        public const float BaseBikeMaxSpeed = 11f;
        public const float BaseBikeTurnSpeed = 130f;
        public const float BaseBikeAcceleration = 10f;
        public const float BaseBikeBrakeForce = 16f;
        public const float BaseBikeDownforce = 5f;

        public static float GetLevelMultiplier(string upgradeId, int level)
        {
            if (string.IsNullOrEmpty(upgradeId))
            {
                return 1f;
            }

            int index = -1;
            string[] ids = MetaProgressionConstants.UpgradeIds;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == upgradeId)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0 || index >= MetaProgressionConstants.UpgradePerLevelMulDelta.Length)
            {
                return 1f;
            }

            int safeLevel = level < 0 ? 0 : level;
            return 1f + (MetaProgressionConstants.UpgradePerLevelMulDelta[index] * safeLevel);
        }

        public static void GetDraftTierWeights(float highTierMul, out int common, out int rare, out int epic)
        {
            int rareWeight = Mathf.Clamp(Mathf.RoundToInt(25f * highTierMul), 4, 70);
            int epicWeight = Mathf.Clamp(Mathf.RoundToInt(5f * highTierMul), 1, 25);
            int commonWeight = 100 - rareWeight - epicWeight;
            if (commonWeight < 5)
            {
                commonWeight = 5;
                int highTotal = 100 - commonWeight;
                int totalHighBase = rareWeight + epicWeight;
                if (totalHighBase > 0)
                {
                    rareWeight = Mathf.RoundToInt((rareWeight / (float)totalHighBase) * highTotal);
                    epicWeight = highTotal - rareWeight;
                }
            }

            common = commonWeight;
            rare = rareWeight;
            epic = epicWeight;
        }

        public static string BuildUpgradeValueDeltaText(
            string upgradeId,
            int currentLevel,
            int nextLevel,
            Func<string, int> getCurrentLevelByUpgradeId)
        {
            if (string.IsNullOrEmpty(upgradeId))
            {
                return "No preview data";
            }

            Func<string, int> getLevel = getCurrentLevelByUpgradeId ?? (_ => 0);
            float currentUpgradeMul = GetLevelMultiplier(upgradeId, currentLevel);
            float nextUpgradeMul = GetLevelMultiplier(upgradeId, nextLevel);

            if (upgradeId == "bike_speed")
            {
                float accelMul = GetLevelMultiplier("bike_accel", getLevel("bike_accel"));
                float currentSpeed = BaseBikeMaxSpeed * currentUpgradeMul;
                float nextSpeed = BaseBikeMaxSpeed * nextUpgradeMul;
                float currentAccel = BaseBikeAcceleration * currentUpgradeMul * accelMul;
                float nextAccel = BaseBikeAcceleration * nextUpgradeMul * accelMul;
                return "Max Speed: " + currentSpeed.ToString("0.00") + " -> " + nextSpeed.ToString("0.00") + " m/s\n" +
                       "Accel Force: " + currentAccel.ToString("0.00") + " -> " + nextAccel.ToString("0.00");
            }

            if (upgradeId == "bike_turn")
            {
                float gripMul = GetLevelMultiplier("bike_grip", getLevel("bike_grip"));
                float currentTurn = BaseBikeTurnSpeed * gripMul * currentUpgradeMul;
                float nextTurn = BaseBikeTurnSpeed * gripMul * nextUpgradeMul;
                return "Turn Rate: " + currentTurn.ToString("0.0") + " -> " + nextTurn.ToString("0.0") + " deg/s";
            }

            if (upgradeId == "bike_accel")
            {
                float speedMul = GetLevelMultiplier("bike_speed", getLevel("bike_speed"));
                float currentAccel = BaseBikeAcceleration * speedMul * currentUpgradeMul;
                float nextAccel = BaseBikeAcceleration * speedMul * nextUpgradeMul;
                return "Accel Force: " + currentAccel.ToString("0.00") + " -> " + nextAccel.ToString("0.00");
            }

            if (upgradeId == "music_luck")
            {
                GetDraftTierWeights(currentUpgradeMul, out int currentCommon, out int currentRare, out int currentEpic);
                GetDraftTierWeights(nextUpgradeMul, out int nextCommon, out int nextRare, out int nextEpic);
                return "Draft Tier Odds C/R/E: " +
                       currentCommon + "/" + currentRare + "/" + currentEpic + "% -> " +
                       nextCommon + "/" + nextRare + "/" + nextEpic + "%";
            }

            if (upgradeId == "bike_grip")
            {
                float turnMul = GetLevelMultiplier("bike_turn", getLevel("bike_turn"));
                float currentTurn = BaseBikeTurnSpeed * currentUpgradeMul * turnMul;
                float nextTurn = BaseBikeTurnSpeed * nextUpgradeMul * turnMul;
                float currentDownforce = BaseBikeDownforce * currentUpgradeMul;
                float nextDownforce = BaseBikeDownforce * nextUpgradeMul;
                return "Turn Rate: " + currentTurn.ToString("0.0") + " -> " + nextTurn.ToString("0.0") + " deg/s\n" +
                       "Downforce: " + currentDownforce.ToString("0.00") + " -> " + nextDownforce.ToString("0.00");
            }

            if (upgradeId == "bike_brake")
            {
                float currentBrake = BaseBikeBrakeForce * currentUpgradeMul;
                float nextBrake = BaseBikeBrakeForce * nextUpgradeMul;
                return "Brake Force: " + currentBrake.ToString("0.00") + " -> " + nextBrake.ToString("0.00");
            }

            if (upgradeId == "reward_bonus")
            {
                float currentReward = currentUpgradeMul;
                float nextReward = nextUpgradeMul;
                int sampleBase = 300;
                int currentSample = Mathf.RoundToInt(sampleBase * currentReward);
                int nextSample = Mathf.RoundToInt(sampleBase * nextReward);
                return "Reward Mul: x" + currentReward.ToString("0.00") + " -> x" + nextReward.ToString("0.00") + "\n" +
                       "Sample $300 -> $" + currentSample + " -> $" + nextSample;
            }

            return "Mul: x" + currentUpgradeMul.ToString("0.00") + " -> x" + nextUpgradeMul.ToString("0.00");
        }
    }
}
