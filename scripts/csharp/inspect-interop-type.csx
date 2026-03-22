#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

if (Args.Count < 3)
{
    Console.Error.WriteLine("Usage: inspect-interop-type.csx <assemblyPath> <typeName> <includeAllMembers> [memberPattern...]");
    return;
}

var assemblyPath = Path.GetFullPath(Args[0]);
var typeName = Args[1];
var includeAllMembers = bool.TryParse(Args[2], out var parsedIncludeAllMembers) && parsedIncludeAllMembers;
var memberPatterns = Args.Skip(3).Where(static pattern => !string.IsNullOrWhiteSpace(pattern)).ToArray();

if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
    return;
}

var assemblyDirectory = Path.GetDirectoryName(assemblyPath) ?? Directory.GetCurrentDirectory();
var bepInExRoot = Directory.GetParent(assemblyDirectory)?.FullName;
var probeDirectories = new[]
{
    assemblyDirectory,
    bepInExRoot == null ? null : Path.Combine(bepInExRoot, "core"),
    bepInExRoot == null ? null : Path.Combine(bepInExRoot, "interop"),
    bepInExRoot == null ? null : Path.Combine(bepInExRoot, "unity-libs")
}
    .Where(static path => !string.IsNullOrWhiteSpace(path))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

AppDomain.CurrentDomain.AssemblyResolve += (_, eventArgs) =>
{
    var assemblyName = new AssemblyName(eventArgs.Name).Name;
    if (string.IsNullOrWhiteSpace(assemblyName))
    {
        return null;
    }

    foreach (var probeDirectory in probeDirectories)
    {
        var candidate = Path.Combine(probeDirectory!, assemblyName + ".dll");
        if (File.Exists(candidate))
        {
            return Assembly.LoadFrom(candidate);
        }
    }

    return null;
};

var assembly = Assembly.LoadFrom(assemblyPath);
var type = ResolveType(assembly, typeName);
if (type == null)
{
    Console.Error.WriteLine($"Type not found: {typeName}");
    var suggestions = GetLoadableTypes(assembly)
        .Where(candidate =>
            (candidate.FullName?.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
            candidate.Name.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0)
        .Take(12)
        .Select(candidate => candidate.FullName ?? candidate.Name)
        .ToArray();

    if (suggestions.Length > 0)
    {
        Console.Error.WriteLine("Matches:");
        foreach (var suggestion in suggestions)
        {
            Console.Error.WriteLine($"  {suggestion}");
        }
    }

    return;
}

Console.WriteLine($"TYPE {type.FullName}");
Console.WriteLine($"ASSEMBLY {assembly.GetName().Name}");
Console.WriteLine($"FILTER {(includeAllMembers ? "<all>" : string.Join(", ", memberPatterns))}");
Console.WriteLine();

var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
WriteMembers(
    "FIELDS",
    type.GetFields(flags)
        .Where(field => Matches(field.Name))
        .OrderBy(field => field.Name)
        .Select(field => $"{GetVisibility(field)} {FormatTypeName(field.FieldType)} {field.Name}"));
WriteMembers(
    "PROPERTIES",
    type.GetProperties(flags)
        .Where(property => Matches(property.Name))
        .OrderBy(property => property.Name)
        .Select(property => $"{GetVisibility(property)} {FormatTypeName(property.PropertyType)} {property.Name} {{ {FormatPropertyAccess(property)} }}"));
WriteMembers(
    "METHODS",
    type.GetMethods(flags)
        .Where(method => !method.IsSpecialName)
        .Where(method => Matches(method.Name))
        .OrderBy(method => method.Name)
        .Select(method => $"{GetVisibility(method)} {FormatTypeName(method.ReturnType)} {method.Name}({FormatParameters(method.GetParameters())})"));

bool Matches(string value)
{
    if (includeAllMembers || memberPatterns.Length == 0)
    {
        return true;
    }

    return memberPatterns.Any(pattern => value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);
}

Type? ResolveType(Assembly targetAssembly, string requestedTypeName)
{
    return targetAssembly.GetType(requestedTypeName, throwOnError: false, ignoreCase: true) ??
        GetLoadableTypes(targetAssembly).FirstOrDefault(candidate =>
            string.Equals(candidate.FullName, requestedTypeName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.Name, requestedTypeName, StringComparison.OrdinalIgnoreCase));
}

IEnumerable<Type> GetLoadableTypes(Assembly targetAssembly)
{
    try
    {
        return targetAssembly.GetTypes();
    }
    catch (ReflectionTypeLoadException ex)
    {
        return ex.Types.Where(static type => type != null)!;
    }
}

void WriteMembers(string label, IEnumerable<string> lines)
{
    Console.WriteLine(label);
    var any = false;
    foreach (var line in lines)
    {
        any = true;
        Console.WriteLine($"  {line}");
    }

    if (!any)
    {
        Console.WriteLine("  <none>");
    }

    Console.WriteLine();
}

string GetVisibility(FieldInfo field)
{
    if (field.IsPublic)
    {
        return "public";
    }

    if (field.IsPrivate)
    {
        return "private";
    }

    if (field.IsFamily)
    {
        return "protected";
    }

    if (field.IsAssembly)
    {
        return "internal";
    }

    return "nonpublic";
}

string GetVisibility(MethodBase method)
{
    if (method.IsPublic)
    {
        return "public";
    }

    if (method.IsPrivate)
    {
        return "private";
    }

    if (method.IsFamily)
    {
        return "protected";
    }

    if (method.IsAssembly)
    {
        return "internal";
    }

    return "nonpublic";
}

string GetVisibility(PropertyInfo property)
{
    var accessor = property.GetMethod ?? property.SetMethod;
    return accessor == null ? "nonpublic" : GetVisibility(accessor);
}

string FormatPropertyAccess(PropertyInfo property)
{
    var accessors = new List<string>();
    if (property.GetMethod != null)
    {
        accessors.Add("get;");
    }

    if (property.SetMethod != null)
    {
        accessors.Add("set;");
    }

    return string.Join(" ", accessors);
}

string FormatParameters(IEnumerable<ParameterInfo> parameters)
{
    return string.Join(", ", parameters.Select(parameter => $"{FormatTypeName(parameter.ParameterType)} {parameter.Name}"));
}

string FormatTypeName(Type typeToFormat)
{
    if (typeToFormat.IsByRef)
    {
        return $"{FormatTypeName(typeToFormat.GetElementType()!)}&";
    }

    if (typeToFormat.IsArray)
    {
        return $"{FormatTypeName(typeToFormat.GetElementType()!)}[]";
    }

    if (!typeToFormat.IsGenericType)
    {
        return typeToFormat.Name;
    }

    var baseName = typeToFormat.Name;
    var tickIndex = baseName.IndexOf('`');
    if (tickIndex >= 0)
    {
        baseName = baseName.Substring(0, tickIndex);
    }

    var genericArguments = string.Join(", ", typeToFormat.GetGenericArguments().Select(FormatTypeName));
    return $"{baseName}<{genericArguments}>";
}
