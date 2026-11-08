
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CloudSaveClient))]
public class CloudSaveLoginTester : MonoBehaviour
{
    private CloudSaveClient client;

    private string username = "player001";
    private string password = "";
    private string status = "等待登录";
    private bool isRequesting;

    private void Awake()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        enabled = false;//临时登录测试框，正式包里不显示
#endif
        client = GetComponent<CloudSaveClient>();
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(
            new Rect(20, 20, 360, 260), GUI.skin.box);

        GUILayout.Label("Game Server Login Test");

        GUILayout.Label("Username");
        username = GUILayout.TextField(username);

        GUILayout.Label("Password");
        password = GUILayout.PasswordField(password, '*');

        GUI.enabled = !isRequesting;

        if (GUILayout.Button("Login"))
        {
            isRequesting = true;
            status = "正在连接服务器...";

            client.Login(username, password, (success, message) =>
            {
                isRequesting = false;

                if (success)
                {
                    status = $"登录成功！Player ID: {client.PlayerId}";
                    Debug.Log(status);

                    // 关闭临时登录测试界面
                    enabled = false;

                    // 进入主菜单
                    SceneManager.LoadScene("MenuScene");
                }
                else
                {
                    status = $"登录失败：{message}";
                    Debug.LogWarning(status);
                }
            });
        }

        GUI.enabled = true;

        GUILayout.Space(10);
        GUILayout.Label(status);

        GUILayout.EndArea();
    }
    public void UploadCurrentSave()
    {
        GameSession session = GameSession.instance;

        if (session == null ||
            !session.HasSelectedFile ||
            session.data == null)
        {
            Debug.LogWarning("当前没有选中的存档");
            return;
        }

        // 先保存本地最新进度
        if (!SaveSystem.TrySave())
        {
            Debug.LogWarning("本地存档保存失败，取消云端上传");
            return;
        }

        int slot = session.curFile;
        SaveData data = session.data;

        client.UploadSave(slot, data, (success, message) =>
        {
            if (success)
                Debug.Log($"云端存档上传成功，槽位：{slot + 1}");
            else
                Debug.LogError($"云端存档上传失败：{message}");
        });
    }
    public void DownloadCurrentSave()
    {
        GameSession session = GameSession.instance;

        if (session == null || !session.HasSelectedFile)
        {
            Debug.LogWarning("当前没有选中的存档");
            return;
        }

        int slot = session.curFile;

        client.DownloadSave(slot, (success, data, message) =>
        {
            if (!success)
            {
                Debug.LogError($"云端下载失败：{message}");
                return;
            }

            Debug.Log(
                $"云端下载成功！" +
                $"槽位：{slot + 1}，" +
                $"存档名：{data.name}，" +
                $"金币：{data.gold}，" +
                $"最高关卡：{data.gameLevel}");
        });
    }
    public void RestoreCloudSaveToSlot(int localSlotNumber)
    {
        if (localSlotNumber < 0)
        {
            Debug.LogWarning("无效的本地存档槽位");
            return;
        }

        GameSession session = GameSession.instance;

        // 防止覆盖当前正在游玩的存档
        if (session != null &&
            session.HasSelectedFile &&
            session.curFile == localSlotNumber)
        {
            Debug.LogWarning("不能覆盖当前正在使用的存档，请先返回选档界面");
            return;
        }

        client.DownloadSave(localSlotNumber, (success, data, message) =>
        {
            if (!success)
            {
                Debug.LogError($"云端下载失败：{message}");
                return;
            }

            // 将下载的数据写入本地指定槽位
            bool restored = SaveSystem.TryRestoreToSlot(
                localSlotNumber, data);

            if (restored)
            {
                Debug.Log(
                    $"云端存档恢复成功！\n" +
                    $"本地槽位：{localSlotNumber}\n" +
                    $"存档名称：{data.name}\n" +
                    $"金币：{data.gold}\n" +
                    $"最高关卡：{data.gameLevel}");
            }
            else
            {
                Debug.LogError("云端存档下载成功，但本地写入失败");
            }
        });
    }
}
