namespace LongYinUpdater;

internal sealed record UpdateOptions(
    int WaitPid,
    string SourceRoot,
    string TargetRoot,
    string AppExecutableName,
    string LogPath,
    string Version)
{
    public string TargetExecutablePath => Path.Combine(TargetRoot, AppExecutableName);

    public static UpdateOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length)
            {
                throw new InvalidOperationException($"缺少参数值：{args[i]}");
            }

            values[args[i]] = args[i + 1];
        }

        return new UpdateOptions(
            WaitPid: ReadInt(values, "--wait-pid"),
            SourceRoot: ReadPath(values, "--source"),
            TargetRoot: ReadPath(values, "--target"),
            AppExecutableName: ReadRequired(values, "--exe"),
            LogPath: ReadPath(values, "--log"),
            Version: values.TryGetValue("--version", out var version) && !string.IsNullOrWhiteSpace(version)
                ? version.Trim()
                : "unknown");
    }

    private static string ReadRequired(Dictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"缺少必要参数：{key}");
        }

        return value.Trim();
    }

    private static string ReadPath(Dictionary<string, string> values, string key)
    {
        return Path.GetFullPath(ReadRequired(values, key));
    }

    private static int ReadInt(Dictionary<string, string> values, string key)
    {
        var text = ReadRequired(values, key);
        if (!int.TryParse(text, out var result) || result < 0)
        {
            throw new InvalidOperationException($"参数无效：{key}={text}");
        }

        return result;
    }
}
