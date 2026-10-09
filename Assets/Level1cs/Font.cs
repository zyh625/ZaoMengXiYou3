using UnityEngine;

public static class Font
{
    private static Sprite[] sprites;
    private static void Initialize()
    {
        if (sprites != null) return;
        sprites = new Sprite[10];
        Sprite[] loaded = Resources.LoadAll<Sprite>("Digits");
        foreach(Sprite sprite in loaded)
        {
            if (int.TryParse(sprite.name, out int number)
    && number >= 0 && number <= 9)
            {
                sprites[number] = sprite;
            }
        }
    }
    public static Sprite GetSprite(int number)
    {
        if (number < 0 || number >= 10) return null;
        Initialize();
        return sprites[number];
    }
}
