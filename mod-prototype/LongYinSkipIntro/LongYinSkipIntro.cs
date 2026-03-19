using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine.Video;

[BepInPlugin("codex.longyin.skipintro", "LongYin Skip Intro", "1.0.0")]
public sealed class LongYinSkipIntroPlugin : BasePlugin
{
    internal static ManualLogSource LoggerInstance = null!;

    private static ConfigEntry<bool> _enabled = null!;
    private static bool _startupIntroSkipped;
    private Harmony? _harmony;

    public override void Load()
    {
        LoggerInstance = Log;
        _enabled = Config.Bind("General", "Enabled", true, "Skips the startup intro video and jumps to the title flow.");

        if (!_enabled.Value)
        {
            Log.LogInfo("LongYin Skip Intro loaded with intro skipping disabled.");
            return;
        }

        _harmony = new Harmony("codex.longyin.skipintro");
        PatchMethod(typeof(EnterSceneController), nameof(EnterSceneController.Start), Type.EmptyTypes, null, nameof(EnterSceneStartPostfix));
        PatchMethod(typeof(EnterSceneController), nameof(EnterSceneController.Update), Type.EmptyTypes, null, nameof(EnterSceneUpdatePostfix));

        Log.LogInfo("LongYin Skip Intro loaded. Startup intro video will be skipped.");
    }

    private void PatchMethod(Type type, string methodName, Type[] parameterTypes, string? prefixName, string? postfixName)
    {
        var target = AccessTools.Method(type, methodName, parameterTypes);
        var prefix = prefixName == null ? null : AccessTools.Method(typeof(LongYinSkipIntroPlugin), prefixName);
        var postfix = postfixName == null ? null : AccessTools.Method(typeof(LongYinSkipIntroPlugin), postfixName);

        if (target == null)
        {
            Log.LogWarning($"Could not patch {type.Name}.{methodName}({parameterTypes.Length} params).");
            return;
        }

        _harmony!.Patch(
            target,
            prefix: prefix == null ? null : new HarmonyMethod(prefix),
            postfix: postfix == null ? null : new HarmonyMethod(postfix));
    }

    private static void EnterSceneStartPostfix(EnterSceneController __instance)
    {
        TrySkipStartupIntro(__instance, "Start");
    }

    private static void EnterSceneUpdatePostfix(EnterSceneController __instance)
    {
        if (_startupIntroSkipped || __instance == null || __instance.videoPlayFinished)
        {
            return;
        }

        TrySkipStartupIntro(__instance, "Update");
    }

    private static void TrySkipStartupIntro(EnterSceneController controller, string source)
    {
        if (_startupIntroSkipped || !_enabled.Value || controller == null)
        {
            return;
        }

        VideoPlayer? logoVideo = null;

        try
        {
            logoVideo = controller.logoVideo;
            if (logoVideo == null)
            {
                return;
            }

            try
            {
                logoVideo.Stop();
            }
            catch (Exception stopEx)
            {
                LoggerInstance.LogDebug($"Skip Intro could not stop the logo video cleanly: {stopEx.Message}");
            }

            controller.VideoPlayFinished(logoVideo);
            _startupIntroSkipped = true;
            LoggerInstance.LogInfo($"Skipped startup intro video via EnterSceneController.{source}.");
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Skip Intro failed during EnterSceneController.{source}: {ex.Message}");
        }
    }
}
