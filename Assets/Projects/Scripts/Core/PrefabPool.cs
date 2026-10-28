using System.Collections.Generic;
using UnityEngine;

//独立特效共用的对象池：活动实例不重复借出，效果结束SetActive(false)后自动成为空闲实例。
public sealed class PrefabPool<T> where T : MonoBehaviour
{
    private readonly List<T> instances = new();//每一种特效拥有自己的缓存，不混用不同预制体或角色的运行数据

    public void Prewarm(T prefab, int count, Transform activeRoot, Transform inactiveRoot)
    {
        if (prefab == null) return;
        while (instances.Count < Mathf.Max(0, count)) Create(prefab, activeRoot, inactiveRoot);
    }

    public T Get(T prefab, Transform activeRoot, Transform inactiveRoot)
    {
        for (int i = instances.Count - 1; i >= 0; i--)
        {
            T instance = instances[i];
            if (instance == null)
            {
                instances.RemoveAt(i);//外部删除过的对象不占用池内位置
                continue;
            }
            if (!instance.gameObject.activeSelf) return instance;
        }
        return Create(prefab, activeRoot, inactiveRoot);//旧效果都还在运行时扩容，不中断或刷新它们的持续时间
    }

    private T Create(T prefab, Transform activeRoot, Transform inactiveRoot)
    {
        T instance = Object.Instantiate(prefab, inactiveRoot);//在停用容器中创建，初始化前不会运行OnEnable或产生可见特效
        instance.gameObject.SetActive(false);
        instance.transform.SetParent(activeRoot, false);//保留预制体本来的局部缩放，生成位置由各自Launch入口设置
        instances.Add(instance);
        return instance;
    }

    public void ReleaseAll()//战斗终结或管理器关闭时回收；各特效的OnDisable只清理自己的来源和运行记录
    {
        foreach (T instance in instances)
            if (instance != null && instance.gameObject.activeSelf) instance.gameObject.SetActive(false);
    }
}
