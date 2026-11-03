using UnityEngine;
using UnityEngine.SceneManagement;

//只保存训练期内存角色数据；不替换GameSession、不调用Load/Save、不触碰原存档对象。
public static class TrainingSession
{
    public const string SceneName="TrainingScene";
    private static PlayerData[] players;
    public static bool Active {get;private set;}
    public static bool EffectsEnabled {get;set;}=true;
    public static int SuppressedReads {get;private set;}
    public static int SuppressedWrites {get;private set;}
    public static void Begin()
    {
        if(Active)return;Active=true;EffectsEnabled=true;SuppressedReads=SuppressedWrites=0;players=new PlayerData[4];
        for(int i=0;i<players.Length;i++)players[i]=new PlayerData{id=(PlayerId)i,skillLevels=new[]{1,1,1}};
        Debug.Log("TRAINING_BEGIN: isolated runtime data; save IO and BattleRun accumulation disabled.");
    }
    public static PlayerData Player(PlayerId id)=>players[(int)id];
    public static void BlockRead()=>SuppressedReads++;
    public static void BlockWrite()=>SuppressedWrites++;
    public static void End()
    {
        if(!Active)return;Active=false;players=null;EffectsEnabled=true;GamePause.Reset();
        Debug.Log("TRAINING_END: original session data, gold, experience and progress retained; no settlement.");
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()
    {Active=false;players=null;EffectsEnabled=true;SuppressedReads=SuppressedWrites=0;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]private static void Subscribe()
    {SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
    private static void Loaded(Scene scene,LoadSceneMode mode){if(Active && scene.name!=SceneName && mode==LoadSceneMode.Single)End();}
}
