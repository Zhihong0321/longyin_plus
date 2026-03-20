using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("codex.longyin.skilltalenttracer", "LongYin Skill Talent Grant", "1.0.0")]
public sealed class LongYinSkillTalentTracerPlugin : BasePlugin
{
    internal static ManualLogSource LoggerInstance = null!;

    private static ConfigEntry<bool> _enabled = null!;
    private static ConfigEntry<int> _levelThreshold = null!;
    private static ConfigEntry<float> _tierPointMultiplier = null!;
    private static ConfigEntry<bool> _playerOnly = null!;
    private Harmony? _harmony;

    private sealed class SkillGrantState
    {
        public bool IsEligible { get; init; }
        public HeroData? Hero { get; init; }
        public KungfuSkillLvData? Skill { get; init; }
        public int SkillLevelBefore { get; init; }
        public int SkillTier { get; init; }
    }

    public override void Load()
    {
        LoggerInstance = Log;
        _enabled = Config.Bind("SkillTalent", "Enabled", true, "Turns the skill-to-talent grant on or off.");
        _levelThreshold = Config.Bind("SkillTalent", "LevelThreshold", 10, "Skill level that triggers the talent-point grant.");
        _tierPointMultiplier = Config.Bind("SkillTalent", "TierPointMultiplier", 2f, "Multiplies the granted talent points by skill tier.");
        _playerOnly = Config.Bind("SkillTalent", "PlayerOnly", true, "Only grant talent points when the player hero levels the skill.");

        if (!_enabled.Value)
        {
            return;
        }

        _harmony = new Harmony("codex.longyin.skilltalenttracer");
        PatchDeclaredByName(
            typeof(HeroData),
            nameof(SkillExpPrefix),
            nameof(SkillExpPostfix),
            "AddSkillBookExp",
            "AddSkillFightExp",
            "CheckSkillUpgrade",
            "UpgradeSkill");
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
        var prefix = prefixName == null ? null : AccessTools.Method(typeof(LongYinSkillTalentTracerPlugin), prefixName);
        var postfix = postfixName == null ? null : AccessTools.Method(typeof(LongYinSkillTalentTracerPlugin), postfixName);

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

    private static void SkillExpPrefix(MethodBase __originalMethod, HeroData? __instance, object[] __args, out SkillGrantState __state)
    {
        __state = new SkillGrantState();

        if (__instance == null || (_playerOnly.Value && !IsPlayerHero(__instance)))
        {
            return;
        }

        var skill = FindSkillArg(__args);
        if (skill == null)
        {
            return;
        }

        __state = new SkillGrantState
        {
            IsEligible = true,
            Hero = __instance,
            Skill = skill,
            SkillLevelBefore = TryGetSkillLevel(skill),
            SkillTier = Math.Max(1, TryGetSkillTier(skill))
        };
    }

    private static void SkillExpPostfix(MethodBase __originalMethod, HeroData? __instance, object[] __args, SkillGrantState __state)
    {
        if (!__state.IsEligible || __instance == null || __state.Skill == null)
        {
            return;
        }

        var skill = FindSkillArg(__args) ?? __state.Skill;
        var skillLevelAfter = TryGetSkillLevel(skill);

        if (__state.SkillLevelBefore < _levelThreshold.Value && skillLevelAfter >= _levelThreshold.Value)
        {
            var grant = ResolveTalentGrant(__state.SkillTier);
            ApplyTalentGrant(__instance, grant);
        }
    }

    private static void ApplyTalentGrant(HeroData hero, float grant)
    {
        if (grant <= 0f)
        {
            return;
        }

        try
        {
            hero.ChangeTagPoint(grant, true);
            return;
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"ChangeTagPoint grant failed for {DescribeHero(hero)}: {ex.Message}");
        }

        TrySetTalentPoint(hero, TryGetTalentPoint(hero) + grant);
    }

    private static bool TrySetTalentPoint(HeroData hero, float value)
    {
        var candidates = new[] { "heroTagPoint", "HeroTagPoint" };
        foreach (var name in candidates)
        {
            try
            {
                var property = hero.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(hero, Convert.ChangeType(value, property.PropertyType));
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                var field = hero.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(hero, Convert.ChangeType(value, field.FieldType));
                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }

    private static float ResolveTalentGrant(int skillTier)
    {
        var scaled = Mathf.Max(1f, skillTier * Mathf.Max(0f, _tierPointMultiplier.Value));
        return Mathf.Max(1f, Mathf.Round(scaled));
    }

    private static KungfuSkillLvData? FindSkillArg(object[]? args)
    {
        if (args == null)
        {
            return null;
        }

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is KungfuSkillLvData skill)
            {
                return skill;
            }
        }

        return null;
    }

