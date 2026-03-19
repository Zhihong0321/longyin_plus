using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("codex.longyin.questsnapshot", "LongYin Quest Snapshot", "1.0.0")]
public sealed class LongYinQuestSnapshotPlugin : BasePlugin
{
    internal static ManualLogSource LoggerInstance = null!;

    private static ConfigEntry<bool> _enabled = null!;
    private static ConfigEntry<float> _refreshIntervalSeconds = null!;
    private static ConfigEntry<bool> _traceMode = null!;

    private static string _snapshotPath = string.Empty;
    private static bool _snapshotDirty = true;
    private static float _nextPeriodicSnapshotAt = -1f;
    private static float _nextAllowedSnapshotAt = -1f;

    private Harmony? _harmony;

    private sealed class SnapshotData
    {
        public bool Available { get; set; }
        public string Status { get; set; } = string.Empty;
        public string WorldDate { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public SnapshotCounts Counts { get; } = new();
        public List<SnapshotEntry> Entries { get; } = new();
    }

    private sealed class SnapshotCounts
    {
        public int Missions { get; set; }
        public int ForceMissions { get; set; }
        public int WorldEvents { get; set; }
        public int AreaEvents { get; set; }
        public int BigMapEvents { get; set; }
        public int PlotEvents { get; set; }
        public int ActiveScene { get; set; }

        public int Total =>
            Missions +
            ForceMissions +
            WorldEvents +
            AreaEvents +
            BigMapEvents +
            PlotEvents +
            ActiveScene;
    }

