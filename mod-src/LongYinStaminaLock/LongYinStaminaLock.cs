using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

[BepInPlugin("codex.longyin.staminalock", "LongYin Stamina Lock", "1.27.11")]
public sealed class LongYinStaminaLockPlugin : BasePlugin
{
    private const string TreasureChestChoiceParamPrefix = "codex_chest_choice:";
    private const string TreasureChestChoicePlotCallbackName = nameof(PlotController.ChangePlotDataBase);
    private const int DailySkillInsightMaxLevel = 10;
    private const int LuckyMoneyMinPercent = 1;
    private const int LuckyMoneyMaxPercent = 30;
    private const int TeachSkillSplashMinPercentFloor = 0;
    private const int TeachSkillSplashMaxPercentCeiling = 500;
private const float TeachSkillSideTabDurationSeconds = 4.5f;
private const string TeachSkillSideTabAtlasName = "IconAtlas";
private const string TeachSkillSideTabIconName = "1";
private const string TeachSkillSideTabSoundName = "Woosh";
private const float TeachSkillSideTabSoundVolume = 1f;
    private const float DrinkFillMatchTolerance = 0.02f;
    private const float DrinkFillDeltaTolerance = 0.005f;
    private static readonly string[] RelationshipBonusMessages =
    {
        "你今天比较帅，好感有多加 {0}",
        "你让他心情很好 ， 好感多加 {0}"
    };
    private static readonly float[] OutsideBattleSpeedCycle = { 1f, 2f, 3f, 5f, 10f };

    internal static ManualLogSource LoggerInstance = null!;

    private static ConfigEntry<bool> _lockExploreStamina = null!;
    private static ConfigEntry<bool> _revealExtraFogOnMove = null!;
    private static ConfigEntry<int> _moveRevealRadius = null!;
    private static ConfigEntry<bool> _revealAllOnStepTile = null!;
    private static ConfigEntry<bool> _treasureChestChoiceEnabled = null!;
    private static ConfigEntry<int> _treasureChestChoiceOptions = null!;
    private static ConfigEntry<int> _treasureChestTotalItems = null!;
    private static ConfigEntry<int> _bookExpMultiplier = null!;
    private static ConfigEntry<int> _creationPointMultiplier = null!;
    private static ConfigEntry<int> _battleSpeedMultiplier = null!;
    private static ConfigEntry<float> _horseBaseSpeedMultiplier = null!;
    private static ConfigEntry<float> _horseTurboSpeedMultiplier = null!;
    private static ConfigEntry<float> _horseTurboDurationMultiplier = null!;
    private static ConfigEntry<float> _horseTurboCooldownMultiplier = null!;
    private static ConfigEntry<bool> _lockHorseTurboStamina = null!;
    private static ConfigEntry<float> _carryWeightCap = null!;
    private static ConfigEntry<bool> _ignoreCarryWeight = null!;
    private static ConfigEntry<int> _merchantCarryCash = null!;
    private static ConfigEntry<int> _luckyMoneyHitChancePercent = null!;
    private static ConfigEntry<int> _extraRelationshipGainChancePercent = null!;
    private static ConfigEntry<float> _debatePlayerDamageTakenMultiplier = null!;
    private static ConfigEntry<float> _debateEnemyDamageTakenMultiplier = null!;
    private static ConfigEntry<bool> _craftRandomPickUpgradeEnabled = null!;
    private static ConfigEntry<float> _drinkPlayerPowerCostMultiplier = null!;
    private static ConfigEntry<float> _drinkEnemyPowerCostMultiplier = null!;
    private static ConfigEntry<int> _dailySkillInsightHitChancePercent = null!;
    private static ConfigEntry<float> _dailySkillInsightExpPercent = null!;
    private static ConfigEntry<bool> _dailySkillInsightUseRarityScaling = null!;
    private static ConfigEntry<float> _dailySkillInsightRealtimeIntervalSeconds = null!;
    private static ConfigEntry<bool> _teachSkillSameSectAreaShareEnabled = null!;
    private static ConfigEntry<int> _teachSkillSameSectAreaShareMinPercent = null!;
    private static ConfigEntry<int> _teachSkillSameSectAreaShareMaxPercent = null!;
    private static ConfigEntry<float> _dialogMonthlyLimitMultiplier = null!;
    private static ConfigEntry<bool> _traceMode = null!;
    private static ConfigEntry<bool> _traceTreasureChestEvents = null!;
    private static ConfigEntry<bool> _freezeDate = null!;
    private static ConfigEntry<KeyCode> _freezeDateHotkey = null!;
    private static ConfigEntry<KeyCode> _outsideBattleSpeedHotkey = null!;
    private static readonly string[] HorseCurrentPowerMemberNames = { "power", "nowPower", "horsePower", "stamina", "nowStamina", "leftPower" };
    private static readonly string[] HorseMaxPowerMemberNames = { "maxPower", "powerMax", "maxStamina", "horseMaxPower" };
    private static readonly string[] DrinkPlayerFillAmountMemberNames = { "playerFillAmount" };
    private static readonly string[] DrinkEnemyFillAmountMemberNames = { "enemyFillAmount" };
    private static readonly System.Random Random = new();
    private static bool _applyingLuckyMoneyRefund;
    private static bool _applyingDailySkillInsightExp;
    private static bool _exploreFullRevealConsumed;
    private static bool _grantingTreasureChestChoiceReward;
    private static bool _grantingTreasureChestBonusItems;
    private static bool _treasureChestChoiceClosingPlot;
    private static bool _dailySkillInsightBaselineReady;
    private static float _nextRealtimeDailySkillInsightAt = -1f;
    private static TimeData? _lastObservedWorldDate;
    private static int _lastDrinkControllerInstanceId;
    private static float _lastDrinkPlayerFillAmount = float.NaN;
    private static float _lastDrinkEnemyFillAmount = float.NaN;
    private static bool? _lastResolvedDrinkTargetIsPlayer;
    private static readonly Dictionary<string, int> _dialogMonthlyUseCounts = new(StringComparer.Ordinal);
    private static HeroData? _activeDialogHero;
    private static int _activeDialogHeroId = -1;
    private static readonly List<KungfuSkillLvData> _dailySkillInsightCandidateBuffer = new();
    private Harmony? _harmony;

    private sealed class MoneyChangeState
    {
        public bool IsEligible { get; init; }
        public int RequestedDelta { get; init; }
        public int? MoneyBefore { get; init; }
        public bool IsSpend { get; init; }
        public bool IsIncome { get; init; }
    }

    private sealed class CalendarChangeState
    {
        public string BeforeText { get; init; } = "Date: unavailable";
        public TimeData? BeforeDate { get; init; }
    }

    private sealed class TeachSkillSplashState
    {
        public bool IsEligible { get; init; }
        public HeroData? SourceHero { get; init; }
        public HeroData? TargetHero { get; init; }
        public int SkillId { get; init; }
        public string SkillName { get; init; } = string.Empty;
        public float TargetBookProgressBefore { get; init; }
        public float TargetFightProgressBefore { get; init; }
    }

    private sealed class TeachSkillRecipientResult
    {
        public string HeroName { get; init; } = string.Empty;
        public string SkillName { get; init; } = string.Empty;
        public float Exp { get; init; }
        public int Percent { get; init; }
    }

    private sealed class ExploreHealingState
    {
        public HeroData? Player { get; init; }
        public float ExternalInjuryBefore { get; init; }
        public float InternalInjuryBefore { get; init; }
        public float PoisonInjuryBefore { get; init; }
        public bool IsHealingTile { get; init; }
    }

    private sealed class TreasureChestChoiceSession
    {
        public HeroData? Player { get; init; }
        public List<ItemData> Options { get; init; } = new();
        public bool SkipManageItemPoison { get; init; }
        public bool Resolved { get; set; }
        public float OpenedAtRealtime { get; init; }
        public string? LastObservedChoiceParam { get; set; }
        public bool PendingClickConfirm { get; set; }
        public int PendingClickConfirmFrames { get; set; }
    }

    private static TreasureChestChoiceSession? _activeTreasureChestChoiceSession;

