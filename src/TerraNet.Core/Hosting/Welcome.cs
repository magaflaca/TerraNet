using TerraNet.Core.Localization;

namespace TerraNet.Core.Hosting;

public static class Welcome
{
    public const string ProjectUrl = "terra.hacks.win";

    public static string Default => Loc.Format("welcomeDefault", ProjectUrl);
}
