using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class SaveSystem
{
    public static string path = Application.persistentDataPath;
    public static void Save()
    {
        TrySave();
    }

    public static bool TrySave()//没有真正选档的编辑器直跑只保留内存数据，绝不默认覆盖save0
    {
        GameSession session = GameSession.instance;
        if (session == null || !session.HasSelectedFile || session.data == null) return false;
        string p = FilePath(session.curFile);
        string temporary = p + ".tmp";//先完整写临时文件，再替换目标，避免写到一半破坏正式存档
        try
        {
            Directory.CreateDirectory(path);
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
    public static SaveData Load(int num)
    {
        if (num < 0) return null;
        string p = FilePath(num);
        return ReadFile(p) ?? ReadFile(p + ".bak");//正式文件损坏时优先保留已有进度，读取成功不自动重写用户文件
    }
    public static void Delete(int num)
    {
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

    private static string FilePath(int num) => Path.Combine(path, "save" + num.ToString(CultureInfo.InvariantCulture) + ".json");

    private static SaveData ReadFile(string file)//不存在静默返回；损坏与权限问题不能阻断选档界面和其他槽读取
    {
        if (!File.Exists(file)) return null;
        try
        {
            string json = File.ReadAllText(file, Encoding.UTF8).Trim();
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
