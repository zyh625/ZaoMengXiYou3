using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

//一键打包 Windows 版：构建列表里勾选的全部场景，正式包（不带 Development Build 水印，数据用 LZ4HC 压缩），输出到项目根目录的 Builds/LunHuiXiXing。
//演示时游戏连接本机服务器（http://localhost），所以打包前把“允许 HTTP 下载”设为始终允许；窗口设为 800x600、可调大小、4:3。
//编辑器里：菜单 造梦西游/打包 Windows 版。命令行（需先关闭编辑器）：Unity.exe -batchmode -quit -projectPath <项目> -executeMethod GameBuilder.BuildBatch
public static class GameBuilder
{
    private const string Folder = "Builds/LunHuiXiXing";
    private const string ExeName = "LunHuiXiXing.exe";

    [MenuItem("造梦西游/打包 Windows 版", false, 100)]
    public static void BuildFromMenu()
    {
        BuildReport report = Build();
        bool ok = report.summary.result == BuildResult.Succeeded;
        string exe = Path.GetFullPath(Path.Combine(Folder, ExeName));
        EditorUtility.DisplayDialog("打包 Windows 版",
            ok ? $"打包成功，用时 {report.summary.totalTime.TotalSeconds:0} 秒。\n{exe}" : $"打包失败：{report.summary.result}，错误 {report.summary.totalErrors} 个，详见 Console。", "确定");
        if (ok) EditorUtility.RevealInFinder(exe);
    }

    public static void BuildBatch()
    {
        BuildReport report = Build();
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    private static BuildReport Build()
    {
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;//本机服务器是 http
        //窗口：默认 800x600 窗口模式，可拖动调整大小；只支持 4:3，运行时由 WindowAspect 保持比例
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.defaultScreenWidth = 800;
        PlayerSettings.defaultScreenHeight = 600;
        PlayerSettings.resizableWindow = true;//Unity 6 已去掉“支持的宽高比”设置，4:3 由运行时的 WindowAspect 保证
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
        Directory.CreateDirectory(Folder);
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(Folder, ExeName),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.CompressWithLz4HC,//数据文件无损压缩：大量非 4 的倍数尺寸的图没法用显卡压缩，透明区域多，LZ4HC 能压掉一大半
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[打包] {report.summary.result}：{scenes.Length} 个场景，{report.summary.totalSize / (1024f * 1024f):0.0} MB，" +
                  $"错误 {report.summary.totalErrors}，警告 {report.summary.totalWarnings}，输出 {Path.GetFullPath(options.locationPathName)}");
        return report;
    }
}
