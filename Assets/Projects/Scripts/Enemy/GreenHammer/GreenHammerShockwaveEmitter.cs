using UnityEngine;

public sealed class GreenHammerShockwaveEmitter : MonoBehaviour
{
    [SerializeField] private GreenHammerShockwave prefab;
    public GreenHammerShockwave Emit(EnemyCombat source,Vector2 position,float width,bool damage,float scale)
        =>GreenHammerShockwavePool.GetOrCreate().Spawn(prefab,source,position,width,damage,scale);
}
