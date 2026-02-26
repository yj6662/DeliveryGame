using UnityEngine;

namespace DeliveryRun.Music
{
    [CreateAssetMenu(fileName = "MusicSynergy", menuName = "DeliveryRun/Music/Synergy", order = 120)]
    public sealed class MusicSynergySO : ScriptableObject
    {
        public string SynergyId;
        public string DisplayName;
        public MusicGenreSO Genre;
        public int RequiredCount;

        [TextArea]
        public string Description;

        public MusicModifierDef[] Modifiers;
    }
}
