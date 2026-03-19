using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("codex.longyin.battleturbo", "LongYin Battle Turbo", "1.1.1")]
public sealed class LongYinBattleTurboPlugin : BasePlugin
{
    internal static ManualLogSource LoggerInstance = null!;

    private static ConfigEntry<bool> _enabled = null!;
    private static ConfigEntry<KeyCode> _toggleHotkey = null!;
    private static ConfigEntry<float> _attackDelayMultiplier = null!;
    private static ConfigEntry<float> _entryDelayMultiplier = null!;
    private static ConfigEntry<float> _maxUnitMoveOneGridTime = null!;
    private static ConfigEntry<float> _forcedAiWaitTime = null!;
    private static ConfigEntry<bool> _disableCameraFocusTweens = null!;
    private static ConfigEntry<bool> _disableFocusAnimations = null!;
    private static ConfigEntry<bool> _disableHighlightAnimations = null!;
    private static ConfigEntry<bool> _disableHitAnimations = null!;
    private static ConfigEntry<bool> _disableSkillSpecialEffects = null!;
    private static ConfigEntry<bool> _disableBattleVoices = null!;
    private static ConfigEntry<bool> _traceMode = null!;
    private static bool _runtimeEnabled;

    private Harmony? _harmony;

    public override void Load()
    {
        LoggerInstance = Log;
        _enabled = Config.Bind("General", "Enabled", true, "Enables battle-only turbo simulation tweaks.");
        _toggleHotkey = Config.Bind("General", "ToggleHotkey", KeyCode.F8, "Hotkey that toggles battle turbo on or off while in game.");
        _attackDelayMultiplier = Config.Bind("Timing", "AttackDelayMultiplier", 0.1f, "Scales attack wait windows. Lower values make AUTO battles resolve faster.");
        _entryDelayMultiplier = Config.Bind("Timing", "EntryDelayMultiplier", 0.05f, "Scales battle-entry and move-to-grid delay windows.");
        _maxUnitMoveOneGridTime = Config.Bind("Timing", "MaxUnitMoveOneGridTime", 0.03f, "Caps how long a unit takes to move one grid during battle. Set to 0 to disable.");
        _forcedAiWaitTime = Config.Bind("Timing", "ForcedAiWaitTime", 0f, "Forces AI think/wait time to this value while battle is running.");
        _disableCameraFocusTweens = Config.Bind("Visuals", "DisableCameraFocusTweens", true, "Skips camera focus tweening during battle actions.");
        _disableFocusAnimations = Config.Bind("Visuals", "DisableFocusAnimations", true, "Skips target focus animations on battle units.");
        _disableHighlightAnimations = Config.Bind("Visuals", "DisableHighlightAnimations", true, "Skips unit highlight animations in battle.");
        _disableHitAnimations = Config.Bind("Visuals", "DisableHitAnimations", false, "Skips hit reaction animations. Turn on only if you want maximum speed.");
        _disableSkillSpecialEffects = Config.Bind("Visuals", "DisableSkillSpecialEffects", true, "Skips spawning named battle special effects.");
        _disableBattleVoices = Config.Bind("Audio", "DisableBattleVoices", true, "Skips battle voice and action audio calls from units.");
        _traceMode = Config.Bind("Debug", "TraceMode", false, "Logs battle turbo adjustments when they are applied.");
        _runtimeEnabled = _enabled.Value;

        _harmony = new Harmony("codex.longyin.battleturbo");

        PatchMethod(typeof(BattleController), "Update", Type.EmptyTypes, null, nameof(BattleControllerUpdatePostfix));
        PatchMethod(typeof(BattleController), nameof(BattleController.HeroEnterBattleFieldCoroutine), new[] { typeof(HeroData), typeof(BattleTeam), typeof(GridUnitData), typeof(int), typeof(float) }, nameof(HeroEnterBattleFieldCoroutinePrefix), null);
        PatchMethod(typeof(BattleController), nameof(BattleController.HeroEnterGridDelay), new[] { typeof(BattleUnit), typeof(GridUnitData), typeof(float) }, nameof(HeroEnterGridDelayPrefix), null);
        PatchMethod(typeof(BattleController), nameof(BattleController.GetBattleUnitAttackHitDelay), new[] { typeof(GridUnitData) }, null, nameof(GetBattleUnitAttackHitDelayPostfix));
        PatchMethod(typeof(BattleController), nameof(BattleController.BattleUnitAttackHit), new[] { typeof(GridUnitData), typeof(float) }, nameof(BattleUnitAttackHitPrefix), null);
        PatchMethod(typeof(BattleController), nameof(BattleController.BattleUnitAttackEnd), new[] { typeof(float) }, nameof(BattleUnitAttackEndPrefix), null);
        PatchMethod(typeof(BattleController), nameof(BattleController.TweenFocusTarget), new[] { typeof(Vector3), typeof(float) }, nameof(TweenFocusTargetPrefix), null);
        PatchMethod(typeof(BattleController), nameof(BattleController.CreateSpeEffect), new[] { typeof(SkillSpeEffectTargetType), typeof(GameObject), typeof(string) }, nameof(CreateSpeEffectPrefix), null);
        PatchMethod(typeof(BattleController), nameof(BattleController.CreateSpeEffect), new[] { typeof(SkillSpeEffectTargetType), typeof(GameObject), typeof(string), typeof(float) }, nameof(CreateSpeEffectPrefix), null);
        PatchMethod(typeof(BattleController), nameof(BattleController.CreateSpeEffect), new[] { typeof(SkillSpeEffectTargetType), typeof(GameObject), typeof(string), typeof(float), typeof(Vector3) }, nameof(CreateSpeEffectPrefix), null);

        PatchMethod(typeof(BattleUnit), nameof(BattleUnit.SetHighLightAnim), new[] { typeof(bool) }, nameof(SetHighLightAnimPrefix), null);
        PatchMethod(typeof(BattleUnit), nameof(BattleUnit.ShowFocusAnim), Type.EmptyTypes, nameof(ShowFocusAnimPrefix), null);
        PatchMethod(typeof(BattleUnit), nameof(BattleUnit.PlayHitAnim), Type.EmptyTypes, nameof(PlayHitAnimPrefix), null);
        PatchMethodByName(typeof(BattleUnit), nameof(BattleUnit.PlayHeroSound), nameof(PlayHeroSoundPrefix), null);

        Log.LogInfo("LongYin Battle Turbo loaded.");
        Log.LogInfo($"Enabled starts {(_enabled.Value ? "ON" : "OFF")}.");
        Log.LogInfo($"Toggle hotkey starts at {_toggleHotkey.Value}.");
        Log.LogInfo($"Attack delay multiplier starts at x{FormatFloat(Mathf.Max(0f, _attackDelayMultiplier.Value))}.");
        Log.LogInfo($"Entry delay multiplier starts at x{FormatFloat(Mathf.Max(0f, _entryDelayMultiplier.Value))}.");
        Log.LogInfo($"Max unit move one-grid config is {FormatFloat(Mathf.Max(0f, _maxUnitMoveOneGridTime.Value))} seconds, but unit move-time overriding is disabled for stability.");
        Log.LogInfo($"Forced AI wait time config is {FormatFloat(Mathf.Max(0f, _forcedAiWaitTime.Value))} seconds, but AI wait overriding is disabled for stability.");
        Log.LogInfo($"Disable camera focus tweens starts {(_disableCameraFocusTweens.Value ? "ON" : "OFF")}.");
        Log.LogInfo($"Disable focus animations starts {(_disableFocusAnimations.Value ? "ON" : "OFF")}.");
        Log.LogInfo($"Disable highlight animations starts {(_disableHighlightAnimations.Value ? "ON" : "OFF")}.");
        Log.LogInfo($"Disable hit animations starts {(_disableHitAnimations.Value ? "ON" : "OFF")}.");
        Log.LogInfo($"Disable skill special effects starts {(_disableSkillSpecialEffects.Value ? "ON" : "OFF")}.");
        Log.LogInfo($"Disable battle voices starts {(_disableBattleVoices.Value ? "ON" : "OFF")}.");
    }

