using System.Collections.Generic;
using UnityEngine;

// One scene-local pool shared by all furnaces, not a pool duplicated per enemy instance.
public sealed class RollingFurnaceShockwavePool : MonoBehaviour
{
    [SerializeField] private RollingFurnaceShockwave prefab;
    private static readonly Dictionary<RollingFurnaceShockwave, List<RollingFurnaceShockwave>> pools = new();
    private static Transform poolRoot;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPools() { pools.Clear(); poolRoot = null; }
    public RollingFurnaceShockwave Emit(Vector2 position, float width)
    {
        if (prefab == null) return null;
        if (poolRoot == null) { pools.Clear(); poolRoot = new GameObject("RollingFurnaceShockwavePool").transform; }
        if (!pools.TryGetValue(prefab, out List<RollingFurnaceShockwave> entries))
        {
            entries = new(); pools.Add(prefab, entries);
            for (int i = 0; i < 10; i++) { var wave = Instantiate(prefab, poolRoot); wave.gameObject.SetActive(false); entries.Add(wave); }
        }
        RollingFurnaceShockwave spare = entries.Find(w => w != null && !w.gameObject.activeSelf);
        if (spare == null) { spare = Instantiate(prefab, poolRoot); entries.Add(spare); }
        spare.Play(position, width); return spare;
    }
}
