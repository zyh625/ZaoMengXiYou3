
using UnityEngine;

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
                status = success
                    ? $"登录成功！Player ID: {client.PlayerId}"
                    : $"登录失败：{message}";

                if (success)
                    Debug.Log(status);
                else
                    Debug.LogWarning(status);
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
}