    private sealed class SnapshotEntry
    {
        public string Bucket { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string LeftTime { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public override void Load()
    {
        LoggerInstance = Log;
        _enabled = Config.Bind("General", "Enabled", true, "Exports a quest and event snapshot for the external overlay.");
        _refreshIntervalSeconds = Config.Bind("General", "RefreshIntervalSeconds", 2f, "How often to refresh the quest snapshot during normal play.");
        _traceMode = Config.Bind("General", "TraceMode", false, "Logs quest snapshot refresh decisions.");

        _snapshotPath = Path.Combine(AppContext.BaseDirectory, "mod-prototype", "LongYinModControl", "LongYinQuestSnapshot.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_snapshotPath) ?? AppContext.BaseDirectory);

        if (!_enabled.Value)
        {
            WriteDisabledSnapshot();
            Log.LogInfo("LongYin Quest Snapshot loaded with exporting disabled.");
            return;
        }

        _harmony = new Harmony("codex.longyin.questsnapshot");
        PatchMethod(typeof(GameController), "Update", Type.EmptyTypes, null, nameof(GameControllerUpdatePostfix));
        PatchMethod(typeof(MissionUIController), nameof(MissionUIController.RefreshMissionTable), Type.EmptyTypes, null, nameof(MarkDirtyPostfix));
        PatchMethod(typeof(MissionUIController), nameof(MissionUIController.RefreshWorldEventTable), Type.EmptyTypes, null, nameof(MarkDirtyPostfix));
        PatchMethod(typeof(MissionUIController), nameof(MissionUIController.RefreshForceMission), Type.EmptyTypes, null, nameof(MarkDirtyPostfix));
        PatchMethod(typeof(MissionUIController), nameof(MissionUIController.ShowMissionUI), new[] { typeof(bool) }, null, nameof(ShowMissionUiPostfix));
        PatchDeclaredByName(typeof(GameController), nameof(MarkDirtyPostfix),
            "CreateMissionEvent",
            "FinishMission",
            "GiveUpMission",
            "GiveUpForceMission",
            "CreateAreaMapRandomEvent",
            "CreateBigMapRandomEvent",
            "RemoveAreaMapRandomEvent",
            "RemoveBigMapRandomEvent",
            "RemoveWorldEvent");
        PatchDeclaredByName(typeof(WorldPlotEventController), nameof(MarkDirtyPostfix),
            "CheckWorldPlotEventDataBase",
            "StartNewWorldPlotEvent",
            "StartNewWorldPlotEventFromDataBase",
            "RemoveWorldPlotEvent");
        PatchDeclaredByName(typeof(HeroData), nameof(MarkDirtyPostfix), "ChangeHeroMissionResult");
        PatchDeclaredByName(typeof(PlotController), nameof(MarkDirtyPostfix),
            "AcceptGuardMission",
            "AcceptGuardNpcMission",
            "AcceptLittleMission",
            "AskNPCMission",
            "AskNPCMissionResult",
            "FinishEventForceMission",
            "FinishEventMission",
            "FinishForceMission",
            "GiveUpEventForceMission",
            "GiveUpEventMission",
            "GiveUpForceMission",
            "WorldPlotEventStart");

        _snapshotDirty = true;
        _nextPeriodicSnapshotAt = 0f;
        _nextAllowedSnapshotAt = 0f;
        TryWriteSnapshot("Load");

        Log.LogInfo($"LongYin Quest Snapshot loaded. Export path: {_snapshotPath}");
    }

    private void PatchDeclaredByName(Type type, string? postfixName, params string[] methodNames)
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            foreach (var methodName in methodNames)
            {
                if (method.Name == methodName)
                {
                    PatchMethod(type, method.Name, GetParameterTypes(method), null, postfixName);
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
        var prefix = prefixName == null ? null : AccessTools.Method(typeof(LongYinQuestSnapshotPlugin), prefixName);
        var postfix = postfixName == null ? null : AccessTools.Method(typeof(LongYinQuestSnapshotPlugin), postfixName);

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

    private static void GameControllerUpdatePostfix()
    {
        if (!_enabled.Value)
        {
            return;
        }

        var now = Time.realtimeSinceStartup;
        if (now < _nextAllowedSnapshotAt)
        {
            return;
        }

        var periodicDue = now >= _nextPeriodicSnapshotAt;
        if (!_snapshotDirty && !periodicDue)
        {
            return;
        }

        TryWriteSnapshot(_snapshotDirty ? "DirtyUpdate" : "PeriodicUpdate");
    }

    private static void ShowMissionUiPostfix(bool showState)
    {
        if (showState)
        {
            MarkDirty("MissionUIController.ShowMissionUI");
        }
    }

    private static void MarkDirtyPostfix(MethodBase __originalMethod)
    {
        MarkDirty(DescribeMethod(__originalMethod));
    }

    private static void MarkDirty(string source)
    {
        if (!_enabled.Value)
        {
            return;
        }

        _snapshotDirty = true;
        if (_traceMode.Value)
        {
            LoggerInstance.LogInfo($"Quest snapshot marked dirty by {source}.");
        }
    }

    private static void TryWriteSnapshot(string source)
    {
        try
        {
            var snapshot = BuildSnapshot();
            WriteSnapshot(snapshot);

            var now = Time.realtimeSinceStartup;
            _snapshotDirty = false;
            _nextAllowedSnapshotAt = now + 0.35f;
            _nextPeriodicSnapshotAt = now + Math.Max(0.5f, _refreshIntervalSeconds.Value);

            if (_traceMode.Value)
            {
                LoggerInstance.LogInfo(
                    $"Quest snapshot exported from {source}: total={snapshot.Counts.Total}, missions={snapshot.Counts.Missions}, force={snapshot.Counts.ForceMissions}, " +
                    $"world={snapshot.Counts.WorldEvents}, area={snapshot.Counts.AreaEvents}, bigMap={snapshot.Counts.BigMapEvents}, plot={snapshot.Counts.PlotEvents}, active={snapshot.Counts.ActiveScene}.");
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.LogWarning($"Quest snapshot export failed from {source}: {ex.Message}");
            _nextAllowedSnapshotAt = Time.realtimeSinceStartup + 1f;
        }
    }

    private static SnapshotData BuildSnapshot()
    {
        var snapshot = new SnapshotData();
        var world = TryGetWorldData();
        var player = TryGetPlayerHero();
        if (world == null || player == null)
        {
            snapshot.Available = false;
            snapshot.Status = "No active save detected yet.";
            return snapshot;
        }

        snapshot.Available = true;
        snapshot.Status = "OK";
        snapshot.WorldDate = FormatWorldDate(world);
        snapshot.PlayerName = TryGetHeroName(player);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        AddMissionEntries(snapshot, seen, player.missions, "missions", "Mission", "Player");

        try
        {
            if (player.forceMission != null)
            {
                AddMissionEntry(snapshot, seen, player.forceMission, "forceMissions", "Force Mission", "Force");
            }
        }
        catch
        {
        }

        AddEventEntries(snapshot, seen, world.WorldEventDatas, "worldEvents", "World Event", "World");
        AddEventEntries(snapshot, seen, world.AreaMapRandomEventDatas, "areaEvents", "Area Event", "Area");
        AddEventEntries(snapshot, seen, world.BigMapRandomEventDatas, "bigMapEvents", "Big Map Event", "Travel");
        AddWorldPlotEntries(snapshot, seen, world.worldPlotEventStartData);
        AddActiveSceneEntries(snapshot, seen, TryGetPlotController());

        return snapshot;
    }

    private static void AddMissionEntries(SnapshotData snapshot, HashSet<string> seen, object? missions, string bucket, string category, string source)
    {
        foreach (var item in EnumerateValues(missions))
        {
            if (item is MissionData mission)
            {
                AddMissionEntry(snapshot, seen, mission, bucket, category, source);
            }
        }
    }

    private static void AddMissionEntry(SnapshotData snapshot, HashSet<string> seen, MissionData mission, string bucket, string category, string source)
    {
        var detail = NormalizeText(TryDescribeMission(mission));
        if (string.IsNullOrWhiteSpace(detail))
        {
            detail = NormalizeText(TryCallString(() => mission.GetMissionTargetDescribe(true)));
        }

        AddEntry(snapshot, seen, new SnapshotEntry
        {
            Bucket = bucket,
            Category = category,
            Name = NormalizeText(mission.name),
            Detail = detail,
            LeftTime = FormatLeftTime(TryConvertToInt(mission.leftTime)),
            Location = NormalizeText(ResolveMissionLocation(mission)),
            Source = NormalizeText($"{source} | {mission.missionSourceType}"),
            Status = NormalizeText(BuildMissionStatus(mission))
        });
    }

    private static void AddEventEntries(SnapshotData snapshot, HashSet<string> seen, object? events, string bucket, string category, string source)
    {
        foreach (var item in EnumerateValues(events))
        {
            if (item is EventData eventData)
            {
                AddEntry(snapshot, seen, new SnapshotEntry
                {
                    Bucket = bucket,
                    Category = category,
                    Name = NormalizeText(TryCallString(eventData.Name) ?? eventData.eventName),
                    Detail = NormalizeText(TryCallString(() => eventData.GetDescribe(true)) ?? eventData.eventDescribe),
                    LeftTime = FormatLeftTime(TryConvertToInt(eventData.leftTime)),
                    Location = NormalizeText(ResolveEventLocation(eventData)),
                    Source = NormalizeText(source),
                    Status = NormalizeText(BuildEventStatus(eventData))
                });
            }
        }
    }

    private static void AddWorldPlotEntries(SnapshotData snapshot, HashSet<string> seen, object? worldPlotEvents)
    {
        foreach (var item in EnumerateValues(worldPlotEvents))
        {
            if (item is not WorldPlotEventStartData plotEvent)
            {
                continue;
            }

            var targetEventDetail = plotEvent.targetEvent == null
                ? string.Empty
                : NormalizeText(TryCallString(() => plotEvent.targetEvent.GetDescribe(true)) ?? plotEvent.targetEvent.eventDescribe);
            var detail = NormalizeText(
                $"{targetEventDetail} Trigger: {plotEvent.triggerType} {NormalizeText(plotEvent.triggerTargetID)} PlotID: {plotEvent.plotID}");

            AddEntry(snapshot, seen, new SnapshotEntry
            {
                Bucket = "plotEvents",
                Category = "Plot Event",
                Name = NormalizeText(plotEvent.name),
                Detail = detail,
                LeftTime = FormatLeftTime(TryConvertToInt(plotEvent.startLeftDay)),
                Location = NormalizeText(plotEvent.targetEvent == null ? plotEvent.triggerTargetID : ResolveEventLocation(plotEvent.targetEvent)),
                Source = "World Plot",
                Status = NormalizeText(plotEvent.noAutoDestroy ? "Persistent" : "Pending")
            });
        }
    }

    private static void AddActiveSceneEntries(SnapshotData snapshot, HashSet<string> seen, PlotController? plotController)
    {
        if (plotController == null)
        {
            return;
        }

        try
        {
            if (plotController.nowMission != null)
            {
                AddMissionEntry(snapshot, seen, plotController.nowMission, "activeScene", "Active Scene", "Plot");
            }
        }
        catch
        {
        }

        try
        {
            if (plotController.nowEvent != null)
            {
                AddEntry(snapshot, seen, new SnapshotEntry
                {
                    Bucket = "activeScene",
                    Category = "Active Scene",
                    Name = NormalizeText(TryCallString(plotController.nowEvent.Name) ?? plotController.nowEvent.eventName),
                    Detail = NormalizeText(TryCallString(() => plotController.nowEvent.GetDescribe(true)) ?? plotController.nowEvent.eventDescribe),
                    LeftTime = FormatLeftTime(TryConvertToInt(plotController.nowEvent.leftTime)),
                    Location = NormalizeText(ResolveEventLocation(plotController.nowEvent)),
                    Source = "Plot Event",
                    Status = "Active"
                });
            }
        }
        catch
        {
        }
    }

    private static void AddEntry(SnapshotData snapshot, HashSet<string> seen, SnapshotEntry entry)
    {
        entry.Name = FallbackText(entry.Name, "(unnamed)");
        entry.Detail = FallbackText(entry.Detail, "(no details)");
        entry.Location = FallbackText(entry.Location, "(location unknown)");
        entry.Source = FallbackText(entry.Source, "(source unknown)");
        entry.Status = FallbackText(entry.Status, "(status unknown)");

        var key = $"{entry.Bucket}|{entry.Name}|{entry.Location}|{entry.Status}|{entry.LeftTime}";
        if (!seen.Add(key))
        {
            return;
        }

        snapshot.Entries.Add(entry);
        switch (entry.Bucket)
        {
            case "missions":
                snapshot.Counts.Missions++;
                break;
            case "forceMissions":
                snapshot.Counts.ForceMissions++;
                break;
            case "worldEvents":
                snapshot.Counts.WorldEvents++;
                break;
            case "areaEvents":
                snapshot.Counts.AreaEvents++;
                break;
            case "bigMapEvents":
                snapshot.Counts.BigMapEvents++;
                break;
            case "plotEvents":
                snapshot.Counts.PlotEvents++;
                break;
            case "activeScene":
                snapshot.Counts.ActiveScene++;
                break;
        }
    }

    private static string TryDescribeMission(MissionData mission)
    {
        return TryCallString(mission.GetMissionDescribe)
               ?? TryCallString(() => mission.GetMissionDescribe(true, true, true, true))
               ?? string.Empty;
    }

    private static string BuildMissionStatus(MissionData mission)
    {
        var parts = new List<string>
        {
            $"Difficulty {SafeFormatValue(mission.difficulty)}",
            mission.noAutoFinish ? "Manual finish" : "Auto finish"
        };

        if (mission.missionHideTargetPlace)
        {
            parts.Add("Hidden location");
        }

        if (mission.missionDisableQuickTravel)
        {
            parts.Add("No quick travel");
        }

        return string.Join(" | ", parts);
    }

    private static string BuildEventStatus(EventData eventData)
    {
        var parts = new List<string>();
        if (eventData.missionTargetEvent)
        {
            parts.Add("Mission target");
        }

        if (eventData.plotTargetEvent)
        {
            parts.Add("Plot target");
        }

        if (eventData.happened)
        {
            parts.Add("Triggered");
        }
        else if (eventData.seen)
        {
            parts.Add("Seen");
        }
        else if (eventData.noticed)
        {
            parts.Add("Noticed");
        }
        else
        {
            parts.Add("Unseen");
        }

        return string.Join(" | ", parts);
    }

    private static string ResolveMissionLocation(MissionData mission)
    {
        var names = new List<string>();

        try
        {
            foreach (var targetArea in EnumerateValues(mission.GetTargetAreaID()))
            {
                var areaId = TryConvertToInt(targetArea);
                if (!areaId.HasValue)
                {
                    continue;
                }

                var areaName = ResolveAreaName(areaId.Value);
                if (!string.IsNullOrWhiteSpace(areaName))
                {
                    names.Add(areaName);
                }
            }
        }
        catch
        {
        }

        if (names.Count > 0)
        {
            return string.Join(", ", names);
        }

        return TryCallString(() => mission.GetMissionTargetDescribe(true))
               ?? mission.missionHideTargetPlaceString
               ?? string.Empty;
    }

    private static string ResolveEventLocation(EventData eventData)
    {
        var direct = NormalizeText(TryCallString(eventData.GetPosText));
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        var areaNames = new List<string>();
        foreach (var areaIdValue in EnumerateValues(eventData.areaID))
        {
            var areaId = TryConvertToInt(areaIdValue);
            if (!areaId.HasValue)
            {
                continue;
            }

            var areaName = ResolveAreaName(areaId.Value);
            if (!string.IsNullOrWhiteSpace(areaName))
            {
                areaNames.Add(areaName);
            }
        }

        if (areaNames.Count > 0)
        {
            return string.Join(", ", areaNames);
        }

        var nearAreaId = TryConvertToInt(eventData.nearAreaID);
        if (nearAreaId.HasValue)
        {
            var areaName = ResolveAreaName(nearAreaId.Value);
            if (!string.IsNullOrWhiteSpace(areaName))
            {
                return areaName;
            }
        }

        if (eventData.resourcePointID > 0)
        {
            return $"Resource point {eventData.resourcePointID}";
        }

        return string.Empty;
    }

    private static string ResolveAreaName(int areaId)
    {
        var world = TryGetWorldData();
        if (world == null)
        {
            return string.Empty;
        }

        try
        {
            var area = world.GetArea(areaId.ToString());
            if (area != null)
            {
                var name = area.GetAreaName();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
        }
        catch
        {
        }

        return $"Area {areaId}";
    }

    private static void WriteSnapshot(SnapshotData snapshot)
    {
        var json = SerializeSnapshot(snapshot);
        var tempPath = _snapshotPath + ".tmp";
        File.WriteAllText(tempPath, json, Encoding.UTF8);

        if (File.Exists(_snapshotPath))
        {
            File.Copy(tempPath, _snapshotPath, overwrite: true);
            File.Delete(tempPath);
            return;
        }

        File.Move(tempPath, _snapshotPath);
    }

    private static void WriteDisabledSnapshot()
    {
        var snapshot = new SnapshotData
        {
            Available = false,
            Status = "Quest snapshot plugin disabled."
        };

        WriteSnapshot(snapshot);
    }

    private static string SerializeSnapshot(SnapshotData snapshot)
    {
        var sb = new StringBuilder(8192);
        sb.AppendLine("{");
        AppendBooleanProperty(sb, 1, "available", snapshot.Available, true);
        AppendStringProperty(sb, 1, "status", snapshot.Status, true);
        AppendStringProperty(sb, 1, "worldDate", snapshot.WorldDate, true);
        AppendStringProperty(sb, 1, "playerName", snapshot.PlayerName, true);

        AppendIndent(sb, 1);
        sb.AppendLine("\"counts\": {");
        AppendIntProperty(sb, 2, "missions", snapshot.Counts.Missions, true);
        AppendIntProperty(sb, 2, "forceMissions", snapshot.Counts.ForceMissions, true);
        AppendIntProperty(sb, 2, "worldEvents", snapshot.Counts.WorldEvents, true);
        AppendIntProperty(sb, 2, "areaEvents", snapshot.Counts.AreaEvents, true);
        AppendIntProperty(sb, 2, "bigMapEvents", snapshot.Counts.BigMapEvents, true);
        AppendIntProperty(sb, 2, "plotEvents", snapshot.Counts.PlotEvents, true);
        AppendIntProperty(sb, 2, "activeScene", snapshot.Counts.ActiveScene, true);
        AppendIntProperty(sb, 2, "total", snapshot.Counts.Total, false);
        AppendIndent(sb, 1);
        sb.AppendLine("},");

        AppendIndent(sb, 1);
        sb.AppendLine("\"entries\": [");
        for (var i = 0; i < snapshot.Entries.Count; i++)
        {
            var entry = snapshot.Entries[i];
            AppendIndent(sb, 2);
            sb.AppendLine("{");
            AppendStringProperty(sb, 3, "category", entry.Category, true);
            AppendStringProperty(sb, 3, "name", entry.Name, true);
            AppendStringProperty(sb, 3, "detail", entry.Detail, true);
            AppendStringProperty(sb, 3, "leftTime", entry.LeftTime, true);
            AppendStringProperty(sb, 3, "location", entry.Location, true);
            AppendStringProperty(sb, 3, "source", entry.Source, true);
            AppendStringProperty(sb, 3, "status", entry.Status, false);
            AppendIndent(sb, 2);
            sb.Append(i == snapshot.Entries.Count - 1 ? "}" : "},");
            sb.AppendLine();
        }

        AppendIndent(sb, 1);
        sb.AppendLine("]");
        sb.Append('}');
        return sb.ToString();
    }

    private static void AppendBooleanProperty(StringBuilder sb, int indent, string name, bool value, bool trailingComma)
    {
        AppendIndent(sb, indent);
        sb.Append('"').Append(name).Append("\": ").Append(value ? "true" : "false");
        if (trailingComma)
        {
            sb.Append(',');
        }

        sb.AppendLine();
    }

    private static void AppendIntProperty(StringBuilder sb, int indent, string name, int value, bool trailingComma)
    {
        AppendIndent(sb, indent);
        sb.Append('"').Append(name).Append("\": ").Append(value);
        if (trailingComma)
        {
            sb.Append(',');
        }

        sb.AppendLine();
    }

    private static void AppendStringProperty(StringBuilder sb, int indent, string name, string value, bool trailingComma)
    {
        AppendIndent(sb, indent);
        sb.Append('"').Append(name).Append("\": ");
        AppendJsonString(sb, value);
        if (trailingComma)
        {
            sb.Append(',');
        }

        sb.AppendLine();
    }

    private static void AppendIndent(StringBuilder sb, int indent)
    {
        for (var i = 0; i < indent; i++)
        {
            sb.Append("  ");
        }
    }

    private static void AppendJsonString(StringBuilder sb, string? value)
    {
        sb.Append('"');
        if (!string.IsNullOrEmpty(value))
        {
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (ch < ' ')
                        {
                            sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(ch);
                        }

                        break;
                }
            }
        }

        sb.Append('"');
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

    private static string? TryCallString(Func<string> getter)
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

    private static PlotController? TryGetPlotController()
    {
        try
        {
            return PlotController.Instance;
        }
        catch
        {
            return null;
        }
    }

    private static string TryGetHeroName(HeroData? hero)
    {
        if (hero == null)
        {
            return string.Empty;
        }

        var nameValue = SafeProperty(hero, "heroName") ?? SafeProperty(hero, "HeroName") ?? SafeField(hero, "heroName");
        return NormalizeText(nameValue?.ToString());
    }

    private static string FormatWorldDate(WorldData world)
    {
        try
        {
            if (world.worldTime != null)
            {
                return NormalizeText(world.worldTime.GetDescribe())
                       ?? $"Y{world.worldTime.year} M{world.worldTime.month} D{world.worldTime.day}";
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    private static int? TryConvertToInt(object? value)
    {
        if (value == null)
        {
            return null;
        }

        try
        {
            return value switch
            {
                int intValue => intValue,
                long longValue => checked((int)longValue),
                short shortValue => shortValue,
                byte byteValue => byteValue,
                float floatValue => (int)floatValue,
                double doubleValue => (int)doubleValue,
                _ => Convert.ToInt32(value)
            };
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
            return string.Empty;
        }

        return value switch
        {
            float f => f.ToString("0.###"),
            double d => d.ToString("0.###"),
            _ => value.ToString() ?? string.Empty
        };
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

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
        while (trimmed.Contains("  "))
        {
            trimmed = trimmed.Replace("  ", " ");
        }

        return trimmed;
    }

    private static string FallbackText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string FormatLeftTime(int? leftTime)
    {
        if (!leftTime.HasValue || leftTime.Value <= 0)
        {
            return string.Empty;
        }

        return $"{leftTime.Value}d left";
    }

    private static string DescribeMethod(MethodBase method)
    {
        return $"{method.DeclaringType?.Name}.{method.Name}";
    }
}
