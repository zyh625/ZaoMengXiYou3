
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class CloudSaveClient : MonoBehaviour
{
    [SerializeField]
    private string serverBaseUrl = "http://localhost:5223";

    public int PlayerId { get; private set; }

    public string Username { get; private set; }

    public string AccessToken { get; private set; }

    [Serializable]
    private class LoginRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    private class LoginResponse
    {
        public int id;
        public string username;
        public string accessToken;
        public string tokenType;
    }
    [Serializable]
    private class UploadRequest
    {
        public int slotNumber;
        public string dataJson;
    }

    public void Login(
        string username,
        string password,
        Action<bool, string> onCompleted)
    {
        StartCoroutine(LoginCoroutine(
            username, password, onCompleted));
    }
    public void UploadSave(
    int localSlotNumber,
    SaveData data,
    Action<bool, string> onCompleted)
    {
        if (string.IsNullOrEmpty(AccessToken))
        {
            onCompleted?.Invoke(false, "请先登录");
            return;
        }

        if (localSlotNumber < 0 || data == null)
        {
            onCompleted?.Invoke(false, "存档参数无效");
            return;
        }

        StartCoroutine(UploadSaveCoroutine(
            localSlotNumber, data, onCompleted));
    }

    private IEnumerator LoginCoroutine(
        string username,
        string password,
        Action<bool, string> onCompleted)
    {
        var body = new LoginRequest
        {
            username = username,
            password = password
        };

        string json = JsonUtility.ToJson(body);

        using (var request = new UnityWebRequest(
            serverBaseUrl + "/api/account/login", "POST"))
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader(
                "Content-Type", "application/json; charset=utf-8");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                AccessToken = null;
                PlayerId = 0;
                Username = null;

                onCompleted?.Invoke(
                    false,
                    $"HTTP {request.responseCode}: {request.downloadHandler.text}");

                yield break;
            }

            LoginResponse response;

            try
            {
                response = JsonUtility.FromJson<LoginResponse>(
                    request.downloadHandler.text);
            }
            catch (Exception error)
            {
                onCompleted?.Invoke(false, error.Message);
                yield break;
            }

            if (response == null ||
                string.IsNullOrEmpty(response.accessToken))
            {
                onCompleted?.Invoke(false, "服务器返回的登录数据无效");
                yield break;
            }

            PlayerId = response.id;
            Username = response.username;
            AccessToken = response.accessToken;

            onCompleted?.Invoke(true, "登录成功");
        }
    }
    private IEnumerator UploadSaveCoroutine(
    int localSlotNumber,
    SaveData data,
    Action<bool, string> onCompleted)
    {
        // 规范化存档数据
        data.Normalize();

        // 将游戏存档转换成 JSON
        string saveJson = JsonUtility.ToJson(data);

        // 本地槽位从 0 开始，服务器槽位从 1 开始
        var body = new UploadRequest
        {
            slotNumber = localSlotNumber + 1,
            dataJson = saveJson
        };

        string json = JsonUtility.ToJson(body);

        using (var request = new UnityWebRequest(
            serverBaseUrl + "/api/save/upload", "POST"))
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader(
                "Content-Type", "application/json; charset=utf-8");

            request.SetRequestHeader(
                "Authorization", "Bearer " + AccessToken);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onCompleted?.Invoke(
                    false,
                    $"HTTP {request.responseCode}: {request.downloadHandler.text}");

                yield break;
            }

            onCompleted?.Invoke(
                true,
                request.downloadHandler.text);
        }
    }
}
