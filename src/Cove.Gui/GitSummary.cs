using System.Text.Json;

namespace Cove.Gui;

public static class GitSummary
{
    public static string Run(string path)
    {
        if (!Directory.Exists(path)) return Fail("not_found");
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("status");
            psi.ArgumentList.Add("--porcelain=v2");
            psi.ArgumentList.Add("--branch");
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return Fail("git_unavailable");
            var stdout = proc.StandardOutput.ReadToEnd();
            if (!proc.WaitForExit(4000))
            {
                proc.Kill(true);
                return Fail("timeout");
            }
            if (proc.ExitCode != 0) return Fail("not_a_repo");
            return Parse(stdout);
        }
        catch (Exception)
        {
            return Fail("git_unavailable");
        }
    }

    public static string Parse(string output)
    {
        var branch = "";
        var ahead = 0;
        var behind = 0;
        var dirty = 0;
        var files = new List<(string Path, string Status)>();
        foreach (var line in output.Split('\n'))
        {
            if (line.StartsWith("# branch.head ", StringComparison.Ordinal))
            {
                branch = line["# branch.head ".Length..].Trim();
            }
            else if (line.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                foreach (var part in line["# branch.ab ".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (part.Length < 2 || !int.TryParse(part[1..], out var n)) continue;
                    if (part[0] == '+') ahead = n;
                    else if (part[0] == '-') behind = n;
                }
            }
            else if (line.Length > 0 && !line.StartsWith('#'))
            {
                dirty++;
                var parsed = ParseFile(line);
                if (parsed is { } file) files.Add(file);
            }
        }
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteBoolean("ok", true);
            w.WriteString("branch", branch);
            w.WriteNumber("ahead", ahead);
            w.WriteNumber("behind", behind);
            w.WriteNumber("dirty", dirty);
            w.WriteStartArray("files");
            foreach (var (path, status) in files)
            {
                w.WriteStartObject();
                w.WriteString("path", path);
                w.WriteString("status", status);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteNull("error");
            w.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static (string Path, string Status)? ParseFile(string line)
    {
        if (line.StartsWith("? ", StringComparison.Ordinal))
            return (line[2..], "A");
        if (line.StartsWith("1 ", StringComparison.Ordinal))
        {
            var fields = line.Split(' ', 9, StringSplitOptions.None);
            return fields.Length == 9 ? (fields[8], NormalizeStatus(fields[1])) : null;
        }
        if (line.StartsWith("2 ", StringComparison.Ordinal))
        {
            var fields = line.Split(' ', 10, StringSplitOptions.None);
            return fields.Length == 10 ? (fields[9].Split('\t')[0], NormalizeStatus(fields[1])) : null;
        }
        return null;
    }

    private static string NormalizeStatus(string xy)
    {
        if (xy.Contains('D')) return "D";
        if (xy.Contains('A') || xy.Contains('?')) return "A";
        return "M";
    }

    private static string Fail(string error)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteBoolean("ok", false);
            w.WriteString("error", error);
            w.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