    private void PatchMethod(Type type, string methodName, Type[] parameterTypes, string? prefixName, string? postfixName)
    {
        var target = AccessTools.Method(type, methodName, parameterTypes);
        var prefix = prefixName == null ? null : AccessTools.Method(typeof(LongYinBattleTurboPlugin), prefixName);
        var postfix = postfixName == null ? null : AccessTools.Method(typeof(LongYinBattleTurboPlugin), postfixName);

        if (target == null)
        {
            Log.LogWarning($"Could not patch {type.Name}.{methodName}({parameterTypes.Length} params).");
            return;
        }

        _harmony!.Patch(
            target,
            prefix: prefix == null ? null : new HarmonyMethod(prefix),
            postfix: postfix == null ? null : new HarmonyMethod(postfix));

        Log.LogInfo($"Patched {type.Name}.{target.Name}({target.GetParameters().Length} params).");
    }

    private void PatchMethodByName(Type type, string methodName, string? prefixName, string? postfixName)
    {
        var target = AccessTools.Method(type, methodName);
        var prefix = prefixName == null ? null : AccessTools.Method(typeof(LongYinBattleTurboPlugin), prefixName);
        var postfix = postfixName == null ? null : AccessTools.Method(typeof(LongYinBattleTurboPlugin), postfixName);

        if (target == null)
        {
            Log.LogWarning($"Could not patch {type.Name}.{methodName}.");
            return;
        }

        _harmony!.Patch(
            target,
            prefix: prefix == null ? null : new HarmonyMethod(prefix),
            postfix: postfix == null ? null : new HarmonyMethod(postfix));

        Log.LogInfo($"Patched {type.Name}.{target.Name}({target.GetParameters().Length} params).");
    }

