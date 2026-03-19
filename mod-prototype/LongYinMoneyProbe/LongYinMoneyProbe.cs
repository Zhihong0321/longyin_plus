using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

[BepInPlugin("codex.longyin.moneyprobe", "LongYin Stamina Lock", "0.4.1")]
public sealed class LongYinMoneyProbePlugin : BasePlugin
{
    internal static ManualLogSource LoggerInstance = null!;

    private static ConfigEntry<bool> _lockExploreStamina = null!;
    private Harmony? _harmony;

    public override void Load()
    {
        LoggerInstance = Log;
        _lockExploreStamina = Config.Bind("Exploration", "LockStamina", true, "Prevents exploration stamina from decreasing.");

        _harmony = new Harmony("codex.longyin.moneyprobe");

        PatchMethod(typeof(ExploreController), "ChangeMoveStep", new[] { typeof(int) }, nameof(ChangeMoveStepPrefix), null);
        PatchMethod(typeof(ExploreController), "ChangeMoveStep", new[] { typeof(int), typeof(bool) }, nameof(ChangeMoveStepWithBoolPrefix), null);

        PatchMethod(typeof(HeroData), nameof(HeroData.ChangePower), new[] { typeof(float), typeof(bool) }, nameof(TracePrefix), nameof(TracePostfix));
        PatchMethod(typeof(HeroData), nameof(HeroData.AddSkillBookExp), new[] { typeof(float), typeof(KungfuSkillLvData), typeof(bool) }, nameof(TracePrefix), nameof(TracePostfix));

        PatchDeclaredByName(typeof(ReadBookController), nameof(TracePrefix), nameof(TracePostfix),
            "ReadBook", "ReadBookChoosen", "ReadBookMoney", "ChooseReadBook", "AutoReadBook", "FinishReadBook");
        PatchDeclaredByName(typeof(StudySkillController), nameof(TracePrefix), nameof(TracePostfix),
            "StudyDayCost", "StudySkill", "FinishStudySkill");
        PatchDeclaredByName(typeof(KungfuSkillLvData), nameof(TracePrefix), nameof(TracePostfix),
            "Exp", "ExpNum", "ExpMax");

        Log.LogInfo("LongYin Stamina Lock loaded.");
        Log.LogInfo("Exploration stamina lock is active.");
        Log.LogInfo("Study trace mode is active. Do one read-book action, quit, and inspect the log.");
    }

    private void PatchDeclaredByName(Type type, string? prefixName, string? postfixName, params string[] methodNames)
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            foreach (var methodName in methodNames)
            {
                if (method.Name == methodName)
                {
                    PatchMethod(type, method.Name, GetParameterTypes(method), prefixName, postfixName);
                }
            }
        }
    }

    private static Type[] GetParameterTypes(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var result = new Type[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            result[i] = parameters[i].ParameterType;
        }

        return result;
    }

    private void PatchMethod(Type type, string methodName, Type[] parameterTypes, string? prefixName, string? postfixName)
    {
        var target = AccessTools.Method(type, methodName, parameterTypes);
        var prefix = prefixName == null ? null : AccessTools.Method(typeof(LongYinMoneyProbePlugin), prefixName);
        var postfix = postfixName == null ? null : AccessTools.Method(typeof(LongYinMoneyProbePlugin), postfixName);

        if (target == null)
        {
            Log.LogWarning($"Could not patch {type.Name}.{methodName}({parameterTypes.Length} params)");
            return;
        }

        _harmony!.Patch(
            target,
            prefix: prefix == null ? null : new HarmonyMethod(prefix),
            postfix: postfix == null ? null : new HarmonyMethod(postfix));
        Log.LogInfo($"Patched {type.Name}.{target.Name}({target.GetParameters().Length} params)");
    }

    private static void ChangeMoveStepPrefix(ref int num)
    {
        if (_lockExploreStamina.Value && num < 0)
        {
            LoggerInstance.LogInfo($"Blocked exploration stamina change {num}.");
            num = 0;
        }
    }

    private static void ChangeMoveStepWithBoolPrefix(ref int num, bool showText)
    {
        ChangeMoveStepPrefix(ref num);
    }

    private static void TracePrefix(MethodBase __originalMethod, object? __instance, object[] __args, out string __state)
    {
        __state = DescribeState(__instance, __args);
        LoggerInstance.LogInfo($"TRACE ENTER {DescribeMethod(__originalMethod)} stateBefore={__state} args={DescribeArgs(__args)}");
    }

    private static void TracePostfix(MethodBase __originalMethod, object? __instance, object[] __args, string __state)
    {
        var after = DescribeState(__instance, __args);
        LoggerInstance.LogInfo($"TRACE EXIT  {DescribeMethod(__originalMethod)} stateBefore={__state} stateAfter={after}");
    }

    private static string DescribeMethod(MethodBase method)
    {
        return $"{method.DeclaringType?.Name}.{method.Name}";
    }

    private static string DescribeArgs(object[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return "[]";
        }

        var parts = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            parts.Add($"{i}:{SafeFormatValue(args[i])}");
        }

        return "[" + string.Join(", ", parts) + "]";
    }

    private static string DescribeState(object? instance, object[]? args)
    {
        var parts = new List<string>();

        var playerHero = TryGetPlayerHero();
        if (playerHero != null)
        {
            parts.Add(DescribeObject("player", playerHero));
        }

        if (instance != null)
        {
            parts.Add(DescribeObject(instance.GetType().Name, instance));
        }

        if (args != null)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] != null && (args[i] is HeroData || args[i] is KungfuSkillLvData || args[i] is ItemData))
                {
                    parts.Add(DescribeObject($"arg{i}", args[i]!));
                }
            }
        }

        return string.Join(" | ", parts);
    }

    private static string DescribeObject(string label, object obj)
    {
        var interesting = new[] { "power", "maxPower", "nowPower", "leftPower", "hp", "exp", "expNum", "expMax", "lv", "level", "studyDayCost", "readCostMoney", "id", "skillID" };
        var parts = new List<string>();

        foreach (var name in interesting)
        {
            var value = SafeProperty(obj, name);
            if (value == null)
            {
                value = SafeField(obj, name);
            }

            if (value != null)
            {
                parts.Add($"{label}.{name}={SafeFormatValue(value)}");
            }
        }

        return parts.Count == 0 ? $"{label}=<{obj.GetType().Name}>" : string.Join("; ", parts);
    }

    private static object? SafeProperty(object target, string name)
    {
        try
        {
            var prop = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            return prop == null ? null : prop.GetValue(target);
        }
        catch
        {
            return null;
        }
    }

    private static object? SafeField(object target, string name)
    {
        try
        {
            var field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            return field == null ? null : field.GetValue(target);
        }
        catch
        {
            return null;
        }
    }

    private static HeroData? TryGetPlayerHero()
    {
        try
        {
            return GameController.Instance?.worldData?.Player();
        }
        catch
        {
            return null;
        }
    }

    private static string SafeFormatValue(object? value)
    {
        if (value == null)
        {
            return "null";
        }

        return value switch
        {
            float f => f.ToString("0.###"),
            double d => d.ToString("0.###"),
            _ => value.ToString() ?? "<null-string>"
        };
    }
}
