using System;
using System.Collections.Generic;
using UnityEngine;

//特效帧库：按名字登记一段特效的帧图和帧率。资产在 Resources/Effects/FxLibrary，帧图来自 Art/UI/Effects 下切好的图集。
//代码通过 Fx.Spawn 按名字播放，不需要给每个角色、Boss 单独做特效预制体。
[CreateAssetMenu(menuName = "ZaoMeng/Fx Library")]
public sealed class FxLibrary : ScriptableObject
{
    [Serializable]
    public sealed class Clip
    {
        public string name;
        [Min(1f)] public float fps = 12f;
        public Sprite[] frames = Array.Empty<Sprite>();
        public float Length => frames.Length / Mathf.Max(1f, fps);
    }

    public Clip[] clips = Array.Empty<Clip>();

    private static FxLibrary loaded;
    private static Dictionary<string, Clip> map;

    public static Clip Get(string name)
    {
        if (map == null)
        {
            map = new Dictionary<string, Clip>();
            if (loaded == null) loaded = Resources.Load<FxLibrary>("Effects/FxLibrary");
            if (loaded != null)
                foreach (Clip clip in loaded.clips)
                    if (clip != null && !string.IsNullOrEmpty(clip.name) && clip.frames.Length > 0) map[clip.name] = clip;
        }
        return name != null && map.TryGetValue(name, out Clip found) ? found : null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() { loaded = null; map = null; }
}
