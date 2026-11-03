using System;
using UnityEngine;

[CreateAssetMenu(fileName="TrainingEnemyCatalog",menuName="造梦西游/运行时训练敌人目录")]
public sealed class TrainingEnemyCatalog : ScriptableObject
{
    [Serializable]public sealed class Entry
    {
        public string displayName;
        public EnemyController prefab;
        public bool summonFallback;
    }
    public Entry[] enemies=Array.Empty<Entry>();
    public int Count=>enemies?.Length??0;
}
