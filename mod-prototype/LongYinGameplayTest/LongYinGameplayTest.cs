using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("codex.longyin.gameplaytest", "LongYin Gameplay Test", "1.2.0")]
public sealed class LongYinGameplayTestPlugin : BasePlugin
{
    internal static ManualLogSource LoggerInstance = null!;

    private static ConfigEntry<bool> _enabled = null!;
    private static ConfigEntry<bool> _enableMoneyBonusTest = null!;
    private static ConfigEntry<int> _bonusAmount = null!;
    private static ConfigEntry<bool> _highlightCorrectTreasure = null!;
    private static ConfigEntry<bool> _forceCorrectTreasureSelection = null!;
    private Harmony? _harmony;

    public override void Load()
    {
        LoggerInstance = Log;
        _enabled = Config.Bind("General", "Enabled", false, "Enables experimental gameplay test hooks.");
        _enableMoneyBonusTest = Config.Bind("General", "EnableMoneyBonusTest", false, "Turns on the old positive-money bonus test.");
        _bonusAmount = Config.Bind("General", "BonusAmount", 1, "Extra money added on positive player gains while the test mod is enabled.");
        _highlightCorrectTreasure = Config.Bind("General", "HighlightCorrectTreasure", false, "Auto-selects the correct treasure in the identify mini-game without pressing confirm.");
        _forceCorrectTreasureSelection = Config.Bind("General", "ForceCorrectTreasureSelection", false, "Replaces any clicked treasure with the correct one before the game processes the choice.");

        if (!_enabled.Value)
        {
            Log.LogInfo("LongYin Gameplay Test loaded with test mode disabled.");
            return;
        }

        _harmony = new Harmony("codex.longyin.gameplaytest");
        var target = AccessTools.Method(typeof(HeroData), nameof(HeroData.ChangeMoney), new[] { typeof(int), typeof(bool) });
        var postfix = AccessTools.Method(typeof(LongYinGameplayTestPlugin), nameof(ChangeMoneyPostfix));

        if (target == null || postfix == null)
        {
            Log.LogWarning("Could not patch HeroData.ChangeMoney(int, bool).");
        }
        else
        {
            _harmony.Patch(target, postfix: new HarmonyMethod(postfix));
        }

        var correctTreasureSetter = AccessTools.Method(typeof(IdentifyMatchController), "set_correctTreasure", new[] { typeof(Il2CppSystem.Collections.Generic.List<GameObject>) });
        var correctTreasurePostfix = AccessTools.Method(typeof(LongYinGameplayTestPlugin), nameof(CorrectTreasurePostfix));

        if (correctTreasureSetter == null || correctTreasurePostfix == null)
        {
            Log.LogWarning("Could not patch IdentifyMatchController.set_correctTreasure(List<GameObject>).");
        }
        else
        {
            _harmony.Patch(correctTreasureSetter, postfix: new HarmonyMethod(correctTreasurePostfix));
        }

        var chooseTarget = AccessTools.Method(typeof(IdentifyMatchController), nameof(IdentifyMatchController.SetNowChooseTreasure), new[] { typeof(GameObject) });
        var choosePrefix = AccessTools.Method(typeof(LongYinGameplayTestPlugin), nameof(SetNowChooseTreasurePrefix));
        if (chooseTarget == null || choosePrefix == null)
        {
            Log.LogWarning("Could not patch IdentifyMatchController.SetNowChooseTreasure(GameObject).");
        }
        else
        {
            _harmony.Patch(chooseTarget, prefix: new HarmonyMethod(choosePrefix));
        }

        var submitTarget = AccessTools.Method(typeof(IdentifyMatchController), nameof(IdentifyMatchController.SureButtonClicked), System.Type.EmptyTypes);
        var submitPrefix = AccessTools.Method(typeof(LongYinGameplayTestPlugin), nameof(SureButtonClickedPrefix));
        if (submitTarget == null || submitPrefix == null)
        {
            Log.LogWarning("Could not patch IdentifyMatchController.SureButtonClicked().");
        }
        else
        {
            _harmony.Patch(submitTarget, prefix: new HarmonyMethod(submitPrefix));
        }

        Log.LogInfo("LongYin Gameplay Test loaded with experimental gameplay hooks enabled.");
    }

    private static void ChangeMoneyPostfix(HeroData __instance, int num)
    {
        if (!_enabled.Value || !_enableMoneyBonusTest.Value || num <= 0 || _bonusAmount.Value <= 0)
        {
            return;
        }

        try
        {
            var player = GameController.Instance?.worldData?.Player();
            if (player == null || __instance != player)
            {
                return;
            }

            __instance.ChangeMoney(_bonusAmount.Value, false);
            LoggerInstance.LogInfo($"Gameplay test added bonus money {_bonusAmount.Value} on gain {num}.");
        }
        catch (System.Exception ex)
        {
            LoggerInstance.LogWarning($"Gameplay test failed while applying bonus: {ex.Message}");
        }
    }

    private static void CorrectTreasurePostfix(IdentifyMatchController __instance)
    {
        if (!_enabled.Value || !_highlightCorrectTreasure.Value)
        {
            return;
        }

        try
        {
            if (__instance == null || __instance.correctTreasure == null || __instance.correctTreasure.Count == 0)
            {
                return;
            }

            var correct = __instance.correctTreasure[0];
            if (correct == null)
            {
                return;
            }

            if (__instance.nowChooseTreasure == correct)
            {
                return;
            }

            __instance.SetNowChooseTreasure(correct);

            var itemIcon = correct.GetComponent<ItemIconController>();
            var itemName = itemIcon?.itemData?.name ?? correct.name;
            LoggerInstance.LogInfo($"Gameplay test highlighted correct treasure: {itemName}");
        }
        catch (System.Exception ex)
        {
            LoggerInstance.LogWarning($"Gameplay test failed while highlighting treasure: {ex.Message}");
        }
    }

    private static void SetNowChooseTreasurePrefix(IdentifyMatchController __instance, ref GameObject targetTreasure)
    {
        if (!_enabled.Value || !_forceCorrectTreasureSelection.Value)
        {
            return;
        }

        var correct = TryGetCorrectTreasure(__instance);
        if (correct == null)
        {
            return;
        }

        if (targetTreasure == correct)
        {
            return;
        }

        targetTreasure = correct;
    }

    private static void SureButtonClickedPrefix(IdentifyMatchController __instance)
    {
        if (!_enabled.Value || !_forceCorrectTreasureSelection.Value)
        {
            return;
        }

        try
        {
            var correct = TryGetCorrectTreasure(__instance);
            if (correct == null)
            {
                return;
            }

            if (__instance.nowChooseTreasure != correct)
            {
                __instance.SetNowChooseTreasure(correct);
                var itemIcon = correct.GetComponent<ItemIconController>();
                var itemName = itemIcon?.itemData?.name ?? correct.name;
                LoggerInstance.LogInfo($"Gameplay test forced correct treasure on submit: {itemName}");
            }
        }
        catch (System.Exception ex)
        {
            LoggerInstance.LogWarning($"Gameplay test failed while forcing treasure choice: {ex.Message}");
        }
    }

    private static GameObject? TryGetCorrectTreasure(IdentifyMatchController? controller)
    {
        if (controller == null || controller.correctTreasure == null || controller.correctTreasure.Count == 0)
        {
            return null;
        }

        return controller.correctTreasure[0];
    }
}
