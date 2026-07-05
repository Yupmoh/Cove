using System.Collections.Generic;
using System.IO;

namespace Cove.Engine;

public static class ShellIntegration
{
    private const string ZshEnv =
        @"[ -f ""${COVE_ZDOTDIR_ORIG:-$HOME}/.zshenv"" ] && source ""${COVE_ZDOTDIR_ORIG:-$HOME}/.zshenv""
";

    private const string ZshProfile =
        @"[ -f ""${COVE_ZDOTDIR_ORIG:-$HOME}/.zprofile"" ] && source ""${COVE_ZDOTDIR_ORIG:-$HOME}/.zprofile""
";

    private const string ZshRc =
        @"[ -f ""${COVE_ZDOTDIR_ORIG:-$HOME}/.zshrc"" ] && source ""${COVE_ZDOTDIR_ORIG:-$HOME}/.zshrc""
_cove_osc7() { printf '\e]7;file://%s%s\a' ""${HOST}"" ""$PWD""; }
autoload -Uz add-zsh-hook 2>/dev/null && add-zsh-hook precmd _cove_osc7 || precmd_functions=(_cove_osc7 $precmd_functions)
_cove_osc7
";

    private const string ZshLogin =
        @"[ -f ""${COVE_ZDOTDIR_ORIG:-$HOME}/.zlogin"" ] && source ""${COVE_ZDOTDIR_ORIG:-$HOME}/.zlogin""
";

    private const string BashRc =
        @"[ -f ""$HOME/.bashrc"" ] && source ""$HOME/.bashrc""
_cove_osc7() { printf '\e]7;file://%s%s\a' ""${HOSTNAME}"" ""$PWD""; }
case ""${PROMPT_COMMAND:-}"" in *_cove_osc7*) ;; *) PROMPT_COMMAND=""_cove_osc7${PROMPT_COMMAND:+;$PROMPT_COMMAND}"" ;; esac
";

    public static string Install(string dataDir)
    {
        var shellDir = Path.Combine(dataDir, "shell");
        var zshDir = Path.Combine(shellDir, "zsh");
        Directory.CreateDirectory(zshDir);

        WriteIfChanged(Path.Combine(zshDir, ".zshenv"), ZshEnv);
        WriteIfChanged(Path.Combine(zshDir, ".zprofile"), ZshProfile);
        WriteIfChanged(Path.Combine(zshDir, ".zshrc"), ZshRc);
        WriteIfChanged(Path.Combine(zshDir, ".zlogin"), ZshLogin);
        WriteIfChanged(Path.Combine(shellDir, "cove.bashrc"), BashRc);

        return shellDir;
    }

    public static IReadOnlyList<string> Apply(string shellPath, string shellDir, IReadOnlyList<string> args, Dictionary<string, string> env)
    {
        var name = Path.GetFileName(shellPath);

        if (name.Contains("zsh", System.StringComparison.Ordinal))
        {
            if (!env.TryGetValue("ZDOTDIR", out var orig) || string.IsNullOrEmpty(orig))
                orig = env.TryGetValue("HOME", out var h) ? h : System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            env["COVE_ZDOTDIR_ORIG"] = orig;
            env["ZDOTDIR"] = Path.Combine(shellDir, "zsh");
            return args;
        }

        if (name.Contains("bash", System.StringComparison.Ordinal))
        {
            if (System.Linq.Enumerable.Contains(args, "--rcfile"))
                return args;
            return new List<string>(args) { "--rcfile", Path.Combine(shellDir, "cove.bashrc") };
        }

        return args;
    }

    private static void WriteIfChanged(string path, string content)
    {
        if (File.Exists(path))
        {
            try
            {
                if (File.ReadAllText(path) == content)
                    return;
            }
            catch
            {
            }
        }
        File.WriteAllText(path, content);
    }
}
