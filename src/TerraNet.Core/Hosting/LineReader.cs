using System.Text;

namespace TerraNet.Core.Hosting;

public static class LineReader
{
    public static async Task PumpAsync(TextReader reader, Action<string> onLine)
    {
        var buffer = new char[4096];
        var line = new StringBuilder();
        int read;
        while ((read = await reader.ReadAsync(buffer)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var c = buffer[i];
                if (c is '\n' or '\r')
                {
                    if (line.Length > 0)
                        onLine(line.ToString());
                    line.Clear();
                }
                else
                {
                    line.Append(c);
                }
            }
        }
        if (line.Length > 0)
            onLine(line.ToString());
    }
}
