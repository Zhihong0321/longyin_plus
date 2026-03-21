using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[BepInPlugin("codex.longyin.tracedata", "LongYin Trace Data", "2.3.0")]
public sealed class LongYinTraceDataPlugin : BasePlugin
{
    internal static ManualLogSource LoggerInstance = null!;

    private static ConfigEntry<bool> _treasureFlowEnabled = null!;
    private static ConfigEntry<bool> _characterCreationFlowEnabled = null!;
    private static ConfigEntry<bool> _dialogFlowEnabled = null!;
    private static ConfigEntry<float> _dialogMonthlyLimitMultiplier = null!;
    private static ConfigEntry<bool> _forceAutoContinueEnabled = null!;
    private static ConfigEntry<KeyCode> _forceAutoContinueHotkey = null!;
    private static ConfigEntry<KeyCode> _forceUnstuckHotkey = null!;
    private static ConfigEntry<bool> _fastForwardSafetyEnabled = null!;
    private static ConfigEntry<int> _fastForwardStuckFrames = null!;
    private static readonly Dictionary<string, string> _dialogRequirementStateCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> _dialogChoiceRowStateCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> _dialogActionStateCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> _dialogMonthlyUseCounts = new(StringComparer.Ordinal);
    private static HeroData? _activeDialogHero;
    private static int _activeDialogHeroId = -1;
    private static int _activeDialogHeroForceLv = -1;
    private static string _activeDialogHeroName = string.Empty;
    private static bool _forceAutoContinueActive;
    private static int _lastDialogProgressFrame = -1;
    private static string _lastDialogProgressSignature = string.Empty;
    private static string _lastDialogStuckSignature = string.Empty;
    private Harmony? _harmony;

