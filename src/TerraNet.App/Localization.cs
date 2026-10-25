using Avalonia.Markup.Xaml;
using TerraNet.Core.Localization;

namespace TerraNet.App;

public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.Get(Key);
}