    public override void Load()
    {
        LoggerInstance = Log;
        _lockExploreStamina = Config.Bind("Exploration", "LockStamina", true, "Prevents exploration stamina from decreasing.");
        _revealExtraFogOnMove = Config.Bind("Exploration", "RevealExtraFogOnMove", false, "Legacy compatibility toggle for the old per-move reveal experiment. No longer used.");
        _moveRevealRadius = Config.Bind("Exploration", "MoveRevealRadius", 2, "Legacy compatibility value for the old per-move reveal experiment. No longer used.");
        _revealAllOnStepTile = Config.Bind("Exploration", "RevealAllOnStepTile", true, "Reveal the whole exploration map once, after the first completed move in each exploration run.");
        _treasureChestChoiceEnabled = Config.Bind("Exploration", "TreasureChestChoiceEnabled", true, "When true, exploration treasure chests show several reward items and let you choose 1.");
        _treasureChestChoiceOptions = Config.Bind("Exploration", "TreasureChestChoiceOptions", 3, "How many reward options each exploration treasure chest should show when choice mode is enabled.");
        _treasureChestTotalItems = Config.Bind("Exploration", "TreasureChestTotalItems", 2, "Total item rewards to grant from exploration treasure chests. Set to 1 for vanilla behavior.");
        _bookExpMultiplier = Config.Bind("ReadBook", "ExpMultiplier", 1, "Multiplies EXP gained from reading books.");
        _creationPointMultiplier = Config.Bind("CharacterCreation", "PointMultiplier", 1, "Multiplies the remaining point pools during character creation.");
        _battleSpeedMultiplier = Config.Bind("Battle", "SpeedMultiplier", 2, "Multiplies the selected in-battle speed option.");
        _horseBaseSpeedMultiplier = Config.Bind("WorldMapHorse", "BaseSpeedMultiplier", 1f, "Multiplies the player horse's normal world-map travel speed.");
        _horseTurboSpeedMultiplier = Config.Bind("WorldMapHorse", "TurboSpeedMultiplier", 1f, "Multiplies the player horse's turbo speed bonus on the world map.");
        _horseTurboDurationMultiplier = Config.Bind("WorldMapHorse", "TurboDurationMultiplier", 1f, "Multiplies how long horse turbo lasts on the world map.");
        _horseTurboCooldownMultiplier = Config.Bind("WorldMapHorse", "TurboCooldownMultiplier", 1f, "Multiplies horse turbo cooldown duration. Set below 1 for a shorter cooldown.");
        _lockHorseTurboStamina = Config.Bind("WorldMapHorse", "LockTurboStamina", true, "Keeps world-map horse stamina available so turbo does not end early from stamina depletion.");
        _carryWeightCap = Config.Bind("Inventory", "CarryWeightCap", 100000f, "Minimum carry-weight cap applied to the player inventory. Set to 0 to disable.");
        _ignoreCarryWeight = Config.Bind("Inventory", "IgnoreCarryWeight", false, "When true, forces the player inventory's current carried weight to 0.");
        _merchantCarryCash = Config.Bind("Commerce", "MerchantCarryCash", 100000, "Minimum cash carried by NPC shop merchants while a Shop trade window is open. Set to 0 to disable.");
        _luckyMoneyHitChancePercent = Config.Bind("MoneyLuck", "LuckyHitChancePercent", 0, "Chance from 0 to 100 that a player money transaction triggers a lucky bonus.");
        _extraRelationshipGainChancePercent = Config.Bind("Relationship", "ExtraRelationshipGainChancePercent", 0, "Chance from 0 to 100 that positive relationship gain becomes double.");
        _debatePlayerDamageTakenMultiplier = Config.Bind("Debate", "PlayerDamageTakenMultiplier", 1f, "Multiplies debate damage dealt to the player side when a round is lost.");
        _debateEnemyDamageTakenMultiplier = Config.Bind("Debate", "EnemyDamageTakenMultiplier", 1f, "Multiplies debate damage dealt to the enemy side when a round is won.");
        _craftRandomPickUpgradeEnabled = Config.Bind("Craft", "RandomPickUpgrade", true, "Uses the picked craft result as the base item, then regenerates it toward the next major tier.");
        _drinkPlayerPowerCostMultiplier = Config.Bind("Drink", "PlayerPowerCostMultiplier", 1f, "Multiplies Qi cost paid by the player side during the drinking minigame.");
        _drinkEnemyPowerCostMultiplier = Config.Bind("Drink", "EnemyPowerCostMultiplier", 1f, "Multiplies Qi cost paid by the enemy side during the drinking minigame.");
        _dailySkillInsightHitChancePercent = Config.Bind("DailySkillInsight", "HitChancePercent", 0, "Chance from 0 to 100 that each elapsed in-game day grants bonus skill EXP to one eligible martial skill.");
        _dailySkillInsightExpPercent = Config.Bind("DailySkillInsight", "ExpPercent", 5f, "Percent of the skill's current-level max EXP to grant when the bonus triggers.");
        _dailySkillInsightUseRarityScaling = Config.Bind("DailySkillInsight", "UseRarityScaling", true, "When true, multiplies the bonus by the skill's rarity EXP rate.");
        _dailySkillInsightRealtimeIntervalSeconds = Config.Bind("DailySkillInsight", "RealtimeIntervalSeconds", 0f, "When above 0, grants the same bonus every X real-time seconds while in game. Useful for testing.");
        _teachSkillSameSectAreaShareEnabled = Config.Bind("Teaching", "SameSectAreaShareEnabled", true, "When the player teaches martial-skill EXP to a same-sect NPC, other same-sect NPCs in the same area who already know that skill also gain EXP.");
        _teachSkillSameSectAreaShareMinPercent = Config.Bind("Teaching", "SameSectAreaShareMinPercent", 80, "Minimum percent of the original taught EXP shared to each additional same-sect NPC in the area.");
        _teachSkillSameSectAreaShareMaxPercent = Config.Bind("Teaching", "SameSectAreaShareMaxPercent", 120, "Maximum percent of the original taught EXP shared to each additional same-sect NPC in the area.");
        _dialogMonthlyLimitMultiplier = Config.Bind("DialogFlow", "MonthlyLimitMultiplier", 3f, "Scales the monthly per-NPC interaction quota used by talk, teach, and similar meet choices.");
        _traceMode = Config.Bind("Debug", "TracerEnabled", false, "Master switch for all mod tracer logs. When false, trace helpers stay silent.");
        _traceTreasureChestEvents = Config.Bind("Debug", "TraceTreasureChestEvents", false, "When TracerEnabled is true, logs treasure chest interception, choice UI, and reward resolution.");
        _freezeDate = Config.Bind("Time", "FreezeDate", false, "Blocks in-game day, month, and year progression.");
        _freezeDateHotkey = Config.Bind("Time", "ToggleFreezeDateHotkey", KeyCode.F10, "Hotkey that toggles date freezing while in game.");
        _outsideBattleSpeedHotkey = Config.Bind("Time", "CycleOutsideBattleSpeedHotkey", KeyCode.F11, "Hotkey that cycles the test speed multiplier outside battle.");
        _harmony = new Harmony("codex.longyin.staminalock");
        PatchMethod(typeof(ExploreController), "ChangeMoveStep", new[] { typeof(int) }, nameof(ChangeMoveStepPrefix), null);
        PatchMethod(typeof(ExploreController), "ChangeMoveStep", new[] { typeof(int), typeof(bool) }, nameof(ChangeMoveStepWithBoolPrefix), null);
        PatchMethod(typeof(ExploreController), nameof(ExploreController.GenerateExploreMap), new[] { typeof(ExploreMapData), typeof(string), typeof(string) }, null, nameof(GenerateExploreMapPostfix));
        PatchMethod(typeof(ExploreController), nameof(ExploreController.ResetExploreMap), Type.EmptyTypes, null, nameof(ResetExploreMapPostfix));
        PatchMethod(typeof(ExploreController), nameof(ExploreController.PlayerFinishMove), Type.EmptyTypes, null, nameof(PlayerFinishMovePostfix));
        PatchMethod(typeof(ExploreController), nameof(ExploreController.ManageTileEvent), new[] { typeof(ExploreTileData) }, nameof(ManageTileEventPrefix), nameof(ManageTileEventPostfix));
        PatchMethod(typeof(HeroData), nameof(HeroData.GetItem), new[] { typeof(ItemData), typeof(bool), typeof(bool), typeof(int), typeof(bool) }, nameof(TreasureChestGetItemPrefix), nameof(TreasureChestGetItemPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.ChangePlotDataBase), new[] { typeof(string) }, nameof(TreasureChestChoicePlotCallbackPrefix), null);
        PatchMethod(typeof(PlotController), nameof(PlotController.PlotBackgroundClicked), Type.EmptyTypes, nameof(TreasureChestChoiceAdvancePrefix), null);
        PatchMethod(typeof(PlotController), nameof(PlotController.ChangeNextPlot), Type.EmptyTypes, nameof(TreasureChestChoiceAdvancePrefix), null);
        PatchMethod(typeof(PlotController), nameof(PlotController.GoNextPlot), Type.EmptyTypes, nameof(TreasureChestChoiceAdvancePrefix), null);
        PatchMethod(typeof(PlotController), nameof(PlotController.AutoPlotButtonClicked), Type.EmptyTypes, nameof(TreasureChestChoiceAdvancePrefix), null);
        PatchMethod(typeof(PlotController), nameof(PlotController.ShowHeroInteractUI), new[] { typeof(HeroData) }, null, nameof(DialogHeroContextPostfix));
        PatchMethod(typeof(PlotController), nameof(PlotController.ManageMeetNpcPlot), new[] { typeof(HeroData) }, null, nameof(DialogHeroContextPostfix));
        PatchMethod(typeof(PlotInteractController), nameof(PlotInteractController.Update), Type.EmptyTypes, null, nameof(DialogChoiceRowPostfix));
        PatchMethod(typeof(PlotInteractController), nameof(PlotInteractController.OnClick), Type.EmptyTypes, nameof(DialogChoiceClickPrefix), null);
        PatchMethod(typeof(BuildChoiceButtonController), nameof(BuildChoiceButtonController.OnClick), Type.EmptyTypes, null, nameof(TreasureChestChoiceButtonClickedPostfix));
        PatchMethod(typeof(UIButton), nameof(UIButton.OnClick), Type.EmptyTypes, null, nameof(TreasureChestChoiceButtonClickedPostfix));
        PatchMethod(typeof(UIButtonMessage), nameof(UIButtonMessage.OnClick), Type.EmptyTypes, null, nameof(TreasureChestChoiceButtonClickedPostfix));
        PatchMethod(typeof(ButtonClick), nameof(ButtonClick.OnPointerClick), new[] { typeof(PointerEventData) }, null, nameof(TreasureChestChoiceButtonClickedPostfix));
        PatchMethod(typeof(HeroData), nameof(HeroData.AddSkillBookExp), new[] { typeof(float), typeof(KungfuSkillLvData), typeof(bool) }, nameof(AddSkillBookExpPrefix), null);
        PatchMethod(typeof(HeroData), nameof(HeroData.ChangeMoney), new[] { typeof(int), typeof(bool) }, nameof(ChangeMoneyPrefix), nameof(ChangeMoneyPostfix));
        PatchMethod(typeof(HeroData), nameof(HeroData.ChangeFavor), new[] { typeof(float), typeof(bool), typeof(float), typeof(float), typeof(bool) }, nameof(ChangeFavorPrefix), null);
        PatchMethod(typeof(PlotController), nameof(PlotController.ManageTeachSkill), new[] { typeof(HeroData), typeof(HeroData), typeof(int), typeof(float), typeof(bool) }, nameof(ManageTeachSkillPrefix), nameof(ManageTeachSkillPostfix));
        PatchMethod(typeof(BattleController), nameof(BattleController.BattleTimeScaleButtonClicked), new[] { typeof(GameObject) }, null, nameof(BattleTimeScaleButtonClickedPostfix));
        PatchMethod(typeof(HorseData), nameof(HorseData.StartSprint), Type.EmptyTypes, null, nameof(HorseStartSprintPostfix));
        PatchMethod(typeof(HeroData), "GetHorseTravelSpeed", Type.EmptyTypes, null, nameof(GetHorseTravelSpeedPostfix));
        PatchMethod(typeof(HeroData), "GetHorseTravelSpeed", new[] { typeof(bool), typeof(bool) }, null, nameof(GetHorseTravelSpeedWithFlagsPostfix));
        PatchMethod(typeof(HeroData), "RefreshHorseState", Type.EmptyTypes, null, nameof(RefreshHorseStatePostfix));
        PatchMethod(typeof(BuildingUIController), nameof(BuildingUIController.ShowBuildingShop), Type.EmptyTypes, null, nameof(ShowBuildingShopPostfix));
        PatchMethod(typeof(TradeUIController), nameof(TradeUIController.ShowTradeUI), new[] { typeof(TradeUIType), typeof(ItemListData), typeof(ItemListData), typeof(bool) }, null, nameof(ShowTradeUiBasicPostfix));
        PatchMethod(typeof(TradeUIController), nameof(TradeUIController.ShowTradeUI), new[] { typeof(TradeUIType), typeof(ItemListType), typeof(ItemListData), typeof(ItemListData) }, null, nameof(ShowTradeUiTypedPostfix));
        PatchMethod(typeof(TradeUIController), nameof(TradeUIController.ShowTradeUI), new[] { typeof(TradeUIType), typeof(ItemListData), typeof(ItemListData), typeof(int), typeof(int) }, null, nameof(ShowTradeUiLevelRangePostfix));
        PatchMethod(typeof(TradeUIController), nameof(TradeUIController.ShowTradeUI), new[] { typeof(TradeUIType), typeof(ItemListType), typeof(ItemListData), typeof(ItemListData), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(float), typeof(float) }, null, nameof(ShowTradeUiFullPostfix));
        PatchMethod(typeof(DebateUIController), nameof(DebateUIController.ChangePatient), new[] { typeof(bool), typeof(float) }, nameof(DebateChangePatientPrefix), null);
        PatchMethod(typeof(PlotController), nameof(PlotController.CraftResultChoosen), new[] { typeof(ItemData) }, nameof(CraftResultChoosenPrefix), null);
        PatchMethod(typeof(DrinkUIController), nameof(DrinkUIController.ShowDrinkUI), new[] { typeof(DrinkType), typeof(HeroData), typeof(ItemData), typeof(string) }, null, nameof(DrinkShowUiPostfix));
        PatchMethod(typeof(DrinkUIController), nameof(DrinkUIController.GetDrinkCost), new[] { typeof(float) }, null, nameof(DrinkGetCostPostfix));
        PatchMethod(typeof(DrinkUIController), nameof(DrinkUIController.HideDrinkUI), Type.EmptyTypes, null, nameof(DrinkHideUiPostfix));
        PatchMethod(typeof(StartMenuController), nameof(StartMenuController.SetAttriPreset), new[] { typeof(int) }, null, nameof(SetAttriPresetPostfix));
        PatchMethod(typeof(StartMenuController), nameof(StartMenuController.ResetPlayerAttri), Type.EmptyTypes, null, nameof(ResetPlayerAttriPostfix));
        PatchMethod(typeof(GameController), "Update", Type.EmptyTypes, null, nameof(GameControllerUpdatePostfix));
        PatchMethod(typeof(GameController), nameof(GameController.ChangeDay), Type.EmptyTypes, nameof(CalendarChangePrefix), nameof(CalendarChangePostfix));
        PatchMethod(typeof(GameController), nameof(GameController.ChangeDay), new[] { typeof(int) }, nameof(CalendarChangePrefix), nameof(CalendarChangePostfix));
        PatchMethod(typeof(GameController), nameof(GameController.ChangeDayDirect), new[] { typeof(int) }, nameof(CalendarChangePrefix), nameof(CalendarChangePostfix));
        PatchMethod(typeof(GameController), nameof(GameController.ChangeMonth), Type.EmptyTypes, nameof(CalendarChangePrefix), nameof(CalendarChangePostfix));
        PatchMethod(typeof(GameController), nameof(GameController.ChangeMonthDirect), new[] { typeof(int) }, nameof(CalendarChangePrefix), nameof(CalendarChangePostfix));
        PatchMethod(typeof(GameController), nameof(GameController.ChangeYear), Type.EmptyTypes, nameof(CalendarChangePrefix), nameof(CalendarChangePostfix));
        PatchMethod(typeof(GameController), nameof(GameController.ChangeYearDirect), new[] { typeof(int) }, nameof(CalendarChangePrefix), nameof(CalendarChangePostfix));
        PatchMethod(typeof(GameController), nameof(GameController.ChangeHour), new[] { typeof(float) }, nameof(HourChangePrefix), nameof(HourChangePostfix));

        Log.LogInfo("LongYin Stamina Lock loaded.");
        Log.LogInfo("Legacy in-game mod panel is disabled. External Mod Control is the supported UI path.");
        Log.LogInfo($"Exploration stamina lock starts {(_lockExploreStamina.Value ? "ON" : "OFF")}.");
        Log.LogInfo($"Exploration first-move full reveal starts {(_revealAllOnStepTile.Value ? "ON" : "OFF")}.");
        Log.LogInfo($"Exploration treasure chest choice mode starts {(_treasureChestChoiceEnabled.Value ? "ON" : "OFF")} with a 3-5 option chest-only picker.");
        Log.LogInfo($"Exploration treasure chest rewards start at x{Math.Max(1, _treasureChestTotalItems.Value)} total items when choice mode is OFF.");
        Log.LogInfo($"Read-book EXP multiplier starts at x{Mathf.Max(1, _bookExpMultiplier.Value)}.");
        Log.LogInfo($"Character creation point multiplier starts at x{Math.Max(1, _creationPointMultiplier.Value)}.");
        Log.LogInfo($"Battle speed multiplier starts at x{Math.Max(1, _battleSpeedMultiplier.Value)}.");
        Log.LogInfo($"World-map horse base speed multiplier starts at x{FormatConfigFloat(_horseBaseSpeedMultiplier.Value)}.");
        Log.LogInfo($"World-map horse turbo speed multiplier starts at x{FormatConfigFloat(_horseTurboSpeedMultiplier.Value)}.");
        Log.LogInfo($"World-map horse turbo duration multiplier starts at x{FormatConfigFloat(_horseTurboDurationMultiplier.Value)}.");
        Log.LogInfo($"World-map horse turbo cooldown multiplier starts at x{FormatConfigFloat(_horseTurboCooldownMultiplier.Value)}.");
        Log.LogInfo($"World-map horse turbo stamina lock starts {(_lockHorseTurboStamina.Value ? "ON" : "OFF")}.");
        Log.LogInfo($"Carry-weight cap starts at {Math.Max(0f, _carryWeightCap.Value):0.###}.");
        Log.LogInfo($"Ignore carry weight starts {(_ignoreCarryWeight.Value ? "ON" : "OFF")}.");
        Log.LogInfo($"Merchant cash floor starts at {Math.Max(0, _merchantCarryCash.Value)}.");
        Log.LogInfo($"Lucky money hit chance starts at {ClampPercent(_luckyMoneyHitChancePercent.Value)}%.");
        Log.LogInfo($"Extra relationship gain chance starts at {ClampPercent(_extraRelationshipGainChancePercent.Value)}%.");
        Log.LogInfo($"Debate player damage taken multiplier starts at x{FormatConfigFloat(_debatePlayerDamageTakenMultiplier.Value)}.");
        Log.LogInfo($"Debate enemy damage taken multiplier starts at x{FormatConfigFloat(_debateEnemyDamageTakenMultiplier.Value)}.");
        Log.LogInfo($"Craft picked-result major-tier upgrade starts {(_craftRandomPickUpgradeEnabled.Value ? "ON" : "OFF")}.");
        Log.LogInfo($"Drink player Qi cost multiplier starts at x{FormatConfigFloat(_drinkPlayerPowerCostMultiplier.Value)}.");
        Log.LogInfo($"Drink enemy Qi cost multiplier starts at x{FormatConfigFloat(_drinkEnemyPowerCostMultiplier.Value)}.");
        Log.LogInfo(
            $"Daily skill insight starts at {ClampPercent(_dailySkillInsightHitChancePercent.Value)}% for {Math.Max(0f, _dailySkillInsightExpPercent.Value):0.###}% max EXP " +
            $"with rarity scaling {(_dailySkillInsightUseRarityScaling.Value ? "ON" : "OFF")}.");
        Log.LogInfo($"Daily skill insight real-time test interval starts at {Math.Max(0f, _dailySkillInsightRealtimeIntervalSeconds.Value):0.###} seconds.");
        Log.LogInfo(
            $"Same-sect teaching splash starts {(_teachSkillSameSectAreaShareEnabled.Value ? "ON" : "OFF")} " +
            $"at {ClampTeachSkillSplashPercent(_teachSkillSameSectAreaShareMinPercent.Value)}%-{ClampTeachSkillSplashPercent(_teachSkillSameSectAreaShareMaxPercent.Value)}%.");
        Log.LogInfo($"Dialog monthly limit multiplier starts at x{FormatConfigFloat(_dialogMonthlyLimitMultiplier.Value)}.");
        Log.LogInfo($"Tracer master starts {(_traceMode.Value ? "ON" : "OFF")}.");
        Log.LogInfo($"Treasure chest tracer starts {(_traceTreasureChestEvents.Value ? "ON" : "OFF")}.");
        Log.LogInfo($"Date freeze starts {(_freezeDate.Value ? "ON" : "OFF")} with hotkey {_freezeDateHotkey.Value}.");
        Log.LogInfo($"Outside-battle speed cycle hotkey is {_outsideBattleSpeedHotkey.Value}.");
    }

