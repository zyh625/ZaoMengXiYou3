using System.IO;
using UnityEngine;

public static class SaveSystem
{
    public static string path = Application.persistentDataPath;
    public static void Save()
    {
        string p = path + "/save" + GameSession.instance.curFile.ToString() + ".json";
        File.WriteAllText(p, JsonUtility.ToJson(GameSession.instance.data));
    }
    public static SaveData Load(int num)
    {
        string p = path + "/save" + num.ToString() + ".json";
        if (!File.Exists(p))
            return null;
        string json=File.ReadAllText(p);
        return JsonUtility.FromJson<SaveData>(json);
    }
    public static void Delete(int num)
    {
        string p = path + "/save" + num.ToString() + ".json";
        File.Delete(p);
    }
}