    private static bool IsPlayerHero(HeroData hero)
    {
        return TryGetHeroId(hero).HasValue && TryGetHeroId(TryGetPlayerHero()) == TryGetHeroId(hero);
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

    private static int TryGetSkillLevel(KungfuSkillLvData? skill)
    {
        if (skill == null)
        {
            return 0;
        }

        try
        {
            return Math.Max(0, skill.lv);
        }
        catch
        {
            var value = SafeProperty(skill, "lv") ?? SafeField(skill, "lv");
            return TryConvertToInt(value) ?? 0;
        }
    }

    private static int TryGetSkillTier(KungfuSkillLvData? skill)
    {
        if (skill == null)
        {
            return 1;
        }

        try
        {
            var skillData = skill.DataBase();
            if (skillData != null)
            {
                var rareLv = SafeProperty(skillData, "rareLv") ?? SafeField(skillData, "rareLv");
                var converted = TryConvertToInt(rareLv);
                if (converted.HasValue && converted.Value > 0)
                {
                    return converted.Value;
                }
            }
        }
        catch
        {
        }

        var fallback = SafeProperty(skill, "rareLv") ?? SafeField(skill, "rareLv");
        return TryConvertToInt(fallback) ?? 1;
    }

    private static string TryGetSkillName(KungfuSkillLvData? skill)
    {
        if (skill == null)
        {
            return "未知技能";
        }

        try
        {
            var name = skill.Name(false);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }
        catch
        {
        }

        return $"技能{TryGetSkillId(skill)}";
    }

    private static int TryGetSkillId(KungfuSkillLvData? skill)
    {
        if (skill == null)
        {
            return -1;
        }

        try
        {
            return skill.skillID;
        }
        catch
        {
            var value = SafeProperty(skill, "skillID") ?? SafeField(skill, "skillID");
            return TryConvertToInt(value) ?? -1;
        }
    }

    private static float TryGetTalentPoint(HeroData? hero)
    {
        if (hero == null)
        {
            return 0f;
        }

        try
        {
            return hero.heroTagPoint;
        }
        catch
        {
            var value = SafeProperty(hero, "heroTagPoint") ?? SafeField(hero, "heroTagPoint");
            return TryConvertToFloat(value) ?? 0f;
        }
    }

    private static int? TryGetHeroId(HeroData? hero)
    {
        if (hero == null)
        {
            return null;
        }

        try
        {
            return hero.heroID;
        }
        catch
        {
            var value = SafeProperty(hero, "heroID") ?? SafeField(hero, "heroID");
            return TryConvertToInt(value);
        }
    }

    private static object? SafeProperty(object target, string name)
    {
        try
        {
            var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            return property == null ? null : property.GetValue(target);
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

    private static int? TryConvertToInt(object? value)
    {
        return value switch
        {
            null => null,
            int intValue => intValue,
            long longValue => (int)longValue,
            short shortValue => shortValue,
            byte byteValue => byteValue,
            float floatValue => Mathf.RoundToInt(floatValue),
            double doubleValue => (int)Math.Round(doubleValue),
            string text when int.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }

    private static float? TryConvertToFloat(object? value)
    {
        return value switch
        {
            null => null,
            float floatValue => floatValue,
            double doubleValue => (float)doubleValue,
            int intValue => intValue,
            long longValue => longValue,
            string text when float.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }

    private static string DescribeHero(HeroData? hero)
    {
        if (hero == null)
        {
            return "hero=null";
        }

        try
        {
            return $"name={TryGetHeroName(hero)}, heroID={hero.heroID}";
        }
        catch
        {
            return "hero=unavailable";
        }
    }

    private static string TryGetHeroName(HeroData? hero)
    {
        if (hero == null)
        {
            return "unknown";
        }

        var nameValue = SafeProperty(hero, "heroName") ?? SafeProperty(hero, "HeroName") ?? SafeField(hero, "heroName");
        return nameValue?.ToString() ?? "unknown";
    }

}
