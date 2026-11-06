using UnityEngine;
using UnityEngine.EventSystems;
using zmxy.Legacy;

//第一关启动器：用新 PlayerSystem/BattleUI 接管玩家与 HUD，旧布景、鸟怪和 Boss 逻辑保持不动。
[DefaultExecutionOrder(-1000)]
public sealed class Level1Bootstrap : MonoBehaviour
{
    [SerializeField] private GameObject playerSystemPrefab;
    [SerializeField] private GameObject battleUIPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private bool hideLegacyPlayerHud = true;

    private void Awake()
    {
        GameSession.EnsureSession();
        Level1Enemy.curBirds = 0;
        Boss1.alive = false;
        Boss1.engry = 0f;
        PlayerManager manager = FindAnyObjectByType<PlayerManager>();
        if (manager == null && playerSystemPrefab != null)
        {
            GameObject system = Instantiate(playerSystemPrefab, transform.position, Quaternion.identity);
            manager = system.GetComponentInChildren<PlayerManager>(true);
        }
        if (manager == null)
        {
            Debug.LogError("[Level1Bootstrap] 场景缺少 PlayerManager，无法创建角色。", this);
            return;
        }

        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        CameraManager legacyCamera = FindAnyObjectByType<CameraManager>();
        if (legacyCamera != null && legacyCamera.ground != null && legacyCamera.ground.Length > 0
            && legacyCamera.ground[0] != null)
            spawnPosition = legacyCamera.ground[0].transform.position + Vector3.up * 2f;
        GameObject spawn = new GameObject("Level1Spawn");
        spawn.transform.position = spawnPosition;
        manager.InitPlayer(spawn.transform);
        Player.Bind(manager);

        if (battleUIPrefab != null && FindAnyObjectByType<UIManager>() == null)
        {
            GameObject ui = Instantiate(battleUIPrefab);
            foreach (Canvas canvas in ui.GetComponentsInChildren<Canvas>(true)) canvas.sortingOrder = 100;//压过旧 HUD
            EventSystem[] systems = FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude);
            for (int i = 1; i < systems.Length; i++) systems[i].gameObject.SetActive(false);
            SetCharacterSlots(ui, manager.Players.Count > 1);
        }

        if (hideLegacyPlayerHud) HideLegacyHud();
        Exit exit = FindAnyObjectByType<Exit>(FindObjectsInactive.Include);
        if (exit != null && exit.GetComponent<Level1ExitTrigger>() == null)
            exit.gameObject.AddComponent<Level1ExitTrigger>();
    }

    private static void SetCharacterSlots(GameObject ui, bool visible)
    {
        foreach (Transform child in ui.GetComponentsInChildren<Transform>(true))
            if (child.name == "Buttons") child.gameObject.SetActive(visible);
    }

    private void HideLegacyHud()
    {
        string[] hidden = { "HPBar", "MPBar", "XPBar", "AngryBar", "AngryFull", "AngryEmpty", "level1", "level2" };
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
        {
            if (canvas.transform.root.GetComponentInChildren<UIManager>(true) != null) continue;//新 BattleUI 的 HUD 保留
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                if (System.Array.IndexOf(hidden, child.name) >= 0) child.gameObject.SetActive(false);
        }
    }
}
