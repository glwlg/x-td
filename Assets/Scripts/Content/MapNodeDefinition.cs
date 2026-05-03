using UnityEngine;

namespace XTD.Content
{
    [CreateAssetMenu(menuName = "神魔镇荒/Content/Map Node Definition", fileName = "MapNodeDefinition")]
    public sealed class MapNodeDefinition : ScriptableObject
    {
        public MapNodeType nodeType = MapNodeType.NormalMonster;
        public int floor = 1;
        public int row = 1;
        public string encounterId;
        public string rewardTableId;
    }
}
