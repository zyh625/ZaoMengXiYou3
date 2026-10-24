using UnityEngine;
using UnityEngine.SceneManagement;
using zmxy.GamePlay;

public class LevelTwoManager : MonoBehaviour
{
    [SerializeField] private int curWave = 0;
    [SerializeField] private CameraManager CM;
    [SerializeField] private int[] leftEnemyMax, rightEnemyMax;
    [SerializeField] private float enemyDelay,enemyMax;//敌人出现的间隔和最多同时出现的个数
    private int leftEnemy = 0, rightEnemy = 0;
    private float lastLeftTime = -1f, lastRightTime = -1f;
    [SerializeField] private bool playerInNextWave = false;
    [SerializeField] private TigerController tiger;
    [SerializeField] private WolfController wolf;
    private TigerController[] tigers = new TigerController[10];
    private WolfController[] wolves = new WolfController[10];
    [SerializeField] private Transform tran;
    private float createY => tran.transform.position.y;
    private float TigerHalfX, WolfHalfX;
    [SerializeField] private PlayerController player;
    private PlayerController curPlayer;
    [SerializeField] private float firstEnemyDelay;
    [SerializeField]private float waveStartTime = -1f;
    private bool createEnemy = false;
    [SerializeField] private int maxWave;
    private bool bossStart = false, bossBorn = false;
    private float bossStartTime = -1f;
    [SerializeField] private float bossBornDelay;
    [SerializeField] private Transform boss, bossBornLocation, exitLocation, lungeLeft, lungeRight;
    private Transform curBoss;
    [SerializeField] private ExitManager exit;
    private ExitManager curExit;
    private bool bossOver = false;
    private int enemyExtraOrder = 0;

    private void Start()
    {        
        InitPlayer();
        InitEnemies();
        InitExit();
    }
    private void FixedUpdate()
    {
        if (!curPlayer.gameObject.activeSelf)
            SceneManager.LoadScene("DefeatScene");
        if (bossOver)
        {
            CheckGameOver();
            return;
        }
        if (bossBorn)
        {
            CheckBossOver();
            return;
        }
        if (bossStart)
        {
            CheckBossBorn();
            return;
        }
        if (!createEnemy) 
        {
            CheckEnemyBorn();
            return;
        }
        int curNum = AliveNum();
        if (curNum >= enemyMax) return;//同时存在的敌人过多
        if (leftEnemy+rightEnemy >= leftEnemyMax[curWave] + rightEnemyMax[curWave])//这一波的全部敌人已经出动
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
        if (AliveNum() >= enemyMax) return;
        if (Random.Range(0f, 10f) >= 5f)
        {
            int idx = FindSpareEnemy(true);
            if (idx == -1) return;
            tigers[idx].transform.position = new
                Vector2((r ? CM.EdgeR : CM.EdgeL) + (r ? 1f : -1f) * TigerHalfX, createY);
            tigers[idx].ChangeFace(r);
            tigers[idx].gameObject.SetActive(true);
        }
        else
        {
            int idx = FindSpareEnemy(false);
            if (idx == -1) return;
            wolves[idx].transform.position = new
                Vector2((r ? CM.EdgeR : CM.EdgeL) + (r ? 1f : -1f) * WolfHalfX, createY);
            wolves[idx].ChangeFace(r);
            wolves[idx].gameObject.SetActive(true);
        }
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
    }
    private int FindSpareEnemy(bool t)
    {
        if (t)
        {
            for (int i = 0; i < tigers.Length; i++)
                if (!tigers[i].gameObject.activeSelf)
                    return i;
        }
        else
            for (int i = 0; i < wolves.Length; i++)
                if (!wolves[i].gameObject.activeSelf)
                    return i;
        return -1;
    }
    private int AliveNum()
    {
        int ans = 0;
        for (int i = 0; i < tigers.Length; i++)
        {
            if (tigers[i].gameObject.activeSelf)
                ans++;
            if (wolves[i].gameObject.activeSelf)
                ans++;
        }
        return ans;
    }
    private void StartEnemy()
    {
        createEnemy = true;
        waveStartTime = -1;
    }
    private void TryNextWave()
    {
        if (curWave == maxWave-1)//开启boss战
        {
            bossStart = true;
            bossStartTime = Time.time;
            return;
        }
        leftEnemy = rightEnemy = 0;
        curWave++;
        createEnemy = false;
        playerInNextWave = false;
        CM.SetEdge(-curWave);
    }
    private void CheckBossOver()
    {
        if (!curBoss.gameObject.activeSelf)
        {
            curExit.gameObject.SetActive(true);
            bossOver = true;
        }
    }
    private void CheckBossBorn()
    {
        if (Time.time - bossStartTime >= bossBornDelay)
        {
            curBoss.gameObject.SetActive(true);
            bossBorn = true;
        }
    }
    private void CheckEnemyBorn()
    {
        if (!playerInNextWave) playerInNextWave = CM.CheckNextWave(curWave);
        else if (waveStartTime == -1f) waveStartTime = Time.time;
        else if (Time.time - waveStartTime > firstEnemyDelay) StartEnemy();
    }
    private void CheckGameOver()
    {
        if (curPlayer.PressedInteract && curExit.PlayerIn)
            SceneManager.LoadScene("VictoryScene");
    }
    private void InitPlayer()
    {
        curPlayer = Instantiate(player);
        curPlayer.transform.position = new Vector2
            (tran.position.x, createY);
        curPlayer.gameObject.SetActive(true);
        playerInNextWave = true;
    }
    private void InitEnemies()
    {
        for (int i = 0; i < tigers.Length; i++)
        {
            tigers[i] = Instantiate(tiger);
            tigers[i].GetComponentInChildren<SpriteRenderer>(true).sortingOrder += enemyExtraOrder++;
            tigers[i].GetComponent<EnemyMovement>().SetTarget(curPlayer.transform);
            if (i == 0) TigerHalfX = CM.CameraHalf - tigers[0].enemyHalf * 3;
            tigers[i].gameObject.SetActive(false);
            wolves[i] = Instantiate(wolf);
            wolves[i].GetComponentInChildren<SpriteRenderer>(true).sortingOrder += enemyExtraOrder++;
            wolves[i].GetComponent<EnemyMovement>().SetTarget(curPlayer.transform);
            if (i == 0) WolfHalfX = CM.CameraHalf - wolves[0].enemyHalf * 3;
            wolves[i].gameObject.SetActive(false);
        }
        curBoss = Instantiate(boss);
        curBoss.transform.position = bossBornLocation.transform.position;
        curBoss.GetComponent<TitanMovement>().SetEdge(lungeLeft.position.x, lungeRight.position.x);
        curBoss.gameObject.SetActive(false);
        curBoss.GetComponent<TitanMovement>().SetTarget(curPlayer.transform);
    }
    private void InitExit()
    {
        curExit = Instantiate(exit);
        curExit.transform.position = exitLocation.position;
        curExit.GetComponentInChildren<SpriteRenderer>().sortingOrder += enemyExtraOrder;
        curPlayer.GetComponentInChildren<SpriteRenderer>().sortingOrder += enemyExtraOrder;
        curExit.gameObject.SetActive(false);
    }
}
