using System;
using UnityEngine;
using zmxy.GamePlay;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private int curWave = 0;
    [SerializeField] private CameraManager CM;
    [SerializeField] private int[] leftEnemyMax, rightEnemyMax;
    [SerializeField] private float enemyDelay,enemyMax;//敌人出现的间隔和最多同时出现的个数
    private int curEnemy = 0, leftEnemy = 0, rightEnemy = 0;
    private float lastLeftTime = -1f, lastRightTime = -1f;
    [SerializeField] private bool playerInNextWave = false;
    [SerializeField] private TigerController tiger;
    private TigerController[] tigers = new TigerController[10];
    [SerializeField] private Transform tran;
    private float createY => tran.transform.position.y;
    private float HalfX;
    [SerializeField] private PlayerController player;
    private PlayerController curPlayer;
    [SerializeField] private float firstEnemyDelay;
    [SerializeField]private float waveStartTime = -1f;
    private bool createEnemy = false;
    [SerializeField] private int maxWave;


    private void Start()
    {
        curPlayer = Instantiate(player);
        curPlayer.transform.position = new Vector2
            (tran.position.x, createY);
        curPlayer.gameObject.SetActive(true);
        playerInNextWave = true;
        for (int i = 0; i < tigers.Length; i++)
        {
            tigers[i] = Instantiate(tiger);
            tigers[i].GetComponent<EnemyMovement>().SetTarget(curPlayer.transform);
            if (i == 0) HalfX = CM.CameraHalf - tigers[0].TigerHalf * 3;
            tigers[i].gameObject.SetActive(false);
        }
    }
    private void FixedUpdate()
    {
        if (!createEnemy) 
        {
            if (!playerInNextWave) playerInNextWave = CM.CheckNextWave(curWave);
            else if (waveStartTime == -1f) waveStartTime = Time.time;
            else if (Time.time - waveStartTime > firstEnemyDelay) StartEnemy();
            return;
        }
        int curNum = AliveNum();
        if (curNum >= enemyMax) return;//同时存在的敌人过多
        if (curEnemy >= leftEnemyMax[curWave] + rightEnemyMax[curWave])//这一波的全部敌人已经出动
        {
            if (curNum == 0) TryNextWave();
            return;
        }
        if ((lastLeftTime == -1 || Time.time - lastLeftTime > enemyDelay)
            && leftEnemy < leftEnemyMax[curWave])
            TryCreateEnemy(false);
        if ((lastRightTime == -1 || Time.time - lastRightTime > enemyDelay)
            && rightEnemy < rightEnemyMax[curWave])
            TryCreateEnemy(true);
    }
    private void TryCreateEnemy(bool r)
    {
        int idx = FindSpareEnemy();
        if (idx == -1) return;
        tigers[idx].transform.position = new
            Vector2((r ? CM.EdgeR : CM.EdgeL) + (r ? 1f : -1f) * HalfX, createY);
        tigers[idx].ChangeFace(r);
        tigers[idx].gameObject.SetActive(true);
        if (r)
        {
            lastRightTime = Time.time;
            rightEnemy++;
        }
        else
        {
            lastLeftTime = Time.time;
            leftEnemy++;
        }
        curEnemy++;
    }
    private int FindSpareEnemy()
    {
        for(int i = 0; i < tigers.Length; i++)
            if (!tigers[i].gameObject.activeSelf)
                return i;
        return -1;
    }
    private int AliveNum()
    {
        int ans = 0;
        for (int i = 0; i < tigers.Length; i++)
            if (tigers[i].gameObject.activeSelf)
                ans++;
        return ans;
    }
    private void StartEnemy()
    {
        createEnemy = true;
        waveStartTime = -1;
    }
    private void TryNextWave()
    {
        if (curWave == maxWave-1)
        {
            Debug.Log("win");
            //成功过关，结算
            return;
        }
        curEnemy = leftEnemy = rightEnemy = 0;
        curWave++;
        createEnemy = false;
        playerInNextWave = false;
        CM.SetEdge(-curWave);
    }
}
