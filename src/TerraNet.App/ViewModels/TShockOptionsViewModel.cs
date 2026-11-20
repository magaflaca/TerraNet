using System.Globalization;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TerraNet.App.Services;
using TerraNet.Core.Hosting;
using TerraNet.Core.Localization;
using TerraNet.Core.Settings;

namespace TerraNet.App.ViewModels;

public sealed partial class TShockOptionsViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly IReadOnlyList<OptionSectionViewModel> _all;

    public TShockOptionsViewModel(MainViewModel main)
    {
        _main = main;
        var settings = main.Host.Settings;
        _all = TShockOptions.All
            .GroupBy(o => o.Section)
            .Select(g => new OptionSectionViewModel(g.Key, g.Select(o => new OptionItemViewModel(o, settings)).ToList()))
            .ToList();
        _visible = _all;
    }

    [ObservableProperty]
    private IReadOnlyList<OptionSectionViewModel> _visible;

    [ObservableProperty]
    private string _search = "";

    partial void OnSearchChanged(string value)
    {
        var query = value.Trim();
        Visible = query.Length == 0
            ? _all
            : _all
                .Select(s => s.Filter(query))
                .Where(s => s.Items.Count > 0)
                .ToList();
    }

    [RelayCommand]
    private async Task ResetAll()
    {
        if (!await _main.ConfirmAsync(Loc.Get("resetOptions"), Loc.Get("reset")))
            return;
        _main.Host.Settings.Update(s => s with { TShockOverrides = new Dictionary<string, string>() });
        foreach (var item in _all.SelectMany(s => s.Items))
            item.Refresh();
        Sfx.Play(Sound.Toggle);
    }
}

public sealed record OptionSectionViewModel(string Key, IReadOnlyList<OptionItemViewModel> Items)
{
    public string Title => Loc.Get("section." + Key);
    public string Sprite => Key switch
    {
        "world" => "world",
        "players" => "person",
        "accounts" => "lock",
        "ssc" => "chest",
        "protection" => "crown",
        "chat" => "console",
        _ => "sparkle"
    };

    public OptionSectionViewModel Filter(string query) =>
        this with { Items = Items.Where(i => i.Matches(query)).ToList() };
}

public sealed partial class OptionItemViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private bool _loading;

    public OptionItemViewModel(TShockOption option, SettingsService settings)
    {
        Option = option;
        _settings = settings;
        Choices = option.Options?.Select(o => new Choice(o, Loc.Get("value." + o), "sparkle")).ToList() ?? [];
        Refresh();
    }

    public TShockOption Option { get; }
    public string Label => Option.Label;
    public string Hint => Option.Hint;
    public bool HasHint => Hint.Length > 0;
    public bool IsBool => Option.Type == TShockOptionType.Bool;
    public bool IsInt => Option.Type == TShockOptionType.Int;
    public bool IsText => Option.Type == TShockOptionType.Text;
    public bool IsChoice => Option.Type == TShockOptionType.Choice;
    public bool IsField => IsInt || IsText;
    public IReadOnlyList<Choice> Choices { get; }

    [ObservableProperty]
    private bool _boolValue;

    [ObservableProperty]
    private string _textValue = "";

    [ObservableProperty]
    private Choice? _choiceValue;

    [ObservableProperty]
    private bool _isCustom;

    public bool Matches(string query) =>
        Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        Hint.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        Option.Key.Contains(query, StringComparison.OrdinalIgnoreCase);

    public void Refresh()
    {
        _loading = true;
        var current = _settings.Current;
        var value = TShockConfig.Effective(current, Option);
        IsCustom = current.TShockOverrides.ContainsKey(Option.Id);
        switch (Option.Type)
        {
            case TShockOptionType.Bool:
                BoolValue = value?.GetValue<bool>() ?? false;
                break;
            case TShockOptionType.Choice:
                ChoiceValue = Choices.FirstOrDefault(c => (string)c.Value == value?.GetValue<string>()) ?? Choices.FirstOrDefault();
                break;
            default:
                TextValue = value?.ToString() ?? "";
                break;
        }
        _loading = false;
    }

    partial void OnBoolValueChanged(bool value) => Store(JsonValue.Create(value));

    partial void OnChoiceValueChanged(Choice? value)
    {
        if (value is not null)
            Store(JsonValue.Create((string)value.Value));
    }

    partial void OnTextValueChanged(string value)
    {
        if (IsInt)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number >= 0)
                Store(JsonValue.Create(number));
        }
        else
            Store(JsonValue.Create(value));
    }

    [RelayCommand]
    private void Reset()
    {
        _settings.Update(s => s with { TShockOverrides = Without(s.TShockOverrides) });
        Refresh();
        Sfx.Play(Sound.Toggle);
    }

    private void Store(JsonNode node)
    {
        if (_loading)
            return;
        _settings.Update(s =>
        {
            var baseline = TShockConfig.Effective(s with { TShockOverrides = Without(s.TShockOverrides) }, Option);
            var overrides = Without(s.TShockOverrides);
            if (!JsonNode.DeepEquals(baseline, node))
                overrides[Option.Id] = node.ToJsonString();
            return s with { TShockOverrides = overrides };
        });
        IsCustom = _settings.Current.TShockOverrides.ContainsKey(Option.Id);
    }

    private Dictionary<string, string> Without(IReadOnlyDictionary<string, string> overrides)
    {
        var copy = new Dictionary<string, string>(overrides);
        copy.Remove(Option.Id);
        return copy;
    }
}