    public override void Load()
    {
        LoggerInstance = Log;
        _treasureFlowEnabled = Config.Bind("TreasureFlow", "Enabled", true, "Logs only exploration treasure/chest flow for focused reverse-engineering.");
        _characterCreationFlowEnabled = Config.Bind("CharacterCreationFlow", "Enabled", false, "Logs character-creation button presses, point-cost queries, and point-pool changes.");
        _dialogFlowEnabled = Config.Bind("DialogFlow", "Enabled", true, "Logs NPC dialog entry points, choice availability checks, row state changes, and fast-forward transitions.");
        _dialogMonthlyLimitMultiplier = Config.Bind("DialogFlow", "MonthlyLimitMultiplier", 3f, "Scales the per-NPC monthly dialog-use limit.");
        _forceAutoContinueEnabled = Config.Bind("DialogFlow", "ForceAutoContinueEnabled", true, "Forces dialog fast-forward when toggled on.");
        _forceAutoContinueHotkey = Config.Bind("DialogFlow", "ForceAutoContinueHotkey", KeyCode.P, "Hotkey used to toggle forced dialog fast-forward.");
        _forceUnstuckHotkey = Config.Bind("DialogFlow", "EmergencyUnstuckHotkey", KeyCode.F12, "Hotkey that clears forced fast-forward and tries to release a wedged dialog/chest session.");
        _fastForwardSafetyEnabled = Config.Bind("DialogFlow", "FastForwardSafetyEnabled", true, "Automatically disables forced fast-forward when a dialog appears stuck.");
        _fastForwardStuckFrames = Config.Bind("DialogFlow", "FastForwardStuckFrames", 180, "How many frames a dialog may remain unchanged before the tracer treats it as stuck.");
        _forceAutoContinueActive = _forceAutoContinueEnabled.Value;

        if (!_dialogFlowEnabled.Value)
        {
            Log.LogInfo("LongYin Trace Data loaded with dialog tracing disabled.");
            return;
        }

        _harmony = new Harmony("codex.longyin.tracedata");

        RegisterDialogFlowPatches();
        Log.LogInfo("Focused dialog fast-forward tracing enabled.");

        Log.LogInfo($"Trace session marker: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    }

    private void RegisterTreasureFlowPatches()
    {
        PatchMethod(typeof(ExploreController), nameof(ExploreController.ManageTileEvent), new[] { typeof(ExploreTileData) }, nameof(ExploreTreasurePrefix), nameof(ExploreTreasurePostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.ChangePlot), new[] { typeof(SinglePlotData) }, nameof(PlotTreasurePrefix), nameof(PlotTreasurePostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.ChangePlotDataBase), new[] { typeof(string) }, nameof(PlotTreasurePrefix), nameof(PlotTreasurePostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.ChooseDigTreasure), new[] { typeof(string) }, nameof(PlotTreasurePrefix), nameof(PlotTreasurePostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.DigTreasureChoosen), Type.EmptyTypes, nameof(PlotTreasurePrefix), nameof(PlotTreasurePostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.PlotBackgroundClicked), Type.EmptyTypes, nameof(PlotTreasurePrefix), nameof(PlotTreasurePostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.ChangeNextPlot), Type.EmptyTypes, nameof(PlotTreasurePrefix), nameof(PlotTreasurePostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.GoNextPlot), Type.EmptyTypes, nameof(PlotTreasurePrefix), nameof(PlotTreasurePostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.PlotChoiceShowFinished), Type.EmptyTypes, nameof(PlotTreasurePrefix), nameof(PlotTreasurePostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.PlotTextShowFinished), Type.EmptyTypes, nameof(PlotTreasurePrefix), nameof(PlotTreasurePostfix));
        PatchMethod(typeof(PlotController), "HideInteractUIBase", Type.EmptyTypes, nameof(PlotTreasurePrefix), nameof(PlotTreasurePostfix));
        PatchMethod(typeof(PlotController), "HideInteractUI", Type.EmptyTypes, nameof(PlotTreasurePrefix), nameof(PlotTreasurePostfix));
        PatchMethod(typeof(PlotController), "HideInteractUITemp", Type.EmptyTypes, nameof(PlotTreasurePrefix), nameof(PlotTreasurePostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.HidePlotItem), Type.EmptyTypes, nameof(PlotTreasurePrefix), nameof(PlotTreasurePostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.SetPlotItem), new[] { typeof(ItemData), typeof(bool) }, nameof(PlotTreasurePrefix), nameof(PlotTreasurePostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.ShowPlotItem), Type.EmptyTypes, nameof(PlotTreasurePrefix), nameof(PlotTreasurePostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.PlayerGetPlotItem), Type.EmptyTypes, nameof(PlotTreasurePrefix), nameof(PlotTreasurePostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.PlayerGetPlotItemSimple), Type.EmptyTypes, nameof(PlotTreasurePrefix), nameof(PlotTreasurePostfix));
        PatchMethod(typeof(HeroData), nameof(HeroData.GetItem), new[] { typeof(ItemData), typeof(bool), typeof(bool), typeof(int), typeof(bool) }, nameof(HeroGetItemPrefix), nameof(HeroGetItemPostfix));
    }

    private void RegisterCharacterCreationFlowPatches()
    {
        PatchMethod(typeof(StartMenuController), nameof(StartMenuController.SetAttriPreset), new[] { typeof(int) }, nameof(CreationFlowPrefix), nameof(CreationFlowPostfix));
        PatchMethod(typeof(StartMenuController), nameof(StartMenuController.ResetPlayerAttri), Type.EmptyTypes, nameof(CreationFlowPrefix), nameof(CreationFlowPostfix));
        PatchMethod(typeof(StartMenuController), nameof(StartMenuController.RefreshPlayerAttri), Type.EmptyTypes, nameof(CreationFlowPrefix), nameof(CreationFlowPostfix));
        PatchMethod(typeof(StartMenuController), nameof(StartMenuController.PlusMinusButtonClicked), new[] { typeof(GameObject) }, nameof(CreationFlowPrefix), nameof(CreationFlowPostfix));
        PatchMethod(typeof(StartMenuController), nameof(StartMenuController.PlusMinus), new[] { typeof(string), typeof(int), typeof(bool) }, nameof(CreationFlowPrefix), nameof(CreationFlowPostfix));
        PatchMethod(typeof(StartMenuController), nameof(StartMenuController.GetPointPlusCost), new[] { typeof(int) }, nameof(CreationFlowPrefix), nameof(CreationPointCostPostfix));
        PatchMethod(typeof(StartMenuController), nameof(StartMenuController.RandomPlayerBaseAttri), Type.EmptyTypes, nameof(CreationFlowPrefix), nameof(CreationFlowPostfix));
        PatchMethod(typeof(StartMenuController), nameof(StartMenuController.RandomPlayerBaseFightSkill), Type.EmptyTypes, nameof(CreationFlowPrefix), nameof(CreationFlowPostfix));
        PatchMethod(typeof(StartMenuController), nameof(StartMenuController.RandomPlayerBaseLivingSkill), Type.EmptyTypes, nameof(CreationFlowPrefix), nameof(CreationFlowPostfix));
    }

    private void RegisterDialogFlowPatches()
    {
        PatchMethod(typeof(PlotController), nameof(PlotController.ShowHeroInteractUI), new[] { typeof(HeroData) }, nameof(DialogFlowPrefix), nameof(DialogFlowPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.ManageMeetNpcPlot), new[] { typeof(HeroData) }, nameof(DialogFlowPrefix), nameof(DialogFlowPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.ChangeMeetNpcPlot), Type.EmptyTypes, nameof(DialogFlowPrefix), nameof(DialogFlowPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.ChangeNormalMeetNpcPlot), Type.EmptyTypes, nameof(DialogFlowPrefix), nameof(DialogFlowPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.EnterMeeting), Type.EmptyTypes, nameof(DialogFlowPrefix), nameof(DialogFlowPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.MeetRandomNpcEvent), new[] { typeof(string) }, nameof(DialogFlowPrefix), nameof(DialogFlowPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.ChatWithNPC), Type.EmptyTypes, nameof(DialogFlowPrefix), nameof(DialogFlowPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.SureChatWithNPC), Type.EmptyTypes, nameof(DialogFlowPrefix), nameof(DialogFlowPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.TeachNPC), Type.EmptyTypes, nameof(DialogFlowPrefix), nameof(DialogFlowPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.StudySkillWithNPC), Type.EmptyTypes, nameof(DialogFlowPrefix), nameof(DialogFlowPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.StudyLivingSkillWithNPC), Type.EmptyTypes, nameof(DialogFlowPrefix), nameof(DialogFlowPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.AskNpcTeachSkill), Type.EmptyTypes, nameof(DialogFlowPrefix), nameof(DialogFlowPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.AskNPCMission), Type.EmptyTypes, nameof(DialogFlowPrefix), nameof(DialogFlowPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.AskJoinNpcParty), Type.EmptyTypes, nameof(DialogFlowPrefix), nameof(DialogFlowPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.StartTalkMob), Type.EmptyTypes, nameof(DialogFlowPrefix), nameof(DialogFlowPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.SpeInteractWithNPC), Type.EmptyTypes, nameof(DialogFlowPrefix), nameof(DialogFlowPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.CheckChoiceMeetRequire), new[] { typeof(Il2CppSystem.Collections.Generic.List<PlotChoiceRequirement>), typeof(bool) }, nameof(DialogRequirementPrefix), nameof(DialogRequirementPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.CheckMeetRequire), new[] { typeof(ChoiceRequirementType), typeof(float), typeof(bool) }, nameof(DialogRequirementPrefix), nameof(DialogRequirementPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.PlotBackgroundClicked), Type.EmptyTypes, nameof(DialogActionPrefix), nameof(DialogActionPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.AutoPlotButtonClicked), Type.EmptyTypes, nameof(DialogActionPrefix), nameof(DialogActionPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.SkipPlotButtonClicked), Type.EmptyTypes, nameof(DialogActionPrefix), nameof(DialogActionPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.SetAutoPlot), new[] { typeof(bool) }, nameof(DialogActionPrefix), nameof(DialogActionPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.SetSkipPlot), new[] { typeof(bool) }, nameof(DialogActionPrefix), nameof(DialogActionPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.PlotTextShowFinished), Type.EmptyTypes, nameof(DialogActionPrefix), nameof(DialogActionPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.PlotChoiceShowFinished), Type.EmptyTypes, nameof(DialogActionPrefix), nameof(DialogActionPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.ChangeNextPlot), Type.EmptyTypes, nameof(DialogActionPrefix), nameof(DialogActionPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.GoNextPlot), Type.EmptyTypes, nameof(DialogActionPrefix), nameof(DialogActionPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.Update), Type.EmptyTypes, nameof(DialogControllerUpdatePrefix), nameof(DialogControllerUpdatePostfix));
        PatchMethod(typeof(PlotInteractController), nameof(PlotInteractController.Update), Type.EmptyTypes, nameof(DialogChoiceRowPrefix), nameof(DialogChoiceRowPostfix));
        PatchMethod(typeof(PlotInteractController), nameof(PlotInteractController.OnClick), Type.EmptyTypes, nameof(DialogChoiceClickPrefix), nameof(DialogChoiceClickPostfix));
    }

    private void PatchMethod(Type type, string methodName, Type[] parameterTypes, string? prefixName, string? postfixName)
    {
        var target = AccessTools.Method(type, methodName, parameterTypes);
        var prefix = prefixName == null ? null : AccessTools.Method(typeof(LongYinTraceDataPlugin), prefixName);
        var postfix = postfixName == null ? null : AccessTools.Method(typeof(LongYinTraceDataPlugin), postfixName);

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

    private static void ExploreTreasurePrefix(MethodBase __originalMethod, ExploreController? __instance, ExploreTileData? targetTileData, out string __state)
    {
        if (!ShouldTraceExploreTile(targetTileData))
        {
            __state = string.Empty;
            return;
        }

        __state = $"controller={DescribeExploreController(__instance)}; tile={DescribeExploreTile(targetTileData)}";
        LoggerInstance.LogInfo($"TREASURE EXPLORE ENTER {DescribeMethod(__originalMethod)} {__state}");
    }

    private static void ExploreTreasurePostfix(MethodBase __originalMethod, ExploreController? __instance, ExploreTileData? targetTileData, string __state)
    {
        if (string.IsNullOrEmpty(__state))
        {
            return;
        }

        LoggerInstance.LogInfo(
            $"TREASURE EXPLORE EXIT  {DescribeMethod(__originalMethod)} before={__state}; " +
            $"tileAfter={DescribeExploreTile(targetTileData)}; controllerAfter={DescribeExploreController(__instance)}");
    }

    private static void PlotTreasurePrefix(MethodBase __originalMethod, PlotController? __instance, object[] __args, out string __state)
    {
        if (!ShouldTracePlotCall(__originalMethod, __instance, __args))
        {
            __state = string.Empty;
            return;
        }

        __state = $"plot={DescribePlotController(__instance)}; args={DescribeArgs(__args)}";
        LoggerInstance.LogInfo($"TREASURE PLOT ENTER {DescribeMethod(__originalMethod)} {__state}");
    }

    private static void PlotTreasurePostfix(MethodBase __originalMethod, PlotController? __instance, object[] __args, string __state)
    {
        if (string.IsNullOrEmpty(__state))
        {
            return;
        }

        LoggerInstance.LogInfo($"TREASURE PLOT EXIT  {DescribeMethod(__originalMethod)} before={__state}; plotAfter={DescribePlotController(__instance)}");
    }

    private static void HeroGetItemPrefix(MethodBase __originalMethod, HeroData? __instance, ItemData? itemData, bool showPopInfo, bool showSpeGetItem, int treasureChestClickTime, bool skipManageItemPoison, out string __state)
    {
        if (!ShouldTraceGetItem(__instance, itemData, treasureChestClickTime))
        {
            __state = string.Empty;
            return;
        }

        __state =
            $"hero={DescribeHero(__instance)}, item={DescribeItem(itemData)}, showPopInfo={showPopInfo}, " +
            $"showSpeGetItem={showSpeGetItem}, treasureChestClickTime={treasureChestClickTime}, skipManageItemPoison={skipManageItemPoison}";
        LoggerInstance.LogInfo($"TREASURE GETITEM ENTER {DescribeMethod(__originalMethod)} {__state}");
    }

    private static void HeroGetItemPostfix(MethodBase __originalMethod, HeroData? __instance, ItemData? itemData, bool showPopInfo, bool showSpeGetItem, int treasureChestClickTime, bool skipManageItemPoison, string __state)
    {
        if (string.IsNullOrEmpty(__state))
        {
            return;
        }

        LoggerInstance.LogInfo($"TREASURE GETITEM EXIT  {DescribeMethod(__originalMethod)} before={__state}; heroAfter={DescribeHero(__instance)}");
    }

    private static void CreationFlowPrefix(MethodBase __originalMethod, StartMenuController? __instance, object[] __args, out string __state)
    {
        if (!ShouldTraceCreationFlow(__instance))
        {
            __state = string.Empty;
            return;
        }

        __state = $"state={DescribeCreationState(__instance)}; args={DescribeArgs(__args)}";
        LoggerInstance.LogInfo($"CREATION ENTER {DescribeMethod(__originalMethod)} {__state}");
    }

    private static void CreationFlowPostfix(MethodBase __originalMethod, StartMenuController? __instance, object[] __args, string __state)
    {
        if (string.IsNullOrEmpty(__state))
        {
            return;
        }

        LoggerInstance.LogInfo($"CREATION EXIT  {DescribeMethod(__originalMethod)} before={__state}; after={DescribeCreationState(__instance)}");
    }

    private static void CreationPointCostPostfix(MethodBase __originalMethod, StartMenuController? __instance, int nowPoint, int __result, string __state)
    {
        if (string.IsNullOrEmpty(__state))
        {
            return;
        }

        LoggerInstance.LogInfo(
            $"CREATION COST  {DescribeMethod(__originalMethod)} nowPoint={nowPoint}, result={__result}; " +
            $"before={__state}; after={DescribeCreationState(__instance)}");
    }

    private static void DialogFlowPrefix(MethodBase __originalMethod, PlotController? __instance, object[] __args, out string __state)
    {
        if (__instance == null)
        {
            __state = string.Empty;
            return;
        }

        CacheActiveDialogContext(__args);
        __state = $"plot={DescribePlotController(__instance)}; args={DescribeArgs(__args)}";
        LoggerInstance.LogInfo($"DIALOG ENTER {DescribeMethod(__originalMethod)} {__state}");
        MarkDialogProgress(__instance, DescribeMethod(__originalMethod));
    }

    private static void DialogFlowPostfix(MethodBase __originalMethod, PlotController? __instance, object[] __args, string __state)
    {
        if (string.IsNullOrEmpty(__state))
        {
            return;
        }

        LoggerInstance.LogInfo($"DIALOG EXIT  {DescribeMethod(__originalMethod)} before={__state}; after={DescribePlotController(__instance)}");
    }

    private static void DialogActionPrefix(MethodBase __originalMethod, PlotController? __instance, object[] __args, out string __state)
    {
        if (__instance == null)
        {
            __state = string.Empty;
            return;
        }

        __state = $"plot={DescribePlotController(__instance)}; args={DescribeArgs(__args)}";
        LoggerInstance.LogInfo($"DIALOG ACTION ENTER {DescribeMethod(__originalMethod)} {__state}");
    }

    private static void DialogActionPostfix(MethodBase __originalMethod, PlotController? __instance, object[] __args, string __state)
    {
        if (__instance == null || string.IsNullOrEmpty(__state))
        {
            return;
        }

        var after = DescribePlotController(__instance);
        var summary = $"before={__state}; after={after}";
        if (TryLogStateChange(_dialogActionStateCache, $"{DescribeMethod(__originalMethod)}|{DescribeArgs(__args)}", summary))
        {
            LoggerInstance.LogInfo($"DIALOG ACTION EXIT  {DescribeMethod(__originalMethod)} {summary}");
        }

        MarkDialogProgress(__instance, DescribeMethod(__originalMethod));
    }

    private static void DialogControllerUpdatePrefix(PlotController? __instance, out string __state)
    {
        if (__instance == null)
        {
            __state = string.Empty;
            return;
        }

        __state = DescribePlotController(__instance);

        if (CheckForcedAutoContinueHotkey())
        {
            _forceAutoContinueActive = !_forceAutoContinueActive;
            LoggerInstance.LogInfo($"FORCE FAST FORWARD TOGGLE active={_forceAutoContinueActive}");
            if (!_forceAutoContinueActive)
            {
                __instance.SetAutoPlot(false);
                __instance.SetSkipPlot(false);
                __instance.plotAutoing = false;
                __instance.plotSkipping = false;
            }
        }

        if (CheckEmergencyUnstuckHotkey())
        {
            TryEmergencyDialogRecovery(__instance);
        }

        ApplyForcedAutoContinueIfNeeded(__instance, "PlotController.Update");
    }

    private static void DialogControllerUpdatePostfix(PlotController? __instance, string __state)
    {
        if (__instance == null)
        {
            return;
        }

        var after = DescribePlotController(__instance);
        if (!string.IsNullOrEmpty(__state) && !string.Equals(__state, after, StringComparison.Ordinal))
        {
            var summary = $"before={__state}; after={after}";
            if (TryLogStateChange(_dialogActionStateCache, "PlotController.Update", summary))
            {
                LoggerInstance.LogInfo($"DIALOG UPDATE {summary}");
            }

            MarkDialogProgress(__instance, "PlotController.Update");
        }

        CheckForFastForwardStuck(__instance);
    }

    private static void DialogRequirementPrefix(MethodBase __originalMethod, PlotController? __instance, object[] __args, out string __state)
    {
        if (__instance == null)
        {
            __state = string.Empty;
            return;
        }

        __state = $"plot={DescribePlotController(__instance)}; args={DescribeDialogRequirementArgs(__originalMethod, __args)}";
    }

    private static void DialogRequirementPostfix(MethodBase __originalMethod, PlotController? __instance, object[] __args, bool __result, string __state)
    {
        if (string.IsNullOrEmpty(__state))
        {
            return;
        }

        var argsSummary = DescribeDialogRequirementArgs(__originalMethod, __args);
        var summary = $"{DescribeMethod(__originalMethod)} result={__result}; {argsSummary}";
        if (!TryLogStateChange(_dialogRequirementStateCache, __originalMethod.Name + "|" + argsSummary, summary))
        {
            return;
        }

        LoggerInstance.LogInfo($"DIALOG REQUIRE {summary}; before={__state}; after={DescribePlotController(__instance)}");
    }

    private static void DialogChoiceRowPrefix(PlotInteractController? __instance)
    {
        if (__instance == null)
        {
            return;
        }

        var choice = __instance.choiceData;
        if (choice == null)
        {
            return;
        }

        var monthlyQuotaState = ApplyDialogMonthlyQuota(__instance, choice, consume: false);
        var summary = DescribeDialogChoiceRow(__instance, choice);
        if (!string.IsNullOrEmpty(monthlyQuotaState))
        {
            summary += $"; monthly={monthlyQuotaState}";
        }
        if (!TryLogStateChange(_dialogChoiceRowStateCache, TryGetChoiceKey(choice), summary))
        {
            return;
        }

        LoggerInstance.LogInfo($"DIALOG ROW {summary}");
    }

    private static void DialogChoiceRowPostfix(PlotInteractController? __instance)
    {
        if (__instance == null)
        {
            return;
        }

        var choice = __instance.choiceData;
        if (choice == null)
        {
            return;
        }

        var monthlyQuotaState = ApplyDialogMonthlyQuota(__instance, choice, consume: false);
        var summary = DescribeDialogChoiceRow(__instance, choice);
        if (!string.IsNullOrEmpty(monthlyQuotaState))
        {
            summary += $"; monthly={monthlyQuotaState}";
        }
        if (!TryLogStateChange(_dialogChoiceRowStateCache, TryGetChoiceKey(choice), summary))
        {
            return;
        }

        LoggerInstance.LogInfo($"DIALOG ROW {summary}");
    }

    private static void DialogChoiceClickPrefix(MethodBase __originalMethod, PlotInteractController? __instance, out string __state)
    {
        if (__instance == null || __instance.choiceData == null)
        {
            __state = string.Empty;
            return;
        }

        ApplyDialogMonthlyQuota(__instance, __instance.choiceData, consume: true);
        __state = DescribeDialogChoiceRow(__instance, __instance.choiceData);
        LoggerInstance.LogInfo($"DIALOG CLICK ENTER {DescribeMethod(__originalMethod)} {__state}");
        MarkDialogProgress(null, "PlotInteractController.OnClick");
    }

    private static void DialogChoiceClickPostfix(MethodBase __originalMethod, PlotInteractController? __instance, string __state)
    {
        if (string.IsNullOrEmpty(__state))
        {
            return;
        }

        var after = __instance == null || __instance.choiceData == null ? "choice=null" : DescribeDialogChoiceRow(__instance, __instance.choiceData);
        LoggerInstance.LogInfo($"DIALOG CLICK EXIT  {DescribeMethod(__originalMethod)} before={__state}; after={after}");
    }

    private static bool CheckForcedAutoContinueHotkey()
    {
        try
        {
            return _forceAutoContinueHotkey != null && Input.GetKeyDown(_forceAutoContinueHotkey.Value);
        }
        catch
        {
            return false;
        }
    }

    private static bool CheckEmergencyUnstuckHotkey()
    {
        try
        {
            return _forceUnstuckHotkey != null && Input.GetKeyDown(_forceUnstuckHotkey.Value);
        }
        catch
        {
            return false;
        }
    }

    private static void TryEmergencyDialogRecovery(PlotController? controller)
    {
        LoggerInstance.LogWarning($"EMERGENCY DIALOG RECOVERY requested; before={DescribePlotController(controller)}");

        _forceAutoContinueActive = false;

        if (controller != null)
        {
            try
            {
                controller.SetAutoPlot(false);
            }
            catch
            {
            }

            try
            {
                controller.SetSkipPlot(false);
            }
            catch
            {
            }

            controller.plotAutoing = false;
            controller.plotSkipping = false;
        }

        if (TryClearTreasureChestChoiceSession())
        {
            LoggerInstance.LogWarning("EMERGENCY DIALOG RECOVERY cleared treasure chest choice session.");
        }

        LoggerInstance.LogWarning($"EMERGENCY DIALOG RECOVERY completed; after={DescribePlotController(controller)}");
    }

    private static bool TryClearTreasureChestChoiceSession()
    {
        try
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var candidateAssembly in assembly)
            {
                Type? sessionType = null;
                try
                {
                    sessionType = candidateAssembly.GetType("LongYinStaminaLockPlugin", throwOnError: false, ignoreCase: false);
                }
                catch
                {
                }

                if (sessionType == null)
                {
                    continue;
                }

                var field = sessionType.GetField("_activeTreasureChestChoiceSession", BindingFlags.Static | BindingFlags.NonPublic);
                if (field == null)
                {
                    continue;
                }

                var session = field.GetValue(null);
                if (session == null)
                {
                    return false;
                }

                TrySetMemberValue(session, "Resolved", true);
                TrySetMemberValue(session, "PendingClickConfirm", false);
                TrySetMemberValue(session, "PendingClickConfirmFrames", 0);
                field.SetValue(null, null);
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static void ApplyForcedAutoContinueIfNeeded(PlotController? controller, string source)
    {
        if (controller == null || !_forceAutoContinueEnabled.Value || !_forceAutoContinueActive)
        {
            return;
        }

        var activeChoice = controller.newChoice ?? controller.nowChoice;
        if (controller.plotChoiceShowing || activeChoice != null)
        {
            if (controller.plotSkipping)
            {
                LoggerInstance.LogInfo($"FORCE FAST FORWARD RELEASE source={source}; before={DescribePlotController(controller)}");
                controller.SetSkipPlot(false);
                controller.plotSkipping = false;
            }

            return;
        }

        if (!controller.plotTextShowing && !controller.plotAutoing)
        {
            return;
        }

        if (!controller.plotSkipping)
        {
            LoggerInstance.LogInfo($"FORCE FAST FORWARD APPLY source={source}; before={DescribePlotController(controller)}");
        }

        controller.SetSkipPlot(true);

        if (!controller.plotSkipping)
        {
            controller.plotSkipping = true;
        }
    }

    private static void MarkDialogProgress(PlotController? controller, string source)
    {
        _lastDialogProgressFrame = Time.frameCount;
        if (controller != null)
        {
            _lastDialogProgressSignature = DescribePlotController(controller);
        }
        else if (string.IsNullOrEmpty(_lastDialogProgressSignature))
        {
            _lastDialogProgressSignature = source;
        }

        _lastDialogStuckSignature = string.Empty;

        if (!string.IsNullOrEmpty(source))
        {
            LoggerInstance.LogInfo($"DIALOG PROGRESS source={source}; frame={Time.frameCount}; snapshot={_lastDialogProgressSignature}");
        }
    }

    private static void CheckForFastForwardStuck(PlotController controller)
    {
        if (!_forceAutoContinueEnabled.Value || !_forceAutoContinueActive)
        {
            return;
        }

        if (!IsDialogOpen(controller))
        {
            return;
        }

        if (_lastDialogProgressFrame < 0)
        {
            _lastDialogProgressFrame = Time.frameCount;
            _lastDialogProgressSignature = DescribePlotController(controller);
            return;
        }

        var framesSinceProgress = Time.frameCount - _lastDialogProgressFrame;
        if (framesSinceProgress < _fastForwardStuckFrames.Value)
        {
            return;
        }

        var stuckSignature = DescribePlotController(controller);
        if (string.Equals(_lastDialogStuckSignature, stuckSignature, StringComparison.Ordinal))
        {
            return;
        }

        _lastDialogStuckSignature = stuckSignature;
        LoggerInstance.LogWarning(
            $"DIALOG STUCK detected at frame={Time.frameCount}; framesSinceProgress={framesSinceProgress}; " +
            $"forceFastForwardActive={_forceAutoContinueActive}; plot={stuckSignature}; hero={DescribeHero(_activeDialogHero)}; " +
            $"controllerFields={DescribeInterestingFields(controller)}; controllerTree={DescribeGameObjectTree(controller.gameObject, 2, 24)}");

        if (_fastForwardSafetyEnabled.Value)
        {
            _forceAutoContinueActive = false;
            controller.SetSkipPlot(false);
            controller.plotSkipping = false;
            LoggerInstance.LogWarning("FORCE FAST FORWARD SAFETY OFF after stuck dialog detection.");
        }
    }

    private static bool IsDialogOpen(PlotController controller)
    {
        return controller.plotHappen || controller.plotChoiceShowing || controller.plotTextShowing;
    }

    private static string DescribeInterestingFields(object? target)
    {
        if (target == null)
        {
            return "fields=null";
        }

        try
        {
            var fields = target.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var parts = new List<string>();
            foreach (var field in fields)
            {
                if (!ShouldDescribeField(field))
                {
                    continue;
                }

                parts.Add($"{field.Name}={DescribeFieldValue(field.GetValue(target))}");
                if (parts.Count >= 24)
                {
                    break;
                }
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "fields=none";
        }
        catch
        {
            return "fields=unavailable";
        }
    }

    private static bool ShouldDescribeField(FieldInfo field)
    {
        try
        {
            var name = field.Name.ToLowerInvariant();
            if (name.Contains("continue") ||
                name.Contains("skip") ||
                name.Contains("auto") ||
                name.Contains("next") ||
                name.Contains("choice") ||
                name.Contains("dialog") ||
                name.Contains("plot") ||
                name.Contains("button") ||
                name.Contains("text") ||
                name.Contains("ui"))
            {
                return true;
            }

            var fieldType = field.FieldType;
            return fieldType.IsPrimitive || fieldType.IsEnum || fieldType == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(fieldType);
        }
        catch
        {
            return false;
        }
    }

    private static string DescribeFieldValue(object? value)
    {
        if (value == null)
        {
            return "null";
        }

        if (value is GameObject gameObject)
        {
            return DescribeGameObject(gameObject);
        }

        if (value is Component component)
        {
            return $"{component.GetType().Name}(go={SafeFormatValue(component.gameObject?.name)})";
        }

        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            return DescribeEnumerable(enumerable);
        }

        return SafeFormatValue(value);
    }

    private static string DescribeEnumerable(System.Collections.IEnumerable enumerable)
    {
        var count = 0;
        var preview = new List<string>();
        foreach (var item in enumerable)
        {
            count++;
            if (preview.Count < 6)
            {
                preview.Add(SafeFormatValue(item));
            }
        }

        return $"count={count}, items=[{string.Join(" | ", preview)}]";
    }

    private static string DescribeGameObjectTree(GameObject root, int maxDepth, int maxNodes)
    {
        try
        {
            var nodes = new List<string>();

            void Walk(Transform? transform, int depth)
            {
                if (transform == null || nodes.Count >= maxNodes || depth > maxDepth)
                {
                    return;
                }

                var gameObject = transform.gameObject;
                var node = $"{new string('>', depth)}{SafeFormatValue(gameObject.name)}[active={gameObject.activeSelf}/{gameObject.activeInHierarchy}]";
                var readableText = TryGetReadableText(gameObject);
                if (!string.IsNullOrEmpty(readableText))
                {
                    node += $", text={SafeFormatValue(readableText)}";
                }

                var buttonInfo = TryDescribeComponentMembers(gameObject, "UIButtonMessage", "functionName", "trigger", "target");
                if (!string.IsNullOrEmpty(buttonInfo))
                {
                    node += $", button={buttonInfo}";
                }

                nodes.Add(node);

                foreach (Transform child in transform)
                {
                    Walk(child, depth + 1);
                    if (nodes.Count >= maxNodes)
                    {
                        break;
                    }
                }
            }

            Walk(root.transform, 0);
            return nodes.Count > 0 ? string.Join(" | ", nodes) : "tree=empty";
        }
        catch
        {
            return "tree=unavailable";
        }
    }

    private static bool ShouldTraceExploreTile(ExploreTileData? tile)
    {
        return tile != null && TryGetExploreEventType(tile) >= 0;
    }

    private static bool ShouldTracePlotCall(MethodBase method, PlotController? controller, object[]? args)
    {
        if (method.Name == nameof(PlotController.ChangePlot) ||
            method.Name == nameof(PlotController.ChangePlotDataBase) ||
            method.Name == nameof(PlotController.ChooseDigTreasure) ||
            method.Name == nameof(PlotController.DigTreasureChoosen))
        {
            return true;
        }

        if (method.Name == nameof(PlotController.PlotBackgroundClicked) ||
            method.Name == nameof(PlotController.ChangeNextPlot) ||
            method.Name == nameof(PlotController.GoNextPlot) ||
            method.Name == nameof(PlotController.PlotChoiceShowFinished) ||
            method.Name == nameof(PlotController.PlotTextShowFinished) ||
            method.Name == nameof(PlotController.HidePlotItem) ||
            method.Name == "HideInteractUIBase" ||
            method.Name == "HideInteractUI" ||
            method.Name == "HideInteractUITemp")
        {
            return controller != null && (controller.plotHappen || controller.plotChoiceShowing || controller.plotTextShowing);
        }

        if (method.Name == nameof(PlotController.SetPlotItem) && args != null && args.Length > 0 && args[0] is ItemData itemData)
        {
            return itemData.type == ItemType.Treasure;
        }

        var activeItem = controller?.plotInteractItem;
        var tempItem = controller?.plotInteractItemTempRecord;
        return IsTreasure(activeItem) || IsTreasure(tempItem);
    }

    private static bool ShouldTraceGetItem(HeroData? hero, ItemData? item, int treasureChestClickTime)
    {
        var player = TryGetPlayerHero();
        if (player == null || hero == null || TryGetHeroId(player) != TryGetHeroId(hero))
        {
            return false;
        }

        if (treasureChestClickTime > 0)
        {
            return true;
        }

        return item != null && item.type == ItemType.Treasure;
    }

    private static bool ShouldTraceCreationFlow(StartMenuController? controller)
    {
        return controller != null;
    }

    private static bool ShouldTraceTagPointFlow(HeroData? hero)
    {
        return hero != null;
    }

    private static bool IsTreasure(ItemData? item)
    {
        return item != null && item.type == ItemType.Treasure;
    }

    private static int TryGetExploreEventType(ExploreTileData? tile)
    {
        if (tile == null)
        {
            return -1;
        }

        try
        {
            return tile.exploreTileEventType;
        }
        catch
        {
            return -1;
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
            return null;
        }
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
            parts.Add($"{i}:{DescribeArg(args[i])}");
        }

        return "[" + string.Join(", ", parts) + "]";
    }

    private static string DescribeArg(object? value)
    {
        return value switch
        {
            null => "null",
            SinglePlotData plot => DescribeSinglePlot(plot),
            ItemData item => DescribeItem(item),
            ExploreTileData tile => DescribeExploreTile(tile),
            HeroData hero => DescribeHero(hero),
            GameObject gameObject => DescribeGameObject(gameObject),
            _ => SafeFormatValue(value)
        };
    }

    private static string DescribePlotController(PlotController? controller)
    {
        if (controller == null)
        {
            return "plot=null";
        }

        return
            $"plotHappen={controller.plotHappen}, plotChoiceShowing={controller.plotChoiceShowing}, plotTextShowing={controller.plotTextShowing}, " +
            $"plotAutoing={controller.plotAutoing}, plotSkipping={controller.plotSkipping}, nowChoice={DescribePlotChoice(controller.nowChoice)}, newChoice={DescribePlotChoice(controller.newChoice)}, " +
            $"plotInteractItem={DescribeItem(controller.plotInteractItem)}, plotInteractItemTempRecord={DescribeItem(controller.plotInteractItemTempRecord)}";
    }

    private static string DescribeCreationState(StartMenuController? controller)
    {
        if (controller == null)
        {
            return "creation=null";
        }

        try
        {
            return
                $"AttriLeft={controller.leftAttriPoint}, " +
                $"FightLeft={controller.leftFightSkillPoint}, " +
                $"LivingLeft={controller.leftLivingSkillPoint}, " +
                $"NeedRefresh={controller.needRefreshPlayerAttri}, " +
                $"PresetCount={TryGetCollectionCount(controller.attriPresetDatas)}";
        }
        catch
        {
            return "creation=unavailable";
        }
    }

    private static string DescribeSinglePlot(SinglePlotData? plot)
    {
        if (plot == null)
        {
            return "singlePlot=null";
        }

        try
        {
            var title = SafeGetMemberValue(plot, "plotDescribe") ?? SafeGetMemberValue(plot, "describe") ?? SafeGetMemberValue(plot, "plotText");
            var choiceCount = 0;
            var choicePreview = string.Empty;

            var choicesObject = SafeGetMemberValue(plot, "choices");
            if (choicesObject is System.Collections.IEnumerable enumerable)
            {
                var previews = new List<string>();
                foreach (var choice in enumerable)
                {
                    choiceCount++;
                    if (previews.Count < 5)
                    {
                        previews.Add(DescribePlotChoice(choice));
                    }
                }

                choicePreview = string.Join(" | ", previews);
            }

            return $"singlePlot=title={SafeFormatValue(title)}, choiceCount={choiceCount}, choices=[{choicePreview}]";
        }
        catch
        {
            return "singlePlot=unavailable";
        }
    }

    private static string DescribePlotChoice(object? choice)
    {
        return choice switch
        {
            null => "null",
            SinglePlotChoiceData plotChoice => DescribeSinglePlotChoice(plotChoice),
            _ => DescribeChoiceObject(choice)
        };
    }

    private static string DescribeSinglePlotChoice(SinglePlotChoiceData? choice)
    {
        if (choice == null)
        {
            return "singleChoice=null";
        }

        try
        {
            var name = SafeGetMemberValue(choice, "choiceText") ?? SafeGetMemberValue(choice, "name") ?? SafeGetMemberValue(choice, "choiceName");
            var describe = SafeGetMemberValue(choice, "describe");
            var callFuc = SafeGetMemberValue(choice, "callFuc");
            var callParam = SafeGetMemberValue(choice, "callParam");
            var timeNeed = SafeGetMemberValue(choice, "playerInteractionTimeNeed");
            var requirements = DescribeChoiceRequirements(SafeGetMemberValue(choice, "requirements"));
            return
                $"name={SafeFormatValue(name)}, describe={SafeFormatValue(describe)}, callFuc={SafeFormatValue(callFuc)}, " +
                $"callParam={SafeFormatValue(callParam)}, timeNeed={SafeFormatValue(timeNeed)}, requirements={requirements}";
        }
        catch
        {
            return "singleChoice=unavailable";
        }
    }

    private static string DescribeChoiceObject(object choice)
    {
        var name = SafeGetMemberValue(choice, "choiceText") ?? SafeGetMemberValue(choice, "name") ?? SafeGetMemberValue(choice, "choiceName");
        var describe = SafeGetMemberValue(choice, "describe");
        var callFuc = SafeGetMemberValue(choice, "callFuc");
        var callParam = SafeGetMemberValue(choice, "callParam");
        var timeNeed = SafeGetMemberValue(choice, "playerInteractionTimeNeed");
        var requirements = DescribeChoiceRequirements(SafeGetMemberValue(choice, "requirements"));
        return
            $"name={SafeFormatValue(name)}, describe={SafeFormatValue(describe)}, callFuc={SafeFormatValue(callFuc)}, " +
            $"callParam={SafeFormatValue(callParam)}, timeNeed={SafeFormatValue(timeNeed)}, requirements={requirements}";
    }

    private static string DescribeChoiceRequirements(object? requirements)
    {
        if (requirements == null)
        {
            return "requirements=null";
        }

        if (requirements is not System.Collections.IEnumerable enumerable)
        {
            return $"requirements={SafeFormatValue(requirements)}";
        }

        var count = 0;
        var previews = new List<string>();
        foreach (var requirement in enumerable)
        {
            count++;
            if (previews.Count < 5)
            {
                previews.Add(DescribeChoiceRequirement(requirement));
            }
        }

        return $"count={count}, items=[{string.Join(" | ", previews)}]";
    }

    private static string DescribeDialogRequirementArgs(MethodBase method, object[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return "[]";
        }

        if (method.Name == nameof(PlotController.CheckChoiceMeetRequire))
        {
            var requirements = args[0] is null ? "requirements=null" : DescribeChoiceRequirements(args[0]);
            var includeTeamMate = args.Length > 1 ? SafeFormatValue(args[1]) : "missing";
            return $"{requirements}; includeTeamMate={includeTeamMate}";
        }

        if (method.Name == nameof(PlotController.CheckMeetRequire))
        {
            var requireType = args.Length > 0 ? SafeFormatValue(args[0]) : "missing";
            var requireNum = args.Length > 1 ? SafeFormatValue(args[1]) : "missing";
            var includeTeamMate = args.Length > 2 ? SafeFormatValue(args[2]) : "missing";
            return $"requireType={requireType}; requireNum={requireNum}; includeTeamMate={includeTeamMate}";
        }

        return DescribeArgs(args);
    }

    private static string DescribeChoiceRequirement(object? requirement)
    {
        if (requirement == null)
        {
            return "null";
        }

        var requireType = SafeGetMemberValue(requirement, "requireType");
        var requireNum = SafeGetMemberValue(requirement, "requireNum");
        return $"{SafeFormatValue(requireType)}>={SafeFormatValue(requireNum)}";
    }

    private static string DescribeDialogChoiceRow(PlotInteractController controller, SinglePlotChoiceData choice)
    {
        return
            $"key={TryGetChoiceKey(choice)}, meetRequire={controller.meetRequire}, meetCost={controller.meetCost}, " +
            $"choice={DescribePlotChoice(choice)}";
    }

    private static string TryGetChoiceKey(SinglePlotChoiceData? choice)
    {
        if (choice == null)
        {
            return "choice=null";
        }

        var callParam = SafeGetMemberValue(choice, "callParam");
        var choiceText = SafeGetMemberValue(choice, "choiceText");
        return
            $"{SafeFormatValue(callParam)}|{SafeFormatValue(choiceText)}|hash={choice.GetHashCode()}";
    }

    private static bool TryLogStateChange(Dictionary<string, string> cache, string key, string summary)
    {
        if (cache.TryGetValue(key, out var previous) && string.Equals(previous, summary, StringComparison.Ordinal))
        {
            return false;
        }

        cache[key] = summary;
        return true;
    }

    private static object? SafeGetMemberValue(object target, string memberName)
    {
        try
        {
            var property = target.GetType().GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return property.GetValue(target);
            }
        }
        catch
        {
        }

        try
        {
            var field = target.GetType().GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return field.GetValue(target);
            }
        }
        catch
        {
        }

        return null;
    }

    private static int TryGetCollectionCount(object? value)
    {
        if (value is System.Collections.ICollection collection)
        {
            return collection.Count;
        }

        return -1;
    }

    private static string DescribeExploreController(ExploreController? controller)
    {
        if (controller == null)
        {
            return "explore=null";
        }

        return $"playerGrid={DescribeGameObject(controller.playerGrid)}, finalGrid={DescribeGameObject(controller.finalGrid)}";
    }

    private static string DescribeExploreTile(ExploreTileData? tile)
    {
        if (tile == null)
        {
            return "tile=null";
        }

        return $"name={SafeFormatValue(tile.name)}, row={tile.row}, column={tile.column}, event={tile.exploreTileEventType}, eventHappen={tile.eventHappen}, seen={tile.seen}, moveAble={tile.moveAble}";
    }

    private static string DescribeHero(HeroData? hero)
    {
        if (hero == null)
        {
            return "hero=null";
        }

        try
        {
            var interactionData = DescribePlayerInteractionTimeData(hero.playerInteractionTimeData);
            var heroForceLv = SafeGetMemberValue(hero, "heroForceLv");
            return $"name={SafeFormatValue(hero.heroName)}, heroID={hero.heroID}, heroForceLv={SafeFormatValue(heroForceLv)}, interactionTime={interactionData}";
        }
        catch
        {
            return "hero=unavailable";
        }
    }

    private static string DescribePlayerInteractionTimeData(PlayerInteractionTimeData? data)
    {
        if (data == null)
        {
            return "interactionTime=null";
        }

        try
        {
            return
                $"playerInteractTimeList={DescribeIntList(SafeGetMemberValue(data, "playerInteractTimeList"))}, " +
                $"releaseHateTime={SafeFormatValue(SafeGetMemberValue(data, "releaseHateTime"))}, " +
                $"attackPlayerTime={SafeFormatValue(SafeGetMemberValue(data, "attackPlayerTime"))}, " +
                $"givePlayerGiftTime={SafeFormatValue(SafeGetMemberValue(data, "givePlayerGiftTime"))}, " +
                $"teachPlayerSkill={SafeFormatValue(SafeGetMemberValue(data, "teachPlayerSkill"))}, " +
                $"invitePlayTime={SafeFormatValue(SafeGetMemberValue(data, "invitePlayTime"))}, " +
                $"askItemTime={SafeFormatValue(SafeGetMemberValue(data, "askItemTime"))}, " +
                $"releasePlayerHateTime={SafeFormatValue(SafeGetMemberValue(data, "releasePlayerHateTime"))}, " +
                $"loverUnhappy={SafeFormatValue(SafeGetMemberValue(data, "loverUnhappy"))}";
        }
        catch
        {
            return "interactionTime=unavailable";
        }
    }

    private static string DescribeIntList(object? list)
    {
        if (list == null)
        {
            return "list=null";
        }

        var count = TryGetCollectionCount(list);
        if (count >= 0)
        {
            var preview = new List<string>();
            for (var i = 0; i < count && i < 8; i++)
            {
                preview.Add(SafeFormatValue(TryGetIndexedValue(list, i)));
            }

            return $"count={count}, items=[{string.Join(" | ", preview)}]";
        }

        if (list is not System.Collections.IEnumerable enumerable)
        {
            return SafeFormatValue(list);
        }

        var enumerableCount = 0;
        var enumerablePreview = new List<string>();
        foreach (var item in enumerable)
        {
            enumerableCount++;
            if (enumerablePreview.Count < 8)
            {
                enumerablePreview.Add(SafeFormatValue(item));
            }
        }

        return $"count={enumerableCount}, items=[{string.Join(" | ", enumerablePreview)}]";
    }

    private static string DescribeItem(ItemData? item)
    {
        if (item == null)
        {
            return "item=null";
        }

        return $"name={SafeFormatValue(item.name)}, id={item.itemID}, type={item.type}, subType={item.subType}, itemLv={item.itemLv}, rareLv={item.rareLv}, value={item.value}";
    }

    private static string DescribeGameObject(GameObject? gameObject)
    {
        if (gameObject == null)
        {
            return "null";
        }

        var parts = new List<string>
        {
            $"gameObject={SafeFormatValue(gameObject.name)}",
            $"path={DescribeGameObjectPath(gameObject)}"
        };

        try
        {
            var itemIcon = gameObject.GetComponent<ItemIconController>();
            if (itemIcon != null)
            {
                parts.Add($"iconItem={DescribeItem(itemIcon.itemData)}");
            }
        }
        catch
        {
        }

        var text = TryGetReadableText(gameObject);
        if (!string.IsNullOrEmpty(text))
        {
            parts.Add($"text={SafeFormatValue(text)}");
        }

        var clickInfo = TryDescribeComponentMembers(gameObject, "UIButtonMessage", "functionName", "trigger", "target");
        if (!string.IsNullOrEmpty(clickInfo))
        {
            parts.Add($"uiButtonMessage={clickInfo}");
        }

        return string.Join(", ", parts);
    }

    private static string DescribeGameObjectPath(GameObject gameObject)
    {
        try
        {
            var names = new List<string>();
            Transform? current = gameObject.transform;
            while (current != null)
            {
                names.Add(SafeFormatValue(current.name));
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }
        catch
        {
            return SafeFormatValue(gameObject.name);
        }
    }

    private static string? TryGetReadableText(GameObject gameObject)
    {
        try
        {
            var text = gameObject.GetComponentInChildren<Text>(true);
            if (text != null && !string.IsNullOrWhiteSpace(text.text))
            {
                return text.text;
            }
        }
        catch
        {
        }

        try
        {
            var text = gameObject.GetComponentInChildren<TMP_Text>(true);
            if (text != null && !string.IsNullOrWhiteSpace(text.text))
            {
                return text.text;
            }
        }
        catch
        {
        }

        try
        {
            var directLabel = gameObject.GetComponent("UILabel");
            var directText = TryReadCommonTextMember(directLabel);
            if (!string.IsNullOrWhiteSpace(directText))
            {
                return directText;
            }
        }
        catch
        {
        }

        try
        {
            foreach (Transform child in gameObject.transform)
            {
                var label = child.gameObject.GetComponent("UILabel");
                var labelText = TryReadCommonTextMember(label);
                if (!string.IsNullOrWhiteSpace(labelText))
                {
                    return labelText;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static string? TryDescribeComponentMembers(GameObject gameObject, string componentName, params string[] memberNames)
    {
        try
        {
            var component = gameObject.GetComponent(componentName);
            if (component == null)
            {
                return null;
            }

            var parts = new List<string>();
            foreach (var memberName in memberNames)
            {
                var value = SafeGetMemberValue(component, memberName);
                if (value != null)
                {
                    parts.Add($"{memberName}={SafeFormatValue(value)}");
                }
            }

            return parts.Count > 0 ? string.Join(", ", parts) : component.GetType().Name;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadCommonTextMember(object? component)
    {
        if (component == null)
        {
            return null;
        }

        foreach (var memberName in new[] { "text", "value", "caption", "label" })
        {
            var value = SafeGetMemberValue(component, memberName);
            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    private static string SafeFormatValue(object? value)
    {
        var text = value?.ToString() ?? "null";
        return text.Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static void CacheActiveDialogContext(object[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return;
        }

        foreach (var arg in args)
        {
            if (arg is not HeroData hero)
            {
                continue;
            }

            CacheActiveDialogHero(hero);
            return;
        }
    }

    private static void CacheActiveDialogHero(HeroData hero)
    {
        _activeDialogHero = hero;

        var heroId = TryGetHeroId(hero);
        if (heroId.HasValue)
        {
            _activeDialogHeroId = heroId.Value;
        }

        var heroForceLv = SafeGetMemberValue(hero, "heroForceLv");
        if (heroForceLv is int level && level >= 0)
        {
            _activeDialogHeroForceLv = level;
        }

        _activeDialogHeroName = SafeFormatValue(SafeGetMemberValue(hero, "heroName"));
    }

    private static string ApplyDialogMonthlyQuota(PlotInteractController controller, SinglePlotChoiceData choice, bool consume)
    {
        var timeNeedValue = SafeGetMemberValue(choice, "playerInteractionTimeNeed");
        var timeNeed = SafeFormatValue(timeNeedValue);
        if (string.IsNullOrEmpty(timeNeed) || string.Equals(timeNeed, "None", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var heroId = _activeDialogHeroId;
        if (heroId < 0)
        {
            return string.Empty;
        }

        var monthKey = GetCurrentWorldMonthKey();
        var key = $"{monthKey}|hero={heroId}|type={timeNeed}";
        var used = _dialogMonthlyUseCounts.TryGetValue(key, out var currentUsed) ? currentUsed : 0;
        var limit = GetDialogMonthlyLimit(heroId, timeNeed);
        var allowed = used < limit;

        if (consume && allowed)
        {
            _dialogMonthlyUseCounts[key] = used + 1;
            used++;
        }

        var remaining = Math.Max(0, limit - used);
        SyncVanillaDialogMonthlyUsage(choice, timeNeedValue, remaining);
        controller.meetCost = allowed;

        return $"heroName={_activeDialogHeroName}, heroID={heroId}, heroForceLv={_activeDialogHeroForceLv}, month={monthKey}, type={timeNeed}, used={used}/{limit}, allowed={allowed}";
    }

    private static int GetDialogMonthlyLimit(int heroId, string timeNeed)
    {
        var multiplier = _dialogMonthlyLimitMultiplier.Value;
        if (multiplier <= 0f)
        {
            return 0;
        }

        var scaled = (int)Math.Ceiling(multiplier);
        return Math.Max(1, scaled);
    }

    private static string GetCurrentWorldMonthKey()
    {
        try
        {
            var worldData = GameController.Instance?.worldData;
            if (worldData == null)
            {
                return "unknown-month";
            }

            var worldTime = SafeGetMemberValue(worldData, "worldTime");
            if (worldTime == null)
            {
                return "unknown-month";
            }

            var year = SafeGetMemberValue(worldTime, "year");
            var month = SafeGetMemberValue(worldTime, "month");
            return $"{SafeFormatValue(year)}-{SafeFormatValue(month)}";
        }
        catch
        {
            return "unknown-month";
        }
    }

    private static void SyncVanillaDialogMonthlyUsage(SinglePlotChoiceData choice, object? timeNeedValue, int remaining)
    {
        if (_activeDialogHero?.playerInteractionTimeData == null || timeNeedValue == null)
        {
            return;
        }

        var list = SafeGetMemberValue(_activeDialogHero.playerInteractionTimeData, "playerInteractTimeList");
        if (list == null)
        {
            return;
        }

        var timeNeedName = SafeFormatValue(timeNeedValue);
        if (string.IsNullOrEmpty(timeNeedName) || string.Equals(timeNeedName, "None", StringComparison.Ordinal))
        {
            return;
        }

        if (!Enum.TryParse(timeNeedName, out PlayerInteractionTimeType parsedType))
        {
            return;
        }

        TrySetIndexedValue(list, (int)parsedType, Math.Max(0, remaining));
    }

    private static object? TryGetIndexedValue(object list, int index)
    {
        try
        {
            var property = list.GetType().GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return property.GetValue(list, new object[] { index });
            }
        }
        catch
        {
        }

        try
        {
            var method = list.GetType().GetMethod("get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null)
            {
                return method.Invoke(list, new object[] { index });
            }
        }
        catch
        {
        }

        return null;
    }

    private static bool TrySetIndexedValue(object list, int index, object value)
    {
        try
        {
            var property = list.GetType().GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null && property.CanWrite)
            {
                property.SetValue(list, value, new object[] { index });
                return true;
            }
        }
        catch
        {
        }

        try
        {
            var method = list.GetType().GetMethod("set_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(list, new object[] { index, value });
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool TrySetMemberValue(object target, string memberName, object? value)
    {
        try
        {
            var type = target.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value, null);
                return true;
            }

            var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
                return true;
            }
        }
        catch
        {
        }

        return false;
    }
}
