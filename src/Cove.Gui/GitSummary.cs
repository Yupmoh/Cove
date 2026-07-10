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
            w.WriteNull("error");
            w.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
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
