using System.Collections.Generic;
using UnityEngine;

namespace XTD.Content
{
    [CreateAssetMenu(menuName = "神魔镇荒/Content/Artifact Definition", fileName = "ArtifactDefinition")]
    public sealed class ArtifactDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public ArtifactRarity rarity = ArtifactRarity.Common;
        public ArtifactTrigger trigger = ArtifactTrigger.Passive;
        [TextArea] public string description;
        public Sprite icon;
        public List<BattleEffectDefinition> effects = new();
    }
}
