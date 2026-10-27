using UnityEngine;

//第二关的虎狼池和巨灵神配置；其他关卡继承LevelEnemyManager即可替换敌人组合。
public class LevelTwoEnemy : LevelEnemyManager
{
    [SerializeField] private TigerController tiger;
    [SerializeField] private WolfController wolf;
    [SerializeField] private Transform lungeLeft, lungeRight;
    private TigerController[] tigers = new TigerController[10];
    private WolfController[] wolves = new WolfController[10];
    private float TigerHalfX, WolfHalfX;

    protected override void InitEnemyPools()
    {
        for (int i = 0; i < tigers.Length; i++)
        {
            tigers[i] = CreateEnemy(tiger);
            if (i == 0) TigerHalfX = CM.CameraHalf - tigers[0].enemyHalf * 3;
            tigers[i].gameObject.SetActive(false);
            wolves[i] = CreateEnemy(wolf);
            if (i == 0) WolfHalfX = CM.CameraHalf - wolves[0].enemyHalf * 3;
            wolves[i].gameObject.SetActive(false);
        }
    }

    protected override void ConfigureBoss() =>
        curBoss.GetComponent<TitanMovement>().SetEdge(lungeLeft.position.x, lungeRight.position.x);

    public override bool TryCreateEnemy(bool r, float createY)
    {
        if (Random.Range(0f, 10f) >= 5f)
        {
            int idx = FindSpareEnemy(true);
            if (idx == -1) return false;
            tigers[idx].transform.position = new
                Vector2((r ? CM.EdgeR : CM.EdgeL) + (r ? 1f : -1f) * TigerHalfX, createY);
            tigers[idx].ChangeFace(r);
            tigers[idx].gameObject.SetActive(true);
        }
        else
        {
            int idx = FindSpareEnemy(false);
            if (idx == -1) return false;
            wolves[idx].transform.position = new
                Vector2((r ? CM.EdgeR : CM.EdgeL) + (r ? 1f : -1f) * WolfHalfX, createY);
            wolves[idx].ChangeFace(r);
            wolves[idx].gameObject.SetActive(true);
        }
        return true;
    }

    private int FindSpareEnemy(bool t)
    {
        if (t)
        {
            for (int i = 0; i < tigers.Length; i++)
                if (!tigers[i].gameObject.activeSelf) return i;
        }
        else
        {
            for (int i = 0; i < wolves.Length; i++)
                if (!wolves[i].gameObject.activeSelf) return i;
        }
        return -1;
    }
}
