using UnityEngine;

namespace DeliveryRun.UI
{
    [CreateAssetMenu(
        fileName = "UiPrefabCatalog",
        menuName = "DeliveryRun/UI Prefab Catalog",
        order = 0)]
    public class UiPrefabCatalogSO : ScriptableObject
    {
        [Header("Legacy Prefab References (deprecated)")]
        public GameObject RunHudPrefab;
        public GameObject MusicSelectionModalPrefab;
        public GameObject RunResultModalPrefab;

        [Header("Addressables Keys (preferred)")]
        public string RunHudKey = "ui/run/hud";
        public string MusicSelectionModalKey = "ui/run/music_selection_modal";
        public string RunResultModalKey = "ui/run/run_result_modal";
        public string UiRunLabel = "ui:run";

        [Header("Audio Keys (Addressables)")]
        public string TestBgmKey = "audio/bgm/test_bgm";
        public string UiClickKey = "audio/ui/click";

        [Header("Lobby UI Skins (Kenney UI Pack)")]
        public Font LobbyPrimaryFont;
        public Texture2D LobbyPanelTexture;
        public Texture2D LobbyButtonTexture;
        public Texture2D LobbyButtonAccentTexture;
        public Texture2D LobbyRegionLockedIconTexture;
        public Texture2D LobbyRegionUnlockableIconTexture;
        public Texture2D LobbyRegionOpenIconTexture;
        public Texture2D LobbyRegionSelectedIconTexture;
    }
}
