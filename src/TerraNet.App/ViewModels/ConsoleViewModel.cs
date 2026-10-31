using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TerraNet.Core.Session;

namespace TerraNet.App.ViewModels;

public sealed partial class ConsoleViewModel : ObservableObject
{
    private const int Capacity = 400;
    private readonly HostSession _session;

    public ConsoleViewModel(HostSession session)
    {
        _session = session;
        foreach (var line in session.Server.ConsoleHistory)
            Lines.Add(line);
        session.Server.ConsoleLine += OnLine;
    }

    public ObservableCollection<string> Lines { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _input = "";

    public event Action? LineAdded;

    private void OnLine(string line) => Dispatcher.UIThread.Post(() =>
    {
        Lines.Add(line);
        while (Lines.Count > Capacity)
            Lines.RemoveAt(0);
        LineAdded?.Invoke();
    });

    private bool CanSend() => !string.IsNullOrWhiteSpace(Input);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private void Send()
    {
        _session.Server.Send(Input);
        Input = "";
    }

    public void Detach() => _session.Server.ConsoleLine -= OnLine;
}
