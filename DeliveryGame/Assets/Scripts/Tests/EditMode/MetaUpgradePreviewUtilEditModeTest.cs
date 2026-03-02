using DeliveryRun.Managers.Core;
using NUnit.Framework;

namespace DeliveryRun.Tests.EditMode
{
    public sealed class MetaUpgradePreviewUtilEditModeTest
    {
        [Test]
        public void GetLevelMultiplier_UsesConfiguredPerLevelDelta()
        {
            float mul = MetaUpgradePreviewUtil.GetLevelMultiplier("bike_speed", 2);
            Assert.AreEqual(1.12f, mul, 0.0001f);
        }

        [Test]
        public void BuildUpgradeValueDeltaText_SpeedUpgrade_ContainsRealValuePreview()
        {
            string text = MetaUpgradePreviewUtil.BuildUpgradeValueDeltaText(
                "bike_speed",
                0,
                1,
                _ => 0);

            StringAssert.Contains("Max Speed: 11.00 -> 11.66 m/s", text);
            StringAssert.Contains("Accel Force: 10.00 -> 10.60", text);
        }

        [Test]
        public void GetDraftTierWeights_PreservesMinimumCommonWeight()
        {
            int common;
            int rare;
            int epic;
            MetaUpgradePreviewUtil.GetDraftTierWeights(3.0f, out common, out rare, out epic);

            Assert.GreaterOrEqual(common, 5);
            Assert.AreEqual(100, common + rare + epic);
        }
    }
}
