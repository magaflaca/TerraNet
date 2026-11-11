using Mono.Cecil;

namespace TerraNet.PackTool;

public static class StubAssemblies
{
    private static readonly HashSet<string> ClientOnly = ["Steamworks.NET", "RailSDK.Net", "Ionic.Zip.CF", "CsvHelper"];

    public static IReadOnlyList<(string Replacement, string Original)> Generate(IEnumerable<string> assemblies, IReadOnlyList<string> searchDirectories, string output)
    {
        var replacements = new List<(string, string)>();
        Directory.CreateDirectory(output);
        var files = searchDirectories
            .SelectMany(d => Directory.EnumerateFiles(d, "*.dll"))
            .Select(f => (Path: f, Name: TryReadName(f)))
            .Where(f => f.Name is not null)
            .GroupBy(f => f.Name!.Name)
            .ToDictionary(g => g.Key, g => g.First());
        var available = files.ToDictionary(f => f.Key, f => f.Value.Name!.Version);

        var stubs = new Dictionary<string, (AssemblyNameReference Name, Dictionary<string, TypeReference> Types)>();
        foreach (var path in assemblies)
        {
            using var module = ModuleDefinition.ReadModule(path);
            foreach (var reference in module.AssemblyReferences)
            {
                if (!ClientOnly.Contains(reference.Name) && available.TryGetValue(reference.Name, out var version) && version >= reference.Version)
                    continue;
                if (!stubs.ContainsKey(reference.Name))
                    stubs[reference.Name] = (reference, []);
            }
            foreach (var type in module.GetTypeReferences())
                if (Root(type).Scope is AssemblyNameReference scope && stubs.TryGetValue(scope.Name, out var stub))
                    stub.Types.TryAdd(type.FullName, type);
        }

        foreach (var (name, (reference, types)) in stubs)
        {
            if (files.TryGetValue(name, out var real) && real.Name!.Version == reference.Version)
            {
                Console.WriteLine($"  retarget {name} {reference.Version}");
                Retarget(real.Path, Path.Combine(output, name + ".dll"));
                replacements.Add((Path.Combine(output, name + ".dll"), real.Path));
                continue;
            }
            Console.WriteLine($"  stub {name} {reference.Version} ({types.Count} types)");
            Write(reference, types.Values, Path.Combine(output, name + ".dll"));
        }
        return replacements;
    }

    private static void Write(AssemblyNameReference reference, IEnumerable<TypeReference> types, string path)
    {
        var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(reference.Name, reference.Version), reference.Name, ModuleKind.Dll);
        var module = assembly.MainModule;
        var runtime = new AssemblyNameReference("System.Runtime", new Version(9, 0, 0, 0));
        module.AssemblyReferences.Add(runtime);
        var objectType = new TypeReference("System", "Object", module, runtime);
        var valueType = new TypeReference("System", "ValueType", module, runtime);
        var int64Type = new TypeReference("System", "Int64", module, runtime, true);
        var created = new Dictionary<string, TypeDefinition>();

        TypeDefinition Define(TypeReference type)
        {
            if (created.TryGetValue(type.FullName, out var existing))
                return existing;
            var baseType = type.IsValueType ? valueType : objectType;
            var definition = new TypeDefinition(type.Namespace, type.Name,
                (type.DeclaringType is null ? TypeAttributes.Public : TypeAttributes.NestedPublic) | (type.IsValueType ? TypeAttributes.Sealed : 0),
                baseType);
            if (type.IsValueType)
                definition.Fields.Add(new FieldDefinition("_value", FieldAttributes.Private, int64Type));
            for (var i = 0; i < type.GenericParameters.Count; i++)
                definition.GenericParameters.Add(new GenericParameter(type.GenericParameters[i].Name, definition));
            created[type.FullName] = definition;
            if (type.DeclaringType is { } parent)
                Define(parent).NestedTypes.Add(definition);
            else
                module.Types.Add(definition);
            return definition;
        }

        foreach (var type in types)
            Define(type);
        assembly.Write(path);
    }

    private static void Retarget(string source, string destination)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(source);
        foreach (var attribute in assembly.CustomAttributes.Where(a => a.AttributeType.Name == "ReferenceAssemblyAttribute").ToList())
            assembly.CustomAttributes.Remove(attribute);
        assembly.MainModule.Architecture = TargetArchitecture.I386;
        assembly.MainModule.Attributes = ModuleAttributes.ILOnly;
        assembly.Write(destination);
    }

    private static TypeReference Root(TypeReference type)
    {
        while (type.DeclaringType is not null)
            type = type.DeclaringType;
        return type;
    }

    private static AssemblyNameDefinition? TryReadName(string path)
    {
        try
        {
            using var assembly = AssemblyDefinition.ReadAssembly(path);
            return assembly.Name;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }
}
