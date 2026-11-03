using UnityEngine;
using UnityEngine.Events;

/// <summary>Independent visual playback; damage, colliders and cast timing belong to the caller.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(SpriteManager))]
public sealed class IndependentSkillEffect : MonoBehaviour
{
    public enum FinishMode { Disable, Destroy, HoldLastFrame }

    [SerializeField] private SpriteManager spriteManager;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private SpriteData frames = new SpriteData();
    [SerializeField] private bool playOnEnable = true;
    [SerializeField, Min(0f)] private float lifetime;
    [SerializeField] private Vector2 velocity;
    [SerializeField] private FinishMode finishMode = FinishMode.Disable;
    [SerializeField] private UnityEvent completed = new UnityEvent();

    private Transform followTarget;
    private Vector3 followOffset;
    private Vector2 currentVelocity;
    private float elapsed, activeLifetime;
    private bool playing, paused;
    private uint playVersion;

    public bool IsPlaying => playing;
    public bool IsPaused => paused;
    public int FrameCount => frames != null && frames.sprites != null ? frames.sprites.Length : 0;
    public UnityEvent Completed => completed;

    private void Awake() => CacheComponents();

    private void CacheComponents()
    {
        if (spriteManager == null) spriteManager = GetComponent<SpriteManager>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (playOnEnable) Play();
    }

    public void Play() => Play(null, Vector3.zero, false, -1f);

    /// <param name="faceRight">Artwork faces left; true mirrors only the visual.</param>
    /// <param name="seconds">Negative uses prefab lifetime; zero allows a loop until explicitly stopped.</param>
    public void Play(Transform target, Vector3 offset, bool faceRight, float seconds = -1f)
    {
        CacheComponents();
        if (FrameCount == 0 || spriteManager == null || spriteRenderer == null)
        {
            playing = false;
            return;
        }
        // OnEnable can start playback when reactivating a pooled instance; this call then configures it.
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        playVersion++;
        elapsed = 0f;
        activeLifetime = seconds < 0f ? lifetime : Mathf.Max(0f, seconds);
        followTarget = target;
        followOffset = offset;
        currentVelocity = new Vector2(faceRight ? -velocity.x : velocity.x, velocity.y);
        playing = true;
        paused = false;
        spriteRenderer.enabled = true;
        spriteRenderer.flipX = faceRight;
        if (followTarget != null) transform.position = followTarget.position + followOffset;
        spriteManager.ChangeState(frames);
    }

    /// <summary>World-space velocity; does not add damage or collision behaviour.</summary>
    public void SetVelocity(Vector2 worldVelocity) => currentVelocity = worldVelocity;

    public void SetFollow(Transform target, Vector3 offset)
    {
        followTarget = target;
        followOffset = offset;
    }

    public void Pause()
    {
        paused = true;
        if (spriteManager != null) spriteManager.Pause();
    }

    public void Resume()
    {
        if (!playing) return;
        paused = false;
        if (spriteManager != null) spriteManager.Resume();
    }

    public void StopEffect() => Finish();

    private void Update()
    {
        if (!playing || paused) return;
        float dt = Time.deltaTime;
        elapsed += dt;
        if (followTarget != null) transform.position = followTarget.position + followOffset;
        else transform.position += new Vector3(currentVelocity.x, currentVelocity.y, 0f) * dt;
        bool expired = activeLifetime > 0f && elapsed >= activeLifetime;
        bool ended = !frames.loop && elapsed >= FrameCount / Mathf.Max(1f, frames.fps);
        if (expired || ended) Finish();
    }

    private void Finish()
    {
        if (!playing) return;
        uint version = playVersion;
        playing = false;
        paused = false;
        if (spriteManager != null) spriteManager.Pause();
        if (finishMode == FinishMode.HoldLastFrame && FrameCount > 0 && spriteRenderer != null)
            spriteRenderer.sprite = frames.sprites[FrameCount - 1];
        completed?.Invoke();
        // A completion listener may restart/reuse the same instance.
        if (playVersion != version || playing) return;
        if (finishMode == FinishMode.Destroy) Destroy(gameObject);
        else if (finishMode == FinishMode.Disable) gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        playing = false;
        paused = false;
        followTarget = null;
        currentVelocity = Vector2.zero;
        if (spriteManager != null) spriteManager.Pause();
    }
}
