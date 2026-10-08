namespace TerraNet.PackTool;

public sealed class TShockSource(string cacheRoot)
{
    public async Task<string> PublishAsync(string tag)
    {
        var output = Path.Combine(cacheRoot, "tshock", tag, "publish");
        if (File.Exists(Path.Combine(output, "TShock.Server.dll")))
            return output;

        var source = Path.Combine(cacheRoot, "tshock", tag, "src");
        if (!Directory.Exists(source))
            await Shell.RunAsync("git", $"clone --quiet --depth 1 --branch {tag} --recurse-submodules --shallow-submodules https://github.com/Pryaxis/TShock.git \"{source}\"");

        await Shell.RunAsync("dotnet", $"publish \"{Path.Combine(source, "TShockLauncher", "TShockLauncher.csproj")}\" -c Release -p:PublishSingleFile=false -p:UseAppHost=false -o \"{output}\"");
        return output;
    }
}
