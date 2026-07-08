using Cove.Platform;
using Cove.Protocol;

namespace Cove.Cli;

internal static class CliCommands
{
    [CoveCommand("version")]
    public static async Task<int> Version(CommandContext ctx)
    {
        var cliVersion = CoveBuild.InformationalVersion;
        var connector = new Cove.Engine.Daemon.DaemonConnector(ctx.Paths, ctx.Endpoint);
        var conn = await connector.TryConnectAndHelloAsync("cli", System.Threading.CancellationToken.None);
        if (conn is not null)
        {
            await conn.DisposeAsync();
            ctx.Stdout.WriteLine($"cove {cliVersion} (daemon: connected)");
        }
        else
        {
            ctx.Stdout.WriteLine($"cove {cliVersion} (daemon: disconnected)");
        }
        return 0;
    }

    [CoveCommand("migrate")]
    public static async Task<int> Migrate(CommandContext ctx)
    {
        var connector = new Cove.Engine.Daemon.DaemonConnector(ctx.Paths, ctx.Endpoint);
        var conn = await connector.TryConnectAndHelloAsync("cli", System.Threading.CancellationToken.None);
        if (conn is not null)
        {
            await conn.DisposeAsync();
            ctx.Stderr.WriteLine("error: daemon_running, stop it first (cove stop)");
            return 1;
        }
        using var loggerFactory = Cove.Platform.CoveLog.CreateConsoleLoggerFactory();
        var runner = new Cove.Engine.Migrations.MigrationRunner(ctx.Paths.DataDir.Root, loggerFactory.CreateLogger("migrate"));
        var result = runner.Migrate();
        if (result.NoOp)
            ctx.Stdout.WriteLine($"no migrations needed (at version {result.ToVersion})");
        else
            ctx.Stdout.WriteLine($"migrated {result.FromVersion} -> {result.ToVersion}");
        return 0;
    }

    [CoveCommand("pane list")]
    public static Task<int> PaneList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/pane.list");

    [CoveCommand("docs generate")]
    public static Task<int> DocsGenerate(CommandContext ctx)
    {
        var args = ctx.Args;
        var outPath = args.Length > 0 ? args[0] : "docs/cli-reference.md";
        var dir = System.IO.Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
        CliReferenceDoc.WriteTo(outPath);
        ctx.Stdout.WriteLine($"wrote {outPath}");
        return Task.FromResult(0);
    }

    [CoveCommand("attach")]
    public static Task<int> Attach(CommandContext ctx)
    {
        var args = ctx.Args;
        var raw = args.Length > 0 && args[0] == "--raw";
        if (!raw)
        {
            ctx.Stderr.WriteLine("usage: cove attach --raw <session>");
            return Task.FromResult(1);
        }
        if (args.Length < 2)
        {
            ctx.Stderr.WriteLine("usage: cove attach --raw <session>");
            return Task.FromResult(1);
        }
        var session = args[1];
        using var attachBuf = new System.IO.MemoryStream();
        using (var attachWriter = new System.Text.Json.Utf8JsonWriter(attachBuf))
        {
            attachWriter.WriteStartObject();
            attachWriter.WriteString("session", session);
            attachWriter.WriteEndObject();
            attachWriter.Flush();
        }
        return ctx.RouteCoreWithParamsAsync("cove://commands/attach.raw", System.Text.Encoding.UTF8.GetString(attachBuf.ToArray()));
    }

    [CoveCommand("skills list")]
    public static Task<int> SkillsList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/skills.index");

