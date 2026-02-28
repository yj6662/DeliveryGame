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
            internal readonly float DeliveryLimitSeconds;
            internal readonly bool IsSeafood;

            internal FoodSelection(
                string foodId,
                string foodName,
                float tempDecayMultiplier,
                float spillGainMultiplier,
                float deliveryLimitSeconds,
                bool isSeafood)
            {
                FoodId = foodId;
                FoodName = foodName;
                TempDecayMultiplier = tempDecayMultiplier;
                SpillGainMultiplier = spillGainMultiplier;
                DeliveryLimitSeconds = deliveryLimitSeconds;
                IsSeafood = isSeafood;
            }
        }

        private static readonly FoodSelection[] FoodDefinitions =
        {
            new FoodSelection("burger", "Burger Set", 0.95f, 1.00f, 125f, false),
            new FoodSelection("pizza", "Pizza", 1.05f, 1.08f, 130f, false),
            new FoodSelection("ramen", "Ramen", 1.20f, 1.18f, 110f, false),
            new FoodSelection("fried_chicken", "Fried Chicken", 0.92f, 1.05f, 122f, false),
            new FoodSelection("coffee", "Coffee", 1.30f, 1.25f, 105f, false),
            new FoodSelection("sushi", "Sushi Box", 0.85f, 1.12f, 88f, true),
            new FoodSelection("shrimp_pasta", "Shrimp Pasta", 0.90f, 1.10f, 92f, true),
            new FoodSelection("grilled_mackerel", "Grilled Mackerel", 0.88f, 1.08f, 84f, true),
            new FoodSelection("crab_rice", "Crab Rice Bowl", 0.89f, 1.14f, 86f, true),
            new FoodSelection("clam_chowder", "Clam Chowder", 0.94f, 1.17f, 80f, true)
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
                return new FoodSelection("food", "Food", 1f, 1f, defaultDeliveryLimitSeconds, false);
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
