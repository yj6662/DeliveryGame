using UnityEngine;

namespace DeliveryRun.Music
{
    [CreateAssetMenu(fileName = "MusicGenre", menuName = "DeliveryRun/Music/Genre", order = 100)]
    public sealed class MusicGenreSO : ScriptableObject
    {
        public string GenreId;
        public string DisplayName;

        [TextArea]
        public string ThemeTitle;

        [TextArea]
        public string ThemeDescription;

        public string PrimaryStatKey;
        public string Tag;
    }
}
