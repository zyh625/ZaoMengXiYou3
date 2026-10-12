using System.IO;
using UnityEngine;

public static class SaveSystem
{
    public static string path = Application.persistentDataPath;
    public static void Save()
    {
        string p = path + "/save" + MainMenu.instance.data.file.ToString() + ".json";
        File.WriteAllText(p, JsonUtility.ToJson(MainMenu.instance.data));
        Debug.Log(p);
    }
    public static SaveData Load(int num)
    {
        string p = path + "/save" + num.ToString() + ".json";
        Debug.Log(p);
        if (!File.Exists(p))
            return null;
        File.ReadAllText(p);
        return JsonUtility.FromJson<SaveData>(p);
    }
    public static void Delete(int num)
    {
        string p = path + "/save" + num.ToString() + ".json";
        File.Delete(p);
    }
}