    private void PatchMethod(Type type, string methodName, Type[] parameterTypes, string? prefixName, string? postfixName)
    {
        var target = AccessTools.Method(type, methodName, parameterTypes);
        var prefix = prefixName == null ? null : AccessTools.Method(typeof(LongYinStaminaLockPlugin), prefixName);
        var postfix = postfixName == null ? null : AccessTools.Method(typeof(LongYinStaminaLockPlugin), postfixName);

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
            num = 0;
        }
    }

    private static void ChangeMoveStepWithBoolPrefix(ref int num, bool showText)
    {
        ChangeMoveStepPrefix(ref num);
    }

    private static void GenerateExploreMapPostfix()
    {
        ResetExploreFullReveal("GenerateExploreMap");
    }

    private static void ResetExploreMapPostfix()
    {
        ResetExploreFullReveal("ResetExploreMap");
    }

    private static void PlayerFinishMovePostfix(ExploreController __instance)
    {
        TryRevealAllExploreFogAfterFirstMove(__instance);
    }

    private static void ManageTileEventPrefix(ExploreTileData targetTileData, out ExploreHealingState __state)
    {
        var player = TryGetPlayerHero();
        __state = new ExploreHealingState
        {
            Player = player,
            ExternalInjuryBefore = player?.externalInjury ?? 0f,
            InternalInjuryBefore = player?.internalInjury ?? 0f,
            PoisonInjuryBefore = player?.poisonInjury ?? 0f,
            IsHealingTile = IsHealingStateTile(targetTileData)
        };
    }

    private static void ManageTileEventPostfix(ExploreController __instance, ExploreTileData targetTileData, ExploreHealingState __state)
    {
        TryHandleHealingStateTile(targetTileData, __state);
    }

    private static bool TreasureChestGetItemPrefix(HeroData __instance, ItemData itemData, bool showPopInfo, bool showSpeGetItem, int treasureChestClickTime, bool skipManageItemPoison)
    {
        TraceTreasureChestEvent(
            "HeroData.GetItem prefix",
            __instance,
            itemData,
            treasureChestClickTime,
            skipManageItemPoison,
            $"showPopInfo={showPopInfo}, showSpeGetItem={showSpeGetItem}");
        return !TryStartTreasureChestChoice(__instance, itemData, treasureChestClickTime, skipManageItemPoison);
    }

    private static void TreasureChestGetItemPostfix(HeroData __instance, ItemData itemData, bool showPopInfo, bool showSpeGetItem, int treasureChestClickTime, bool skipManageItemPoison)
    {
        TraceTreasureChestEvent(
            "HeroData.GetItem postfix",
            __instance,
            itemData,
            treasureChestClickTime,
            skipManageItemPoison,
            $"showPopInfo={showPopInfo}, showSpeGetItem={showSpeGetItem}, choiceEnabled={_treasureChestChoiceEnabled.Value}");
        if (_treasureChestChoiceEnabled.Value)
        {
            return;
        }

        TryGrantTreasureChestBonusItems(__instance, itemData, treasureChestClickTime, skipManageItemPoison);
    }

    private static bool TreasureChestChoicePlotCallbackPrefix(object[] __args)
    {
        var param = __args != null && __args.Length > 0 ? __args[0] as string : null;
        return !TryResolveTreasureChestChoiceFromPlot(param);
    }

    private static bool TreasureChestChoiceAdvancePrefix(PlotController? __instance)
    {
        if (_treasureChestChoiceClosingPlot)
        {
            return true;
        }

        var session = _activeTreasureChestChoiceSession;
        if (session != null && !session.Resolved)
        {
            TryResolveTreasureChestChoiceAndClose(__instance);
            return false;
        }

        return true;
    }

    private static void TreasureChestChoiceButtonClickedPostfix()
    {
        var session = _activeTreasureChestChoiceSession;
        if (session != null)
        {
            session.PendingClickConfirm = true;
        }
    }

    private static void TryHandleHealingStateTile(ExploreTileData? targetTileData, ExploreHealingState? healingState)
    {
        if (healingState == null)
        {
            return;
        }

        var player = healingState.Player ?? TryGetPlayerHero();
        if (player == null)
        {
            return;
        }

        var externalAfter = player.externalInjury;
        var internalAfter = player.internalInjury;
        var poisonAfter = player.poisonInjury;
        var anyHealingDetected =
            externalAfter + 0.001f < healingState.ExternalInjuryBefore ||
            internalAfter + 0.001f < healingState.InternalInjuryBefore ||
            poisonAfter + 0.001f < healingState.PoisonInjuryBefore;

        if (!healingState.IsHealingTile && !anyHealingDetected)
        {
            return;
        }

        var curedAnything = false;

        try
        {
            curedAnything |= TryClearHeroInjuryValue(player, player.externalInjury, static (hero, amount) => hero.ChangeExternalInjury(-amount, false, false, false), "externalInjury");
        }
        catch
        {
            curedAnything |= TrySetFloatMembers(player, new[] { "externalInjury", "ExternalInjury" }, 0f);
        }

        try
        {
            curedAnything |= TryClearHeroInjuryValue(player, player.internalInjury, static (hero, amount) => hero.ChangeInternalInjury(-amount, false, false, false), "internalInjury");
        }
        catch
        {
            curedAnything |= TrySetFloatMembers(player, new[] { "internalInjury", "InternalInjury" }, 0f);
        }

        try
        {
            curedAnything |= TryClearHeroInjuryValue(player, player.poisonInjury, static (hero, amount) => hero.ChangePoisonInjury(-amount, false, false, false), "poisonInjury");
        }
        catch
        {
            curedAnything |= TrySetFloatMembers(player, new[] { "poisonInjury", "PoisonInjury" }, 0f);
        }

        if (curedAnything)
        {
            PushPlayerLog("治疗地块额外清除了外伤、内伤、毒伤");
            LoggerInstance.LogInfo(
                $"Healing tile cleared all injury types: hero={TryGetHeroName(player)}, " +
                $"external={SafeFormatValue(player.externalInjury)}, internal={SafeFormatValue(player.internalInjury)}, poison={SafeFormatValue(player.poisonInjury)}.");
        }
    }

    private static bool TryStartTreasureChestChoice(HeroData? targetHero, ItemData? itemData, int treasureChestClickTime, bool skipManageItemPoison)
    {
        if (treasureChestClickTime > 0)
        {
            TraceTreasureChestEvent("TryStartTreasureChestChoice enter", targetHero, itemData, treasureChestClickTime, skipManageItemPoison);
        }

        if (!_treasureChestChoiceEnabled.Value || _grantingTreasureChestChoiceReward || _grantingTreasureChestBonusItems)
        {
            if (treasureChestClickTime > 0)
            {
                TraceTreasureChestEvent(
                    "TryStartTreasureChestChoice skip",
                    targetHero,
                    itemData,
                    treasureChestClickTime,
                    skipManageItemPoison,
                    $"choiceEnabled={_treasureChestChoiceEnabled.Value}, grantingChoiceReward={_grantingTreasureChestChoiceReward}, grantingBonusItems={_grantingTreasureChestBonusItems}");
            }
            return false;
        }

        if (treasureChestClickTime <= 0 || targetHero == null || itemData == null)
        {
            return false;
        }

        if (ShouldSkipTreasureChestChoiceForOriginalReward(itemData))
        {
            TraceTreasureChestEvent(
                "TryStartTreasureChestChoice skip original book/manual reward",
                targetHero,
                itemData,
                treasureChestClickTime,
                skipManageItemPoison,
                $"itemType={SafeFormatValue(TryGetItemTypeName(itemData))}");
            return false;
        }

        var player = TryGetPlayerHero();
        if (player == null || TryGetHeroId(targetHero) != TryGetHeroId(player))
        {
            TraceTreasureChestEvent(
                "TryStartTreasureChestChoice skip non-player",
                targetHero,
                itemData,
                treasureChestClickTime,
                skipManageItemPoison,
                $"player={TryGetHeroName(player)}/{SafeFormatValue(TryGetHeroId(player))}");
            return false;
        }

        if (_activeTreasureChestChoiceSession != null)
        {
            LoggerInstance.LogWarning("Skipped treasure chest choice because another chest choice session is already active.");
            TraceTreasureChestEvent("TryStartTreasureChestChoice skip active session", targetHero, itemData, treasureChestClickTime, skipManageItemPoison);
            return false;
        }

        var options = BuildTreasureChestChoiceOptions(itemData, player);
        if (options.Count <= 1)
        {
            TraceTreasureChestEvent(
                "TryStartTreasureChestChoice skip insufficient options",
                targetHero,
                itemData,
                treasureChestClickTime,
                skipManageItemPoison,
                $"options={DescribeItemSummaries(options)}");
            return false;
        }

        if (!TryShowTreasureChestChoicePlot(player, options, skipManageItemPoison))
        {
            TraceTreasureChestEvent(
                "TryStartTreasureChestChoice failed to show plot",
                targetHero,
                itemData,
                treasureChestClickTime,
                skipManageItemPoison,
                $"options={DescribeItemSummaries(options)}");
            return false;
        }

        TraceTreasureChestEvent(
            "TryStartTreasureChestChoice activated",
            targetHero,
            itemData,
            treasureChestClickTime,
            skipManageItemPoison,
            $"options={DescribeItemSummaries(options)}");
        LoggerInstance.LogInfo(
            $"Treasure chest opened as choose-one reward with {options.Count} options: " +
            $"{string.Join(", ", DescribeItemNames(options))}.");
        return true;
    }

    private static List<ItemData> BuildTreasureChestChoiceOptions(ItemData sourceItem, HeroData player)
    {
        var options = new List<ItemData>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        AddTreasureChestChoiceOption(options, seenKeys, sourceItem);

        var maxChoiceCount = Math.Max(3, Math.Min(5, _treasureChestChoiceOptions.Value));
        var desiredCount = maxChoiceCount <= 3 ? 3 : Random.Next(3, maxChoiceCount + 1);
        var maxAttempts = Math.Max(desiredCount * 4, 8);
        for (var attempt = 0; attempt < maxAttempts && options.Count < desiredCount; attempt++)
        {
            var generated = TryCreateTreasureChestBonusItem(sourceItem, player);
            AddTreasureChestChoiceOption(options, seenKeys, generated);
        }

        TraceTreasureChestEvent(
            "BuildTreasureChestChoiceOptions result",
            player,
            sourceItem,
            1,
            false,
            $"desiredCount={desiredCount}, maxChoiceCount={maxChoiceCount}, options={DescribeItemSummaries(options)}");
        return options;
    }

    private static void AddTreasureChestChoiceOption(List<ItemData> options, HashSet<string> seenKeys, ItemData? item)
    {
        if (item == null)
        {
            return;
        }

        var key = $"{item.itemID}|{item.itemLv}|{item.rareLv}|{item.value}|{item.name}";
        if (!seenKeys.Add(key))
        {
            return;
        }

        options.Add(item);
    }

    private static bool TryShowTreasureChestChoicePlot(HeroData player, List<ItemData> options, bool skipManageItemPoison)
    {
        var plotController = PlotController.Instance;
        if (plotController == null)
        {
            LoggerInstance.LogWarning("Could not show treasure chest choice plot because PlotController was unavailable.");
            return false;
        }

        var choiceTexts = new Il2CppSystem.Collections.Generic.List<string>();
        foreach (var option in options)
        {
            choiceTexts.Add(option.name ?? $"id={option.itemID}");
        }

        _activeTreasureChestChoiceSession = new TreasureChestChoiceSession
        {
            Player = player,
            Options = options,
            SkipManageItemPoison = skipManageItemPoison,
            OpenedAtRealtime = Time.realtimeSinceStartup
        };

        try
        {
            var choiceDataList = new Il2CppSystem.Collections.Generic.List<SinglePlotChoiceData>();
            for (var i = 0; i < options.Count; i++)
            {
                var choice = new SinglePlotChoiceData
                {
                    inited = true,
                    choiceText = options[i].name ?? $"id={options[i].itemID}",
                    callFuc = TreasureChestChoicePlotCallbackName,
                    callParam = TreasureChestChoiceParamPrefix + i,
                    describe = DescribeItemSummary(options[i])
                };
                choiceDataList.Add(choice);
            }

            var plot = new SinglePlotData
            {
                plotText = "宝箱里翻出几样东西，选一样带走。",
                noAutoJump = true,
                clickCallFuc = string.Empty,
                choices = choiceDataList
            };

            plotController.ChangePlot(plot);
            PushPlayerLog($"宝箱奖励改为 {options.Count} 选 1");
            TraceTreasureChestEvent(
                "TryShowTreasureChestChoicePlot shown",
                player,
                options.Count > 0 ? options[0] : null,
                1,
                skipManageItemPoison,
                $"options={DescribeItemSummaries(options)}");
            return true;
        }
        catch (Exception ex)
        {
            _activeTreasureChestChoiceSession = null;
            LoggerInstance.LogWarning($"Failed to show treasure chest choice plot: {ex.Message}");
            TraceTreasureChestEvent(
                "TryShowTreasureChestChoicePlot exception",
                player,
                options.Count > 0 ? options[0] : null,
                1,
                skipManageItemPoison,
                $"options={DescribeItemSummaries(options)}, exception={ex.Message}");
            return false;
        }
    }

    private static bool TryResolveTreasureChestChoiceFromPlot(string? param)
    {
        var session = _activeTreasureChestChoiceSession;
        if (session == null || session.Resolved)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(param) || !param.StartsWith(TreasureChestChoiceParamPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(param.Substring(TreasureChestChoiceParamPrefix.Length), out var index))
        {
            return false;
        }

        if (index < 0 || index >= session.Options.Count)
        {
            LoggerInstance.LogWarning($"Treasure chest choice index out of range: {index}");
            TraceTreasureChestEvent(
                "TryResolveTreasureChestChoiceFromPlot index out of range",
                session.Player,
                null,
                1,
                session.SkipManageItemPoison,
                $"param={SafeFormatValue(param)}, index={index}, options={session.Options.Count}");
            return true;
        }

        TraceTreasureChestEvent(
            "TryResolveTreasureChestChoiceFromPlot resolved",
            session.Player,
            session.Options[index],
            1,
            session.SkipManageItemPoison,
            $"param={SafeFormatValue(param)}, index={index}");
        GrantTreasureChestChoiceReward(session, session.Options[index], $"plot:{index}");
        return true;
    }

    private static bool TryResolveTreasureChestChoiceFromCurrentSelection(PlotController? plotController)
    {
        var session = _activeTreasureChestChoiceSession;
        if (session == null || session.Resolved || plotController == null)
        {
            return false;
        }

        try
        {
            var newChoiceParam = TryGetChoiceParam(plotController.newChoice);
            if (!string.IsNullOrWhiteSpace(newChoiceParam) &&
                TryResolveTreasureChestChoiceFromPlot(newChoiceParam))
            {
                return true;
            }

            var currentChoiceParam = TryGetChoiceParam(plotController.nowChoice);
            if (!string.IsNullOrWhiteSpace(currentChoiceParam) &&
                TryResolveTreasureChestChoiceFromPlot(currentChoiceParam))
            {
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Failed to resolve treasure chest choice from current selection: {ex.Message}");
            return false;
        }
    }

    private static void TryResolveTreasureChestChoiceAndClose(PlotController? plotController)
    {
        if (plotController == null)
        {
            return;
        }

        if (!TryResolveTreasureChestChoiceFromCurrentSelection(plotController))
        {
            return;
        }

        TryCloseTreasureChestChoicePlot(plotController);
    }

    private static void TryCloseTreasureChestChoicePlot(PlotController plotController)
    {
        if (_treasureChestChoiceClosingPlot)
        {
            return;
        }

        _treasureChestChoiceClosingPlot = true;
        try
        {
            plotController.PlotTextShowFinished();
            plotController.PlotChoiceShowFinished();
            plotController.HideInteractUI();
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Failed to close treasure chest choice plot: {ex.Message}");
        }
        finally
        {
            _treasureChestChoiceClosingPlot = false;
        }
    }

    private static void GrantTreasureChestChoiceReward(TreasureChestChoiceSession session, ItemData chosenItem, string source)
    {
        if (session.Resolved)
        {
            return;
        }

        var player = session.Player ?? TryGetPlayerHero();
        if (player == null)
        {
            LoggerInstance.LogWarning($"Could not grant treasure chest choice reward from {source} because the player was unavailable.");
            session.Resolved = true;
            _activeTreasureChestChoiceSession = null;
            return;
        }

        session.Resolved = true;
        _grantingTreasureChestChoiceReward = true;
        TraceTreasureChestEvent(
            "GrantTreasureChestChoiceReward enter",
            player,
            chosenItem,
            1,
            session.SkipManageItemPoison,
            $"source={source}");

        try
        {
            player.GetItem(chosenItem, true, true, 0, session.SkipManageItemPoison);
            PushPlayerLog($"宝箱选择获得：{chosenItem.name}");
            LoggerInstance.LogInfo($"Treasure chest choice granted from {source}: {DescribeItemSummary(chosenItem)}");
            TraceTreasureChestEvent(
                "GrantTreasureChestChoiceReward success",
                player,
                chosenItem,
                0,
                session.SkipManageItemPoison,
                $"source={source}");
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Failed to grant chosen treasure chest item from {source}: {ex.Message}");
            TraceTreasureChestEvent(
                "GrantTreasureChestChoiceReward exception",
                player,
                chosenItem,
                0,
                session.SkipManageItemPoison,
                $"source={source}, exception={ex.Message}");
        }
        finally
        {
            _grantingTreasureChestChoiceReward = false;
            _activeTreasureChestChoiceSession = null;
        }
    }

    private static IEnumerable<string> DescribeItemNames(IEnumerable<ItemData> items)
    {
        foreach (var item in items)
        {
            yield return item?.name ?? "unknown";
        }
    }

    private static string? TryGetTreasureChestCurrentChoiceParam(PlotController? plotController)
    {
        if (plotController == null)
        {
            return null;
        }

        try
        {
            var param = TryGetChoiceParam(plotController.newChoice);
            if (!string.IsNullOrWhiteSpace(param))
            {
                return param;
            }

            return TryGetChoiceParam(plotController.nowChoice);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetChoiceParam(SinglePlotChoiceData? choice)
    {
        if (choice == null)
        {
            return null;
        }

        try
        {
            var param = choice.callParam;
            return string.IsNullOrWhiteSpace(param) ? null : param;
        }
        catch
        {
            return null;
        }
    }

    private static void UpdateTreasureChestChoiceSession()
    {
        var session = _activeTreasureChestChoiceSession;
        if (session == null || session.Resolved)
        {
            return;
        }

        var plotController = PlotController.Instance;
        if (plotController == null)
        {
            return;
        }

        var currentParam = TryGetTreasureChestCurrentChoiceParam(plotController);
        if (!string.IsNullOrWhiteSpace(currentParam))
        {
            if (string.IsNullOrWhiteSpace(session.LastObservedChoiceParam))
            {
                session.LastObservedChoiceParam = currentParam;
            }
            else if (!string.Equals(session.LastObservedChoiceParam, currentParam, StringComparison.Ordinal))
            {
                session.LastObservedChoiceParam = currentParam;
                session.PendingClickConfirm = true;
                session.PendingClickConfirmFrames = 2;
            }
        }

        if (Input.GetMouseButtonDown(0) && Time.realtimeSinceStartup - session.OpenedAtRealtime > 0.15f)
        {
            session.PendingClickConfirm = true;
            session.PendingClickConfirmFrames = Math.Max(session.PendingClickConfirmFrames, 2);
        }

        if (!session.PendingClickConfirm)
        {
            return;
        }

        if (session.PendingClickConfirmFrames > 0)
        {
            session.PendingClickConfirmFrames--;
            return;
        }

        session.PendingClickConfirm = false;
        plotController.AutoPlotButtonClicked();
    }

    private static void TryGrantTreasureChestBonusItems(HeroData? targetHero, ItemData? itemData, int treasureChestClickTime, bool skipManageItemPoison)
    {
        if (_grantingTreasureChestBonusItems || treasureChestClickTime <= 0 || targetHero == null || itemData == null)
        {
            return;
        }

        var player = TryGetPlayerHero();
        if (player == null || TryGetHeroId(targetHero) != TryGetHeroId(player))
        {
            return;
        }

        var totalItems = Math.Max(1, _treasureChestTotalItems.Value);
        var extraItemCount = totalItems - 1;
        if (extraItemCount <= 0)
        {
            return;
        }

        var bonusNames = new List<string>();
        _grantingTreasureChestBonusItems = true;

        try
        {
            for (var i = 0; i < extraItemCount; i++)
            {
                var bonusItem = TryCreateTreasureChestBonusItem(itemData, player);
                if (bonusItem == null)
                {
                    continue;
                }

                player.GetItem(bonusItem, false, false, 0, skipManageItemPoison);
                bonusNames.Add(bonusItem.name ?? $"id={bonusItem.itemID}");
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Failed to grant bonus treasure chest items: {ex.Message}");
        }
        finally
        {
            _grantingTreasureChestBonusItems = false;
        }

        if (bonusNames.Count <= 0)
        {
            return;
        }

        PushPlayerLog($"宝箱额外获得：{string.Join("、", bonusNames)}");
        LoggerInstance.LogInfo(
            $"Treasure chest granted {bonusNames.Count} bonus item(s): " +
            $"{string.Join(", ", bonusNames)}.");
    }

    private static void ShowBuildingShopPostfix(BuildingUIController __instance)
    {
        ApplyPlayerCarryWeightOverride("BuildingUI.ShowBuildingShop");
        ApplyMerchantCarryCash(TradeUIType.Shop, __instance?.targetBuildingData?.shopItemList, "BuildingUI.ShowBuildingShop");
    }

    private static void ShowTradeUiBasicPostfix(TradeUIType targetType, ItemListData leftItemList, ItemListData rightItemList, bool _useAreaItemPrice)
    {
        ApplyPlayerCarryWeightOverride("TradeUI.ShowTradeUI/basic");
        ApplyMerchantCarryCash(targetType, rightItemList, "TradeUI.ShowTradeUI/basic");
    }

    private static void ShowTradeUiTypedPostfix(TradeUIType targetType, ItemListType targetItemListType, ItemListData leftItemList, ItemListData rightItemList)
    {
        ApplyPlayerCarryWeightOverride("TradeUI.ShowTradeUI/typed");
        ApplyMerchantCarryCash(targetType, rightItemList, "TradeUI.ShowTradeUI/typed");
    }

    private static void ShowTradeUiLevelRangePostfix(TradeUIType targetType, ItemListData leftItemList, ItemListData rightItemList, int _minItemLv, int _maxItemLv)
    {
        ApplyPlayerCarryWeightOverride("TradeUI.ShowTradeUI/level-range");
        ApplyMerchantCarryCash(targetType, rightItemList, "TradeUI.ShowTradeUI/level-range");
    }

    private static void ShowTradeUiFullPostfix(TradeUIType targetType, ItemListType targetItemListType, ItemListData leftItemList, ItemListData rightItemList, int _minItemLv, int _maxItemLv, bool _useAreaItemPrice, bool _noSell, float _speSellValueRate, float _speBuyValueRate)
    {
        ApplyPlayerCarryWeightOverride("TradeUI.ShowTradeUI/full");
        ApplyMerchantCarryCash(targetType, rightItemList, "TradeUI.ShowTradeUI/full");
    }

    private static void AddSkillBookExpPrefix(HeroData __instance, ref float num)
    {
        if (_applyingDailySkillInsightExp)
        {
            return;
        }

        var multiplier = Mathf.Max(1, _bookExpMultiplier.Value);
        if (multiplier <= 1 || num <= 0f)
        {
            return;
        }

        var player = TryGetPlayerHero();
        if (player == null || __instance != player)
        {
            return;
        }

        num *= multiplier;
    }

    private static void ManageTeachSkillPrefix(HeroData sourceHero, HeroData targetHero, int skillID, float rate, bool showInfo, out TeachSkillSplashState __state)
    {
        __state = new TeachSkillSplashState();

        if (!_teachSkillSameSectAreaShareEnabled.Value)
        {
            return;
        }

        if (!IsPlayerHero(sourceHero))
        {
            return;
        }

        if (sourceHero == null || targetHero == null)
        {
            return;
        }

        var sourceForceId = ResolveHeroForceId(sourceHero);
        if (sourceForceId <= 0)
        {
            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo($"Teach splash skipped: source hero {TryGetHeroName(sourceHero)} is not in a sect.");
            }

            return;
        }

        bool sameForce;
        try
        {
            sameForce = sourceHero.SameForce(targetHero);
        }
        catch
        {
            sameForce = false;
        }

        if (!sameForce)
        {
            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo(
                    $"Teach splash skipped: target {TryGetHeroName(targetHero)} is not in the same sect as {TryGetHeroName(sourceHero)}.");
            }

            return;
        }

        var targetSkill = TryFindHeroSkill(targetHero, skillID);
        if (targetSkill == null)
        {
            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo(
                    $"Teach splash skipped: target {TryGetHeroName(targetHero)} does not already know skill {skillID}.");
            }

            return;
        }

        __state = new TeachSkillSplashState
        {
            IsEligible = true,
            SourceHero = sourceHero,
            TargetHero = targetHero,
            SkillId = skillID,
            SkillName = TryGetSkillName(targetSkill),
            TargetBookProgressBefore = ResolveSkillExpProgress(targetSkill, useFightExp: false),
            TargetFightProgressBefore = ResolveSkillExpProgress(targetSkill, useFightExp: true)
        };

        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo(
                $"Teach splash trace enter: source={TryGetHeroName(sourceHero)}, target={TryGetHeroName(targetHero)}, " +
                $"skill={__state.SkillName}/{skillID}, rate={SafeFormatValue(rate)}, showInfo={showInfo}, area={TryGetAreaName(sourceHero)}.");
        }
    }

    private static void ManageTeachSkillPostfix(HeroData sourceHero, HeroData targetHero, int skillID, float rate, bool showInfo, TeachSkillSplashState __state)
    {
        if (!__state.IsEligible || __state.SourceHero == null || __state.TargetHero == null)
        {
            return;
        }

        var targetSkill = TryFindHeroSkill(__state.TargetHero, __state.SkillId);
        if (targetSkill == null)
        {
            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo(
                    $"Teach splash skipped after teach: target {TryGetHeroName(__state.TargetHero)} no longer has skill {__state.SkillId}.");
            }

            return;
        }

        var bookDelta = ResolveSkillExpProgress(targetSkill, useFightExp: false) - __state.TargetBookProgressBefore;
        var fightDelta = ResolveSkillExpProgress(targetSkill, useFightExp: true) - __state.TargetFightProgressBefore;
        var useFightExp = fightDelta > bookDelta;
        var baseExp = Mathf.Max(bookDelta, fightDelta);
        if (baseExp <= 0.001f)
        {
            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo(
                    $"Teach splash skipped: target {TryGetHeroName(__state.TargetHero)} gained no tracked EXP for {__state.SkillName}/{__state.SkillId}.");
            }

            return;
        }

        var recipients = ApplyTeachSkillSameSectAreaShare(__state.SourceHero, __state.TargetHero, __state.SkillId, baseExp, useFightExp, out var recipientResults);
        if (recipients <= 0)
        {
            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo(
                    $"Teach splash found no extra recipients: source={TryGetHeroName(__state.SourceHero)}, target={TryGetHeroName(__state.TargetHero)}, " +
                    $"skill={__state.SkillName}/{__state.SkillId}, baseExp={SafeFormatValue(baseExp)}, area={TryGetAreaName(__state.SourceHero)}.");
            }

            return;
        }

        PublishTeachSkillRecipientSideTabs(recipientResults, useFightExp);

        LoggerInstance.LogInfo(
            $"Teach splash applied: source={TryGetHeroName(__state.SourceHero)}, target={TryGetHeroName(__state.TargetHero)}, " +
            $"skill={__state.SkillName}/{__state.SkillId}, baseExp={SafeFormatValue(baseExp)}, expType={(useFightExp ? "fight" : "book")}, " +
            $"recipients={recipients}, area={TryGetAreaName(__state.SourceHero)}, detail=[{string.Join(", ", recipientResults.ConvertAll(FormatTeachSkillRecipientSummary))}].");
    }

    private static void ChangeMoneyPrefix(HeroData __instance, int num, out MoneyChangeState __state)
    {
        var isPlayerHero = IsPlayerHero(__instance);
        __state = new MoneyChangeState
        {
            IsEligible = !_applyingLuckyMoneyRefund && num != 0 && ClampPercent(_luckyMoneyHitChancePercent.Value) > 0 && isPlayerHero,
            RequestedDelta = num,
            MoneyBefore = TryGetHeroMoney(__instance),
            IsSpend = num < 0,
            IsIncome = num > 0
        };
    }

    private static void ChangeMoneyPostfix(HeroData __instance, int num, bool showInfo, MoneyChangeState __state)
    {
        if (!__state.IsEligible)
        {
            return;
        }

        var hitChancePercent = ClampPercent(_luckyMoneyHitChancePercent.Value);
        var roll = Random.Next(1, 101);
        if (roll > hitChancePercent)
        {
            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo($"Lucky money miss: roll {roll} > {hitChancePercent} for delta {__state.RequestedDelta}.");
            }

            return;
        }

        var changedAmount = ResolveChangedAmount(__state.RequestedDelta, __state.MoneyBefore, TryGetHeroMoney(__instance), __state.IsSpend);
        if (changedAmount <= 0)
        {
            return;
        }

        var rebatePercent = Random.Next(LuckyMoneyMinPercent, LuckyMoneyMaxPercent + 1);
        var rebateAmount = Mathf.Clamp(Mathf.RoundToInt(changedAmount * (rebatePercent / 100f)), 1, changedAmount);
        if (rebateAmount <= 0)
        {
            return;
        }

        try
        {
            _applyingLuckyMoneyRefund = true;
            __instance.ChangeMoney(rebateAmount, false);
        }
        finally
        {
            _applyingLuckyMoneyRefund = false;
        }

        var popup = __state.IsSpend
            ? $"感谢你长期光顾，现金回扣 {rebateAmount}"
            : $"你这个东西比想象的好，多给你 {rebateAmount}";
        PushPlayerLog(popup);
        LoggerInstance.LogInfo(
            $"Lucky money bonus applied: kind={(__state.IsSpend ? "spend" : "income")}, base={changedAmount}, bonus={rebateAmount}, percent={rebatePercent}, chance={hitChancePercent}, roll={roll}.");
    }

    private static void ChangeFavorPrefix(HeroData __instance, ref float num)
    {
        if (num <= 0f || IsPlayerHero(__instance))
        {
            return;
        }

        var hitChancePercent = ClampPercent(_extraRelationshipGainChancePercent.Value);
        if (hitChancePercent <= 0)
        {
            return;
        }

        var roll = Random.Next(1, 101);
        if (roll > hitChancePercent)
        {
            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo($"Relationship bonus miss: roll {roll} > {hitChancePercent} for favor gain {SafeFormatValue(num)}.");
            }

            return;
        }

        var originalGain = num;
        num *= 2f;

        var messageTemplate = RelationshipBonusMessages[Random.Next(RelationshipBonusMessages.Length)];
        var message = string.Format(messageTemplate, SafeFormatValue(originalGain));
        PushPlayerLog(message);
        LoggerInstance.LogInfo(
            $"Relationship bonus applied: hero={TryGetHeroName(__instance)}, gain {SafeFormatValue(originalGain)} -> {SafeFormatValue(num)}, chance={hitChancePercent}, roll={roll}.");
    }

    private static void DebateChangePatientPrefix(bool isPlayer, ref float num)
    {
        if (num >= 0f)
        {
            return;
        }

        var multiplier = Mathf.Max(0f, isPlayer
            ? _debatePlayerDamageTakenMultiplier.Value
            : _debateEnemyDamageTakenMultiplier.Value);
        if (Math.Abs(multiplier - 1f) < 0.001f)
        {
            return;
        }

        var original = num;
        num *= multiplier;

        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo(
                $"Debate damage scaled for {(isPlayer ? "player" : "enemy")}: {SafeFormatValue(original)} -> {SafeFormatValue(num)} with x{FormatConfigFloat(multiplier)}.");
        }
    }

    private static void CraftResultChoosenPrefix(ref ItemData craftResult)
    {
        if (!_craftRandomPickUpgradeEnabled.Value)
        {
            return;
        }

        var replacement = ResolveCraftReplacement(craftResult);
        if (replacement == null)
        {
            return;
        }

        var original = craftResult;
        craftResult = replacement;

        if (original != replacement)
        {
            PushPlayerLog($"【巧手偏锋】：改为【{replacement.name}】");
        }

        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo(
                $"Craft result rerolled: original={DescribeItemSummary(original)}, replacement={DescribeItemSummary(replacement)}.");
        }
    }

    private static void DrinkShowUiPostfix(DrinkUIController __instance)
    {
        ResetDrinkTracking(__instance);
    }

    private static void DrinkHideUiPostfix(DrinkUIController __instance)
    {
        ResetDrinkTracking(null);
    }

    private static void DrinkGetCostPostfix(DrinkUIController __instance, float fillAmount, ref float __result)
    {
        if (__result >= 0f)
        {
            UpdateDrinkTracking(__instance);
            return;
        }

        var targetIsPlayer = ResolveDrinkCostTargetIsPlayer(__instance, fillAmount);
        var multiplier = ResolveDrinkPowerCostMultiplier(targetIsPlayer);
        UpdateDrinkTracking(__instance);

        if (Math.Abs(multiplier - 1f) < 0.001f)
        {
            return;
        }

        var original = __result;
        __result *= multiplier;

        if (_traceMode.Value)
        {
            var targetLabel = targetIsPlayer.HasValue
                ? (targetIsPlayer.Value ? "player" : "enemy")
                : "unknown";
            LoggerInstance.LogInfo(
                $"Drink Qi cost scaled for {targetLabel}: {SafeFormatValue(original)} -> {SafeFormatValue(__result)} with x{FormatConfigFloat(multiplier)} at fill {SafeFormatValue(fillAmount)}.");
        }
    }

    private static void SetAttriPresetPostfix(StartMenuController __instance, int presetID)
    {
        ApplyCharacterCreationPointMultiplier(__instance, $"preset {presetID}");
    }

    private static void ResetPlayerAttriPostfix(StartMenuController __instance)
    {
        ApplyCharacterCreationPointMultiplier(__instance, "reset");
    }

    private static void BattleTimeScaleButtonClickedPostfix()
    {
        var multiplier = Math.Max(1, _battleSpeedMultiplier.Value);
        if (multiplier <= 1)
        {
            return;
        }

        var worldData = GameController.Instance?.worldData;
        if (worldData == null)
        {
            return;
        }

        var selectedSpeed = worldData.battleTimeScale;
        var adjustedSpeed = Mathf.Max(1f, selectedSpeed * multiplier);
        if (Math.Abs(adjustedSpeed - selectedSpeed) < 0.01f)
        {
            return;
        }

        worldData.battleTimeScale = adjustedSpeed;
        LoggerInstance.LogInfo($"Battle speed adjusted from x{selectedSpeed:0.###} to x{adjustedSpeed:0.###} using multiplier x{multiplier}.");
    }

    private static void HorseStartSprintPostfix(HorseData __instance)
    {
        if (!IsPlayerHorse(__instance))
        {
            return;
        }

        var durationMultiplier = Math.Max(0.01f, _horseTurboDurationMultiplier.Value);
        var cooldownMultiplier = Math.Max(0.01f, _horseTurboCooldownMultiplier.Value);
        var originalDuration = __instance.sprintTimeLeft;
        var originalCooldown = __instance.sprintTimeCd;
        var changed = false;

        if (Math.Abs(durationMultiplier - 1f) > 0.001f && originalDuration > 0f)
        {
            __instance.sprintTimeLeft = Mathf.Max(0f, originalDuration * durationMultiplier);
            changed = true;
        }

        if (Math.Abs(cooldownMultiplier - 1f) > 0.001f && originalCooldown > 0f)
        {
            __instance.sprintTimeCd = Mathf.Max(0f, originalCooldown * cooldownMultiplier);
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        LoggerInstance.LogInfo(
            $"Horse turbo adjusted: duration {originalDuration:0.###}->{__instance.sprintTimeLeft:0.###}, " +
            $"cooldown {originalCooldown:0.###}->{__instance.sprintTimeCd:0.###}, " +
            $"base x{FormatConfigFloat(_horseBaseSpeedMultiplier.Value)}, turbo x{FormatConfigFloat(_horseTurboSpeedMultiplier.Value)}.");

        KeepPlayerHorseTurboReady("StartSprint");
    }

    private static void GetHorseTravelSpeedPostfix(HeroData __instance, ref float __result)
    {
        if (!IsPlayerHero(__instance))
        {
            return;
        }

        __result = ApplyHorseTravelMultiplier(__instance, __result, IsHorseTurboActive(TryGetPlayerHorse()));
    }

    private static void GetHorseTravelSpeedWithFlagsPostfix(HeroData __instance, bool havePower, bool isSprint, ref float __result)
    {
        if (!IsPlayerHero(__instance))
        {
            return;
        }

        __result = ApplyHorseTravelMultiplier(__instance, __result, ResolveHorseTurboTravelState(havePower, isSprint));
    }

    private static void RefreshHorseStatePostfix(HeroData __instance)
    {
        if (!IsPlayerHero(__instance))
        {
            return;
        }

        KeepPlayerHorseTurboReady("RefreshHorseState");
    }

    private static bool CalendarChangePrefix(MethodBase __originalMethod, object[] __args, out CalendarChangeState __state)
    {
        __state = new CalendarChangeState
        {
            BeforeText = GetWorldDateText(includeHour: true),
            BeforeDate = TryGetWorldDateSnapshot()
        };

        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo($"TRACE DATE ENTER {DescribeMethod(__originalMethod)} dateBefore={__state.BeforeText} args={DescribeArgs(__args)}");
        }

        if (_freezeDate.Value)
        {
            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo($"Freeze Date blocked {DescribeMethod(__originalMethod)} at {__state.BeforeText}.");
            }

            return false;
        }

        return true;
    }

    private static void CalendarChangePostfix(MethodBase __originalMethod, object[] __args, CalendarChangeState __state)
    {
        var afterText = GetWorldDateText(includeHour: true);
        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo($"TRACE DATE EXIT  {DescribeMethod(__originalMethod)} dateBefore={__state.BeforeText} dateAfter={afterText}");
        }

        HandleDailySkillInsightDateProgress(__state.BeforeDate, TryGetWorldDateSnapshot(), DescribeMethod(__originalMethod));
    }

    private static bool HourChangePrefix(MethodBase __originalMethod, object[] __args, out string __state)
    {
        __state = GetWorldDateText(includeHour: true);

        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo($"TRACE TIME ENTER {DescribeMethod(__originalMethod)} dateBefore={__state} args={DescribeArgs(__args)}");
        }

        return true;
    }

    private static void HourChangePostfix(MethodBase __originalMethod, object[] __args, string __state)
    {
        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo($"TRACE TIME EXIT  {DescribeMethod(__originalMethod)} dateBefore={__state} dateAfter={GetWorldDateText(includeHour: true)}");
        }
    }

    private static void GameControllerUpdatePostfix()
    {
        EnsureDailySkillInsightBaseline();
        TryRunRealtimeSkillInsight();
        UpdateTreasureChestChoiceSession();
        KeepPlayerHorseTurboReady("Update");
        ApplyPlayerCarryWeightOverride("Update");

        if (Input.GetKeyDown(_freezeDateHotkey.Value))
        {
            ToggleFreezeDate("hotkey");
        }

        if (Input.GetKeyDown(_outsideBattleSpeedHotkey.Value))
        {
            CycleOutsideBattleSpeed();
        }
    }

    private static void DialogHeroContextPostfix(HeroData __0)
    {
        CacheActiveDialogHero(__0);
    }

    private static void DialogChoiceRowPostfix(PlotInteractController __instance)
    {
        if (__instance?.choiceData == null)
        {
            return;
        }

        ApplyDialogMonthlyQuota(__instance, __instance.choiceData, consume: false);
    }

    private static void DialogChoiceClickPrefix(PlotInteractController __instance)
    {
        if (__instance?.choiceData == null)
        {
            return;
        }

        ApplyDialogMonthlyQuota(__instance, __instance.choiceData, consume: true);
    }

    private static void CacheActiveDialogHero(HeroData? hero)
    {
        _activeDialogHero = hero;
        _activeDialogHeroId = TryGetHeroId(hero) ?? -1;
    }

    private static void ApplyDialogMonthlyQuota(PlotInteractController controller, SinglePlotChoiceData choice, bool consume)
    {
        var timeNeedValue = SafeGetMemberValue(choice, "playerInteractionTimeNeed");
        var timeNeed = SafeFormatValue(timeNeedValue);
        if (string.IsNullOrEmpty(timeNeed) || string.Equals(timeNeed, "None", StringComparison.Ordinal))
        {
            return;
        }

        var heroId = _activeDialogHeroId;
        if (heroId < 0)
        {
            return;
        }

        var monthKey = GetCurrentWorldMonthKey();
        var key = $"{monthKey}|hero={heroId}|type={timeNeed}";
        var used = _dialogMonthlyUseCounts.TryGetValue(key, out var currentUsed) ? currentUsed : 0;
        var limit = GetDialogMonthlyLimit(heroId, timeNeed);
        var allowed = used < limit;

        if (consume && allowed)
        {
            used++;
            _dialogMonthlyUseCounts[key] = used;
        }

        SyncVanillaDialogMonthlyUsage(choice, timeNeedValue, Math.Max(0, limit - used));
        controller.meetCost = allowed;
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
        var playerInteractionTimeData = SafeGetMemberValue(_activeDialogHero, "playerInteractionTimeData");
        if (playerInteractionTimeData == null || timeNeedValue == null)
        {
            return;
        }

        var list = SafeGetMemberValue(playerInteractionTimeData, "playerInteractTimeList");
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

    private static void ToggleFreezeDate(string source)
    {
        _freezeDate.Value = !_freezeDate.Value;
        LoggerInstance.LogInfo($"Freeze Date {(_freezeDate.Value ? "enabled" : "disabled")} from {source} at {GetWorldDateText(includeHour: true)}.");
        PushPlayerLog($"Mod: Freeze Date {(_freezeDate.Value ? "ON" : "OFF")}");
    }

    private static void CycleOutsideBattleSpeed()
    {
        var worldData = GameController.Instance?.worldData;
        if (worldData == null)
        {
            LoggerInstance.LogWarning("Outside-battle speed hotkey ignored because world data is unavailable.");
            return;
        }

        if (IsBattleUiActive())
        {
            LoggerInstance.LogInfo("Outside-battle speed hotkey ignored because battle UI is active.");
            PushPlayerLog("Mod: Outside battle speed only works outside battle");
            return;
        }

        var current = worldData.battleTimeScale;
        var next = GetNextOutsideBattleSpeed(current);
        worldData.battleTimeScale = next;

        LoggerInstance.LogInfo($"Outside-battle speed changed from x{current:0.###} to x{next:0.###}.");
        PushPlayerLog($"Mod: Outside battle speed x{next:0.###}");
    }

    private static void PushPlayerLog(string text)
    {
        var delivered = false;
        var deliveredChannels = new List<string>();

        try
        {
            var infoController = InfoController.Instance;
            if (infoController != null)
            {
                infoController.AddInfo(InfoType.WorldInfo, text);
                infoController.AddInfo(InfoType.PersonalInfo, text);
                infoController.BuildInfoList();
                delivered = true;
                deliveredChannels.Add("InfoController");
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Player log via InfoController failed: {ex.Message}");
        }

        try
        {
            var gameController = GameController.Instance;
            if (gameController != null)
            {
                gameController.ShowTextOnMouse(text, 28, Color.yellow);
                delivered = true;
                deliveredChannels.Add("ShowTextOnMouse");
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Player log via ShowTextOnMouse failed: {ex.Message}");
        }

        try
        {
            var player = TryGetPlayerHero();
            if (player != null)
            {
                player.AddLog(text);
                delivered = true;
                deliveredChannels.Add("HeroData");
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Player log via HeroData failed: {ex.Message}");
        }

        try
        {
            var areaController = AreaController.Instance;
            var areaData = areaController?.areaData;
            if (areaController != null && areaData != null)
            {
                areaData.AddLog(text);
                areaData.areaInfoDirty = true;
                var areaLog = areaController.areaLog;
                if (areaLog != null)
                {
                    areaLog.text = areaData.GetRecordLog();
                }

                areaController.FreshAreaInfo(true);
                delivered = true;
                deliveredChannels.Add("AreaData");
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Player log via AreaData failed: {ex.Message}");
        }

        if (!delivered)
        {
            LoggerInstance.LogWarning($"PLAYER LOG SKIPPED: {text}");
            return;
        }

        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo($"PLAYER LOG DELIVERED via {string.Join(", ", deliveredChannels)}: {text}");
        }
    }

    private static void PushPlayerSideTabLog(string text)
    {
        var delivered = false;

        try
        {
            var infoController = InfoController.Instance;
            if (infoController != null)
            {
                infoController.AddInfoTab(
                    text,
                    TeachSkillSideTabAtlasName,
                    TeachSkillSideTabIconName,
                    TeachSkillSideTabSoundName,
                    TeachSkillSideTabSoundVolume,
                    TeachSkillSideTabDurationSeconds,
                    Color.clear);
                delivered = true;
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Player side-tab log failed: {ex.Message}");
        }

        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo($"{(delivered ? "PLAYER SIDE TAB DELIVERED" : "PLAYER SIDE TAB SKIPPED")}: {text}");
        }
    }

    private static string DescribeMethod(MethodBase method)
    {
        return $"{method.DeclaringType?.Name}.{method.Name}";
    }

    private static int ClampPercent(int value)
    {
        return Mathf.Clamp(value, 0, 100);
    }

    private static ItemData? ResolveCraftReplacement(ItemData? original)
    {
        if (original == null)
        {
            return original;
        }

        var targetMajorTier = GetCraftMajorTier(original) + 1;
        if (targetMajorTier <= GetCraftMajorTier(original))
        {
            return original;
        }

        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo(
                $"Craft reroll start: base={DescribeItemSummary(original)}, targetMajorTier={targetMajorTier}.");
        }

        var generated = TryGenerateCraftUpgrade(original, targetMajorTier);
        if (generated != null)
        {
            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo(
                    $"Craft reroll success: base={DescribeItemSummary(original)}, generated={DescribeItemSummary(generated)}.");
            }

            return generated;
        }

        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo(
                $"Craft reroll kept original: base={DescribeItemSummary(original)}, targetMajorTier={targetMajorTier}.");
        }

        return original;
    }

    private static ItemData? TryGenerateCraftUpgrade(ItemData original, int targetMajorTier)
    {
        var gameController = GameController.Instance;
        if (gameController == null)
        {
            return null;
        }

        var controller = CraftUIController.Instance;
        var player = TryGetPlayerHero();
        var targetItemType = (int)original.type;
        var subType = original.subType;
        var littleType = original.equipmentData?.littleType ?? 0;
        var weaponType = controller?.targetWeaponType ?? 0;
        var bossLv = Mathf.Max(1f, controller?.targetBuilding?.lv ?? Math.Max(1, original.itemLv + 1));
        var baseValue = Math.Max(1, original.value);

        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo(
                $"Craft reroll inputs: type={(int)original.type}, subType={subType}, littleType={littleType}, weaponType={weaponType}, bossLv={bossLv:0.###}, baseValue={baseValue}, targetMajorTier={targetMajorTier}.");
        }

        ItemData? bestCandidate = null;
        var valueMultipliers = new[] { 1.15f, 1.3f, 1.5f, 1.75f, 2.1f, 2.6f, 3.2f, 4f, 5f };
        foreach (var multiplier in valueMultipliers)
        {
            var targetValue = Mathf.Max(baseValue + 1, Mathf.RoundToInt(baseValue * multiplier));
            ItemData? candidate = null;

            try
            {
                candidate = gameController.GenerateRandomItemValue(
                    targetValue,
                    targetItemType,
                    bossLv,
                    subType,
                    littleType,
                    player,
                    weaponType);
            }
            catch (Exception ex)
            {
                if (_traceMode.Value)
                {
                    LoggerInstance.LogWarning($"Craft regenerate failed at value {targetValue}: {ex.Message}");
                }

                continue;
            }

            if (candidate == null)
            {
                if (_traceMode.Value)
                {
                    LoggerInstance.LogInfo(
                        $"Craft reroll try x{multiplier:0.###} targetValue={targetValue} returned null.");
                }

                continue;
            }

            var candidateMajorTier = GetCraftMajorTier(candidate);
            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo(
                    $"Craft reroll try x{multiplier:0.###} targetValue={targetValue} -> {DescribeItemSummary(candidate)}.");
            }

            if (candidateMajorTier > GetCraftMajorTier(bestCandidate))
            {
                bestCandidate = candidate;
            }

            if (candidateMajorTier >= targetMajorTier)
            {
                return candidate;
            }
        }

        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo(
                $"Craft reroll best candidate after all tries: {DescribeItemSummary(bestCandidate)}.");
        }

        return bestCandidate != null && GetCraftMajorTier(bestCandidate) > GetCraftMajorTier(original)
            ? bestCandidate
            : null;
    }

    private static string DescribeItemSummary(ItemData? item)
    {
        if (item == null)
        {
            return "null";
        }

        return $"{item.name} (id={item.itemID}, itemLv={item.itemLv}, rare={item.rareLv}, value={item.value}, type={SafeFormatValue(TryGetItemTypeName(item))})";
    }

    private static string DescribeItemSummaries(IEnumerable<ItemData> items)
    {
        if (items == null)
        {
            return "none";
        }

        var parts = new List<string>();
        foreach (var item in items)
        {
            parts.Add(DescribeItemSummary(item));
        }

        return parts.Count > 0 ? string.Join(" || ", parts) : "none";
    }

    private static bool IsTreasureChestTraceEnabled()
    {
        return _traceMode.Value && _traceTreasureChestEvents.Value;
    }

    private static bool ShouldSkipTreasureChestChoiceForOriginalReward(ItemData? item)
    {
        if (item == null)
        {
            return false;
        }

        try
        {
            if (item.type == ItemType.Book)
            {
                return true;
            }
        }
        catch
        {
        }

        var itemTypeName = TryGetItemTypeName(item);
        if (string.Equals(itemTypeName, nameof(ItemType.Book), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var itemName = item.name;
        return !string.IsNullOrWhiteSpace(itemName) &&
               (itemName.Contains("秘籍", StringComparison.Ordinal) ||
                itemName.Contains("秘笈", StringComparison.Ordinal) ||
                itemName.Contains("功法", StringComparison.Ordinal));
    }

    private static string? TryGetItemTypeName(ItemData? item)
    {
        if (item == null)
        {
            return null;
        }

        try
        {
            return Enum.GetName(typeof(ItemType), item.type) ?? item.type.ToString();
        }
        catch
        {
        }

        return SafeGetMemberValue(item, "type")?.ToString();
    }

    private static void TraceTreasureChestEvent(string stage, HeroData? targetHero, ItemData? itemData, int treasureChestClickTime, bool? skipManageItemPoison, string? extra = null)
    {
        if (!IsTreasureChestTraceEnabled())
        {
            return;
        }

        var player = TryGetPlayerHero();
        var plotController = PlotController.Instance;
        var session = _activeTreasureChestChoiceSession;
        var sessionSummary = session == null
            ? "inactive"
            : $"active(resolved={session.Resolved}, options={session.Options.Count}, pending={session.PendingClickConfirm}, lastChoice={SafeFormatValue(session.LastObservedChoiceParam)})";
        var plotSummary = plotController == null
            ? "plot=unavailable"
            : $"plotChoiceNow={SafeFormatValue(TryGetChoiceParam(plotController.nowChoice))}, plotChoiceNew={SafeFormatValue(TryGetChoiceParam(plotController.newChoice))}, plotText={SafeFormatValue(TryReadPlotText(plotController))}";

        LoggerInstance.LogInfo(
            $"[TRACE][TreasureChest] {stage}: " +
            $"target={TryGetHeroName(targetHero)}/{SafeFormatValue(TryGetHeroId(targetHero))}, " +
            $"player={TryGetHeroName(player)}/{SafeFormatValue(TryGetHeroId(player))}, " +
            $"item={DescribeItemSummary(itemData)}, chestClick={treasureChestClickTime}, " +
            $"skipManageItemPoison={SafeFormatValue(skipManageItemPoison)}, session={sessionSummary}, {plotSummary}" +
            $"{(string.IsNullOrWhiteSpace(extra) ? string.Empty : $", {extra}")}");
    }

    private static string? TryReadPlotText(PlotController? plotController)
    {
        if (plotController == null)
        {
            return null;
        }

        var directText = TryReadStringMember(plotController, new[] { "plotText", "PlotText" });
        if (!string.IsNullOrWhiteSpace(directText))
        {
            return directText;
        }

        foreach (var memberName in new[] { "nowPlot", "newPlot", "plotData", "nowPlotData", "showPlotData" })
        {
            var plotData = SafeGetMemberValue(plotController, memberName);
            var plotText = TryReadStringMember(plotData, new[] { "plotText", "PlotText" });
            if (!string.IsNullOrWhiteSpace(plotText))
            {
                return plotText;
            }
        }

        return null;
    }

    private static int GetCraftMajorTier(ItemData? item)
    {
        return item?.itemLv ?? int.MinValue;
    }

    private static void ResetDrinkTracking(DrinkUIController? controller)
    {
        _lastDrinkControllerInstanceId = controller == null ? 0 : controller.GetInstanceID();
        _lastDrinkPlayerFillAmount = TryReadFloatMember(controller, DrinkPlayerFillAmountMemberNames) ?? float.NaN;
        _lastDrinkEnemyFillAmount = TryReadFloatMember(controller, DrinkEnemyFillAmountMemberNames) ?? float.NaN;
        _lastResolvedDrinkTargetIsPlayer = null;
    }

    private static void UpdateDrinkTracking(DrinkUIController? controller)
    {
        if (controller == null)
        {
            return;
        }

        EnsureDrinkTracking(controller);
        _lastDrinkPlayerFillAmount = TryReadFloatMember(controller, DrinkPlayerFillAmountMemberNames) ?? float.NaN;
        _lastDrinkEnemyFillAmount = TryReadFloatMember(controller, DrinkEnemyFillAmountMemberNames) ?? float.NaN;
    }

    private static bool? ResolveDrinkCostTargetIsPlayer(DrinkUIController? controller, float fillAmount)
    {
        if (controller == null)
        {
            return _lastResolvedDrinkTargetIsPlayer;
        }

        EnsureDrinkTracking(controller);

        var playerFillAmount = TryReadFloatMember(controller, DrinkPlayerFillAmountMemberNames);
        var enemyFillAmount = TryReadFloatMember(controller, DrinkEnemyFillAmountMemberNames);
        bool? resolved = null;

        var playerMatch = playerFillAmount.HasValue && Math.Abs(playerFillAmount.Value - fillAmount) <= DrinkFillMatchTolerance;
        var enemyMatch = enemyFillAmount.HasValue && Math.Abs(enemyFillAmount.Value - fillAmount) <= DrinkFillMatchTolerance;
        if (playerMatch ^ enemyMatch)
        {
            resolved = playerMatch;
        }

        if (!resolved.HasValue)
        {
            var playerDelta = playerFillAmount.HasValue && !float.IsNaN(_lastDrinkPlayerFillAmount)
                ? Math.Abs(playerFillAmount.Value - _lastDrinkPlayerFillAmount)
                : 0f;
            var enemyDelta = enemyFillAmount.HasValue && !float.IsNaN(_lastDrinkEnemyFillAmount)
                ? Math.Abs(enemyFillAmount.Value - _lastDrinkEnemyFillAmount)
                : 0f;

            var playerChanged = playerDelta > DrinkFillDeltaTolerance;
            var enemyChanged = enemyDelta > DrinkFillDeltaTolerance;
            if (playerChanged ^ enemyChanged)
            {
                resolved = playerChanged;
            }
            else if (playerChanged && enemyChanged)
            {
                resolved = playerDelta >= enemyDelta;
            }
        }

        if (!resolved.HasValue && playerFillAmount.HasValue && enemyFillAmount.HasValue)
        {
            var playerDiff = Math.Abs(playerFillAmount.Value - fillAmount);
            var enemyDiff = Math.Abs(enemyFillAmount.Value - fillAmount);
            if (playerDiff + DrinkFillMatchTolerance < enemyDiff)
            {
                resolved = true;
            }
            else if (enemyDiff + DrinkFillMatchTolerance < playerDiff)
            {
                resolved = false;
            }
        }

        if (resolved.HasValue)
        {
            _lastResolvedDrinkTargetIsPlayer = resolved;
        }

        return resolved ?? _lastResolvedDrinkTargetIsPlayer;
    }

    private static void EnsureDrinkTracking(DrinkUIController controller)
    {
        var instanceId = controller.GetInstanceID();
        if (_lastDrinkControllerInstanceId == instanceId)
        {
            return;
        }

        ResetDrinkTracking(controller);
    }

    private static float ResolveDrinkPowerCostMultiplier(bool? targetIsPlayer)
    {
        if (!targetIsPlayer.HasValue)
        {
            var sharedMultiplier = Mathf.Max(0f, _drinkPlayerPowerCostMultiplier.Value);
            return Math.Abs(sharedMultiplier - Mathf.Max(0f, _drinkEnemyPowerCostMultiplier.Value)) < 0.001f
                ? sharedMultiplier
                : 1f;
        }

        return Mathf.Max(0f, targetIsPlayer.Value
            ? _drinkPlayerPowerCostMultiplier.Value
            : _drinkEnemyPowerCostMultiplier.Value);
    }

    private static float GetNextOutsideBattleSpeed(float current)
    {
        for (var i = 0; i < OutsideBattleSpeedCycle.Length; i++)
        {
            if (Math.Abs(current - OutsideBattleSpeedCycle[i]) < 0.01f)
            {
                return OutsideBattleSpeedCycle[(i + 1) % OutsideBattleSpeedCycle.Length];
            }
        }

        return OutsideBattleSpeedCycle[0];
    }

    private static bool IsBattleUiActive()
    {
        try
        {
            var battleUi = BattleController.Instance?.battleTimeUI;
            return battleUi != null && battleUi.activeInHierarchy;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyCharacterCreationPointMultiplier(StartMenuController? controller, string source)
    {
        var multiplier = Math.Max(1, _creationPointMultiplier.Value);
        if (controller == null || multiplier <= 1)
        {
            return;
        }

        var attri = controller.leftAttriPoint;
        var fight = controller.leftFightSkillPoint;
        var living = controller.leftLivingSkillPoint;

        controller.leftAttriPoint = attri * multiplier;
        controller.leftFightSkillPoint = fight * multiplier;
        controller.leftLivingSkillPoint = living * multiplier;

        LoggerInstance.LogInfo(
            $"Character creation points multiplied x{multiplier} from {source}: " +
            $"Attri {attri}->{controller.leftAttriPoint}, " +
            $"Fight {fight}->{controller.leftFightSkillPoint}, " +
            $"Living {living}->{controller.leftLivingSkillPoint}.");
    }

    private static int TryGetCollectionCount(object? value)
    {
        if (value is System.Collections.ICollection collection)
        {
            return collection.Count;
        }

        return -1;
    }

    private static string DescribeArgs(object[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return "[]";
        }

        var parts = new string[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            parts[i] = $"{i}:{SafeFormatValue(args[i])}";
        }

        return "[" + string.Join(", ", parts) + "]";
    }

    private static int ApplyTeachSkillSameSectAreaShare(HeroData sourceHero, HeroData directTarget, int skillId, float baseExp, bool useFightExp, out List<TeachSkillRecipientResult> recipientResults)
    {
        recipientResults = new List<TeachSkillRecipientResult>();
        if (baseExp <= 0f)
        {
            return 0;
        }

        var candidateHeroes = GetHeroesInSameArea(sourceHero);
        if (candidateHeroes.Count == 0)
        {
            return 0;
        }

        var minPercent = ClampTeachSkillSplashPercent(_teachSkillSameSectAreaShareMinPercent.Value);
        var maxPercent = ClampTeachSkillSplashPercent(_teachSkillSameSectAreaShareMaxPercent.Value);
        if (maxPercent < minPercent)
        {
            (minPercent, maxPercent) = (maxPercent, minPercent);
        }

        var recipientCount = 0;
        foreach (var candidate in candidateHeroes)
        {
            if (candidate == null || candidate == sourceHero || candidate == directTarget)
            {
                continue;
            }

            bool sameForce;
            try
            {
                sameForce = sourceHero.SameForce(candidate);
            }
            catch
            {
                sameForce = false;
            }

            if (!sameForce)
            {
                continue;
            }

            var recipientSkill = TryFindHeroSkill(candidate, skillId);
            if (recipientSkill == null)
            {
                if (_traceMode.Value)
                {
                    LoggerInstance.LogInfo($"Teach splash candidate skipped: {TryGetHeroName(candidate)} does not know skill {skillId}.");
                }

                continue;
            }

            if (useFightExp ? !CanGainFightExp(recipientSkill) : !CanGainBookExp(recipientSkill))
            {
                if (_traceMode.Value)
                {
                    LoggerInstance.LogInfo(
                        $"Teach splash candidate skipped: {TryGetHeroName(candidate)} cannot gain more {(useFightExp ? "fight" : "book")} EXP for {TryGetSkillName(recipientSkill)}.");
                }

                continue;
            }

            var sharePercent = Random.Next(minPercent, maxPercent + 1);
            var sharedExp = baseExp * (sharePercent / 100f);
            if (sharedExp <= 0f)
            {
                continue;
            }

            try
            {
                if (useFightExp)
                {
                    candidate.AddSkillFightExp(sharedExp, recipientSkill, false);
                }
                else
                {
                    candidate.AddSkillBookExp(sharedExp, recipientSkill, false);
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.LogWarning(
                    $"Teach splash failed for {TryGetHeroName(candidate)} on skill {TryGetSkillName(recipientSkill)}: {ex.Message}");
                continue;
            }

            recipientCount++;
            recipientResults.Add(new TeachSkillRecipientResult
            {
                HeroName = TryGetHeroName(candidate),
                SkillName = TryGetSkillName(recipientSkill),
                Exp = sharedExp,
                Percent = sharePercent
            });
        }

        return recipientCount;
    }

    private static void PublishTeachSkillRecipientSideTabs(List<TeachSkillRecipientResult> recipientResults, bool useFightExp)
    {
        if (recipientResults.Count == 0)
        {
            return;
        }

        foreach (var result in recipientResults)
        {
            PushPlayerSideTabLog(BuildTeachSkillRecipientSideTabText(result, useFightExp));
        }
    }

    private static string BuildTeachSkillRecipientSideTabText(TeachSkillRecipientResult result, bool useFightExp)
    {
        var expLabel = useFightExp ? "实战经验" : "理论经验";
        return $"<color=#78BE00>{result.HeroName}</color><color=#8C8C8C>{result.SkillName}</color><color=#00B400>{expLabel}+{FormatTeachSkillExp(result.Exp)}</color>({result.Percent}%)";
    }

    private static string FormatTeachSkillRecipientSummary(TeachSkillRecipientResult result)
    {
        return $"{result.HeroName}:{SafeFormatValue(result.Exp)}({result.Percent}%)";
    }

    private static string FormatTeachSkillExp(float exp)
    {
        return Math.Abs(exp - Mathf.Round(exp)) <= 0.001f
            ? Mathf.RoundToInt(exp).ToString()
            : exp.ToString("0.##");
    }

    private static List<HeroData> GetHeroesInSameArea(HeroData sourceHero)
    {
        var results = new List<HeroData>();
        var seenHeroIds = new HashSet<int>();

        void AddHero(HeroData? hero)
        {
            if (hero == null)
            {
                return;
            }

            var heroId = TryGetHeroId(hero);
            if (!heroId.HasValue)
            {
                results.Add(hero);
                return;
            }

            if (seenHeroIds.Add(heroId.Value))
            {
                results.Add(hero);
            }
        }

        try
        {
            var area = sourceHero.GetArea();
            if (area != null)
            {
                var insideHeros = area.insideHeros;
                if (insideHeros != null)
                {
                    for (var i = 0; i < insideHeros.Count; i++)
                    {
                        AddHero(GameController.Instance?.worldData?.GetHero(insideHeros[i]));
                    }
                }
            }
        }
        catch
        {
        }

        if (results.Count > 0)
        {
            return results;
        }

        var sourceAreaId = TryGetHeroAreaId(sourceHero);
        if (!sourceAreaId.HasValue)
        {
            return results;
        }

        try
        {
            var worldHeroes = GameController.Instance?.worldData?.Heros;
            if (worldHeroes != null)
            {
                for (var i = 0; i < worldHeroes.Count; i++)
                {
                    var hero = worldHeroes[i];
                    if (hero != null && TryGetHeroAreaId(hero) == sourceAreaId)
                    {
                        AddHero(hero);
                    }
                }
            }
        }
        catch
        {
        }

        return results;
    }

    private static KungfuSkillLvData? TryFindHeroSkill(HeroData? hero, int skillId)
    {
        if (hero == null)
        {
            return null;
        }

        try
        {
            return hero.FindSkill(skillId);
        }
        catch
        {
            return null;
        }
    }

    private static int ClampTeachSkillSplashPercent(int value)
    {
        return Mathf.Clamp(value, TeachSkillSplashMinPercentFloor, TeachSkillSplashMaxPercentCeiling);
    }

    private static float ResolveSkillExpProgress(KungfuSkillLvData? skill, bool useFightExp)
    {
        if (skill == null)
        {
            return 0f;
        }

        int level;
        try
        {
            level = Math.Max(1, skill.lv);
        }
        catch
        {
            level = 1;
        }

        var progress = 0f;
        for (var currentLevel = 1; currentLevel < level; currentLevel++)
        {
            progress += TryGetSkillLevelMaxExp(skill, currentLevel);
        }

        try
        {
            progress += Mathf.Max(0f, useFightExp ? skill.fightExp : skill.bookExp);
        }
        catch
        {
        }

        return progress;
    }

    private static float TryGetSkillLevelMaxExp(KungfuSkillLvData skill, int level)
    {
        try
        {
            return Mathf.Max(0f, skill.SkillGetMaxExp(Math.Max(1, level)));
        }
        catch
        {
            return 0f;
        }
    }

    private static bool CanGainFightExp(KungfuSkillLvData skill)
    {
        try
        {
            return !skill.FightExpFull();
        }
        catch
        {
            return true;
        }
    }

    private static string GetWorldDateText(bool includeHour)
    {
        try
        {
            var worldData = GameController.Instance?.worldData;
            var worldTime = worldData?.worldTime;
            if (worldTime == null)
            {
                return "Date: unavailable";
            }

            var dateText = $"Date: Y{worldTime.year} M{worldTime.month} D{worldTime.day}";
            if (!includeHour)
            {
                return dateText;
            }

            return $"{dateText} H{worldData?.hour.ToString("0.##") ?? "?"}";
        }
        catch
        {
            return "Date: unavailable";
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

    private static string FormatConfigFloat(float value)
    {
        return value.ToString("0.###");
    }

    private static bool ResolveHorseTurboTravelState(bool havePower, bool isSprint)
    {
        if (isSprint)
        {
            return havePower || _lockHorseTurboStamina.Value;
        }

        return _lockHorseTurboStamina.Value && IsHorseTurboActive(TryGetPlayerHorse());
    }

    private static float ApplyHorseTravelMultiplier(HeroData hero, float speed, bool turboActive)
    {
        if (speed <= 0f)
        {
            return speed;
        }

        var multiplier = Math.Max(0.01f, _horseBaseSpeedMultiplier.Value);
        if (turboActive)
        {
            multiplier *= Math.Max(0.01f, _horseTurboSpeedMultiplier.Value);
        }

        return speed * multiplier;
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

    private static bool IsPlayerHero(HeroData? hero)
    {
        var player = TryGetPlayerHero();
        return player != null && hero != null && player == hero;
    }

    private static HorseData? TryGetPlayerHorse()
    {
        var player = TryGetPlayerHero();
        if (player == null)
        {
            return null;
        }

        return SafeProperty(player, "horse") as HorseData
               ?? SafeField(player, "horse") as HorseData
               ?? SafeProperty(player, "Horse") as HorseData
               ?? SafeField(player, "Horse") as HorseData;
    }

    private static bool IsPlayerHorse(HorseData? horse)
    {
        var playerHorse = TryGetPlayerHorse();
        return playerHorse != null && horse != null && playerHorse == horse;
    }

    private static bool IsHorseTurboActive(HorseData? horse)
    {
        return horse != null && horse.sprintTimeLeft > 0f;
    }

    private static void KeepPlayerHorseTurboReady(string source)
    {
        if (!_lockHorseTurboStamina.Value)
        {
            return;
        }

        var horse = TryGetPlayerHorse();
        if (horse == null)
        {
            return;
        }

        var changed = false;
        var maxPower = TryReadFloatMember(horse, HorseMaxPowerMemberNames);
        if (maxPower.HasValue && maxPower.Value > 0f)
        {
            changed |= TrySetFloatMembers(horse, HorseCurrentPowerMemberNames, maxPower.Value);
            changed |= TrySetFloatMembers(TryGetPlayerHero(), new[] { "horsePower" }, maxPower.Value);
        }
        else
        {
            var currentPower = TryReadFloatMember(horse, HorseCurrentPowerMemberNames);
            if (currentPower.HasValue && currentPower.Value < 1f)
            {
                changed |= TrySetFloatMembers(horse, HorseCurrentPowerMemberNames, 1f);
                changed |= TrySetFloatMembers(TryGetPlayerHero(), new[] { "horsePower" }, 1f);
            }
        }

        changed |= TrySetBoolMembers(horse, new[] { "havePower" }, true);

        if (_traceMode.Value && changed && source != "Update")
        {
            LoggerInstance.LogInfo($"Horse turbo stamina refreshed from {source}.");
        }
    }

    private static int ResolveChangedAmount(int requestedDelta, int? moneyBefore, int? moneyAfter, bool isSpend)
    {
        var requestedAmount = Math.Abs(requestedDelta);
        if (moneyBefore.HasValue && moneyAfter.HasValue)
        {
            var actualAmount = isSpend
                ? Math.Max(0, moneyBefore.Value - moneyAfter.Value)
                : Math.Max(0, moneyAfter.Value - moneyBefore.Value);
            if (actualAmount > 0)
            {
                return actualAmount;
            }
        }

        return requestedAmount;
    }

    private static int? TryGetHeroMoney(HeroData? hero)
    {
        if (hero == null)
        {
            return null;
        }

        foreach (var memberName in new[] { "money", "Money", "nowMoney", "coin", "Coin", "gold", "Gold" })
        {
            var value = SafeProperty(hero, memberName) ?? SafeField(hero, memberName);
            var intValue = TryConvertToInt(value);
            if (intValue.HasValue)
            {
                return intValue.Value;
            }
        }

        return null;
    }

    private static int? TryGetItemListMoney(ItemListData? itemList)
    {
        if (itemList == null)
        {
            return null;
        }

        var value = SafeProperty(itemList, "money") ?? SafeField(itemList, "money") ?? SafeProperty(itemList, "Money") ?? SafeField(itemList, "Money");
        return TryConvertToInt(value);
    }

    private static float? TryGetItemListWeight(ItemListData? itemList)
    {
        if (itemList == null)
        {
            return null;
        }

        var value = SafeProperty(itemList, "weight") ?? SafeField(itemList, "weight") ?? SafeProperty(itemList, "Weight") ?? SafeField(itemList, "Weight");
        return TryConvertToFloat(value);
    }

    private static float? TryGetItemListMaxWeight(ItemListData? itemList)
    {
        if (itemList == null)
        {
            return null;
        }

        var value = SafeProperty(itemList, "maxWeight") ?? SafeField(itemList, "maxWeight") ?? SafeProperty(itemList, "MaxWeight") ?? SafeField(itemList, "MaxWeight");
        return TryConvertToFloat(value);
    }

    private static int? TryConvertToInt(object? value)
    {
        return value switch
        {
            null => null,
            int intValue => intValue,
            float floatValue => Mathf.RoundToInt(floatValue),
            double doubleValue => (int)Math.Round(doubleValue),
            long longValue when longValue <= int.MaxValue && longValue >= int.MinValue => (int)longValue,
            _ => null
        };
    }

    private static bool? TryConvertToBool(object? value)
    {
        return value switch
        {
            null => null,
            bool boolValue => boolValue,
            int intValue => intValue != 0,
            long longValue => longValue != 0,
            float floatValue => Math.Abs(floatValue) > 0.001f,
            double doubleValue => Math.Abs(doubleValue) > 0.001,
            _ => null
        };
    }

    private static float? TryReadFloatMember(object? target, IEnumerable<string> memberNames)
    {
        if (target == null)
        {
            return null;
        }

        foreach (var memberName in memberNames)
        {
            var value = SafeProperty(target, memberName) ?? SafeField(target, memberName);
            var floatValue = TryConvertToFloat(value);
            if (floatValue.HasValue)
            {
                return floatValue.Value;
            }
        }

        return null;
    }

    private static string? TryReadStringMember(object? target, IEnumerable<string> memberNames)
    {
        if (target == null)
        {
            return null;
        }

        foreach (var memberName in memberNames)
        {
            var value = SafeProperty(target, memberName) ?? SafeField(target, memberName);
            var stringValue = value?.ToString();
            if (!string.IsNullOrWhiteSpace(stringValue))
            {
                return stringValue;
            }
        }

        return null;
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
            _ => null
        };
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

    private static int? TryGetHeroId(HeroData? hero)
    {
        if (hero == null)
        {
            return null;
        }

        var value = SafeProperty(hero, "heroID") ?? SafeField(hero, "heroID") ?? SafeProperty(hero, "HeroID") ?? SafeField(hero, "HeroID");
        return TryConvertToInt(value);
    }

    private static int? TryGetHeroAreaId(HeroData? hero)
    {
        if (hero == null)
        {
            return null;
        }

        try
        {
            return hero.atAreaID;
        }
        catch
        {
            var value = SafeProperty(hero, "atAreaID") ?? SafeField(hero, "atAreaID");
            return TryConvertToInt(value);
        }
    }

    private static int ResolveHeroForceId(HeroData? hero)
    {
        if (hero == null)
        {
            return 0;
        }

        try
        {
            if (hero.belongForceID > 0)
            {
                return hero.belongForceID;
            }

            if (hero.servantForceID > 0)
            {
                return hero.servantForceID;
            }
        }
        catch
        {
        }

        var belongForce = TryConvertToInt(SafeProperty(hero, "belongForceID") ?? SafeField(hero, "belongForceID"));
        if (belongForce.GetValueOrDefault() > 0)
        {
            return belongForce.GetValueOrDefault();
        }

        var servantForce = TryConvertToInt(SafeProperty(hero, "servantForceID") ?? SafeField(hero, "servantForceID"));
        return Math.Max(0, servantForce.GetValueOrDefault());
    }

    private static string TryGetAreaName(HeroData? hero)
    {
        if (hero == null)
        {
            return "unknown";
        }

        try
        {
            var area = hero.GetArea();
            if (area != null && !string.IsNullOrWhiteSpace(area.areaName))
            {
                return area.areaName;
            }
        }
        catch
        {
        }

        try
        {
            var areaName = hero.AtAreaName();
            if (!string.IsNullOrWhiteSpace(areaName))
            {
                return areaName;
            }
        }
        catch
        {
        }

        return "unknown";
    }

    private static object? SafeGetMemberValue(object? target, string name)
    {
        if (target == null)
        {
            return null;
        }

        return SafeProperty(target, name) ?? SafeField(target, name);
    }

    private static object? SafeProperty(object target, string name)
    {
        try
        {
            var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            return property?.GetValue(target);
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
            return field?.GetValue(target);
        }
        catch
        {
            return null;
        }
    }

    private static bool TrySetFloatMembers(object? target, IEnumerable<string> memberNames, float value)
    {
        var changed = false;
        foreach (var memberName in memberNames)
        {
            changed |= TrySetMemberValue(target, memberName, value);
        }

        return changed;
    }

    private static bool TrySetBoolMembers(object? target, IEnumerable<string> memberNames, bool value)
    {
        var changed = false;
        foreach (var memberName in memberNames)
        {
            changed |= TrySetMemberValue(target, memberName, value);
        }

        return changed;
    }

    private static bool TrySetMemberValue(object? target, string name, object value)
    {
        if (target == null)
        {
            return false;
        }

        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        try
        {
            var property = target.GetType().GetProperty(name, Flags);
            if (property != null && property.CanWrite && TryConvertMemberValue(property.PropertyType, value, out var convertedPropertyValue))
            {
                property.SetValue(target, convertedPropertyValue);
                return true;
            }
        }
        catch
        {
        }

        try
        {
            var field = target.GetType().GetField(name, Flags);
            if (field != null && TryConvertMemberValue(field.FieldType, value, out var convertedFieldValue))
            {
                field.SetValue(target, convertedFieldValue);
                return true;
            }
        }
        catch
        {
        }

        return false;
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

    private static void ApplyMerchantCarryCash(TradeUIType targetType, ItemListData? merchantItemList, string source)
    {
        var targetCash = Math.Max(0, _merchantCarryCash.Value);
        if (targetCash <= 0 || targetType != TradeUIType.Shop || merchantItemList == null)
        {
            return;
        }

        var currentCash = TryGetItemListMoney(merchantItemList) ?? 0;
        if (currentCash >= targetCash)
        {
            return;
        }

        if (!TrySetMemberValue(merchantItemList, "money", targetCash))
        {
            if (_traceMode.Value)
            {
                LoggerInstance.LogWarning($"Merchant cash floor could not be applied from {source}.");
            }

            return;
        }

        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo($"Merchant cash floor applied from {currentCash} to {targetCash} via {source}.");
        }
    }

    private static void ApplyPlayerCarryWeightOverride(string source)
    {
        var playerInventory = TryGetPlayerHero()?.itemListData;
        if (playerInventory == null)
        {
            return;
        }

        var changed = false;
        var carryWeightCap = Math.Max(0f, _carryWeightCap.Value);
        var currentMaxWeight = TryGetItemListMaxWeight(playerInventory) ?? 0f;
        if (carryWeightCap > 0f && currentMaxWeight < carryWeightCap)
        {
            changed |= TrySetMemberValue(playerInventory, "maxWeight", carryWeightCap);
        }

        if (_ignoreCarryWeight.Value)
        {
            var currentWeight = TryGetItemListWeight(playerInventory) ?? 0f;
            if (Math.Abs(currentWeight) > 0.001f)
            {
                changed |= TrySetMemberValue(playerInventory, "weight", 0f);
            }
        }

        if (changed && _traceMode.Value && source != "Update")
        {
            LoggerInstance.LogInfo(
                $"Player carry weight override applied from {source}: weight={SafeFormatValue(TryGetItemListWeight(playerInventory))}, max={SafeFormatValue(TryGetItemListMaxWeight(playerInventory))}.");
        }
    }

    private static bool TryConvertMemberValue(Type targetType, object value, out object? convertedValue)
    {
        convertedValue = null;

        if (targetType == typeof(float))
        {
            var floatValue = TryConvertToFloat(value);
            if (floatValue.HasValue)
            {
                convertedValue = floatValue.Value;
                return true;
            }
        }
        else if (targetType == typeof(double))
        {
            var floatValue = TryConvertToFloat(value);
            if (floatValue.HasValue)
            {
                convertedValue = (double)floatValue.Value;
                return true;
            }
        }
        else if (targetType == typeof(int))
        {
            var intValue = TryConvertToInt(value);
            if (intValue.HasValue)
            {
                convertedValue = intValue.Value;
                return true;
            }
        }
        else if (targetType == typeof(long))
        {
            var intValue = TryConvertToInt(value);
            if (intValue.HasValue)
            {
                convertedValue = (long)intValue.Value;
                return true;
            }
        }
        else if (targetType == typeof(bool) && value is bool boolValue)
        {
            convertedValue = boolValue;
            return true;
        }

        return false;
    }

    private static void EnsureDailySkillInsightBaseline()
    {
        if (_dailySkillInsightBaselineReady)
        {
            return;
        }

        var currentDate = TryGetWorldDateSnapshot();
        if (currentDate == null)
        {
            return;
        }

        _lastObservedWorldDate = currentDate;
        _dailySkillInsightBaselineReady = true;

        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo($"Daily skill insight baseline initialized at {FormatDate(currentDate)}.");
        }
    }

    private static void HandleDailySkillInsightDateProgress(TimeData? beforeDate, TimeData? afterDate, string source)
    {
        if (afterDate == null)
        {
            return;
        }

        if (!_dailySkillInsightBaselineReady || _lastObservedWorldDate == null)
        {
            _lastObservedWorldDate = afterDate;
            _dailySkillInsightBaselineReady = true;
            return;
        }

        if (beforeDate != null && !AreDatesEqual(beforeDate, _lastObservedWorldDate) && !AreDatesEqual(afterDate, _lastObservedWorldDate))
        {
            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo(
                    $"Daily skill insight baseline resynced from {FormatDate(_lastObservedWorldDate)} to {FormatDate(afterDate)} because {source} started from {FormatDate(beforeDate)}.");
            }

            _lastObservedWorldDate = afterDate;
            return;
        }

        var elapsedDays = GetElapsedDayCount(_lastObservedWorldDate, afterDate);
        _lastObservedWorldDate = afterDate;
        if (elapsedDays <= 0)
        {
            return;
        }

        for (var i = 0; i < elapsedDays; i++)
        {
            TryRollDailySkillInsight(i + 1, elapsedDays, source);
        }
    }

    private static void TryRollDailySkillInsight(int dayIndex, int totalDays, string source)
    {
        var hitChancePercent = ClampPercent(_dailySkillInsightHitChancePercent.Value);
        var expPercent = Math.Max(0f, _dailySkillInsightExpPercent.Value);
        if (hitChancePercent <= 0 || expPercent <= 0f)
        {
            return;
        }

        var player = TryGetPlayerHero();
        if (player == null)
        {
            return;
        }

        var eligibleSkills = GetDailySkillInsightCandidates(player);
        if (eligibleSkills.Count == 0)
        {
            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo($"Daily skill insight skipped on day {dayIndex}/{totalDays} from {source}: no eligible skills.");
            }

            return;
        }

        var roll = Random.Next(1, 101);
        if (roll > hitChancePercent)
        {
            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo($"Daily skill insight miss on day {dayIndex}/{totalDays} from {source}: roll {roll} > {hitChancePercent}.");
            }

            return;
        }

        if (!TryAwardDailySkillInsightExp(player, eligibleSkills, out var skillName, out var awardedExp, out var usedSkill))
        {
            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo($"Daily skill insight hit on day {dayIndex}/{totalDays} from {source}, but no candidate accepted EXP.");
            }

            return;
        }

        var skillTierText = GetSkillTierText(usedSkill);
        PushPlayerLog($"【心得涌现】：【{skillTierText}{skillName}】获得 {FormatInsightExp(awardedExp)} 经验值");
        LoggerInstance.LogInfo(
            $"Daily skill insight applied on day {dayIndex}/{totalDays} from {source}: skill={skillName}, exp={SafeFormatValue(awardedExp)}, chance={hitChancePercent}, roll={roll}.");
    }

    private static void TryRunRealtimeSkillInsight()
    {
        var intervalSeconds = Math.Max(0f, _dailySkillInsightRealtimeIntervalSeconds.Value);
        if (intervalSeconds <= 0f)
        {
            _nextRealtimeDailySkillInsightAt = -1f;
            return;
        }

        var now = Time.realtimeSinceStartup;
        if (_nextRealtimeDailySkillInsightAt < 0f)
        {
            _nextRealtimeDailySkillInsightAt = now + intervalSeconds;
            return;
        }

        if (now < _nextRealtimeDailySkillInsightAt)
        {
            return;
        }

        _nextRealtimeDailySkillInsightAt = now + intervalSeconds;
        TryTriggerRealtimeSkillInsight(intervalSeconds);
    }

    private static void TryTriggerRealtimeSkillInsight(float intervalSeconds)
    {
        var expPercent = Math.Max(0f, _dailySkillInsightExpPercent.Value);
        if (expPercent <= 0f)
        {
            return;
        }

        var player = TryGetPlayerHero();
        if (player == null)
        {
            return;
        }

        FillDailySkillInsightCandidates(player, _dailySkillInsightCandidateBuffer);
        if (_dailySkillInsightCandidateBuffer.Count == 0)
        {
            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo($"Realtime skill insight skipped: no eligible skills at interval {intervalSeconds:0.###} seconds.");
            }

            return;
        }

        if (!TryAwardDailySkillInsightExp(player, _dailySkillInsightCandidateBuffer, out var skillName, out var awardedExp, out var usedSkill))
        {
            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo($"Realtime skill insight interval {intervalSeconds:0.###} seconds fired, but no candidate accepted EXP.");
            }

            return;
        }

        var skillTierText = GetSkillTierText(usedSkill);
        PushPlayerLog($"【心得涌现】：【{skillTierText}{skillName}】获得 {FormatInsightExp(awardedExp)} 经验值");
        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo($"Realtime skill insight applied: interval={intervalSeconds:0.###}s, skill={skillName}, exp={SafeFormatValue(awardedExp)}.");
        }
    }

    private static List<KungfuSkillLvData> GetDailySkillInsightCandidates(HeroData player)
    {
        var candidates = new List<KungfuSkillLvData>();
        FillDailySkillInsightCandidates(player, candidates);
        return candidates;
    }

    private static void FillDailySkillInsightCandidates(HeroData player, List<KungfuSkillLvData> candidates)
    {
        candidates.Clear();

        try
        {
            var skills = player.kungfuSkills;
            if (skills == null)
            {
                return;
            }

            for (var i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill == null || skill.lv >= DailySkillInsightMaxLevel)
                {
                    continue;
                }

                if (!CanGainBookExp(skill))
                {
                    continue;
                }

                candidates.Add(skill);
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Failed to collect daily skill insight candidates: {ex.Message}");
        }
    }

    private static bool CanGainBookExp(KungfuSkillLvData skill)
    {
        try
        {
            return !skill.BookExpFull();
        }
        catch
        {
            return true;
        }
    }

    private static bool TryAwardDailySkillInsightExp(HeroData player, List<KungfuSkillLvData> candidates, out string skillName, out float awardedExp, out KungfuSkillLvData? usedSkill)
    {
        skillName = string.Empty;
        awardedExp = 0f;
        usedSkill = null;
        if (candidates.Count == 0)
        {
            return false;
        }

        var startIndex = candidates.Count == 1 ? 0 : Random.Next(candidates.Count);
        for (var offset = 0; offset < candidates.Count; offset++)
        {
            var skill = candidates[(startIndex + offset) % candidates.Count];
            var plannedExp = ResolveDailySkillInsightExp(player, skill);
            if (plannedExp <= 0f)
            {
                continue;
            }

            var beforeLevel = skill.lv;
            var beforeBookExp = skill.bookExp;

            try
            {
                _applyingDailySkillInsightExp = true;
                player.AddSkillBookExp(plannedExp, skill, false);
            }
            catch (Exception ex)
            {
                LoggerInstance.LogWarning($"Daily skill insight failed for skill {TryGetSkillName(skill)}: {ex.Message}");
                continue;
            }
            finally
            {
                _applyingDailySkillInsightExp = false;
            }

            if (skill.lv != beforeLevel || Math.Abs(skill.bookExp - beforeBookExp) > 0.001f)
            {
                skillName = TryGetSkillName(skill);
                awardedExp = plannedExp;
                usedSkill = skill;
                return true;
            }
        }

        return false;
    }

    private static float ResolveDailySkillInsightExp(HeroData player, KungfuSkillLvData skill)
    {
        var expPercent = Math.Max(0f, _dailySkillInsightExpPercent.Value);
        if (expPercent <= 0f)
        {
            return 0f;
        }

        float maxExp;
        try
        {
            maxExp = skill.SkillGetMaxExp(Math.Max(1, skill.lv));
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Could not read max EXP for skill {TryGetSkillName(skill)}: {ex.Message}");
            return 0f;
        }

        if (maxExp <= 0f)
        {
            return 0f;
        }

        var rarityMultiplier = 1f;
        if (_dailySkillInsightUseRarityScaling.Value)
        {
            rarityMultiplier = ResolveSkillRarityExpRate(player, skill);
        }

        var result = maxExp * (expPercent / 100f) * rarityMultiplier;
        return Mathf.Max(1f, result);
    }

    private static float ResolveSkillRarityExpRate(HeroData player, KungfuSkillLvData skill)
    {
        try
        {
            var skillData = skill.DataBase();
            if (skillData == null)
            {
                return 1f;
            }

            var rate = player.GetSkillRareLvExpRate(skillData.rareLv);
            return rate > 0f ? rate : 1f;
        }
        catch
        {
            return 1f;
        }
    }

    private static string GetSkillTierText(KungfuSkillLvData? skill)
    {
        if (skill == null)
        {
            return string.Empty;
        }

        try
        {
            var skillData = skill.DataBase();
            if (skillData == null)
            {
                return string.Empty;
            }

            return $"【{skillData.rareLv}阶】";
        }
        catch
        {
            return string.Empty;
        }
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

        return $"技能{skill.skillID}";
    }

    private static TimeData? TryGetWorldDateSnapshot()
    {
        try
        {
            var worldTime = GameController.Instance?.worldData?.worldTime;
            if (worldTime == null)
            {
                return null;
            }

            return new TimeData(worldTime.year, worldTime.month, worldTime.day);
        }
        catch
        {
            return null;
        }
    }

    private static int GetElapsedDayCount(TimeData fromDate, TimeData toDate)
    {
        if (CompareDates(toDate, fromDate) <= 0)
        {
            return 0;
        }

        try
        {
            var delta = toDate.DeltaDay(fromDate);
            if (delta > 0)
            {
                return delta;
            }
        }
        catch
        {
        }

        return ApproximateElapsedDayCount(fromDate, toDate);
    }

    private static int ApproximateElapsedDayCount(TimeData fromDate, TimeData toDate)
    {
        var fromSerial = (fromDate.year * 372) + (fromDate.month * 31) + fromDate.day;
        var toSerial = (toDate.year * 372) + (toDate.month * 31) + toDate.day;
        return Math.Max(0, toSerial - fromSerial);
    }

    private static int CompareDates(TimeData left, TimeData right)
    {
        var yearCompare = left.year.CompareTo(right.year);
        if (yearCompare != 0)
        {
            return yearCompare;
        }

        var monthCompare = left.month.CompareTo(right.month);
        if (monthCompare != 0)
        {
            return monthCompare;
        }

        return left.day.CompareTo(right.day);
    }

    private static bool AreDatesEqual(TimeData? left, TimeData? right)
    {
        return left != null && right != null
            && left.year == right.year
            && left.month == right.month
            && left.day == right.day;
    }

    private static bool IsHealingStateTile(ExploreTileData? tile)
    {
        if (tile == null)
        {
            return false;
        }

        try
        {
            return tile.exploreTileEventType == 7;
        }
        catch
        {
            var eventTypeValue = TryConvertToInt(SafeProperty(tile, "exploreTileEventType") ?? SafeField(tile, "exploreTileEventType"));
            return eventTypeValue == 7;
        }
    }

    private static void TryRevealAllExploreFogAfterFirstMove(ExploreController? controller)
    {
        if (!_revealAllOnStepTile.Value || _exploreFullRevealConsumed)
        {
            return;
        }

        if (!TryRevealAllExploreFog(controller))
        {
            return;
        }

        _exploreFullRevealConsumed = true;
        LoggerInstance.LogInfo("Exploration fog fully revealed after first completed move.");
    }

    private static bool TryRevealAllExploreFog(ExploreController? controller)
    {
        if (controller == null)
        {
            return false;
        }

        try
        {
            controller.SeeAllTile();
            return true;
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Failed to reveal full exploration fog after Step(1): {ex.Message}");
            return false;
        }
    }

    private static ItemData? TryCreateTreasureChestBonusItem(ItemData sourceItem, HeroData targetHero)
    {
        try
        {
            var gameController = GameController.Instance;
            if (gameController != null)
            {
                var generated = gameController.GenerateRandomItemValue(
                    Math.Max(1f, sourceItem.value),
                    Math.Max(1f, sourceItem.itemLv),
                    targetHero);

                if (generated != null)
                {
                    return generated;
                }
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Failed to reroll bonus treasure chest item: {ex.Message}");
        }

        try
        {
            return sourceItem.Clone() as ItemData;
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Failed to clone treasure chest item: {ex.Message}");
            return null;
        }
    }

    private static void ResetExploreFullReveal(string source)
    {
        if (_exploreFullRevealConsumed)
        {
            LoggerInstance.LogInfo($"Exploration full-reveal state reset from {source}.");
        }

        _exploreFullRevealConsumed = false;
    }

    private static bool TryClearHeroInjuryValue(HeroData hero, float currentValue, Func<HeroData, float, float> applyChange, string memberName)
    {
        if (currentValue <= 0.001f)
        {
            return false;
        }

        try
        {
            applyChange(hero, currentValue);
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Failed to clear {memberName} via change call: {ex.Message}");
        }

        return TrySetFloatMembers(hero, new[] { memberName, UppercaseFirst(memberName) }, 0f) || currentValue > 0.001f;
    }

    private static string UppercaseFirst(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (value.Length == 1)
        {
            return value.ToUpperInvariant();
        }

        return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }

    private static string DescribeExploreTile(ExploreTileData? tile)
    {
        if (tile == null)
        {
            return "null";
        }

        var column = TryConvertToInt(SafeProperty(tile, "column") ?? SafeField(tile, "column"));
        var row = TryConvertToInt(SafeProperty(tile, "row") ?? SafeField(tile, "row"));
        var eventTypeValue = TryConvertToInt(SafeProperty(tile, "exploreTileEventType") ?? SafeField(tile, "exploreTileEventType"));
        var eventHandled = TryConvertToBool(SafeProperty(tile, "eventHappen") ?? SafeField(tile, "eventHappen"));
        return $"pos=({column?.ToString() ?? "?"},{row?.ToString() ?? "?"}), event={eventTypeValue?.ToString() ?? "?"}, eventHappen={eventHandled?.ToString() ?? "?"}";
    }

    private static string FormatDate(TimeData? date)
    {
        return date == null ? "Date: unavailable" : $"Y{date.year} M{date.month} D{date.day}";
    }

    private static string FormatInsightExp(float value)
    {
        return value >= 10f || Math.Abs(value - Mathf.Round(value)) < 0.001f
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.###");
    }
}