    [CoveCommand("skills resolve-prompt-sigils")]
    public static Task<int> SkillsResolvePromptSigils(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/skills.resolve-prompt-sigils");

    [CoveCommand("agent definition list")]
    public static Task<int> AgentDefinitionList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/agent.definition.list");

    [CoveCommand("agent definition show")]
    public static Task<int> AgentDefinitionShow(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/agent.definition.show");

    [CoveCommand("agent definition delete")]
    public static Task<int> AgentDefinitionDelete(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/agent.definition.delete");

    [CoveCommand("launch-profile list")]
    public static Task<int> LaunchProfileList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/launch-profile.list");

    [CoveCommand("launch-profile set-default")]
    public static Task<int> LaunchProfileSetDefault(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/launch-profile.set-default");

    [CoveCommand("launch-profile delete")]
    public static Task<int> LaunchProfileDelete(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/launch-profile.delete");

    [CoveCommand("adapter-env list")]
    public static Task<int> AdapterEnvList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/adapter-env.list");

    [CoveCommand("adapter-env save")]
    public static Task<int> AdapterEnvSave(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/adapter-env.save");

    [CoveCommand("adapter-env resolve")]
    public static Task<int> AdapterEnvResolve(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/adapter-env.resolve");

    [CoveCommand("hook emit")]
    public static async Task<int> HookEmit(CommandContext ctx)
    {
        var args = ctx.Args;
        var portFile = System.IO.Path.Combine(ctx.Paths.DataDir.Root, "hook-port");
        if (!System.IO.File.Exists(portFile))
        {
            await ctx.Stderr.WriteLineAsync("error: hook server not running");
            return 1;
        }
        if (!int.TryParse(System.IO.File.ReadAllText(portFile).Trim(), out var port))
        {
            await ctx.Stderr.WriteLineAsync("error: invalid hook-port file");
            return 1;
        }
        var adapter = ArgValue(args, "--adapter");
        var paneId = ArgValue(args, "--pane-id");
        var verbose = Array.IndexOf(args, "--verbose") >= 0;
        var @event = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : null;
        if (adapter is null || @event is null)
        {
            await ctx.Stderr.WriteLineAsync("error: event and --adapter required");
            return 1;
        }
        var payload = "{}";
        if (System.Console.IsInputRedirected)
        {
            using var stdin = System.Console.In;
            var stdinPayload = stdin.ReadToEnd();
            if (!string.IsNullOrEmpty(stdinPayload))
                payload = stdinPayload;
        }
        var client = new Cove.Engine.Hooks.HookEmitClient(port);
        var result = await client.EmitAsync(adapter, @event, payload, paneId);
        if (!result.Ok)
        {
            await ctx.Stderr.WriteLineAsync($"error: hook emit failed status={result.StatusCode}");
            return 1;
        }
        if (verbose && result.Body is not null)
            await ctx.Stderr.WriteLineAsync(result.Body);
        return 0;
    }

    private static string? ArgValue(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == flag)
                return args[i + 1];
        }
        return null;
    }

    [CoveCommand("config get")]
    public static Task<int> ConfigGet(CommandContext ctx)
    {
        var args = ctx.Args;
        if (args.Length < 1)
        {
            ctx.Stderr.WriteLine("usage: cove config get <key>");
            return Task.FromResult(1);
        }
        var key = args[0];
        var configPath = System.IO.Path.Combine(ctx.Paths.DataDir.Root, "config.json");
        if (!System.IO.File.Exists(configPath))
        {
            ctx.Stderr.WriteLine($"error: config key '{key}' not found");
            return Task.FromResult(1);
        }
        var json = System.IO.File.ReadAllText(configPath);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (TryGetConfigValue(doc.RootElement, key, out var val))
        {
            ctx.Stdout.WriteLine(val);
            return Task.FromResult(0);
        }
        ctx.Stderr.WriteLine($"error: config key '{key}' not found");
        return Task.FromResult(1);
    }

    private static bool TryGetConfigValue(System.Text.Json.JsonElement root, string key, out string value)
    {
        value = "";
        if (root.ValueKind == System.Text.Json.JsonValueKind.Object && root.TryGetProperty(key, out var flat))
        {
            value = flat.ValueKind == System.Text.Json.JsonValueKind.String ? flat.GetString() ?? "" : flat.GetRawText();
            return true;
        }
        var parts = key.Split('.');
        var current = root;
        foreach (var part in parts)
        {
            if (current.ValueKind != System.Text.Json.JsonValueKind.Object || !current.TryGetProperty(part, out var next))
                return false;
            current = next;
        }
        value = current.ValueKind == System.Text.Json.JsonValueKind.String ? current.GetString() ?? "" : current.GetRawText();
        return true;
    }
    [CoveCommand("config set")]
    public static Task<int> ConfigSet(CommandContext ctx)
    {
        var args = ctx.Args;
        if (args.Length < 2)
        {
            ctx.Stderr.WriteLine("usage: cove config set <key> <value>");
            return Task.FromResult(1);
        }
        var key = args[0];
        var value = args[1];
        var configPath = System.IO.Path.Combine(ctx.Paths.DataDir.Root, "config.json");
        var config = new System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>();
        if (System.IO.File.Exists(configPath))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(configPath));
                foreach (var prop in doc.RootElement.EnumerateObject())
                    config[prop.Name] = prop.Value.Clone();
            }
            catch (System.Text.Json.JsonException) { ctx.Stderr.WriteLine("warning: config.json corrupt, overwriting"); }
        }
        config.Remove(key);
        using var setBuf = new System.IO.MemoryStream();
        using (var setWriter = new System.Text.Json.Utf8JsonWriter(setBuf, new System.Text.Json.JsonWriterOptions { Indented = true }))
        {
            setWriter.WriteStartObject();
            foreach (var kv in config)
            {
                setWriter.WritePropertyName(kv.Key);
                kv.Value.WriteTo(setWriter);
            }
            setWriter.WriteString(key, value);
            setWriter.WriteEndObject();
            setWriter.Flush();
        }
        System.IO.File.WriteAllText(configPath, System.Text.Encoding.UTF8.GetString(setBuf.ToArray()));
        ctx.Stdout.WriteLine($"set {key} = {value}");
        return Task.FromResult(0);
    }

    [CoveCommand("commands")]
    public static async Task<int> Commands(CommandContext ctx)
    {
        var cliCatalogue = Cove.Generated.CoveCommandRegistry.Catalogue;
        var engineCatalogue = Cove.Engine.EngineCommandCatalogue.Entries;
        var dataDir = Cove.Platform.CoveDataDir.Resolve(ctx.Channel);
        var manifests = new Cove.Adapters.AdapterManifestStore(System.IO.Path.Combine(dataDir.Root, "adapters"), null);
        var extensions = new Cove.Engine.Protocol.ExtensionRegistry(manifests);
        var extensionCommands = extensions.List();
        var allCommands = new System.Collections.Generic.List<(string Command, string? Description, string Source)>();
        foreach (var e in cliCatalogue)
            allCommands.Add((e.Command, e.Description, e.Source));
        foreach (var e in engineCatalogue)
            allCommands.Add((e.Command, e.Description, e.Source));
        foreach (var ext in extensionCommands)
            allCommands.Add((ext.Command, ext.Description, ext.Source));
        if (ctx.IsJson)
        {
            using var buffer = new System.IO.MemoryStream();
            using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
            {
                writer.WriteStartArray();
                foreach (var entry in allCommands)
                {
                    writer.WriteStartObject();
                    writer.WriteString("command", entry.Command);
                    if (entry.Description is not null)
                        writer.WriteString("description", entry.Description);
                    writer.WriteString("source", entry.Source);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.Flush();
            }
            ctx.Stdout.WriteLine(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
        }
        else
        {
            ctx.Stdout.WriteLine("Commands:");
            foreach (var entry in allCommands.OrderBy(c => c.Source).ThenBy(c => c.Command))
                ctx.Stdout.WriteLine($"  [{entry.Source}] {entry.Command}");
            ctx.Stdout.WriteLine($"Total: {allCommands.Count}");
        }
        await Task.CompletedTask;
        return 0;
    }

    [CoveCommand("context")]
    public static Task<int> Context(CommandContext ctx)
    {
        var paneId = System.Environment.GetEnvironmentVariable("COVE_PANE_ID") ?? "(unset)";
        var cwd = System.Environment.CurrentDirectory;
        var workspace = System.Environment.GetEnvironmentVariable("COVE_WORKSPACE_ID") ?? "(unset)";
        var room = System.Environment.GetEnvironmentVariable("COVE_ROOM_ID") ?? "(unset)";
        ctx.Stdout.WriteLine($"pane: {paneId}");
        ctx.Stdout.WriteLine($"workspace: {workspace}");
        ctx.Stdout.WriteLine($"room: {room}");
        ctx.Stdout.WriteLine($"cwd: {cwd}");
        return Task.FromResult(0);
    }

    [CoveCommand("extension list")]
    public static Task<int> ExtensionList(CommandContext ctx)
    {
        var dataDir = Cove.Platform.CoveDataDir.Resolve(ctx.Channel);
        var manifests = new Cove.Adapters.AdapterManifestStore(System.IO.Path.Combine(dataDir.Root, "adapters"), null);
        var extensions = new Cove.Engine.Protocol.ExtensionRegistry(manifests);
        var commands = extensions.List();
        if (ctx.IsJson)
        {
            using var buffer = new System.IO.MemoryStream();
            using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
            {
                writer.WriteStartArray();
                foreach (var ext in commands)
                {
                    writer.WriteStartObject();
                    writer.WriteString("command", ext.Command);
                    writer.WriteString("source", ext.Source);
                    writer.WriteString("adapter", ext.Adapter);
                    writer.WriteString("method", ext.Method);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.Flush();
            }
            ctx.Stdout.WriteLine(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
        }
        else
        {
            foreach (var ext in commands.OrderBy(e => e.Adapter).ThenBy(e => e.Method))
                ctx.Stdout.WriteLine($"{ext.Command}  (adapter: {ext.Adapter}, method: {ext.Method})");
            ctx.Stdout.WriteLine($"Total: {commands.Count}");
        }
        return Task.FromResult(0);
    }

    [CoveCommand("extension run")]
    public static Task<int> ExtensionRun(CommandContext ctx)
    {
        var args = ctx.Args;
        if (args.Length < 1)
        {
            ctx.Stderr.WriteLine("usage: cove extension run <extension.adapter.method> [--params '<json>']");
            return Task.FromResult(1);
        }
        var command = args[0];
        string? paramsJson = null;
        for (var i = 1; i < args.Length - 1; i++)
        {
            if (args[i] == "--params")
                paramsJson = args[i + 1];
        }
        using var payloadBuf = new System.IO.MemoryStream();
        using (var payloadWriter = new System.Text.Json.Utf8JsonWriter(payloadBuf))
        {
            payloadWriter.WriteStartObject();
            payloadWriter.WriteString("command", command);
            if (paramsJson is not null)
            {
                try
                {
                    using var paramsDoc = System.Text.Json.JsonDocument.Parse(paramsJson);
                    payloadWriter.WritePropertyName("params");
                    paramsDoc.RootElement.WriteTo(payloadWriter);
                }
                catch (System.Text.Json.JsonException)
                {
                    ctx.Stderr.WriteLine("error: invalid_params");
                    ctx.Stderr.WriteLine("usage: --params '<json>'");
                    return Task.FromResult(1);
                }
            }
            payloadWriter.WriteEndObject();
            payloadWriter.Flush();
        }
        return ctx.RouteCoreWithParamsAsync("cove://commands/extension.run", System.Text.Encoding.UTF8.GetString(payloadBuf.ToArray()));
    }
    [CoveCommand("exec")]
    public static Task<int> Exec(CommandContext ctx)
    {
        var args = ctx.Args;
        if (args.Length < 1)
        {
            ctx.Stderr.WriteLine("usage: cove exec <dot.name> [--params '<json>']");
            return Task.FromResult(1);
        }
        var uri = "cove://commands/" + args[0].Replace(".", "/");
        return ctx.RouteCoreAsync(uri);
    }
    [CoveCommand("protocol resolve")]
    public static Task<int> ProtocolResolve(CommandContext ctx)
    {
        var args = ctx.Args;
        if (args.Length < 1)
        {
            ctx.Stderr.WriteLine("usage: cove protocol resolve <uri>");
            return Task.FromResult(1);
        }
        var uri = args[0];
        return ctx.RouteCoreAsync($"cove://commands/protocol.resolve?uri={uri}");
    }

    [CoveCommand("task list")]
    public static Task<int> TaskList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.list");

    [CoveCommand("task get")]
    public static Task<int> TaskGet(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.get");

    [CoveCommand("task create")]
    public static Task<int> TaskCreate(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.create");

    [CoveCommand("task update")]
    public static Task<int> TaskUpdate(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.update");

    [CoveCommand("task delete")]
    public static Task<int> TaskDelete(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.delete");

    [CoveCommand("task status list")]
    public static Task<int> TaskStatusList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.status.list");

    [CoveCommand("task status create")]
    public static Task<int> TaskStatusCreate(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.status.create");

    [CoveCommand("task status delete")]
    public static Task<int> TaskStatusDelete(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.status.delete");

    [CoveCommand("task status reorder")]
    public static Task<int> TaskStatusReorder(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.status.reorder");

    [CoveCommand("task status hide")]
    public static Task<int> TaskStatusHide(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.status.set-hidden");

    [CoveCommand("task label list")]
    public static Task<int> TaskLabelList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.label.list");

    [CoveCommand("task label create")]
    public static Task<int> TaskLabelCreate(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.label.create");

    [CoveCommand("task label delete")]
    public static Task<int> TaskLabelDelete(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.label.delete");

    [CoveCommand("task label assign")]
    public static Task<int> TaskLabelAssign(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.label.assign");

    [CoveCommand("task label unassign")]
    public static Task<int> TaskLabelUnassign(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.label.unassign");

    [CoveCommand("task label reorder")]
    public static Task<int> TaskLabelReorder(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.label.reorder");

    [CoveCommand("task label filter")]
    public static Task<int> TaskLabelFilter(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.label.filter");

    [CoveCommand("task comment add")]
    public static Task<int> TaskCommentAdd(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.comment.add");

    [CoveCommand("task comment list")]
    public static Task<int> TaskCommentList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.comment.list");

    [CoveCommand("task comment count")]
    public static Task<int> TaskCommentCount(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.comment.count");

    [CoveCommand("task comment delete")]
    public static Task<int> TaskCommentDelete(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.comment.delete");

    [CoveCommand("task launch-config get")]
    public static Task<int> TaskLaunchConfigGet(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.launch-config.get");

    [CoveCommand("task launch-config set")]
    public static Task<int> TaskLaunchConfigSet(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.launch-config.set");

    [CoveCommand("task binding get")]
    public static Task<int> TaskBindingGet(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.binding.get");

    [CoveCommand("task binding set")]
    public static Task<int> TaskBindingSet(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.binding.set");

    [CoveCommand("task binding resolve-profile")]
    public static Task<int> TaskBindingResolveProfile(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.binding.resolve-profile");

    [CoveCommand("task launch")]
    public static Task<int> TaskLaunch(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.launch");

    [CoveCommand("task set-in-review")]
    public static Task<int> TaskSetInReview(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.set-in-review");

    [CoveCommand("task set-done")]
    public static Task<int> TaskSetDone(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.set-done");

    [CoveCommand("task claim")]
    public static Task<int> TaskClaim(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.claim");

    [CoveCommand("run list")]
    public static Task<int> RunList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/run.list");

    [CoveCommand("run show")]
    public static Task<int> RunShow(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/run.show");

    [CoveCommand("run segments")]
    public static Task<int> RunSegments(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/run.segments");

    [CoveCommand("run cancel")]
    public static Task<int> RunCancel(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/run.cancel");

    [CoveCommand("run complete")]
    public static Task<int> RunComplete(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/run.complete");

    [CoveCommand("run resume")]
    public static Task<int> RunResume(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/run.resume");

    [CoveCommand("run set-pending-prompt")]
    public static Task<int> RunSetPendingPrompt(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/run.set-pending-prompt");

    [CoveCommand("task repeat set")]
    public static Task<int> TaskRepeatSet(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.repeat.set");

    [CoveCommand("task repeat get")]
    public static Task<int> TaskRepeatGet(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.repeat.get");

    [CoveCommand("task repeat pause")]
    public static Task<int> TaskRepeatPause(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.repeat.pause");

    [CoveCommand("task repeat resume")]
    public static Task<int> TaskRepeatResume(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.repeat.resume");

    [CoveCommand("task repeat skip-next")]
    public static Task<int> TaskRepeatSkipNext(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.repeat.skip-next");

    [CoveCommand("task repeat stop")]
    public static Task<int> TaskRepeatStop(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.repeat.stop");

    [CoveCommand("note list")]
    public static Task<int> NoteList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/note.list");

    [CoveCommand("note get")]
    public static Task<int> NoteGet(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/note.get");

    [CoveCommand("note create")]
    public static Task<int> NoteCreate(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/note.create");

    [CoveCommand("note update")]
    public static Task<int> NoteUpdate(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/note.update");

    [CoveCommand("note delete")]
    public static Task<int> NoteDelete(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/note.delete");

    [CoveCommand("note search")]
    public static Task<int> NoteSearch(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/note.search");

    [CoveCommand("note read")]
    public static Task<int> NoteRead(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/note.read");

    [CoveCommand("note write")]
    public static Task<int> NoteWrite(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/note.write");

    [CoveCommand("note history")]
    public static Task<int> NoteHistory(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/note.history");

    [CoveCommand("note media save")]
    public static Task<int> NoteMediaSave(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/note.media.save");

    [CoveCommand("note get-state")]
    public static Task<int> NoteGetState(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/note.get-state");

    [CoveCommand("note save-state")]
    public static Task<int> NoteSaveState(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/note.save-state");

    [CoveCommand("canvas action")]
    public static Task<int> CanvasAction(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/canvas.action");

    [CoveCommand("memory add")]
    public static Task<int> MemoryAdd(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/memory.add");

    [CoveCommand("memory search")]
    public static Task<int> MemorySearch(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/memory.search");

    [CoveCommand("memory recall")]
    public static Task<int> MemoryRecall(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/memory.recall");

    [CoveCommand("memory show")]
    public static Task<int> MemoryShow(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/memory.show");

    [CoveCommand("memory supersede")]
    public static Task<int> MemorySupersede(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/memory.supersede");

    [CoveCommand("memory reindex")]
    public static Task<int> MemoryReindex(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/memory.reindex");

    [CoveCommand("memory consolidate")]
    public static Task<int> MemoryConsolidate(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/memory.consolidate");

    [CoveCommand("memory propose")]
    public static Task<int> MemoryPropose(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/memory.propose");

    [CoveCommand("memory proposal transition")]
    public static Task<int> MemoryProposalTransition(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/memory.proposal.transition");

    [CoveCommand("edits find")]
    public static Task<int> EditsFind(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/edits.find");

    [CoveCommand("vault search")]
    public static Task<int> VaultSearch(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/vault.search");

    [CoveCommand("vault resume")]
    public static Task<int> VaultResume(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/vault.resume");

    [CoveCommand("vault set-setting")]
    public static Task<int> VaultSetSetting(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/vault.set-setting");

    [CoveCommand("vault reindex")]
    public static Task<int> VaultReindex(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/vault.reindex");

    [CoveCommand("library list")]
    public static Task<int> LibraryList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/library.list");

    [CoveCommand("library materialize")]
    public static Task<int> LibraryMaterialize(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/library.materialize");
    [CoveCommand("timeline list")]
    public static Task<int> TimelineList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/timeline.list");

    [CoveCommand("task run-now")]
    public static Task<int> TaskRunNow(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.run-now");

    [CoveCommand("task repeat continue")]
    public static Task<int> TaskRepeatContinue(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.repeat.continue");

    [CoveCommand("task repeat finish")]
    public static Task<int> TaskRepeatFinish(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task.repeat.finish");

    [CoveCommand("task-board export")]
    public static Task<int> TaskBoardExport(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task-board.export");

    [CoveCommand("task-board diff")]
    public static Task<int> TaskBoardDiff(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/task-board.diff");

    [CoveCommand("timeline append")]
    public static Task<int> TimelineAppend(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/timeline.append");

    [CoveCommand("knowledge ping")]
    public static Task<int> KnowledgePing(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/knowledge.ping");

    [CoveCommand("blackboard post")]
    public static Task<int> BlackboardPost(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/blackboard.post");

    [CoveCommand("blackboard show")]
    public static Task<int> BlackboardShow(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/blackboard.show");

    [CoveCommand("pane-types list")]
    public static Task<int> PaneTypesList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/pane-types.list");

    [CoveCommand("browser open")]
    public static Task<int> BrowserOpen(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/browser.open");

    [CoveCommand("browser navigate")]
    public static Task<int> BrowserNavigate(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/browser.navigate");

    [CoveCommand("browser back")]
    public static Task<int> BrowserBack(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/browser.back");

    [CoveCommand("browser forward")]
    public static Task<int> BrowserForward(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/browser.forward");

    [CoveCommand("browser reload")]
    public static Task<int> BrowserReload(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/browser.reload");

    [CoveCommand("browser close")]
    public static Task<int> BrowserClose(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/browser.close");

    [CoveCommand("review add-comment")]
    public static Task<int> ReviewAddComment(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/review.add-comment");

    [CoveCommand("review list-comments")]
    public static Task<int> ReviewListComments(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/review.list-comments");

    [CoveCommand("review resolve")]
    public static Task<int> ReviewResolve(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/review.resolve");

    [CoveCommand("review reopen")]
    public static Task<int> ReviewReopen(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/review.reopen");

    [CoveCommand("review close")]
    public static Task<int> ReviewClose(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/review.close");

    [CoveCommand("review re-anchor")]
    public static Task<int> ReviewReAnchor(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/review.re-anchor");

    [CoveCommand("review audit")]
    public static Task<int> ReviewAudit(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/review.audit");

    [CoveCommand("review telemetry")]
    public static Task<int> ReviewTelemetry(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/review.telemetry");
}
