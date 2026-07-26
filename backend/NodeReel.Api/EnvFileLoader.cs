namespace NodeReel.Api;

/// <summary>
/// Dokploy (and similar) often write Environment Settings into a .env file next to the Dockerfile
/// at build time. ASP.NET Core does not load .env automatically — only process env vars.
/// </summary>
public static class EnvFileLoader
{
    public static int Load(params string[] paths)
    {
        var loaded = 0;
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                var eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                var key = line[..eq].Trim();
                if (key.Length == 0)
                    continue;

                var value = line[(eq + 1)..].Trim();
                if (value.Length >= 2 &&
                    ((value.StartsWith('"') && value.EndsWith('"')) ||
                     (value.StartsWith('\'') && value.EndsWith('\''))))
                {
                    value = value[1..^1];
                }

                // Do not override real process env (Dokploy runtime -e wins if present).
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    continue;

                Environment.SetEnvironmentVariable(key, value);
                loaded++;
            }
        }

        return loaded;
    }
}
