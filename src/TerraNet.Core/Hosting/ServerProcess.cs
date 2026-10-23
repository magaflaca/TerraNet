namespace TerraNet.Core.Hosting;

public sealed record ProcessSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment);

public interface IServerProcess : IDisposable
{
    event Action<string>? Output;

    Task<int> Exited { get; }

    void Send(string line);

    void Kill();
}

public interface IProcessLauncher
{
    IServerProcess Start(ProcessSpec spec);
}
