using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class SaveSystem
{
    public static string path = Application.persistentDataPath;

    //本地存档是当前账号云端存档的副本：登录后读写 accounts/<账号ID>/，不同账号互不可见；
    //未登录（编辑器直接运行场景、自动化验收）时仍使用根目录 path，行为与以前一致。
    private const string AccountsFolder = "accounts";
    private static int accountId;
    public static string SaveDirectory => accountId > 0
        ? Path.Combine(path, AccountsFolder, accountId.ToString(CultureInfo.InvariantCulture))
        : path;

    public static void SetAccount(int playerId) => accountId = Math.Max(0, playerId);
    public static void ClearAccount() => accountId = 0;

    //注销账号时删除该账号的全部本地存档（只删 save*.json 及其备份、临时文件，不递归，不跟随链接）
    public static bool DeleteAccountSaves(int playerId)
    {
        if (playerId <= 0) return false;
        string root = Path.GetFullPath(Path.Combine(path, AccountsFolder));
        string folder = Path.GetFullPath(Path.Combine(root, playerId.ToString(CultureInfo.InvariantCulture)));
        if (!folder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
        if (!Directory.Exists(folder)) return true;
        bool ok = true;
        try
        {
            if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) return false;
            foreach (string file in Directory.GetFiles(folder, "save*.json*", SearchOption.TopDirectoryOnly))
            {
                try { File.Delete(file); }
                catch (Exception error) when (IsFileError(error)) { ok = false; Debug.LogWarning($"本地存档删除失败：{error.Message}"); }
            }
            if (Directory.GetFileSystemEntries(folder).Length == 0) Directory.Delete(folder, false);
        }
        catch (Exception error) when (IsFileError(error))
        {
            Debug.LogWarning($"账号存档目录清理失败：{error.Message}");
            return false;
        }
        return ok;
    }

    public static event Action<int, SaveData> LocalSaveSucceeded;
    public static void Save()
    {
        TrySave();
    }

    public static bool TrySave()//没有真正选档的编辑器直跑只保留内存数据，绝不默认覆盖save0
    {
        if(TrainingSession.Active){TrainingSession.BlockWrite();return false;}
        GameSession session = GameSession.instance;
        if (session == null || !session.HasSelectedFile || session.data == null) return false;
        string p = FilePath(session.curFile);
        string temporary = p + ".tmp";//先完整写临时文件，再替换目标，避免写到一半破坏正式存档
        try
        {
            Directory.CreateDirectory(SaveDirectory);
            session.data.Normalize();
            session.maxLevel = session.data.gameLevel;
            session.data.date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            File.WriteAllText(temporary, JsonUtility.ToJson(session.data, true), new UTF8Encoding(false));
            if (File.Exists(p))
            {
                try { File.Replace(temporary, p, p + ".bak"); }
                catch (NotSupportedException)//个别运行平台不支持Replace时仍保留可恢复的上次副本
                {
                    File.Copy(p, p + ".bak", true);
                    File.Copy(temporary, p, true);
                    File.Delete(temporary);
                }
            }
            else File.Move(temporary, p);

            // 本地保存成功后，通知云端同步模块
            try
            {
                LocalSaveSucceeded?.Invoke(
                    session.curFile,
                    session.data);
            }
            catch (Exception error)
            {
                Debug.LogWarning(
                    $"本地存档成功，但云端同步通知失败：{error.Message}");
            }

            return true;
        }
        catch (Exception error) when (IsFileError(error))
        {
            Debug.LogWarning($"存档保存失败：{error.Message}");
            return false;
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception error) when (IsFileError(error)) { Debug.LogWarning($"存档临时文件清理失败：{error.Message}"); }
        }
    }

    public static bool TryRestoreToSlot(int num, SaveData data)
    {
        if (TrainingSession.Active)
        {
            TrainingSession.BlockWrite();
            return false;
        }

        if (num < 0 || data == null)
            return false;

        // 禁止覆盖当前正在使用的存档槽位
        GameSession session = GameSession.instance;

        if (session != null &&
            session.HasSelectedFile &&
            session.curFile == num)
        {
            Debug.LogWarning("不能覆盖当前正在使用的存档，请先返回存档选择界面。");
            return false;
        }

        string p = FilePath(num);
        string temporary = p + ".tmp";

        try
        {
            Directory.CreateDirectory(SaveDirectory);

            data.Normalize();

            // 保留云端存档原有的日期，不在恢复时修改
            string json = JsonUtility.ToJson(data, true);

            File.WriteAllText(
                temporary,
                json,
                new UTF8Encoding(false)
            );

            if (File.Exists(p))
            {
                try
                {
                    File.Replace(temporary, p, p + ".bak");
                }
                catch (NotSupportedException)
                {
                    File.Copy(p, p + ".bak", true);
                    File.Copy(temporary, p, true);
                    File.Delete(temporary);
                }
            }
            else
            {
                File.Move(temporary, p);
            }

            return true;
        }
        catch (Exception error) when (IsFileError(error))
        {
            Debug.LogWarning($"云端存档恢复失败：{error.Message}");
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            catch (Exception error) when (IsFileError(error))
            {
                Debug.LogWarning($"临时文件清理失败：{error.Message}");
            }
        }
    }


    public static SaveData Load(int num)
    {
        if(TrainingSession.Active){TrainingSession.BlockRead();return null;}
        if (num < 0) return null;
        string p = FilePath(num);
        return ReadFile(p) ?? ReadFile(p + ".bak");//正式文件损坏时优先保留已有进度，读取成功不自动重写用户文件
    }
    public static void Delete(int num)
    {
        if(TrainingSession.Active){TrainingSession.BlockWrite();return;}
        if (num < 0) return;
        string p = FilePath(num);
        foreach (string file in new[] { p, p + ".bak", p + ".tmp" })//删除只限指定槽，备份也一同清理以免下次又恢复已删除的档
        {
            try { if (File.Exists(file)) File.Delete(file); }
            catch (Exception error) when (IsFileError(error)) { Debug.LogWarning($"存档删除失败：{error.Message}"); }
        }
        if (GameSession.instance != null && GameSession.instance.curFile == num)
            GameSession.instance.ClearSelectedFile();
    }

    private static string FilePath(int num) => Path.Combine(SaveDirectory, "save" + num.ToString(CultureInfo.InvariantCulture) + ".json");

    //旧版本在中文系统上可能用 GBK 写过存档名；UTF-8 解码失败时回退到 GB18030，避免用户名显示为乱码。
    private static string ReadText(string file)
    {
        byte[] bytes = File.ReadAllBytes(file);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException)
        {
            try { return Encoding.GetEncoding("GB18030").GetString(bytes); }
            catch (Exception) { return Encoding.UTF8.GetString(bytes); }
        }
    }

    private static SaveData ReadFile(string file)//不存在静默返回；损坏与权限问题不能阻断选档界面和其他槽读取
    {
        if (!File.Exists(file)) return null;
        try
        {
            string json = ReadText(file).Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}")
                || !(json.Contains("\"players\"") || json.Contains("\"gameLevel\"") || json.Contains("\"name\"")))
                throw new ArgumentException("文件不是有效的游戏存档");
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null) return null;
            data.Normalize();
            return data;
        }
        catch (Exception error) when (IsFileError(error))
        {
            Debug.LogWarning($"存档读取失败：{Path.GetFileName(file)}，{error.Message}");
            return null;
        }
    }

    private static bool IsFileError(Exception error) => error is IOException || error is UnauthorizedAccessException
        || error is ArgumentException || error is NotSupportedException || error is System.Security.SecurityException;
}
