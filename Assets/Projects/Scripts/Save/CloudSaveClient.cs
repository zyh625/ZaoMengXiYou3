using System.Collections.Generic;
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;


public class CloudSaveClient : MonoBehaviour
{
    public static CloudSaveClient Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        SaveSystem.LocalSaveSucceeded += HandleLocalSaveSucceeded;
    }

    private void OnDisable()
    {
        SaveSystem.LocalSaveSucceeded -= HandleLocalSaveSucceeded;
    }

    private void HandleLocalSaveSucceeded(int slot, SaveData data)
    {
        // 未登录时只保存本地
        if (string.IsNullOrEmpty(AccessToken))
            return;

        UploadSave(slot, data, (success, message) =>
        {
            if (success)
            {
                Debug.Log(
                    $"存档槽位 {slot + 1} 已自动同步到云端");
            }
            else
            {
                Debug.LogWarning(
                    $"本地保存成功，但云端同步失败：{message}");
            }
        });
    }

    [Serializable]
    public class CloudSaveInfo
    {
        public int slotNumber;
        public string name;
        public int gameLevel;
        public string updatedAtUtc;
        public int rebirth;
    }

    [Serializable]
    private class SaveListResponse
    {
        public CloudSaveInfo[] saves = new CloudSaveInfo[0];
    }

    public void GetSaveList(
    Action<bool, CloudSaveInfo[], string> onCompleted)
    {
        if (string.IsNullOrEmpty(AccessToken))
        {
            onCompleted?.Invoke(false, null, "请先登录");
            return;
        }

        StartCoroutine(GetSaveListCoroutine(onCompleted));
    }
    private IEnumerator GetSaveListCoroutine(
    Action<bool, CloudSaveInfo[], string> onCompleted)
    {
        string url = serverBaseUrl + "/api/save/list";

        using (var request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader(
                "Authorization", "Bearer " + AccessToken);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onCompleted?.Invoke(
                    false,
                    null,
                    $"HTTP {request.responseCode}: {request.downloadHandler.text}");

                yield break;
            }

            SaveListResponse response;

            try
            {
                response = JsonUtility.FromJson<SaveListResponse>(
                    request.downloadHandler.text);
            }
            catch (Exception error)
            {
                onCompleted?.Invoke(false, null, error.Message);
                yield break;
            }

            if (response == null || response.saves == null)
            {
                onCompleted?.Invoke(false, null, "存档列表解析失败");
                yield break;
            }

            onCompleted?.Invoke(
                true,
                response.saves,
                "获取存档列表成功");
        }
    }


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
        public int id = 0;
        public string username = "";
        public string accessToken = "";
        public string tokenType = "";
    }
    [Serializable]
    private class UploadRequest
    {
        public int slotNumber;
        public string dataJson;
    }
    [Serializable]
    private class DownloadResponse
    {
        public int slotNumber = 0;
        public string dataJson = "";
        public string updatedAtUtc = "";
    }




    public void DownloadSave(
    int localSlotNumber,
    Action<bool, SaveData, string> onCompleted)
    {
        if (string.IsNullOrEmpty(AccessToken))
        {
            onCompleted?.Invoke(false, null, "请先登录");
            return;
        }

        if (localSlotNumber < 0)
        {
            onCompleted?.Invoke(false, null, "无效的存档槽位");
            return;
        }

        StartCoroutine(DownloadSaveCoroutine(
            localSlotNumber, onCompleted));
    }

    private IEnumerator DownloadSaveCoroutine(
    int localSlotNumber,
    Action<bool, SaveData, string> onCompleted)
    {
        // 本地槽位从0开始，服务器槽位从1开始
        int serverSlot = localSlotNumber + 1;

        string url = serverBaseUrl +
            "/api/save/download/" + serverSlot;

        using (var request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader(
                "Authorization", "Bearer " + AccessToken);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onCompleted?.Invoke(
                    false,
                    null,
                    $"HTTP {request.responseCode}: {request.downloadHandler.text}");

                yield break;
            }

            SaveData data;

            try
            {
                var response = JsonUtility.FromJson<DownloadResponse>(
                    request.downloadHandler.text);

                if (response == null ||
                    string.IsNullOrEmpty(response.dataJson))
                {
                    throw new Exception("服务器返回的存档数据为空");
                }

                data = JsonUtility.FromJson<SaveData>(
                    response.dataJson);

                if (data == null)
                    throw new Exception("存档解析失败");

                data.Normalize();
            }
            catch (Exception error)
            {
                onCompleted?.Invoke(
                    false, null, error.Message);

                yield break;
            }

            onCompleted?.Invoke(
                true, data, "云端存档下载成功");
        }
    }

    public bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken);

    // 删除云端存档；云端本来就没有该槽位（404）也算删除成功
    public void DeleteSave(
    int localSlotNumber,
    Action<bool, string> onCompleted)
    {
        if (!IsLoggedIn)
        {
            onCompleted?.Invoke(false, "请先登录");
            return;
        }

        if (localSlotNumber < 0)
        {
            onCompleted?.Invoke(false, "无效的存档槽位");
            return;
        }

        StartCoroutine(DeleteSaveCoroutine(
            localSlotNumber, onCompleted));
    }

    private IEnumerator DeleteSaveCoroutine(
    int localSlotNumber,
    Action<bool, string> onCompleted)
    {
        // 本地槽位从0开始，服务器槽位从1开始
        string url = serverBaseUrl +
            "/api/save/delete/" + (localSlotNumber + 1);

        using (var request = UnityWebRequest.Delete(url))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader(
                "Authorization", "Bearer " + AccessToken);

            yield return request.SendWebRequest();

            if (request.responseCode == 404)
            {
                onCompleted?.Invoke(true, "云端没有该存档");
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                onCompleted?.Invoke(
                    false,
                    $"HTTP {request.responseCode}: {request.downloadHandler.text}");

                yield break;
            }

            onCompleted?.Invoke(true, "云端存档删除成功");
        }
    }

    //账号接口的结果，登录界面据此显示对应的中文提示
    public enum AccountResult
    {
        Success,
        ServerUnavailable,//连不上服务器或超时
        AccountNotFound,
        WrongPassword,
        AccountExists,
        InvalidInput,//账号或密码格式不符合服务器要求
        SessionExpired,//未登录或登录令牌已失效
        Failed//其他错误
    }

    [Serializable]
    private class DeleteAccountRequest
    {
        public string password;
    }

    public bool IsLoggedInAccount => !string.IsNullOrEmpty(AccessToken);

    public void Logout()//切换账号或注销后清除登录身份，之后的存档不再同步到旧账号
    {
        AccessToken = null;
        PlayerId = 0;
        Username = null;
        SaveSystem.ClearAccount();//本地存档目录随登录身份切换
    }

    public void DeleteAccount(string password, Action<AccountResult, string> onCompleted)
    {
        if (string.IsNullOrEmpty(AccessToken))
        {
            onCompleted?.Invoke(AccountResult.SessionExpired, "请先登录");
            return;
        }
        StartCoroutine(DeleteAccountCoroutine(password, onCompleted));
    }

    private IEnumerator DeleteAccountCoroutine(string password, Action<AccountResult, string> onCompleted)
    {
        string json = JsonUtility.ToJson(new DeleteAccountRequest { password = password });
        using (var request = new UnityWebRequest(serverBaseUrl + "/api/account/delete", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = accountRequestTimeout;
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            request.SetRequestHeader("Authorization", "Bearer " + AccessToken);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onCompleted?.Invoke(
                    ClassifyAccountError(request, false),
                    $"HTTP {request.responseCode}: {request.downloadHandler.text}");
                yield break;
            }

            //云端已删除，本地副本一并删除
            if (!SaveSystem.DeleteAccountSaves(PlayerId))
                Debug.LogWarning("账号已注销，但部分本地存档未能删除");
            Logout();
            onCompleted?.Invoke(AccountResult.Success, "账号已注销");
        }
    }

    [Serializable]
    private class AccountErrorResponse
    {
        public string code = "";
        public string message = "";
    }

    [SerializeField, Min(1)] private int accountRequestTimeout = 8;//登录/注册请求超时秒数，超时按服务器未连接处理

    public void Login(
        string username,
        string password,
        Action<bool, string> onCompleted)
    {
        LoginAccount(username, password,
            (result, message) => onCompleted?.Invoke(result == AccountResult.Success, message));
    }

    public void LoginAccount(
        string username,
        string password,
        Action<AccountResult, string> onCompleted)
    {
        StartCoroutine(LoginCoroutine(username, password, onCompleted));
    }

    public void RegisterAccount(
        string username,
        string password,
        Action<AccountResult, string> onCompleted)
    {
        StartCoroutine(RegisterCoroutine(username, password, onCompleted));
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

    private UnityWebRequest PostAccountJson(string path, string username, string password)
    {
        string json = JsonUtility.ToJson(new LoginRequest
        {
            username = username,
            password = password
        });

        var request = new UnityWebRequest(serverBaseUrl + path, "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = accountRequestTimeout;
        request.SetRequestHeader(
            "Content-Type", "application/json; charset=utf-8");
        return request;
    }

    //优先按服务器返回的 code 判断，旧版服务器没有 code 时再按状态码判断
    private static AccountResult ClassifyAccountError(UnityWebRequest request, bool login)
    {
        if (request.result == UnityWebRequest.Result.ConnectionError || request.responseCode == 0)
            return AccountResult.ServerUnavailable;

        string code = "";
        try
        {
            code = JsonUtility.FromJson<AccountErrorResponse>(request.downloadHandler.text)?.code ?? "";
        }
        catch (Exception)
        {
            // 非 JSON 的错误页（如 400 校验结果以外的格式）按状态码处理
        }

        switch (code)
        {
            case "account_not_found": return AccountResult.AccountNotFound;
            case "wrong_password": return AccountResult.WrongPassword;
            case "account_exists": return AccountResult.AccountExists;
        }

        return request.responseCode switch
        {
            400 => AccountResult.InvalidInput,
            404 when login => AccountResult.AccountNotFound,
            401 when login => AccountResult.WrongPassword,
            401 => AccountResult.SessionExpired,//已登录接口的 401 来自令牌校验
            409 when !login => AccountResult.AccountExists,
            _ => AccountResult.Failed
        };
    }

    private IEnumerator LoginCoroutine(
        string username,
        string password,
        Action<AccountResult, string> onCompleted)
    {
        using (var request = PostAccountJson("/api/account/login", username, password))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Logout();//登录失败不保留上一个账号的身份和存档目录

                onCompleted?.Invoke(
                    ClassifyAccountError(request, true),
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
                onCompleted?.Invoke(AccountResult.Failed, error.Message);
                yield break;
            }

            if (response == null ||
                string.IsNullOrEmpty(response.accessToken))
            {
                onCompleted?.Invoke(AccountResult.Failed, "服务器返回的登录数据无效");
                yield break;
            }

            PlayerId = response.id;
            Username = response.username;
            AccessToken = response.accessToken;
            SaveSystem.SetAccount(PlayerId);//之后的本地存档读写都在这个账号自己的目录里

            onCompleted?.Invoke(AccountResult.Success, "登录成功");
        }
    }

    private IEnumerator RegisterCoroutine(
        string username,
        string password,
        Action<AccountResult, string> onCompleted)
    {
        using (var request = PostAccountJson("/api/account/register", username, password))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onCompleted?.Invoke(
                    ClassifyAccountError(request, false),
                    $"HTTP {request.responseCode}: {request.downloadHandler.text}");
                yield break;
            }

            onCompleted?.Invoke(AccountResult.Success, "注册成功");
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
