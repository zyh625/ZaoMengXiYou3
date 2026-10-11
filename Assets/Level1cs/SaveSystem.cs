using System.IO;
using UnityEngine;

public static class SaveSystem
{
    public static string path = Path.Combine(Application.persistentDataPath, "save.json");
    public static void Save()
    {
        File.WriteAllText(path, JsonUtility.ToJson(MainMenu.instance.data));
        Debug.Log(path);
    }
    public static SaveData Load()
    {
        Debug.Log(path);
        if (!File.Exists(path))
            return null;
        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<SaveData>(json);

    }
}
