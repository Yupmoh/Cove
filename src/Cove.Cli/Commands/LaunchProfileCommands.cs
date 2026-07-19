using Cove.Protocol;

namespace Cove.Cli;

internal static class LaunchProfileCommands
{
    [CoveCommand("launch-profile list")]
        public static Task<int> LaunchProfileList(CommandContext ctx)
            => ctx.RouteCoreWithParamsAsync("cove://commands/launch-profile.list", BuildAdapterParams(ctx.Args));

    [CoveCommand("launch-profile get")]
        public static Task<int> LaunchProfileGet(CommandContext ctx)
            => ctx.RouteCoreWithParamsAsync("cove://commands/launch-profile.get", BuildGetParams(ctx.Args));

    [CoveCommand("launch-profile create")]
        public static Task<int> LaunchProfileCreate(CommandContext ctx)
            => ctx.RouteCoreWithParamsAsync("cove://commands/launch-profile.create", BuildWriteParams(ctx.Args, create: true));

    [CoveCommand("launch-profile update")]
        public static Task<int> LaunchProfileUpdate(CommandContext ctx)
            => ctx.RouteCoreWithParamsAsync("cove://commands/launch-profile.update", BuildWriteParams(ctx.Args, create: false));

    [CoveCommand("launch-profile set-default")]
        public static Task<int> LaunchProfileSetDefault(CommandContext ctx)
            => ctx.RouteCoreWithParamsAsync("cove://commands/launch-profile.set-default", BuildGetParams(ctx.Args));

    [CoveCommand("launch-profile delete")]
        public static Task<int> LaunchProfileDelete(CommandContext ctx)
            => ctx.RouteCoreWithParamsAsync("cove://commands/launch-profile.delete", BuildGetParams(ctx.Args));

    private static string? ArgValue(string[] args, string flag)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == flag)
                    return args[i + 1];
            }
            return null;
        }

    private static string? BuildAdapterParams(string[] args)
        {
            var adapter = ArgValue(args, "--adapter");
            if (adapter is null) return null;
            return "{\"adapter\":\"" + EscapeJson(adapter) + "\"}";
        }

    private static string? BuildGetParams(string[] args)
        {
            var adapter = ArgValue(args, "--adapter");
            var slug = ArgValue(args, "--slug");
            if (adapter is null || slug is null) return null;
            return "{\"adapter\":\"" + EscapeJson(adapter) + "\",\"slug\":\"" + EscapeJson(slug) + "\"}";
        }

    private static string? BuildWriteParams(string[] args, bool create)
        {
            var adapter = ArgValue(args, "--adapter");
            var slug = ArgValue(args, "--slug");
            if (adapter is null || slug is null) return null;
            var name = ArgValue(args, "--name");
            if (create && name is null) return null;
            var model = ArgValue(args, "--model");
            var effort = ArgValue(args, "--effort");
            var agent = ArgValue(args, "--agent");
            var makeDefault = Array.IndexOf(args, "--default") >= 0;
            var argFlags = RepeatableValues(args, "--arg");
            var envPairs = RepeatableValues(args, "--env");

            using var buf = new System.IO.MemoryStream();
            using (var w = new System.Text.Json.Utf8JsonWriter(buf))
            {
                w.WriteStartObject();
                w.WriteString("adapter", adapter);
                w.WriteString("slug", slug);
                if (name is not null) w.WriteString("name", name);
                if (model is not null) w.WriteString("model", model);
                if (effort is not null) w.WriteString("effort", effort);
                if (agent is not null) w.WriteString("agent", agent);
                if (argFlags.Count > 0)
                {
                    w.WritePropertyName("cliArgs");
                    w.WriteStartArray();
                    foreach (var a in argFlags) w.WriteStringValue(a);
                    w.WriteEndArray();
                }
                if (envPairs.Count > 0)
                {
                    w.WritePropertyName("env");
                    w.WriteStartObject();
                    foreach (var kv in envPairs)
                    {
                        var eq = kv.IndexOf('=');
                        var k = eq < 0 ? kv : kv[..eq];
                        var v = eq < 0 ? "" : kv[(eq + 1)..];
                        w.WriteString(k, v);
                    }
                    w.WriteEndObject();
                }
                if (makeDefault) w.WriteBoolean("isDefault", true);
                w.WriteEndObject();
            }
            return System.Text.Encoding.UTF8.GetString(buf.ToArray());
        }

    private static System.Collections.Generic.List<string> RepeatableValues(string[] args, string flag)
        {
            var values = new System.Collections.Generic.List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == flag && i + 1 < args.Length)
                    values.Add(args[i + 1]);
            }
            return values;
        }

    private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
