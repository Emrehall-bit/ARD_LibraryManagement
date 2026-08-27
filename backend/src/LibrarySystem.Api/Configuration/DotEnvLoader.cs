namespace LibrarySystem.Api.Configuration;

internal static class DotEnvLoader
{
    public static void LoadNearest(string fileName = ".env")
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);

        while (current is not null)
        {
            var path = Path.Combine(current.FullName, fileName);
            if (File.Exists(path))
            {
                Load(path);
                return;
            }

            current = current.Parent;
        }
    }

    private static void Load(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim().Trim('"').Trim('\'');

            if (key.Length == 0 || Environment.GetEnvironmentVariable(key) is not null)
            {
                continue;
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
