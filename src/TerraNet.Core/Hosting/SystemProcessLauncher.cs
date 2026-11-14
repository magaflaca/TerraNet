using System.Diagnostics;
using System.Text;

namespace TerraNet.Core.Hosting;

public sealed class SystemProcessLauncher : IProcessLauncher
{
    public IServerProcess Start(ProcessSpec spec)
    {
        var info = new ProcessStartInfo(spec.FileName)
        {
            WorkingDirectory = spec.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in spec.Arguments)
            info.ArgumentList.Add(argument);
        foreach (var (key, value) in spec.Environment)
            info.Environment[key] = value;

        var process = Process.Start(info) ?? throw new InvalidOperationException("server process did not start");
        return new SystemServerProcess(process);
    }

    private sealed class SystemServerProcess : IServerProcess
    {
        private readonly Process _process;
        private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SystemServerProcess(Process process)
        {
            _process = process;
            var pumps = Task.WhenAll(
                LineReader.PumpAsync(process.StandardOutput, Raise),
                LineReader.PumpAsync(process.StandardError, Raise));
            _ = Task.Run(async () =>
            {
                await process.WaitForExitAsync();
                await pumps.WaitAsync(TimeSpan.FromSeconds(2)).ContinueWith(_ => { });
                _exited.TrySetResult(process.ExitCode);
            });
        }

        public event Action<string>? Output;

        public int Id => _process.Id;

        public Task<int> Exited => _exited.Task;

        public void Send(string line)
        {
            try
            {
                _process.StandardInput.WriteLine(line);
                _process.StandardInput.Flush();
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
            }
        }

        public void Kill()
        {
            try
            {
                _process.Kill(true);
            }
            catch (InvalidOperationException)
            {
            }
        }

        public void Dispose() => _process.Dispose();

        private void Raise(string line) => Output?.Invoke(line);
    }
}
