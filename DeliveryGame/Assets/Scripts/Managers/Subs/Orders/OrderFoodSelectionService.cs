using System;
using System.Collections.Generic;
using DeliveryRun.Delivery.Orders;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class OrderFoodSelectionService
    {
        internal readonly struct FoodSelection
        {
            internal readonly string FoodId;
            internal readonly string FoodName;
            internal readonly float TempDecayMultiplier;
            internal readonly float SpillGainMultiplier;
            internal readonly float RewardMultiplier;
            internal readonly float DeliveryLimitSeconds;
            internal readonly bool IsSeafood;

            internal FoodSelection(
                string foodId,
                string foodName,
                float tempDecayMultiplier,
                float spillGainMultiplier,
                float rewardMultiplier,
                float deliveryLimitSeconds,
                bool isSeafood)
            {
                FoodId = foodId;
                FoodName = foodName;
                TempDecayMultiplier = tempDecayMultiplier;
                SpillGainMultiplier = spillGainMultiplier;
                RewardMultiplier = rewardMultiplier;
                DeliveryLimitSeconds = deliveryLimitSeconds;
                IsSeafood = isSeafood;
            }
        }

        private static readonly FoodSelection[] FoodDefinitions =
        {
            new FoodSelection("burger", "Burger Set", 0.70f, 0.68f, 0.90f, 138f, false),
            new FoodSelection("pizza", "Pizza", 0.92f, 0.86f, 1.00f, 136f, false),
            new FoodSelection("ramen", "Ramen", 1.70f, 1.92f, 1.20f, 104f, false),
            new FoodSelection("fried_chicken", "Fried Chicken", 0.80f, 0.76f, 0.97f, 132f, false),
            new FoodSelection("coffee", "Coffee", 2.00f, 2.25f, 1.24f, 96f, false),
            new FoodSelection("salad", "Chicken Salad", 0.62f, 0.56f, 0.86f, 145f, false),
            new FoodSelection("tteokbokki", "Tteokbokki", 1.56f, 1.78f, 1.16f, 106f, false),
            new FoodSelection("sushi", "Sushi Box", 0.58f, 0.95f, 1.08f, 94f, true),
            new FoodSelection("shrimp_pasta", "Shrimp Pasta", 1.36f, 1.28f, 1.15f, 100f, true),
            new FoodSelection("grilled_mackerel", "Grilled Mackerel", 1.15f, 1.00f, 1.10f, 92f, true),
            new FoodSelection("crab_rice", "Crab Rice Bowl", 1.22f, 1.16f, 1.17f, 94f, true),
            new FoodSelection("clam_chowder", "Clam Chowder", 1.88f, 2.10f, 1.30f, 84f, true)
        };

        internal void BuildFoodRestaurantMap(
            List<OrderBuildingAnchor> restaurantAnchors,
            Dictionary<string, OrderBuildingAnchor> foodRestaurantMap)
        {
            if (foodRestaurantMap == null)
            {
                return;
            }

            foodRestaurantMap.Clear();
            if (restaurantAnchors == null || restaurantAnchors.Count <= 0)
            {
                return;
            }

            int restaurantCount = restaurantAnchors.Count;
            for (int i = 0; i < FoodDefinitions.Length; i++)
            {
                FoodSelection food = FoodDefinitions[i];
                OrderBuildingAnchor anchor = restaurantAnchors[i % restaurantCount];
                foodRestaurantMap[food.FoodId] = anchor;
            }
        }

        internal FoodSelection PickFoodDefinition(string regionId, float defaultDeliveryLimitSeconds)
        {
            if (FoodDefinitions.Length <= 0)
            {
                return new FoodSelection("food", "Food", 1f, 1f, 1f, defaultDeliveryLimitSeconds, false);
            }

            bool seasideRegion = string.Equals(regionId, "seaside", StringComparison.Ordinal);
            int seafoodCount = 0;
            int nonSeafoodCount = 0;
            for (int i = 0; i < FoodDefinitions.Length; i++)
            {
                if (FoodDefinitions[i].IsSeafood)
                {
                    seafoodCount++;
                }
                else
                {
                    nonSeafoodCount++;
                }
            }

            if (seasideRegion && seafoodCount > 0)
            {
                const int seafoodWeight = 85;
                bool pickSeafood = UnityEngine.Random.Range(0, 100) < seafoodWeight || nonSeafoodCount <= 0;
                if (pickSeafood)
                {
                    int seafoodIndex = UnityEngine.Random.Range(0, seafoodCount);
                    int seen = 0;
                    for (int i = 0; i < FoodDefinitions.Length; i++)
                    {
                        if (!FoodDefinitions[i].IsSeafood)
                        {
                            continue;
                        }

                        if (seen == seafoodIndex)
                        {
                            return FoodDefinitions[i];
                        }

                        seen++;
                    }
                }
            }

            return FoodDefinitions[UnityEngine.Random.Range(0, FoodDefinitions.Length)];
        }
    }
}
