using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TerraNet.App.Services;
using TerraNet.Core.Localization;
using TerraNet.Core.Worlds;

namespace TerraNet.App.ViewModels;

public sealed partial class WorldEditorViewModel(AppHost host, MainViewModel main) : ObservableObject
{
    private const int MaxNameLength = 27;

    public IReadOnlyList<Choice> Sizes => Choices.Sizes;
    public IReadOnlyList<Choice> Difficulties => Choices.Difficulties;
    public IReadOnlyList<Choice> Evils => Choices.Evils;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _name = WorldNames.Random();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHeavyHint), nameof(HeavyHint))]
    private Choice _size = Choices.Find(Choices.Sizes, WorldSize.Small);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHeavyHint))]
    private WorldSize? _acceptedSize;

    private WorldSize SelectedSize => (WorldSize)Size.Value;

    public bool ShowHeavyHint => host.Platform.Tier.IsHeavy(SelectedSize) && AcceptedSize != SelectedSize;

    public string HeavyHint => Loc.Format("heavyHint", Size.Label.ToLowerInvariant());

    [RelayCommand]
    private void UseSmall()
    {
        Size = Choices.Find(Choices.Sizes, WorldSize.Small);
        Sfx.Play(Sound.Select);
    }

    [RelayCommand]
    private void KeepSize()
    {
        AcceptedSize = SelectedSize;
        Sfx.Play(Sound.Click);
    }

    [ObservableProperty]
    private Choice _difficulty = Choices.Find(Choices.Difficulties, WorldDifficulty.Classic);

    [ObservableProperty]
    private Choice _evil = Choices.Find(Choices.Evils, WorldEvil.Random);

    [ObservableProperty]
    private string _seed = "";

    [RelayCommand]
    private void Randomize() => Name = WorldNames.Random();

    private bool CanCreate() => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void Create()
    {
        var name = Name.Trim();
        host.Worlds.Create(new WorldProfile
        {
            Name = name.Length > MaxNameLength ? name[..MaxNameLength] : name,
            Size = SelectedSize,
            Difficulty = (WorldDifficulty)Difficulty.Value,
            Evil = (WorldEvil)Evil.Value,
            Seed = Seed.Trim()
        });
        Sfx.Play(Sound.Create);
        main.Back();
    }
}
