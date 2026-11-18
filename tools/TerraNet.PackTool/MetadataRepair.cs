using Mono.Cecil;

namespace TerraNet.PackTool;

public static class MetadataRepair
{
    public static int RepairDefaults(string assemblyPath)
    {
        try
        {
            return Repair(assemblyPath);
        }
        catch (BadImageFormatException)
        {
            return 0;
        }
    }

    private static int Repair(string assemblyPath)
    {
        var repaired = 0;
        var temporary = assemblyPath + ".repaired";
        using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath))
        {
            var parameters = assembly.MainModule.GetTypes()
                .SelectMany(t => t.Methods)
                .SelectMany(m => m.Parameters)
                .Where(p => p.HasDefault && !p.HasConstant);
            foreach (var parameter in parameters)
            {
                parameter.HasDefault = false;
                repaired++;
            }
            if (repaired > 0)
                assembly.Write(temporary);
        }
        if (repaired > 0)
            File.Move(temporary, assemblyPath, true);
        return repaired;
    }
}