    private static void BattleControllerUpdatePostfix(BattleController __instance)
    {
        TryHandleToggleHotkey();
    }

    private static void HeroEnterBattleFieldCoroutinePrefix(ref float waitTime)
    {
        if (!IsEnabled())
        {
            return;
        }

        waitTime = ScaleDelay(waitTime, _entryDelayMultiplier.Value);
    }

    private static void HeroEnterGridDelayPrefix(ref float delayTime)
    {
        if (!IsEnabled())
        {
            return;
        }

        delayTime = ScaleDelay(delayTime, _entryDelayMultiplier.Value);
    }

    private static void GetBattleUnitAttackHitDelayPostfix(ref float __result)
    {
        if (!IsEnabled())
        {
            return;
        }

        __result = ScaleDelay(__result, _attackDelayMultiplier.Value);
    }

    private static void BattleUnitAttackHitPrefix(ref float startDelay)
    {
        if (!IsEnabled())
        {
            return;
        }

        startDelay = ScaleDelay(startDelay, _attackDelayMultiplier.Value);
    }

    private static void BattleUnitAttackEndPrefix(ref float delayTime)
    {
        if (!IsEnabled())
        {
            return;
        }

        delayTime = ScaleDelay(delayTime, _attackDelayMultiplier.Value);
    }

    private static bool TweenFocusTargetPrefix()
    {
        return !(IsEnabled() && _disableCameraFocusTweens.Value);
    }

    private static bool CreateSpeEffectPrefix()
    {
        return !(IsEnabled() && _disableSkillSpecialEffects.Value);
    }

    private static bool SetHighLightAnimPrefix()
    {
        return !(IsEnabled() && _disableHighlightAnimations.Value);
    }

    private static bool ShowFocusAnimPrefix()
    {
        return !(IsEnabled() && _disableFocusAnimations.Value);
    }

    private static bool PlayHitAnimPrefix()
    {
        return !(IsEnabled() && _disableHitAnimations.Value);
    }

    private static bool PlayHeroSoundPrefix()
    {
        return !(IsEnabled() && _disableBattleVoices.Value);
    }

    private static void TryHandleToggleHotkey()
    {
        if (_toggleHotkey.Value == KeyCode.None || !Input.GetKeyDown(_toggleHotkey.Value))
        {
            return;
        }

        _runtimeEnabled = !_runtimeEnabled;
        LoggerInstance.LogInfo($"Battle turbo {(_runtimeEnabled ? "enabled" : "disabled")} from hotkey {_toggleHotkey.Value}.");
        PushPlayerLog($"Mod: Battle Turbo {(_runtimeEnabled ? "ON" : "OFF")}");

        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo($"Battle turbo hotkey toggled {(_runtimeEnabled ? "ON" : "OFF")}.");
        }
    }

    private static bool IsEnabled()
    {
        return _runtimeEnabled;
    }

    private static float ScaleDelay(float original, float multiplier)
    {
        if (original <= 0f)
        {
            return original;
        }

        return original * Mathf.Max(0f, multiplier);
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.###");
    }

    private static void PushPlayerLog(string text)
    {
        try
        {
            var infoController = InfoController.Instance;
            if (infoController != null)
            {
                infoController.AddInfo(InfoType.WorldInfo, text);
                infoController.AddInfo(InfoType.PersonalInfo, text);
                infoController.BuildInfoList();
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Battle turbo player log via InfoController failed: {ex.Message}");
        }

        try
        {
            var gameController = GameController.Instance;
            if (gameController != null)
            {
                gameController.ShowTextOnMouse(text, 28, Color.yellow);
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Battle turbo player log via ShowTextOnMouse failed: {ex.Message}");
        }
    }
}
