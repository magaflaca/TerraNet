using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace TerraNet.PackTool;

public static class PlatformPatcher
{
    public static int Patch(string assemblyPath, string searchDirectory)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(searchDirectory);
        resolver.AddSearchDirectory(Path.GetDirectoryName(assemblyPath)!);

        var temp = assemblyPath + ".patched";
        int count;
        using (var module = ModuleDefinition.ReadModule(assemblyPath, new ReaderParameters { AssemblyResolver = resolver, InMemory = true }))
        {
            var operatingSystem = new TypeReference("System", "OperatingSystem", module, CoreScope(module));
            var isLinux = StaticBoolMethod(module, operatingSystem, nameof(OperatingSystem.IsLinux));
            var isAndroid = StaticBoolMethod(module, operatingSystem, nameof(OperatingSystem.IsAndroid));

            count = module.GetTypes()
                .SelectMany(t => t.Methods)
                .Where(m => m.HasBody)
                .Sum(m => PatchMethod(m, isLinux, isAndroid));

            if (count == 0)
                return 0;
            module.Write(temp);
        }

        File.Move(temp, assemblyPath, true);
        return count;
    }

    private static IMetadataScope CoreScope(ModuleDefinition module) =>
        module.AssemblyReferences.FirstOrDefault(r => r.Name == "System.Runtime")
        ?? module.TypeSystem.Boolean.Scope;

    private static MethodReference StaticBoolMethod(ModuleDefinition module, TypeReference type, string name) =>
        new(name, module.TypeSystem.Boolean, type) { HasThis = false };

    private static int PatchMethod(MethodDefinition method, MethodReference isLinux, MethodReference isAndroid)
    {
        var instructions = method.Body.Instructions;
        if (!instructions.Any(IsLinuxCheck))
            return 0;

        method.Body.SimplifyMacros();
        var count = 0;
        for (var i = 0; i < instructions.Count - 1; i++)
        {
            if (!IsLinuxCheck(instructions[i]))
                continue;

            var il = method.Body.GetILProcessor();
            instructions[i].OpCode = OpCodes.Call;
            instructions[i].Operand = isLinux;
            instructions[i + 1].OpCode = OpCodes.Call;
            instructions[i + 1].Operand = isAndroid;
            il.InsertAfter(instructions[i + 1], il.Create(OpCodes.Or));
            count++;
        }
        method.Body.OptimizeMacros();
        return count;
    }

    private static bool IsLinuxCheck(Instruction instruction) =>
        IsCall(instruction, "System.Runtime.InteropServices.OSPlatform", "get_Linux") &&
        instruction.Next is { } next &&
        IsCall(next, "System.Runtime.InteropServices.RuntimeInformation", "IsOSPlatform");

    private static bool IsCall(Instruction instruction, string type, string name) =>
        instruction.OpCode.Code is Code.Call or Code.Callvirt &&
        instruction.Operand is MethodReference m &&
        m.Name == name &&
        m.DeclaringType.FullName == type;
}
