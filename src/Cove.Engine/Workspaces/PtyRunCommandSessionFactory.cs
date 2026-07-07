using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Workspaces;

public sealed class PtyRunCommandSessionFactory : IRunCommandSessionFactory
{
    private readonly IPtyHost _host;
    private readonly SpawnEnvironment? _spawnEnv;
    private readonly string? _shellDir;
    private readonly ILogger _logger;
    private readonly Func<string> _newId;

    public PtyRunCommandSessionFactory(IPtyHost host, SpawnEnvironment? spawnEnv, string? shellDir, ILogger logger, Func<string>? newId = null)
    {
        _host = host;
        _spawnEnv = spawnEnv;
        _shellDir = shellDir;
        _logger = logger;
        _newId = newId ?? (() => "rcs-" + Guid.NewGuid().ToString("N"));
    }

    public IRunCommandSession Create(RunCommandDefinition def, Action<byte[]> onOutput)
        => new PtyRunCommandSession(_host, _spawnEnv, _shellDir, _logger, _newId(), def, onOutput);

    private sealed class PtyRunCommandSession : IRunCommandSession
    {
        private readonly IPtySession _session;
        private readonly PtyRingBuffer _ring;
        private readonly PtySessionReader _reader;
        private readonly Action<byte[]> _onOutput;
        private volatile bool _running;

        public string SessionId { get; }
        public bool IsRunning => _running;
        public int? ExitCode => _session.HasExited ? _session.ExitCode : null;

        public PtyRunCommandSession(IPtyHost host, SpawnEnvironment? spawnEnv, string? shellDir, ILogger logger, string sessionId, RunCommandDefinition def, Action<byte[]> onOutput)
        {
            SessionId = sessionId;
            _onOutput = onOutput;
            var args = ParseCommandLine(def.Command);
            var command = args.Length > 0 ? args[0] : def.Command;
            var restArgs = args.Length > 1 ? args[1..] : [];
            var envDict = spawnEnv is { } se ? se.Build(sessionId, null) : null;
            if (envDict is Dictionary<string, string> ed && shellDir is { } sd)
                restArgs = ShellIntegration.Apply(command, sd, restArgs, ed).ToArray();
            var cwd = string.IsNullOrEmpty(def.Cwd) ? PaneRegistry.ResolveWorkingDirectory(null, null) : def.Cwd;

            var request = new PtySpawnRequest
            {
                Command = command,
                Args = restArgs,
                WorkingDirectory = cwd,
                Environment = envDict,
                Cols = 80,
                Rows = 24,
            };
            _session = host.Spawn(request);
            _ring = new PtyRingBuffer();
            var signal = new PtyRingSignal();
            _reader = new PtySessionReader(_session, _ring, signal, logger);
        }

        public void Start()
        {
            _running = true;
            _reader.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _session.Kill(); } catch { }
        }

        public void Dispose()
        {
            _running = false;
            try { _reader.Dispose(); } catch { }
            try { _session.Dispose(); } catch { }
        }

        private static string[] ParseCommandLine(string command)
        {
            var parts = new List<string>();
            var current = new System.Text.StringBuilder();
            var inQuote = false;
            var quoteChar = '\0';
            for (int i = 0; i < command.Length; i++)
            {
                var c = command[i];
                if (inQuote)
                {
                    if (c == quoteChar)
                    {
                        inQuote = false;
                        continue;
                    }
                    current.Append(c);
                }
                else if (c == '"' || c == '\'')
                {
                    inQuote = true;
                    quoteChar = c;
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (current.Length > 0)
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            if (current.Length > 0)
                parts.Add(current.ToString());
            return parts.ToArray();
        }
    }
}
