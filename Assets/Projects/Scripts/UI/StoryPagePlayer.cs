using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public sealed class StoryPagePlayer : MonoBehaviour
{
    [SerializeField] private Sprite[] pages;
    private Image pageImage;
    private int pageIndex;
    private int openedOnFrame;

    private void Start()
    {
        pageImage = GetComponent<Image>();
        if (pages == null || pages.Length == 0)
        {
            Debug.LogError("漫画场景未配置页面", this);
            enabled = false;
            return;
        }

        openedOnFrame = Time.frameCount;
        pageImage.sprite = pages[0];
    }

    private void Update()
    {
        // Ignore the input that opened the scene, then advance at most once per frame.
        if (Time.frameCount == openedOnFrame || !AdvancePressed()) return;

        pageIndex++;
        if (pageIndex < pages.Length)
        {
            pageImage.sprite = pages[pageIndex];
            return;
        }

        enabled = false;
        if (GameSession.instance != null) GameSession.instance.priScene = gameObject.scene.name;
        SceneManager.LoadScene("MapScene");
    }

    private static bool AdvancePressed()
    {
        if (Mouse.current?.leftButton.wasPressedThisFrame == true ||
            Touchscreen.current?.primaryTouch.press.wasPressedThisFrame == true) return true;

        var keyboard = Keyboard.current;
        if (keyboard == null) return false;
        foreach (var key in keyboard.allKeys)
            if (key.wasPressedThisFrame) return true;
        return false;
    }
}
