using CommunityToolkit.Mvvm.ComponentModel;
using TerraNet.Core.Localization;
using TerraNet.Core.Worlds;

namespace TerraNet.App.ViewModels;

public sealed partial class WorldItemViewModel(WorldEntry entry) : ObservableObject
{
    public WorldEntry Entry { get; } = entry;
    public string Id => Entry.Profile.Id;
    public string Name => Entry.Profile.Name;
    public string SizeLabel => Loc.Get(Entry.Profile.Size.ToString().ToLowerInvariant());
    public string DifficultyLabel => Loc.Get(Entry.Profile.Difficulty.ToString().ToLowerInvariant());
    public string DifficultySprite => Choices.DifficultySprite(Entry.Profile.Difficulty);
    public string EvilSprite => Choices.EvilSprite(Entry.Profile.Evil);

    [ObservableProperty]
    private bool _isLive;
}
