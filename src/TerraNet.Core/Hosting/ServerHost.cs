using TerraNet.Agent;

namespace TerraNet.Core.Hosting;

public sealed class ServerHost(IProcessLauncher launcher)
{
    private const int ConsoleCapacity = 400;
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(45);

    private readonly Lock _gate = new();
    private readonly LinkedList<string> _console = new();
    private IServerProcess? _process;
    private bool _stopRequested;

    public event Action<ServerState>? StateChanged;
    public event Action<string>? ConsoleLine;

    public ServerState State { get; private set; } = ServerState.Idle;

    public IReadOnlyList<string> ConsoleHistory
    {
        get
        {
            lock (_gate)
                return _console.ToList();
        }
    }

    public Task<int> StartAsync(ProcessSpec spec)
    {
        lock (_gate)
        {
            if (_process is not null)
                throw new InvalidOperationException("server already running");
            _stopRequested = false;
            _console.Clear();
            Publish(new ServerState.Starting());
            Diagnostics.AppLog.Write("start " + spec.FileName + " " + string.Join(' ', spec.Arguments));
            _process = launcher.Start(spec);
            _process.Output += OnOutput;
            return WatchAsync(_process);
        }
    }

    public void Send(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;
        _process?.Send(command.Trim());
        Append("> " + command.Trim());
    }

    public async Task StopAsync()
    {
        IServerProcess? process;
        lock (_gate)
        {
            process = _process;
            if (process is null || _stopRequested)
                return;
            _stopRequested = true;
        }

        var wasGenerating = State is ServerState.Generating or ServerState.Starting;
        Publish(new ServerState.Stopping());
        if (wasGenerating)
            process.Kill();
        else
            process.Send("off");
        if (await Task.WhenAny(process.Exited, Task.Delay(StopTimeout)) != process.Exited)
            process.Kill();
        await process.Exited;
    }

    public void ReportFailure(Exception error)
    {
        Append(error.GetType().Name + ": " + error.Message);
        Diagnostics.AppLog.Error("server", error);
        lock (_gate)
            Publish(new ServerState.Crashed(-1));
    }

    private async Task<int> WatchAsync(IServerProcess process)
    {
        var code = await process.Exited;
        Diagnostics.AppLog.Write($"server exited {code}");
        lock (_gate)
        {
            process.Output -= OnOutput;
            process.Dispose();
            _process = null;
            Publish(_stopRequested || code == 0 ? ServerState.Idle : new ServerState.Crashed(code));
        }
        return code;
    }

    private void OnOutput(string line)
    {
        if (AgentWire.TryDecode(line) is { } agentEvent)
        {
            Apply(agentEvent);
            return;
        }
        Append(line);
        if (State is ServerState.Starting)
            Diagnostics.AppLog.Write("out " + line);
    }

    private void Apply(AgentEvent value)
    {
        lock (_gate)
        {
            if (_stopRequested)
                return;
            switch (value.Kind)
            {
                case AgentEventKind.Progress:
                    Publish(new ServerState.Generating(value.Progress ?? 0));
                    break;
                case AgentEventKind.Ready:
                    Publish(new ServerState.Running(value.World ?? "", value.MaxPlayers ?? 0, []));
                    break;
                case AgentEventKind.Players when State is ServerState.Running running:
                    Publish(running with { Players = value.Players ?? [] });
                    break;
            }
        }
    }

    private void Append(string line)
    {
        lock (_gate)
        {
            _console.AddLast(line);
            while (_console.Count > ConsoleCapacity)
                _console.RemoveFirst();
        }
        ConsoleLine?.Invoke(line);
    }

    private void Publish(ServerState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }
}
