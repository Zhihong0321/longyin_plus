using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("codex.longyin.tracedata", "LongYin Trace Data", "1.5.3")]
public sealed class LongYinTraceDataPlugin : BasePlugin
{
    internal static ManualLogSource LoggerInstance = null!;

    private static ConfigEntry<bool> _enabled = null!;
    private static ConfigEntry<bool> _traceFightMatchSimulation = null!;
    private Harmony? _harmony;

    public override void Load()
    {
        LoggerInstance = Log;
        _enabled = Config.Bind("General", "Enabled", false, "Turns the trace logger on for targeted reverse-engineering runs.");
        _traceFightMatchSimulation = Config.Bind("FightMatch", "TraceSimulation", true, "Logs grand competition / fight match simulation flow and battle handoff.");

        if (!_enabled.Value && !_traceFightMatchSimulation.Value)
        {
            Log.LogInfo("LongYin Trace Data loaded with tracing disabled.");
            return;
        }

        _harmony = new Harmony("codex.longyin.tracedata");

        if (_enabled.Value)
        {
            PatchMethod(typeof(HeroData), nameof(HeroData.ChangePower), new[] { typeof(float), typeof(bool) }, nameof(TracePrefix), nameof(TracePostfix));
            PatchMethod(typeof(HeroData), "ChangeNowPower", new[] { typeof(float) }, nameof(TracePrefix), nameof(TracePostfix));
            PatchMethod(typeof(HeroData), nameof(HeroData.AddSkillBookExp), new[] { typeof(float), typeof(KungfuSkillLvData), typeof(bool) }, nameof(TracePrefix), nameof(TracePostfix));
            PatchMethod(typeof(InfoController), nameof(InfoController.AddInfo), new[] { typeof(InfoType), typeof(string) }, nameof(InfoPrefix), nameof(InfoPostfix));
            PatchMethod(typeof(InfoController), nameof(InfoController.RealAddInfo), new[] { typeof(InfoData) }, nameof(RealInfoPrefix), nameof(RealInfoPostfix));
            PatchMethod(typeof(InfoController), nameof(InfoController.AddInfoTab), new[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(float), typeof(float), typeof(Color) }, nameof(InfoTabPrefix), nameof(InfoTabPostfix));
            PatchMethod(typeof(InfoController), nameof(InfoController.RealAddInfoTab), new[] { typeof(InfoTabData) }, nameof(RealInfoTabPrefix), nameof(RealInfoTabPostfix));
            PatchMethod(typeof(InfoController), nameof(InfoController.Awake), Type.EmptyTypes, null, nameof(InfoControllerAwakePostfix));

            PatchDeclaredByName(typeof(ReadBookController), nameof(TracePrefix), nameof(TracePostfix),
                "AutoReadBook", "BookSelectFinished", "ChooseReadBook", "ChooseReadBookMoney", "FinishReadBook",
                "GenerateReadBookPanel", "ReadBookChoosen", "RealStartReadBook", "ShowReadBookPanel",
                "StartReadBook", "SureStartReadBook");
            PatchDeclaredByName(typeof(StudySkillController), nameof(TracePrefix), nameof(TracePostfix),
                "FinishStudySkill", "RealStartStudySkill", "StudyDayCost", "StudyMoneyCost", "SureStartStudySkill");
            PatchDeclaredByName(typeof(KungfuSkillLvData), nameof(TracePrefix), nameof(TracePostfix),
                "Exp", "ExpNum", "ExpMax");
            PatchDeclaredByName(typeof(StartMenuController), nameof(TracePrefix), nameof(TracePostfix),
                "BirthSettingClicked", "PlusMinus", "PlusMinusButtonClicked", "RandomPlayerBaseAttri",
                "RandomPlayerBaseFightSkill", "RandomPlayerBaseLivingSkill", "RefreshPlayerAttri",
                "ResetPlayerAttri", "SetAttriPreset", "ShowStartMenu",
                "set_leftAttriPoint", "set_leftFightSkillPoint", "set_leftLivingSkillPoint");
            PatchDeclaredByName(typeof(AttriPresetButtonController), nameof(TracePrefix), nameof(TracePostfix),
                "OnClick");
            PatchDeclaredByName(typeof(AttriPresetData), nameof(TracePrefix), nameof(TracePostfix),
                "set_leftAttriPoint", "set_leftFightSkillPoint", "set_leftLivingSkillPoint");
            PatchDeclaredByName(typeof(BattleController), nameof(TracePrefix), nameof(TracePostfix),
                "BattleTimeScaleButtonClicked", "CanUseFastButton", "GetHalfBattleTimeScale", "GetThirdBattleTimeScale");
            PatchDeclaredByName(typeof(TimeScaleController), nameof(TracePrefix), nameof(TracePostfix),
                "SetSlowTime");
            PatchDeclaredByName(typeof(WorldData), nameof(TracePrefix), nameof(TracePostfix),
                "set_battleTimeScale");
            PatchDeclaredByName(typeof(HorseIconController), nameof(TracePrefix), nameof(TracePostfix),
                "SprintHorse");
            PatchDeclaredByName(typeof(HorseData), nameof(TracePrefix), nameof(TracePostfix),
                "StartSprint", "set_sprintTimeLeft", "set_sprintTimeCd");
            PatchDeclaredByName(typeof(HeroData), nameof(TracePrefix), nameof(TracePostfix),
                "RefreshHorseState");
            PatchDeclaredByName(typeof(BigMapController), nameof(TracePrefix), nameof(TracePostfix),
                "SetHorseButton");
            PatchMethod(typeof(IdentifyMatchController), nameof(IdentifyMatchController.ShowIdentifyMatchUI), new[] { typeof(float), typeof(string) }, nameof(IdentifyMatchPrefix), nameof(IdentifyMatchPostfix));
            PatchMethod(typeof(IdentifyMatchController), nameof(IdentifyMatchController.SetNowChooseTreasure), new[] { typeof(GameObject) }, nameof(IdentifyMatchPrefix), nameof(IdentifyMatchPostfix));
            PatchMethod(typeof(IdentifyMatchController), nameof(IdentifyMatchController.SureButtonClicked), Type.EmptyTypes, nameof(IdentifyMatchPrefix), nameof(IdentifyMatchPostfix));
            PatchMethod(typeof(IdentifyMatchController), nameof(IdentifyMatchController.RefreshResult), Type.EmptyTypes, nameof(IdentifyMatchPrefix), nameof(IdentifyMatchPostfix));
            PatchMethod(typeof(IdentifyMatchController), nameof(IdentifyMatchController.HideIdentifyMatchUI), Type.EmptyTypes, nameof(IdentifyMatchPrefix), nameof(IdentifyMatchPostfix));
            PatchMethod(typeof(ItemData), nameof(ItemData.ManagePlayerGuessTreasureLv), new[] { typeof(float) }, nameof(TreasureItemPrefix), nameof(TreasureItemPostfix));
            PatchMethod(typeof(ItemData), nameof(ItemData.TryIdentify), new[] { typeof(float) }, nameof(TreasureItemPrefix), nameof(TreasureItemFloatPostfix));
            PatchMethod(typeof(ItemData), nameof(ItemData.FullIdentify), Type.EmptyTypes, nameof(TreasureItemPrefix), nameof(TreasureItemFloatPostfix));
            PatchMethod(typeof(ItemData), nameof(ItemData.TryIdentifyOneResult), new[] { typeof(float), typeof(float) }, nameof(TryIdentifyOneResultPrefix), nameof(TryIdentifyOneResultPostfix));
            PatchMethod(typeof(DebateUIController), nameof(DebateUIController.ShowDebateUI), new[] { typeof(HeroData), typeof(string) }, nameof(DebatePrefix), nameof(DebatePostfix));
            PatchMethod(typeof(DebateUIController), nameof(DebateUIController.UseDebateCard), new[] { typeof(GameObject) }, nameof(DebatePrefix), nameof(DebatePostfix));
            PatchMethod(typeof(DebateUIController), nameof(DebateUIController.NextDebateRound), Type.EmptyTypes, nameof(DebatePrefix), nameof(DebatePostfix));
            PatchMethod(typeof(DebateUIController), nameof(DebateUIController.ChangePatient), new[] { typeof(bool), typeof(float) }, nameof(DebateChangePatientPrefix), nameof(DebateChangePatientPostfix));
            PatchMethod(typeof(DebateUIController), nameof(DebateUIController.RefreshPatientUI), Type.EmptyTypes, nameof(DebatePrefix), nameof(DebatePostfix));
            PatchMethod(typeof(DebateUIController), nameof(DebateUIController.HideDebateUI), Type.EmptyTypes, nameof(DebatePrefix), nameof(DebatePostfix));
            PatchMethod(typeof(DebateCardController), nameof(DebateCardController.OnClick), Type.EmptyTypes, nameof(DebateCardPrefix), nameof(DebateCardPostfix));
            PatchMethod(typeof(DrinkUIController), nameof(DrinkUIController.ShowDrinkUI), new[] { typeof(DrinkType), typeof(HeroData), typeof(ItemData), typeof(string) }, nameof(DrinkPrefix), nameof(DrinkPostfix));
            PatchDeclaredByName(typeof(DrinkUIController), nameof(DrinkPrefix), nameof(DrinkPostfix),
                "PlayerPour", "EnemyPour");
            PatchMethod(typeof(DrinkUIController), nameof(DrinkUIController.GetDrinkCost), new[] { typeof(float) }, nameof(DrinkCostPrefix), nameof(DrinkCostPostfix));
            PatchDeclaredByName(typeof(DrinkUIController), nameof(DrinkPowerPrefix), nameof(DrinkPowerPostfix),
                "ChangePower", "ChangeNowPower");
            PatchMethod(typeof(DrinkUIController), nameof(DrinkUIController.HideDrinkUI), Type.EmptyTypes, nameof(DrinkPrefix), nameof(DrinkPostfix));
            PatchMethod(typeof(CraftUIController), nameof(CraftUIController.OpenCraftUI), new[] { typeof(CraftType), typeof(AreaBuildingData), typeof(bool) }, nameof(CraftPrefix), nameof(CraftPostfix));
            PatchMethod(typeof(CraftUIController), nameof(CraftUIController.GetCraftTime), Type.EmptyTypes, nameof(CraftTimePrefix), nameof(CraftTimePostfix));
            PatchMethod(typeof(CraftUIController), nameof(CraftUIController.CraftButtonClicked), Type.EmptyTypes, nameof(CraftPrefix), nameof(CraftPostfix));
            PatchMethod(typeof(CraftUIController), nameof(CraftUIController.ShowCraftResultChoosePanel), Type.EmptyTypes, nameof(CraftPrefix), nameof(CraftPostfix));
            PatchMethod(typeof(CraftUIController), nameof(CraftUIController.CraftResultChoosen), new[] { typeof(int) }, nameof(CraftResultChoicePrefix), nameof(CraftResultChoicePostfix));
            PatchMethod(typeof(CraftUIController), nameof(CraftUIController.HideCraftUI), Type.EmptyTypes, nameof(CraftPrefix), nameof(CraftPostfix));
            PatchMethod(typeof(CraftPoisonUIController), nameof(CraftPoisonUIController.OpenCraftPoisonUI), new[] { typeof(AreaBuildingData), typeof(bool) }, nameof(CraftPoisonPrefix), nameof(CraftPoisonPostfix));
            PatchMethod(typeof(CraftPoisonUIController), nameof(CraftPoisonUIController.RefreshCraftPoisonInfo), Type.EmptyTypes, nameof(CraftPoisonPrefix), nameof(CraftPoisonPostfix));
            PatchMethod(typeof(CraftPoisonUIController), nameof(CraftPoisonUIController.GetCostTime), Type.EmptyTypes, nameof(CraftPoisonTimePrefix), nameof(CraftPoisonTimePostfix));
            PatchMethod(typeof(EnhanceUIController), nameof(EnhanceUIController.OpenEnhanceUI), new[] { typeof(CraftType), typeof(AreaBuildingData), typeof(bool) }, nameof(EnhancePrefix), nameof(EnhancePostfix));
            PatchMethod(typeof(EnhanceUIController), nameof(EnhanceUIController.EnhanceNeedTime), Type.EmptyTypes, nameof(EnhanceTimePrefix), nameof(EnhanceTimePostfix));
            PatchMethod(typeof(EnhanceUIController), nameof(EnhanceUIController.EnhanceButtonClicked), Type.EmptyTypes, nameof(EnhancePrefix), nameof(EnhancePostfix));
            PatchMethod(typeof(SpeEnhanceEquipController), nameof(SpeEnhanceEquipController.ShowSpeEnhanceEquipUI), Type.EmptyTypes, nameof(SpeEnhancePrefix), nameof(SpeEnhancePostfix));
            PatchMethod(typeof(SpeEnhanceEquipController), nameof(SpeEnhanceEquipController.GetTimeNeed), Type.EmptyTypes, nameof(SpeEnhanceTimePrefix), nameof(SpeEnhanceTimePostfix));
            PatchMethod(typeof(SpeEnhanceEquipController), nameof(SpeEnhanceEquipController.EnhanceButtonClicked), Type.EmptyTypes, nameof(SpeEnhancePrefix), nameof(SpeEnhancePostfix));
            PatchMethod(typeof(PlotController), nameof(PlotController.CraftResultChoosen), new[] { typeof(ItemData) }, nameof(PlotCraftResultPrefix), nameof(PlotCraftResultPostfix));
            PatchMethod(typeof(PlotController), nameof(PlotController.FinishCraft), Type.EmptyTypes, nameof(PlotCraftPrefix), nameof(PlotCraftPostfix));
        }

        if (_traceFightMatchSimulation.Value)
        {
            PatchDeclaredByName(typeof(FightMatchController), nameof(FightMatchPrefix), nameof(FightMatchPostfix),
                "RestartFightMatch", "StartFightMatch", "StartFightRound", "EndFightRound", "EndFightMatch",
                "SureWatchFight", "CancelWatchFight", "NextButtonClicked", "RefreshNextButton",
                "SetRound", "SetSkippingButtonState", "SetSkippingState", "SkipButtonClicked",
                "RoundFinished", "RegenerateFightMatchCouples", "FightCoupleHavePlayer");
            PatchDeclaredByName(typeof(BattleController), nameof(BattleFlowPrefix), nameof(BattleFlowPostfix),
                "PrepareBattleMap", "ShowPrepareUIPanel", "StartBattle", "StartBattleButtonClicked",
                "PrepareHavePlayer", "SetHavePlayer", "BattleEnd", "BattleRealEnd",
                "BattleSkipButtonClicked", "SureSkipBattle", "GiveUpBattleButtonClicked");
            Log.LogInfo("Fight match simulation tracing enabled.");
        }

        Log.LogInfo($"LongYin Trace Data loaded. Full trace={_enabled.Value}, fight match trace={_traceFightMatchSimulation.Value}");
        Log.LogInfo($"Trace session marker: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        if (_enabled.Value)
        {
            LogCraftMethodInventory();
        }
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

    private static void InfoPrefix(MethodBase __originalMethod, object[] __args, out string __state)
    {
        var type = __args.Length > 0 ? __args[0] : null;
        var text = __args.Length > 1 ? __args[1] : null;
        __state = $"type={SafeFormatValue(type)}, text={SafeFormatValue(text)}";
        LoggerInstance.LogInfo($"INFO ENTER {DescribeMethod(__originalMethod)} {__state}");
    }

    private static void InfoPostfix(MethodBase __originalMethod, string __state)
    {
        LoggerInstance.LogInfo($"INFO EXIT  {DescribeMethod(__originalMethod)} {__state}");
    }

    private static void RealInfoPrefix(MethodBase __originalMethod, object[] __args, out string __state)
    {
        var infoData = __args.Length > 0 ? __args[0] as InfoData : null;
        __state = DescribeInfoData(infoData);
        LoggerInstance.LogInfo($"INFO REAL ENTER {DescribeMethod(__originalMethod)} {__state}");
    }

    private static void RealInfoPostfix(MethodBase __originalMethod, string __state)
    {
        LoggerInstance.LogInfo($"INFO REAL EXIT  {DescribeMethod(__originalMethod)} {__state}");
    }

    private static void InfoTabPrefix(MethodBase __originalMethod, object[] __args, out string __state)
    {
        var infoText = __args.Length > 0 ? __args[0] : null;
        var atlasName = __args.Length > 1 ? __args[1] : null;
        var infoPic = __args.Length > 2 ? __args[2] : null;
        var soundName = __args.Length > 3 ? __args[3] : null;
        var volumn = __args.Length > 4 ? __args[4] : null;
        var lastTime = __args.Length > 5 ? __args[5] : null;
        var picColor = __args.Length > 6 ? __args[6] : null;
        __state =
            $"text={SafeFormatValue(infoText)}, atlas={SafeFormatValue(atlasName)}, pic={SafeFormatValue(infoPic)}, sound={SafeFormatValue(soundName)}, vol={SafeFormatValue(volumn)}, last={SafeFormatValue(lastTime)}, color={DescribeColorObject(picColor)}";
        LoggerInstance.LogInfo($"INFOTAB ENTER {DescribeMethod(__originalMethod)} {__state}");
    }

    private static void InfoTabPostfix(MethodBase __originalMethod, string __state)
    {
        LoggerInstance.LogInfo($"INFOTAB EXIT  {DescribeMethod(__originalMethod)} {__state}");
    }

    private static void RealInfoTabPrefix(MethodBase __originalMethod, object[] __args, out string __state)
    {
        var infoTabData = __args.Length > 0 ? __args[0] as InfoTabData : null;
        __state = DescribeInfoTabData(infoTabData);
        LoggerInstance.LogInfo($"INFOTAB REAL ENTER {DescribeMethod(__originalMethod)} {__state}");
    }

    private static void RealInfoTabPostfix(MethodBase __originalMethod, string __state)
    {
        LoggerInstance.LogInfo($"INFOTAB REAL EXIT  {DescribeMethod(__originalMethod)} {__state}");
    }

    private static void InfoControllerAwakePostfix(InfoController? __instance)
    {
        if (__instance == null)
        {
            return;
        }

        LoggerInstance.LogInfo(
            $"INFOCONTROLLER AWAKE popInfoTabPrefab={DescribeGameObjectTree(__instance.popInfoTabPrefab, maxDepth: 2)}");
    }

    private static void FightMatchPrefix(MethodBase __originalMethod, FightMatchController? __instance, object[] __args, out string __state)
    {
        __state = DescribeFightMatch(__instance);
        LoggerInstance.LogInfo($"FIGHTMATCH ENTER {DescribeMethod(__originalMethod)} stateBefore={__state} args={DescribeFightMatchArgs(__args)}");
    }

    private static void FightMatchPostfix(MethodBase __originalMethod, FightMatchController? __instance, object[] __args, string __state)
    {
        LoggerInstance.LogInfo($"FIGHTMATCH EXIT  {DescribeMethod(__originalMethod)} stateBefore={__state} stateAfter={DescribeFightMatch(__instance)}");
    }

    private static void BattleFlowPrefix(MethodBase __originalMethod, BattleController? __instance, object[] __args, out string __state)
    {
        __state = DescribeBattleFlow(__instance);
        LoggerInstance.LogInfo($"BATTLEFLOW ENTER {DescribeMethod(__originalMethod)} stateBefore={__state} args={DescribeFightMatchArgs(__args)}");
    }

    private static void BattleFlowPostfix(MethodBase __originalMethod, BattleController? __instance, object[] __args, string __state)
    {
        LoggerInstance.LogInfo($"BATTLEFLOW EXIT  {DescribeMethod(__originalMethod)} stateBefore={__state} stateAfter={DescribeBattleFlow(__instance)}");
    }

    private static void IdentifyMatchPrefix(MethodBase __originalMethod, IdentifyMatchController? __instance, object[] __args, out string __state)
    {
        __state = DescribeIdentifyMatch(__instance);
        LoggerInstance.LogInfo($"IDENTIFY ENTER {DescribeMethod(__originalMethod)} stateBefore={__state} args={DescribeArgs(__args)}");
    }

    private static void IdentifyMatchPostfix(MethodBase __originalMethod, IdentifyMatchController? __instance, object[] __args, string __state)
    {
        var after = DescribeIdentifyMatch(__instance);
        LoggerInstance.LogInfo($"IDENTIFY EXIT  {DescribeMethod(__originalMethod)} stateBefore={__state} stateAfter={after}");
    }

    private static void TreasureItemPrefix(MethodBase __originalMethod, ItemData? __instance, object[] __args, out string __state)
    {
        if (!ShouldTraceTreasureItem(__instance))
        {
            __state = string.Empty;
            return;
        }

        __state = DescribeItemData(__instance!, includeValues: true);
        LoggerInstance.LogInfo($"TREASURE ENTER {DescribeMethod(__originalMethod)} itemBefore={__state} args={DescribeArgs(__args)}");
    }

    private static void TreasureItemPostfix(MethodBase __originalMethod, ItemData? __instance, object[] __args, string __state)
    {
        if (string.IsNullOrEmpty(__state) || __instance == null)
        {
            return;
        }

        LoggerInstance.LogInfo($"TREASURE EXIT  {DescribeMethod(__originalMethod)} itemBefore={__state} itemAfter={DescribeItemData(__instance, includeValues: true)}");
    }

    private static void TreasureItemFloatPostfix(MethodBase __originalMethod, ItemData? __instance, object[] __args, string __state, float __result)
    {
        if (string.IsNullOrEmpty(__state) || __instance == null)
        {
            return;
        }

        LoggerInstance.LogInfo($"TREASURE EXIT  {DescribeMethod(__originalMethod)} result={SafeFormatValue(__result)} itemBefore={__state} itemAfter={DescribeItemData(__instance, includeValues: true)}");
    }

    private static void TryIdentifyOneResultPrefix(MethodBase __originalMethod, object[] __args, out string __state)
    {
        if (!IsIdentifyMatchActive())
        {
            __state = string.Empty;
            return;
        }

        __state = DescribeIdentifyMatch(IdentifyMatchController.Instance);
        LoggerInstance.LogInfo($"TREASURE ENTER {DescribeMethod(__originalMethod)} identifyState={__state} args={DescribeArgs(__args)}");
    }

    private static void TryIdentifyOneResultPostfix(MethodBase __originalMethod, object[] __args, string __state, bool __result)
    {
        if (string.IsNullOrEmpty(__state))
        {
            return;
        }

        LoggerInstance.LogInfo($"TREASURE EXIT  {DescribeMethod(__originalMethod)} result={__result} identifyStateAfter={DescribeIdentifyMatch(IdentifyMatchController.Instance)}");
    }

    private static void DebatePrefix(MethodBase __originalMethod, DebateUIController? __instance, object[] __args, out string __state)
    {
        __state = DescribeDebate(__instance);
        LoggerInstance.LogInfo($"DEBATE ENTER {DescribeMethod(__originalMethod)} stateBefore={__state} args={DescribeDebateArgs(__args)}");
    }

    private static void DebatePostfix(MethodBase __originalMethod, DebateUIController? __instance, object[] __args, string __state)
    {
        LoggerInstance.LogInfo($"DEBATE EXIT  {DescribeMethod(__originalMethod)} stateBefore={__state} stateAfter={DescribeDebate(__instance)}");
    }

    private static void DebateChangePatientPrefix(MethodBase __originalMethod, DebateUIController? __instance, bool isPlayer, float num, object[] __args, out string __state)
    {
        __state = DescribeDebate(__instance);
        var side = isPlayer ? "player" : "enemy";
        LoggerInstance.LogInfo(
            $"DEBATE HP ENTER {DescribeMethod(__originalMethod)} target={side} delta={SafeFormatValue(num)} stateBefore={__state} args={DescribeDebateArgs(__args)}");
    }

    private static void DebateChangePatientPostfix(MethodBase __originalMethod, DebateUIController? __instance, bool isPlayer, float num, object[] __args, string __state)
    {
        var side = isPlayer ? "player" : "enemy";
        LoggerInstance.LogInfo(
            $"DEBATE HP EXIT  {DescribeMethod(__originalMethod)} target={side} delta={SafeFormatValue(num)} stateBefore={__state} stateAfter={DescribeDebate(__instance)}");
    }

    private static void DebateCardPrefix(MethodBase __originalMethod, DebateCardController? __instance, object[] __args, out string __state)
    {
        __state = DescribeDebateCardController(__instance);
        LoggerInstance.LogInfo(
            $"DEBATE CARD ENTER {DescribeMethod(__originalMethod)} cardBefore={__state} debateState={DescribeDebate(DebateUIController.Instance)} args={DescribeDebateArgs(__args)}");
    }

    private static void DebateCardPostfix(MethodBase __originalMethod, DebateCardController? __instance, object[] __args, string __state)
    {
        LoggerInstance.LogInfo(
            $"DEBATE CARD EXIT  {DescribeMethod(__originalMethod)} cardBefore={__state} debateStateAfter={DescribeDebate(DebateUIController.Instance)}");
    }

    private static void DrinkPrefix(MethodBase __originalMethod, DrinkUIController? __instance, object[] __args, out string __state)
    {
        __state = DescribeDrink(__instance);
        LoggerInstance.LogInfo($"DRINK ENTER {DescribeMethod(__originalMethod)} stateBefore={__state} args={DescribeDrinkArgs(__args)}");
    }

    private static void DrinkPostfix(MethodBase __originalMethod, DrinkUIController? __instance, object[] __args, string __state)
    {
        LoggerInstance.LogInfo($"DRINK EXIT  {DescribeMethod(__originalMethod)} stateBefore={__state} stateAfter={DescribeDrink(__instance)}");
    }

    private static void DrinkCostPrefix(MethodBase __originalMethod, DrinkUIController? __instance, float fillAmount, object[] __args, out string __state)
    {
        __state = DescribeDrink(__instance);
        LoggerInstance.LogInfo(
            $"DRINK COST ENTER {DescribeMethod(__originalMethod)} input={SafeFormatValue(fillAmount)} stateBefore={__state} args={DescribeDrinkArgs(__args)}");
    }

    private static void DrinkCostPostfix(MethodBase __originalMethod, DrinkUIController? __instance, float fillAmount, object[] __args, string __state, float __result)
    {
        LoggerInstance.LogInfo(
            $"DRINK COST EXIT  {DescribeMethod(__originalMethod)} input={SafeFormatValue(fillAmount)} result={SafeFormatValue(__result)} stateBefore={__state} stateAfter={DescribeDrink(__instance)}");
    }

    private static void DrinkPowerPrefix(MethodBase __originalMethod, DrinkUIController? __instance, object[] __args, out string __state)
    {
        __state = DescribeDrink(__instance);
        LoggerInstance.LogInfo($"DRINK POWER ENTER {DescribeMethod(__originalMethod)} stateBefore={__state} args={DescribeDrinkArgs(__args)}");
    }

    private static void DrinkPowerPostfix(MethodBase __originalMethod, DrinkUIController? __instance, object[] __args, string __state)
    {
        LoggerInstance.LogInfo($"DRINK POWER EXIT  {DescribeMethod(__originalMethod)} stateBefore={__state} stateAfter={DescribeDrink(__instance)}");
    }

    private static void CraftPrefix(MethodBase __originalMethod, CraftUIController? __instance, object[] __args, out string __state)
    {
        __state = DescribeCraft(__instance);
        LoggerInstance.LogInfo($"CRAFT ENTER {DescribeMethod(__originalMethod)} stateBefore={__state} args={DescribeCraftArgs(__args)}");
    }

    private static void CraftPostfix(MethodBase __originalMethod, CraftUIController? __instance, object[] __args, string __state)
    {
        LoggerInstance.LogInfo($"CRAFT EXIT  {DescribeMethod(__originalMethod)} stateBefore={__state} stateAfter={DescribeCraft(__instance)}");
    }

    private static void CraftTimePrefix(MethodBase __originalMethod, CraftUIController? __instance, object[] __args, out string __state)
    {
        __state = DescribeCraft(__instance);
        LoggerInstance.LogInfo($"CRAFT TIME ENTER {DescribeMethod(__originalMethod)} stateBefore={__state} args={DescribeCraftArgs(__args)}");
    }

    private static void CraftTimePostfix(MethodBase __originalMethod, CraftUIController? __instance, object[] __args, string __state, int __result)
    {
        LoggerInstance.LogInfo($"CRAFT TIME EXIT  {DescribeMethod(__originalMethod)} result={__result} stateBefore={__state} stateAfter={DescribeCraft(__instance)}");
    }

    private static void CraftResultChoicePrefix(MethodBase __originalMethod, CraftUIController? __instance, int id, object[] __args, out string __state)
    {
        __state = DescribeCraft(__instance);
        var selectedItem = TryGetCraftResult(__instance, id);
        LoggerInstance.LogInfo(
            $"CRAFT PICK ENTER {DescribeMethod(__originalMethod)} id={id} selected={DescribeOptionalItem(selectedItem)} stateBefore={__state} args={DescribeCraftArgs(__args)}");
    }

    private static void CraftResultChoicePostfix(MethodBase __originalMethod, CraftUIController? __instance, int id, object[] __args, string __state)
    {
        var selectedItem = TryGetCraftResult(__instance, id);
        LoggerInstance.LogInfo(
            $"CRAFT PICK EXIT  {DescribeMethod(__originalMethod)} id={id} selectedAfter={DescribeOptionalItem(selectedItem)} stateBefore={__state} stateAfter={DescribeCraft(__instance)}");
    }

    private static void PlotCraftResultPrefix(MethodBase __originalMethod, ItemData? craftResult, object[] __args, out string __state)
    {
        __state = DescribeCraft(CraftUIController.Instance);
        LoggerInstance.LogInfo(
            $"CRAFT PLOT ENTER {DescribeMethod(__originalMethod)} item={DescribeOptionalItem(craftResult)} craftState={__state} args={DescribeCraftArgs(__args)}");
    }

    private static void PlotCraftResultPostfix(MethodBase __originalMethod, ItemData? craftResult, object[] __args, string __state)
    {
        LoggerInstance.LogInfo(
            $"CRAFT PLOT EXIT  {DescribeMethod(__originalMethod)} itemAfter={DescribeOptionalItem(craftResult)} craftStateBefore={__state} craftStateAfter={DescribeCraft(CraftUIController.Instance)}");
    }

    private static void PlotCraftPrefix(MethodBase __originalMethod, PlotController? __instance, object[] __args, out string __state)
    {
        __state = DescribeCraft(CraftUIController.Instance);
        LoggerInstance.LogInfo($"CRAFT FINISH ENTER {DescribeMethod(__originalMethod)} craftState={__state} args={DescribeCraftArgs(__args)}");
    }

    private static void PlotCraftPostfix(MethodBase __originalMethod, PlotController? __instance, object[] __args, string __state)
    {
        LoggerInstance.LogInfo($"CRAFT FINISH EXIT  {DescribeMethod(__originalMethod)} craftStateBefore={__state} craftStateAfter={DescribeCraft(CraftUIController.Instance)}");
    }

    private static void CraftPoisonPrefix(MethodBase __originalMethod, CraftPoisonUIController? __instance, object[] __args, out string __state)
    {
        __state = DescribeCraftPoison(__instance);
        LoggerInstance.LogInfo($"CRAFT POISON ENTER {DescribeMethod(__originalMethod)} stateBefore={__state} args={DescribeCraftArgs(__args)}");
    }

    private static void CraftPoisonPostfix(MethodBase __originalMethod, CraftPoisonUIController? __instance, object[] __args, string __state)
    {
        LoggerInstance.LogInfo($"CRAFT POISON EXIT  {DescribeMethod(__originalMethod)} stateBefore={__state} stateAfter={DescribeCraftPoison(__instance)}");
    }

    private static void CraftPoisonTimePrefix(MethodBase __originalMethod, CraftPoisonUIController? __instance, object[] __args, out string __state)
    {
        __state = DescribeCraftPoison(__instance);
        LoggerInstance.LogInfo($"CRAFT POISON TIME ENTER {DescribeMethod(__originalMethod)} stateBefore={__state} args={DescribeCraftArgs(__args)}");
    }

    private static void CraftPoisonTimePostfix(MethodBase __originalMethod, CraftPoisonUIController? __instance, object[] __args, string __state, int __result)
    {
        LoggerInstance.LogInfo($"CRAFT POISON TIME EXIT  {DescribeMethod(__originalMethod)} result={__result} stateBefore={__state} stateAfter={DescribeCraftPoison(__instance)}");
    }

    private static void EnhancePrefix(MethodBase __originalMethod, EnhanceUIController? __instance, object[] __args, out string __state)
    {
        __state = DescribeEnhance(__instance);
        LoggerInstance.LogInfo($"ENHANCE ENTER {DescribeMethod(__originalMethod)} stateBefore={__state} args={DescribeCraftArgs(__args)}");
    }

    private static void EnhancePostfix(MethodBase __originalMethod, EnhanceUIController? __instance, object[] __args, string __state)
    {
        LoggerInstance.LogInfo($"ENHANCE EXIT  {DescribeMethod(__originalMethod)} stateBefore={__state} stateAfter={DescribeEnhance(__instance)}");
    }

    private static void EnhanceTimePrefix(MethodBase __originalMethod, EnhanceUIController? __instance, object[] __args, out string __state)
    {
        __state = DescribeEnhance(__instance);
        LoggerInstance.LogInfo($"ENHANCE TIME ENTER {DescribeMethod(__originalMethod)} stateBefore={__state} args={DescribeCraftArgs(__args)}");
    }

    private static void EnhanceTimePostfix(MethodBase __originalMethod, EnhanceUIController? __instance, object[] __args, string __state, int __result)
    {
        LoggerInstance.LogInfo($"ENHANCE TIME EXIT  {DescribeMethod(__originalMethod)} result={__result} stateBefore={__state} stateAfter={DescribeEnhance(__instance)}");
    }

    private static void SpeEnhancePrefix(MethodBase __originalMethod, SpeEnhanceEquipController? __instance, object[] __args, out string __state)
    {
        __state = DescribeSpeEnhance(__instance);
        LoggerInstance.LogInfo($"SPE ENHANCE ENTER {DescribeMethod(__originalMethod)} stateBefore={__state} args={DescribeCraftArgs(__args)}");
    }

    private static void SpeEnhancePostfix(MethodBase __originalMethod, SpeEnhanceEquipController? __instance, object[] __args, string __state)
    {
        LoggerInstance.LogInfo($"SPE ENHANCE EXIT  {DescribeMethod(__originalMethod)} stateBefore={__state} stateAfter={DescribeSpeEnhance(__instance)}");
    }

    private static void SpeEnhanceTimePrefix(MethodBase __originalMethod, SpeEnhanceEquipController? __instance, object[] __args, out string __state)
    {
        __state = DescribeSpeEnhance(__instance);
        LoggerInstance.LogInfo($"SPE ENHANCE TIME ENTER {DescribeMethod(__originalMethod)} stateBefore={__state} args={DescribeCraftArgs(__args)}");
    }

    private static void SpeEnhanceTimePostfix(MethodBase __originalMethod, SpeEnhanceEquipController? __instance, object[] __args, string __state, int __result)
    {
        LoggerInstance.LogInfo($"SPE ENHANCE TIME EXIT  {DescribeMethod(__originalMethod)} result={__result} stateBefore={__state} stateAfter={DescribeSpeEnhance(__instance)}");
    }

    private static string DescribeMethod(MethodBase method)
    {
        return $"{method.DeclaringType?.Name}.{method.Name}";
    }

    private static void LogCraftMethodInventory()
    {
        LogMethodInventory(typeof(CraftUIController), "Craft", "Time", "Hour", "Day", "Result", "Wait", "Coroutine", "Delay", "Refresh");
        LogMethodInventory(typeof(PlotController), "Craft", "Time", "Hour", "Day", "Result", "Wait", "Coroutine", "Delay", "Finish");
        LogMethodInventory(typeof(GameController), "Craft", "Time", "Hour", "Day", "Result", "Wait", "Coroutine", "Delay");
    }

    private static void LogMethodInventory(Type type, params string[] terms)
    {
        try
        {
            LoggerInstance.LogInfo($"METHOD INVENTORY {type.Name} START");
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                foreach (var term in terms)
                {
                    if (method.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    var parameterText = string.Join(", ", Array.ConvertAll(method.GetParameters(), parameter => parameter.ParameterType.Name));
                    LoggerInstance.LogInfo($"METHOD INVENTORY {type.Name}: {method.ReturnType.Name} {method.Name}({parameterText})");
                    break;
                }
            }

            LoggerInstance.LogInfo($"METHOD INVENTORY {type.Name} END");
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Method inventory failed for {type.Name}: {ex.Message}");
        }
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

        var playerHorse = TryGetPlayerHorse(playerHero);
        if (playerHorse != null)
        {
            parts.Add(DescribeObject("horse", playerHorse));
        }

        var worldData = TryGetWorldData();
        if (worldData != null)
        {
            parts.Add(DescribeObject("world", worldData));
        }

        var globalData = DescribeStaticType("global", typeof(GlobalData));
        if (!string.IsNullOrEmpty(globalData))
        {
            parts.Add(globalData);
        }

        var timeScaleController = TryGetTimeScaleController();
        if (timeScaleController != null && !ReferenceEquals(timeScaleController, instance))
        {
            parts.Add(DescribeObject("timeScale", timeScaleController));
        }

        var battleController = TryGetBattleController();
        if (battleController != null && !ReferenceEquals(battleController, instance))
        {
            parts.Add(DescribeObject("battle", battleController));
        }

        var drinkController = TryGetDrinkController();
        if (drinkController != null && !ReferenceEquals(drinkController, instance))
        {
            parts.Add(DescribeObject("drink", drinkController));
        }

        if (instance != null)
        {
            parts.Add(DescribeObject(instance.GetType().Name, instance));
        }

        if (args != null)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] != null && (args[i] is HeroData || args[i] is KungfuSkillLvData || args[i] is ItemData || args[i] is HorseData || args[i] is IdentifyMatchController || args[i] is DebateUIController || args[i] is DebateCardController || args[i] is DebateCardData || args[i] is DrinkUIController || args[i] is CraftUIController || args[i] is GameObject))
                {
                    parts.Add(DescribeObject($"arg{i}", args[i]!));
                }
            }
        }

        return string.Join(" | ", parts);
    }

    private static string DescribeObject(string label, object obj)
    {
        if (obj is IdentifyMatchController identifyMatch)
        {
            return $"{label}={DescribeIdentifyMatch(identifyMatch)}";
        }

        if (obj is DebateUIController debate)
        {
            return $"{label}={DescribeDebate(debate)}";
        }

        if (obj is DebateCardController debateCardController)
        {
            return $"{label}={DescribeDebateCardController(debateCardController)}";
        }

        if (obj is DebateCardData debateCardData)
        {
            return $"{label}={DescribeDebateCard(debateCardData)}";
        }

        if (obj is DrinkUIController drink)
        {
            return $"{label}={DescribeDrink(drink)}";
        }

        if (obj is CraftUIController craft)
        {
            return $"{label}={DescribeCraft(craft)}";
        }

        if (obj is ItemData item)
        {
            return $"{label}={DescribeItemData(item, includeValues: false)}";
        }

        if (obj is GameObject gameObject)
        {
            return $"{label}={DescribeTreasureGameObject(gameObject)}";
        }

        var interesting = new[]
        {
            "power", "maxPower", "nowPower", "leftPower",
            "leftAttriPoint", "leftFightSkillPoint", "leftLivingSkillPoint",
            "battleTimeScale", "nowScale", "battleTime", "nowSlowTimeScale",
            "paused", "battlePaused", "playingAnim",
            "speed", "sprint", "speedAdd", "sprintAdd",
            "sprintTimeLeft", "sprintTimeCd", "travelSpeed", "moveSpeed",
            "havePower", "isSprint", "inSafeArea", "horsePower",
            "hp", "exp", "expNum", "expMax", "lv", "level",
            "studyDayCost", "readCostMoney", "id", "skillID"
        };
        var parts = new List<string>();

        foreach (var name in interesting)
        {
            var value = SafeProperty(obj, name) ?? SafeField(obj, name);
            if (value != null)
            {
                parts.Add($"{label}.{name}={SafeFormatValue(value)}");
            }
        }

        return parts.Count == 0 ? $"{label}=<{obj.GetType().Name}>" : string.Join("; ", parts);
    }

    private static bool ShouldTraceTreasureItem(ItemData? item)
    {
        if (item == null || item.treasureData == null)
        {
            return false;
        }

        return IsIdentifyMatchActive();
    }

    private static bool IsIdentifyMatchActive()
    {
        try
        {
            var controller = IdentifyMatchController.Instance;
            if (controller == null)
            {
                return false;
            }

            if (controller.identifyMatchState != IdentifyMatchState.None)
            {
                return true;
            }

            return controller.identifyMatchUIPanel != null && controller.identifyMatchUIPanel.activeInHierarchy;
        }
        catch
        {
            return false;
        }
    }

    private static string DescribeIdentifyMatch(IdentifyMatchController? controller)
    {
        if (controller == null)
        {
            return "identifyMatch=null";
        }

        var parts = new List<string>
        {
            $"state={controller.identifyMatchState}",
            $"difficulty={SafeFormatValue(controller.difficulty)}",
            $"correctNum={controller.correctNum}",
            $"fightEnd={SafeFormatValue(controller.fightEndCallFuc)}",
            $"selected={DescribeTreasureGameObject(controller.nowChooseTreasure)}",
            $"correct={DescribeTreasureList(controller.correctTreasure)}",
            $"results={DescribeBoolList(controller.identifyResult)}"
        };

        var panelTreasures = DescribePanelTreasures(controller);
        if (!string.IsNullOrEmpty(panelTreasures))
        {
            parts.Add($"panel={panelTreasures}");
        }

        return string.Join("; ", parts);
    }

    private static string DescribeDebate(DebateUIController? controller)
    {
        if (controller == null)
        {
            return "debate=null";
        }

        DebateCardData? playerOutCard = null;
        DebateCardData? enemyOutCard = null;

        try
        {
            playerOutCard = controller.GetOutCard(true);
        }
        catch
        {
        }

        try
        {
            enemyOutCard = controller.GetOutCard(false);
        }
        catch
        {
        }

        var parts = new List<string>
        {
            $"state={controller.debateState}",
            $"playerPatient={SafeFormatValue(controller.playerPatient)}/{SafeFormatValue(controller.playerMaxPatient)}",
            $"enemyPatient={SafeFormatValue(controller.enemyPatient)}/{SafeFormatValue(controller.enemyMaxPatient)}",
            $"playerRound={controller.playerActiveRound}",
            $"playerAngryRound={controller.playerAngryRound}",
            $"enemyAngryRound={controller.enemyAngryRound}",
            $"cardUsed={controller.cardUsed}",
            $"playerWin={controller.playerWin}",
            $"topic={controller.nowTopic}",
            $"nextTopic={controller.nextDebateTopic}",
            $"enemy={TryGetHeroName(controller.enemyData)}",
            $"playerOut={DescribeDebateCard(playerOutCard)}",
            $"enemyOut={DescribeDebateCard(enemyOutCard)}"
        };

        return string.Join("; ", parts);
    }

    private static string DescribeDebateCardController(DebateCardController? controller)
    {
        if (controller == null)
        {
            return "debateCardController=null";
        }

        return DescribeDebateCard(controller.cardData);
    }

    private static string DescribeDebateCard(DebateCardData? card)
    {
        if (card == null)
        {
            return "null";
        }

        return $"playerCard={card.isPlayerCard}, spe={card.isSpeCard}, rareLv={card.rareLv}, targetAttriID={card.targetAttriID}, attriLv={card.attriLv}";
    }

    private static string DescribeDebateArgs(object[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return "[]";
        }

        var parts = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var value = args[i];
            if (value is DebateCardData debateCard)
            {
                parts.Add($"{i}:{DescribeDebateCard(debateCard)}");
                continue;
            }

            if (value is DebateCardController debateCardController)
            {
                parts.Add($"{i}:{DescribeDebateCardController(debateCardController)}");
                continue;
            }

            if (value is GameObject gameObject)
            {
                var debateCardFromObject = TryGetDebateCardController(gameObject);
                if (debateCardFromObject != null)
                {
                    parts.Add($"{i}:{DescribeDebateCardController(debateCardFromObject)}");
                    continue;
                }
            }

            parts.Add($"{i}:{SafeFormatValue(value)}");
        }

        return "[" + string.Join(", ", parts) + "]";
    }

    private static string DescribeFightMatch(FightMatchController? controller)
    {
        if (controller == null)
        {
            return "fightMatch=null";
        }

        var parts = new List<string>
        {
            $"type={SafeFormatValue(controller.fightMatchType)}",
            $"watch={SafeFormatValue(controller.watchFightType)}",
            $"round={SafeFormatValue(controller.fightRound)}",
            $"skipping={SafeFormatValue(controller.skipping)}",
            $"isForceMatch={SafeFormatValue(controller.isForceMatch)}",
            $"isForceGroupMatch={SafeFormatValue(controller.isForceGroupMatch)}",
            $"matchDifficulty={SafeFormatValue(controller.matchDifficulty)}",
            $"now={DescribeFightMatchCouple(controller.nowFightMatchCouple)}",
            $"next={DescribeFightMatchCouple(controller.nextFightMatchCouple)}",
            $"heroFinals={DescribeHeroEnumerable(controller.HeroFinalList)}",
            $"forceGroupHeroes={DescribeHeroEnumerable(controller.forceGroupMatchHeroList)}"
        };

        return string.Join("; ", parts);
    }

    private static string DescribeBattleFlow(BattleController? controller)
    {
        if (controller == null)
        {
            return "battle=null";
        }

        var playerTeam = SafeProperty(controller, "playerBattleUnit") ?? SafeField(controller, "playerBattleUnit");
        var parts = new List<string>
        {
            $"state={SafeFormatValue(controller.battleState)}",
            $"type={SafeFormatValue(controller.battleType)}",
            $"paused={SafeFormatValue(controller.battlePaused)}",
            $"havePlayer={SafeFormatValue(controller.havePlayer)}",
            $"playerControlTeam={SafeFormatValue(SafeBattleCall(() => controller.GetPlayerControlTeamID()))}",
            $"teams={SafeFormatValue(SafeProperty(controller.teams, "Count") ?? SafeField(controller.teams, "Count"))}",
            $"gridUnits={SafeFormatValue(SafeProperty(controller.gridUnits, "Count") ?? SafeField(controller.gridUnits, "Count"))}",
            $"nowActiveUnit={DescribeBattleUnit(controller.nowActiveUnit)}",
            $"playerBattleUnit={DescribeBattleUnit(playerTeam)}",
            $"battleTime={SafeFormatValue(controller.battleTime)}",
            $"timeLimit={SafeFormatValue(controller.timeLimit)}"
        };

        return string.Join("; ", parts);
    }

    private static string DescribeFightMatchArgs(object[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return "[]";
        }

        var parts = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var value = args[i];
            if (value is HeroData hero)
            {
                parts.Add($"{i}:hero={TryGetHeroName(hero)}/{DescribeHeroPower(hero)}");
                continue;
            }

            if (value is FightMatchCouple couple)
            {
                parts.Add($"{i}:couple={DescribeFightMatchCouple(couple)}");
                continue;
            }

            if (value is IEnumerable enumerable and not string)
            {
                parts.Add($"{i}:list={DescribeHeroEnumerable(enumerable)}");
                continue;
            }

            parts.Add($"{i}:{SafeFormatValue(value)}");
        }

        return "[" + string.Join(", ", parts) + "]";
    }

    private static string DescribeFightMatchCouple(object? coupleObject)
    {
        if (coupleObject == null)
        {
            return "null";
        }

        var team0 = SafeProperty(coupleObject, "heroList0") ?? SafeField(coupleObject, "heroList0");
        var team1 = SafeProperty(coupleObject, "heroList1") ?? SafeField(coupleObject, "heroList1");
        var winTeam = SafeProperty(coupleObject, "winTeam") ?? SafeField(coupleObject, "winTeam");
        var id = SafeProperty(coupleObject, "id") ?? SafeField(coupleObject, "id");

        return $"id={SafeFormatValue(id)}, team0={DescribeHeroEnumerable(team0)}, team1={DescribeHeroEnumerable(team1)}, winTeam={SafeFormatValue(winTeam)}, hasPlayer={FightMatchHasPlayer(team0, team1)}";
    }

    private static string DescribeHeroEnumerable(object? values)
    {
        var entries = new List<string>();
        foreach (var value in EnumerateValues(values))
        {
            if (value is HeroData hero)
            {
                entries.Add(TryGetHeroName(hero));
            }
            else
            {
                entries.Add(SafeFormatValue(value));
            }
        }

        return "[" + string.Join(", ", entries) + "]";
    }

    private static string FightMatchHasPlayer(object? team0, object? team1)
    {
        var playerName = TryGetHeroName(TryGetPlayerHero());
        if (playerName == "unknown")
        {
            return "unknown";
        }

        foreach (var value in EnumerateValues(team0))
        {
            if (value is HeroData hero && TryGetHeroName(hero) == playerName)
            {
                return "team0";
            }
        }

        foreach (var value in EnumerateValues(team1))
        {
            if (value is HeroData hero && TryGetHeroName(hero) == playerName)
            {
                return "team1";
            }
        }

        return "no";
    }

    private static string DescribeBattleUnit(object? unitObject)
    {
        if (unitObject == null)
        {
            return "null";
        }

        var hero = SafeProperty(unitObject, "heroData") as HeroData
                   ?? SafeField(unitObject, "heroData") as HeroData
                   ?? SafeProperty(unitObject, "HeroData") as HeroData
                   ?? SafeField(unitObject, "HeroData") as HeroData;
        var grid = SafeProperty(unitObject, "gridData") ?? SafeField(unitObject, "gridData");
        var team = SafeProperty(unitObject, "battleTeam") ?? SafeField(unitObject, "battleTeam");

        return $"hero={TryGetHeroName(hero)}, team={SafeFormatValue(team)}, grid={SafeFormatValue(grid)}";
    }

    private static string DescribeDrink(DrinkUIController? controller)
    {
        if (controller == null)
        {
            return "drink=null";
        }

        var enemyHero =
            SafeProperty(controller, "heroData") as HeroData ??
            SafeField(controller, "heroData") as HeroData ??
            SafeProperty(controller, "enemyData") as HeroData ??
            SafeField(controller, "enemyData") as HeroData;

        var parts = new List<string>
        {
            $"state={SafeFormatValue(SafeProperty(controller, "drinkState") ?? SafeField(controller, "drinkState"))}",
            $"type={SafeFormatValue(SafeProperty(controller, "drinkType") ?? SafeField(controller, "drinkType"))}",
            $"targetWine={SafeFormatValue(SafeProperty(controller, "targetWine") ?? SafeField(controller, "targetWine"))}",
            $"wineScore={SafeFormatValue(SafeProperty(controller, "wineScore") ?? SafeField(controller, "wineScore"))}",
            $"extraWineRate={SafeFormatValue(SafeProperty(controller, "extraWineRate") ?? SafeField(controller, "extraWineRate"))}",
            $"player={TryGetHeroName(TryGetPlayerHero())}/{DescribeHeroPower(TryGetPlayerHero())}",
            $"enemy={TryGetHeroName(enemyHero)}/{DescribeHeroPower(enemyHero)}"
        };

        return string.Join("; ", parts);
    }

    private static string DescribeDrinkArgs(object[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return "[]";
        }

        var parts = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var value = args[i];
            if (value is HeroData hero)
            {
                parts.Add($"{i}:hero={TryGetHeroName(hero)}/{DescribeHeroPower(hero)}");
                continue;
            }

            if (value is ItemData item)
            {
                parts.Add($"{i}:{DescribeItemData(item, includeValues: false)}");
                continue;
            }

            parts.Add($"{i}:{SafeFormatValue(value)}");
        }

        return "[" + string.Join(", ", parts) + "]";
    }

    private static string DescribeCraft(CraftUIController? controller)
    {
        if (controller == null)
        {
            return "craft=null";
        }

        var parts = new List<string>
        {
            $"type={controller.craftType}",
            $"forceCraft={controller.forceCraft}",
            $"targetBuildingID={controller.targetBuilding?.buildingID.ToString() ?? "null"}",
            $"targetBuildingLv={controller.targetBuilding?.lv.ToString() ?? "null"}",
            $"targetSubType={controller.targetSubType}",
            $"targetFoodSubType={controller.targetFoodSubType}",
            $"targetWeaponType={controller.targetWeaponType}",
            $"materialMain={DescribeOptionalItem(controller.craftMaterialData)}",
            $"materialSub={DescribeOptionalItem(controller.craftMaterialDataSub)}",
            $"finalValue={SafeFormatValue(SafeCraftCall(() => controller.GetCraftFinalValue()))}",
            $"skillType={SafeFormatValue(SafeCraftCall(() => controller.GetCraftTargetSkillType()))}",
            $"skillNum={SafeFormatValue(SafeCraftCall(() => controller.GetCraftTargetSkillNum()))}",
            $"resultList={DescribeCraftResultList(controller.craftResultList)}"
        };

        return string.Join("; ", parts);
    }

    private static string DescribeCraftPoison(CraftPoisonUIController? controller)
    {
        if (controller == null)
        {
            return "craftPoison=null";
        }

        var parts = new List<string>
        {
            $"type={controller.craftPoisonType}",
            $"targetBuildingID={controller.targetBuilding?.buildingID.ToString() ?? "null"}",
            $"targetBuildingLv={controller.targetBuilding?.lv.ToString() ?? "null"}"
        };

        return string.Join("; ", parts);
    }

    private static string DescribeEnhance(EnhanceUIController? controller)
    {
        if (controller == null)
        {
            return "enhance=null";
        }

        var parts = new List<string>
        {
            $"type={controller.enhanceType}",
            $"useMoney={controller.useMoney}",
            $"targetBuildingID={controller.targetBuilding?.buildingID.ToString() ?? "null"}",
            $"targetBuildingLv={controller.targetBuilding?.lv.ToString() ?? "null"}",
            $"targetItem={DescribeOptionalItem(TryGetItemDataFromGameObject(controller.enhanceTargetItemIcon))}",
            $"materialItem={DescribeOptionalItem(TryGetItemDataFromGameObject(controller.enhanceMaterialItemIcon))}",
            $"needBuildingLv={SafeFormatValue(SafeCraftCall(() => controller.EnhanceNeedBuildingLv()))}",
            $"needSkillLv={SafeFormatValue(SafeCraftCall(() => controller.EnhanceNeedSkillLv()))}",
            $"canEnhance={SafeFormatValue(SafeCraftCall(() => controller.CanEnhance()))}"
        };

        return string.Join("; ", parts);
    }

    private static string DescribeSpeEnhance(SpeEnhanceEquipController? controller)
    {
        if (controller == null)
        {
            return "speEnhance=null";
        }

        var parts = new List<string>
        {
            $"targetItem={DescribeOptionalItem(TryGetItemDataFromGameObject(controller.enhanceTargetItemIcon))}",
            $"canEnhance={SafeFormatValue(SafeCraftCall(() => controller.CanEnhance()))}"
        };

        return string.Join("; ", parts);
    }

    private static string DescribeCraftArgs(object[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return "[]";
        }

        var parts = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var value = args[i];
            if (value is ItemData item)
            {
                parts.Add($"{i}:{DescribeItemData(item, includeValues: true)}");
                continue;
            }

            if (value is AreaBuildingData building)
            {
                parts.Add($"{i}:buildingID={building.buildingID}, lv={building.lv}, areaID={building.areaID}");
                continue;
            }

            parts.Add($"{i}:{SafeFormatValue(value)}");
        }

        return "[" + string.Join(", ", parts) + "]";
    }

    private static string DescribePanelTreasures(IdentifyMatchController controller)
    {
        var panel = controller.identifyMatchUIPanel;
        if (panel == null)
        {
            return "[]";
        }

        try
        {
            var icons = panel.GetComponentsInChildren<ItemIconController>(true);
            if (icons == null || icons.Length == 0)
            {
                return "[]";
            }

            var seen = new HashSet<int>();
            var entries = new List<string>();
            foreach (var icon in icons)
            {
                if (icon == null || icon.gameObject == null)
                {
                    continue;
                }

                var id = icon.gameObject.GetInstanceID();
                if (!seen.Add(id))
                {
                    continue;
                }

                entries.Add(DescribeTreasureGameObject(icon.gameObject));
            }

            return "[" + string.Join(", ", entries) + "]";
        }
        catch
        {
            return "[panel-scan-failed]";
        }
    }

    private static string DescribeTreasureList(object? treasures)
    {
        var entries = new List<string>();
        foreach (var item in EnumerateValues(treasures))
        {
            if (item is GameObject gameObject)
            {
                entries.Add(DescribeTreasureGameObject(gameObject));
            }
            else
            {
                entries.Add(SafeFormatValue(item));
            }
        }

        return "[" + string.Join(", ", entries) + "]";
    }

    private static string DescribeBoolList(object? results)
    {
        var entries = new List<string>();
        foreach (var item in EnumerateValues(results))
        {
            entries.Add(SafeFormatValue(item));
        }

        return "[" + string.Join(", ", entries) + "]";
    }

    private static string DescribeTreasureGameObject(GameObject? target)
    {
        if (target == null)
        {
            return "null";
        }

        var item = TryGetItemDataFromGameObject(target);
        var label = $"{target.name}#{target.GetInstanceID()}";
        if (item == null)
        {
            return label;
        }

        return $"{label}<{DescribeItemData(item, includeValues: true)}>";
    }

    private static string DescribeCraftResultList(object? results)
    {
        var entries = new List<string>();
        var index = 0;
        foreach (var item in EnumerateValues(results))
        {
            if (item is ItemData itemData)
            {
                entries.Add($"{index}:{DescribeItemData(itemData, includeValues: true)}");
            }
            else
            {
                entries.Add($"{index}:{SafeFormatValue(item)}");
            }

            index++;
        }

        return "[" + string.Join(" | ", entries) + "]";
    }

    private static ItemData? TryGetCraftResult(CraftUIController? controller, int id)
    {
        if (controller?.craftResultList == null || id < 0)
        {
            return null;
        }

        try
        {
            if (id >= controller.craftResultList.Count)
            {
                return null;
            }

            return controller.craftResultList[id];
        }
        catch
        {
            return null;
        }
    }

    private static string DescribeOptionalItem(ItemData? item)
    {
        return item == null ? "null" : DescribeItemData(item, includeValues: true);
    }

    private static ItemData? TryGetItemDataFromGameObject(GameObject target)
    {
        try
        {
            var direct = target.GetComponent<ItemIconController>();
            if (direct != null)
            {
                return direct.itemData;
            }

            var child = target.GetComponentInChildren<ItemIconController>(true);
            return child?.itemData;
        }
        catch
        {
            return null;
        }
    }

    private static DebateCardController? TryGetDebateCardController(GameObject target)
    {
        try
        {
            var direct = target.GetComponent<DebateCardController>();
            if (direct != null)
            {
                return direct;
            }

            return target.GetComponentInChildren<DebateCardController>(true);
        }
        catch
        {
            return null;
        }
    }

    private static string DescribeItemData(ItemData item, bool includeValues)
    {
        var parts = new List<string>
        {
            $"name={SafeFormatValue(item.name)}",
            $"id={item.itemID}",
            $"type={item.type}",
            $"subType={item.subType}",
            $"itemLv={item.itemLv}",
            $"rareLv={item.rareLv}"
        };

        if (item.equipmentData != null)
        {
            parts.Add($"equipLittleType={item.equipmentData.littleType}");
            parts.Add($"equipAttriType={item.equipmentData.attriType}");
            parts.Add($"equipEnhanceLv={item.equipmentData.enhanceLv}");
            parts.Add($"equipSpeEnhanceLv={item.equipmentData.speEnhanceLv}");
            parts.Add($"equipSpeWeightLv={item.equipmentData.speWeightLv}");
        }

        if (item.medFoodData != null)
        {
            parts.Add($"medEnhanceLv={item.medFoodData.enhanceLv}");
            parts.Add($"medRandomSpeAddValue={item.medFoodData.randomSpeAddValue}");
        }

        if (item.treasureData != null)
        {
            parts.Add($"guessLv={DescribeEnumerable(item.treasureData.playerGuessTreasureLv)}");
            parts.Add($"realLv={DescribeEnumerable(item.treasureData.treasureLv)}");
            parts.Add($"need={SafeFormatValue(item.treasureData.identifyKnowledgeNeed)}");
            parts.Add($"difficulty={DescribeEnumerable(item.treasureData.identifyDifficulty)}");
        }

        if (includeValues)
        {
            parts.Add($"storedValue={item.value}");
            parts.Add($"shownValue={SafeFormatValue(SafeTreasureCall(() => item.GetTreasureValue(false)))}");
            parts.Add($"guessValue={SafeFormatValue(SafeTreasureCall(() => item.GetTreasureValue(true)))}");
            parts.Add($"realValue={SafeFormatValue(SafeTreasureCall(() => item.GetTreasureRealValue()))}");
        }

        return string.Join(", ", parts);
    }

    private static object? SafeCraftCall(Func<object> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static object? SafeBattleCall(Func<object> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static object? SafeTreasureCall(Func<object> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static string DescribeEnumerable(object? values)
    {
        var entries = new List<string>();
        foreach (var value in EnumerateValues(values))
        {
            entries.Add(SafeFormatValue(value));
        }

        return "[" + string.Join(", ", entries) + "]";
    }

    private static IEnumerable<object?> EnumerateValues(object? values)
    {
        if (values == null)
        {
            yield break;
        }

        if (values is IEnumerable enumerable)
        {
            foreach (var value in enumerable)
            {
                yield return value;
            }

            yield break;
        }

        var count = SafeProperty(values, "Count") ?? SafeProperty(values, "Length") ?? SafeField(values, "Count") ?? SafeField(values, "Length");
        if (count is int intCount)
        {
            for (var i = 0; i < intCount; i++)
            {
                yield return SafeIndex(values, i);
            }
        }
    }

    private static object? SafeIndex(object values, int index)
    {
        try
        {
            var property = values.GetType().GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return property.GetValue(values, new object[] { index });
            }
        }
        catch
        {
        }

        try
        {
            var method = values.GetType().GetMethod("get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null)
            {
                return method.Invoke(values, new object[] { index });
            }
        }
        catch
        {
        }

        return null;
    }

    private static string DescribeStaticType(string label, Type type)
    {
        var interesting = new[] { "HorseSprintLast", "HorseSprintCd", "BaseTravelSpeed" };
        var parts = new List<string>();

        foreach (var name in interesting)
        {
            var value = SafeStaticProperty(type, name) ?? SafeStaticField(type, name);
            if (value != null)
            {
                parts.Add($"{label}.{name}={SafeFormatValue(value)}");
            }
        }

        return parts.Count == 0 ? string.Empty : string.Join("; ", parts);
    }

    private static string DescribeInfoData(InfoData? infoData)
    {
        if (infoData == null)
        {
            return "infoData=null";
        }

        return $"type={SafeFormatValue(infoData.infoType)}, time={SafeFormatValue(infoData.infotime)}, text={SafeFormatValue(infoData.infoText)}";
    }

    private static string DescribeInfoTabData(InfoTabData? infoTabData)
    {
        if (infoTabData == null)
        {
            return "infoTabData=null";
        }

        return
            $"text={SafeFormatValue(infoTabData.infoText)}, atlas={SafeFormatValue(infoTabData.atlasName)}, pic={SafeFormatValue(infoTabData.infoPic)}, sound={SafeFormatValue(infoTabData.soundName)}, vol={SafeFormatValue(infoTabData.volumn)}, last={SafeFormatValue(infoTabData.lastTime)}, color={DescribeColor(infoTabData.picColor)}";
    }

    private static string DescribeColor(Color color)
    {
        return $"RGBA({color.r:0.###},{color.g:0.###},{color.b:0.###},{color.a:0.###})";
    }

    private static string DescribeColorObject(object? color)
    {
        return color is Color typedColor ? DescribeColor(typedColor) : SafeFormatValue(color);
    }

    private static string DescribeGameObjectTree(GameObject? gameObject, int maxDepth)
    {
        if (gameObject == null)
        {
            return "null";
        }

        var lines = new List<string>();
        AppendGameObjectTree(gameObject.transform, 0, maxDepth, lines);
        return string.Join(" || ", lines);
    }

    private static void AppendGameObjectTree(Transform? transform, int depth, int maxDepth, List<string> lines)
    {
        if (transform == null)
        {
            return;
        }

        var indent = new string('>', depth);
        var componentNames = new HashSet<string>();
        var components = transform.gameObject.GetComponents<Component>();
        if (components != null)
        {
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component != null)
                {
                    componentNames.Add(component.GetType().Name);
                }
            }
        }

        lines.Add($"{indent}{transform.name}[active={transform.gameObject.activeSelf}, comps={string.Join("/", componentNames)}]");

        if (depth >= maxDepth)
        {
            return;
        }

        for (var i = 0; i < transform.childCount; i++)
        {
            AppendGameObjectTree(transform.GetChild(i), depth + 1, maxDepth, lines);
        }
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

    private static object? SafeStaticProperty(Type target, string name)
    {
        try
        {
            var prop = target.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            return prop == null ? null : prop.GetValue(null);
        }
        catch
        {
            return null;
        }
    }

    private static object? SafeStaticField(Type target, string name)
    {
        try
        {
            var field = target.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            return field == null ? null : field.GetValue(null);
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

    private static HorseData? TryGetPlayerHorse(HeroData? playerHero)
    {
        if (playerHero == null)
        {
            return null;
        }

        return SafeProperty(playerHero, "horse") as HorseData
               ?? SafeField(playerHero, "horse") as HorseData
               ?? SafeProperty(playerHero, "Horse") as HorseData
               ?? SafeField(playerHero, "Horse") as HorseData;
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

    private static string DescribeHeroPower(HeroData? hero)
    {
        if (hero == null)
        {
            return "power=unknown";
        }

        var nowPower = SafeProperty(hero, "power") ?? SafeProperty(hero, "nowPower") ?? SafeField(hero, "power") ?? SafeField(hero, "nowPower");
        var maxPower = SafeProperty(hero, "maxPower") ?? SafeField(hero, "maxPower");
        if (nowPower == null && maxPower == null)
        {
            return "power=unknown";
        }

        return $"power={SafeFormatValue(nowPower)}/{SafeFormatValue(maxPower)}";
    }

    private static WorldData? TryGetWorldData()
    {
        try
        {
            return GameController.Instance?.worldData;
        }
        catch
        {
            return null;
        }
    }

    private static TimeScaleController? TryGetTimeScaleController()
    {
        try
        {
            return TimeScaleController.Instance;
        }
        catch
        {
            return null;
        }
    }

    private static BattleController? TryGetBattleController()
    {
        try
        {
            return BattleController.Instance;
        }
        catch
        {
            return null;
        }
    }

    private static DrinkUIController? TryGetDrinkController()
    {
        try
        {
            return DrinkUIController.Instance;
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
