namespace TerraNet.PackTool;

public static class AgentBuilder
{
    public static async Task<string> BuildAsync(string repo, string tshock, string cache)
    {
        var project = Path.Combine(repo, "src", "TerraNet.ServerAgent", "TerraNet.ServerAgent.csproj");
        var output = Path.Combine(cache, "agent");
        await Shell.RunAsync("dotnet", $"build \"{project}\" -c Release -o \"{output}\" -p:TShockDir=\"{tshock}\"");
        return Path.Combine(output, "TerraNet.ServerAgent.dll");
    }
}
