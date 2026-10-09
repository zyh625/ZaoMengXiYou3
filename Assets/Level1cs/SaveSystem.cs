using System.IO;
using UnityEngine;

public static class SaveSystem
{
    public static string path = Path.Combine(Application.persistentDataPath, "save.json");
    public static void Save(SaveData data)
    {
        File.WriteAllText(path, JsonUtility.ToJson(data));
    }
    public static SaveData Load()
    {
        if (!File.Exists(path))
            return null;
        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<SaveData>(json);
    }
}
