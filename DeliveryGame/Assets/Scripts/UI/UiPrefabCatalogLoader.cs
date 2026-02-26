using UnityEngine;

namespace DeliveryRun.UI
{
    public static class UiPrefabCatalogLoader
    {
        public static UiPrefabCatalogSO LoadOrNull()
        {
            UiPrefabCatalogSO catalog = Resources.Load<UiPrefabCatalogSO>("Bootstrap/UiPrefabCatalog");
            if (catalog == null)
            {
                Debug.LogError("[UiPrefabCatalogLoader] Missing catalog at Resources/Bootstrap/UiPrefabCatalog.");
            }

            return catalog;
        }
    }
}
