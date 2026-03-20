using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace LongYinHorseStaminaMultiplier;

[BepInPlugin("codex.longyin.horsestamina", "LongYin Horse Stamina Multiplier", "1.0.0")]
public sealed class HorseStaminaMultiplierPlugin : BasePlugin
{
    private static ConfigEntry<float> HorseStaminaMultiplier = null!;
    private Harmony? _harmony;

    public override void Load()
    {
        HorseStaminaMultiplier = Config.Bind(
            "WorldMapHorse",
            "StaminaMultiplier",
            1f,
            "Scales horse stamina drain and recovery. Values above 1 make the horse last longer and refill more slowly."
        );

        _harmony = new Harmony("codex.longyin.horsestamina");
        _harmony.PatchAll(typeof(HorseStaminaMultiplierPlugin).Assembly);

        Log.LogInfo($"Horse stamina multiplier starts at x{HorseStaminaMultiplier.Value:0.###}.");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(HorseData), nameof(HorseData.ChangeNowPower))]
    private static void ChangeNowPowerPrefix(HorseData __instance, ref float delta)
    {
        if (__instance == null || !__instance.equiped)
        {
            return;
        }

        float multiplier = Math.Max(0.01f, HorseStaminaMultiplier.Value);
        if (Math.Abs(multiplier - 1f) < 0.001f)
        {
            return;
        }

        delta /= multiplier;
    }
}
