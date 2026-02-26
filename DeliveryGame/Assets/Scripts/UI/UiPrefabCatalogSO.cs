using UnityEngine;

namespace DeliveryRun.UI
{
    [CreateAssetMenu(
        fileName = "UiPrefabCatalog",
        menuName = "DeliveryRun/UI Prefab Catalog",
        order = 0)]
    public class UiPrefabCatalogSO : ScriptableObject
    {
        public GameObject RunHudPrefab;
        public GameObject MusicSelectionModalPrefab;
    }
}
