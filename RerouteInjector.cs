using AsmResolver.PE;
using AsmResolver.PE.Imports;
using System.Reflection;

namespace CrossTalkPatcher;

/// AsmResolver was a pain to get working with, i tried to even to get to test with Yahoo Messenger but it wouldnt, when it did this program told me that it will only do .NET programs, i have NEVER seen a .net program that uses PE in its own other than retro messaging client, i hate this stupid plugin so much and this was a pain, this took me 2 whole days, and it was acting bad. This has been moved to Liefimportinjector.cs. - corncat 
public static class RerouteInjector
{
    public static void AddRerouteImport(string exePath, string dllName = "reroute.dll", string functionName = "ImportMe")
    {
        if (!File.Exists(exePath))
            throw new FileNotFoundException("Target executable not found", exePath);

        string backup = exePath + ".bak";
        try
        {
            if (!File.Exists(backup))
            {
                File.Copy(exePath, backup);
                Console.WriteLine($"Backup saved to {backup}");
            }
            else
            {
                Console.WriteLine($"Backup already exists at {backup} (not overwriting)");
            }
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException(
                $"Access denied writing to \"{Path.GetDirectoryName(exePath)}\". " +
                "Re-run this program as Administrator (right-click your terminal -> Run as administrator).");
        }

        var image = PEImage.FromFile(exePath);

        var module = image.Imports.FirstOrDefault(m => m.Name == dllName);
        if (module == null)
        {
            module = new ImportedModule(dllName);
            image.Imports.Add(module);
            Console.WriteLine($"Added new import module: {dllName}");
        }

        bool alreadyImported = module.Symbols.Any(s => s.Name == functionName);
        if (!alreadyImported)
        {
            module.Symbols.Add(new ImportedSymbol(0, functionName));
            Console.WriteLine($"Added import: {dllName}!{functionName}");
        }
        else
        {
            Console.WriteLine($"{dllName}!{functionName} is already imported - nothing to add.");
        }

        BuildAndWrite(image, exePath);
        Console.WriteLine($"Saved changes to {Path.GetFileName(exePath)}");
    }

    private static void BuildAndWrite(PEImage image, string exePath)
    {
        object? managedBuilder = TryCreateBuilderByName("ManagedPEFileBuilder");
        if (managedBuilder != null)
        {
            try
            {
                object builtFile = InvokeCreateFile(managedBuilder, image);
                WriteFile(builtFile, exePath);
                return;
            }
            catch (Exception ex)
            {
                string reason = Unwrap(ex).Message;
                Console.WriteLine($"Managed PE builder didn't work ({reason}) - trying a native-capable PE builder instead.");
            }
        }
        else
        {
            Console.WriteLine("ManagedPEFileBuilder not found in this AsmResolver version - going straight to a native-capable PE builder.");
        }

        object nativeBuilder = CreateNativeCapableBuilder();
        Console.WriteLine($"Using {nativeBuilder.GetType().FullName}.");
        SetPropertyIfExists(nativeBuilder, "TrampolineImports", true);

        try
        {
            object nativeBuiltFile = InvokeCreateFile(nativeBuilder, image);
            WriteFile(nativeBuiltFile, exePath);
        }
        catch (Exception ex)
        {
            throw Unwrap(ex);
        }
    }

    private static void SetPropertyIfExists(object obj, string propertyName, object value)
    {
        var prop = obj.GetType().GetProperty(propertyName);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(obj, value);
            Console.WriteLine($"Set {obj.GetType().Name}.{propertyName} = {value}");
        }
        else
        {
            Console.WriteLine($"Warning: {obj.GetType().Name} has no writable '{propertyName}' property - the import table may not actually be rebuilt.");
        }
    }

    /// Reflection wraps every exception in a TargetInvocationException whose
    /// own Message is just "Exception has been thrown by the target of an
    /// invocation." - this digs out the real underlying exception so error
    /// messages are actually useful.
    /// blah blah blah, you already know what exceptions are anyway.

    private static Exception Unwrap(Exception ex) =>
        ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;

    private static object InvokeCreateFile(object builder, PEImage image)
    {
        var method = builder.GetType().GetMethod("CreateFile", new[] { typeof(PEImage) })
            ?? throw new InvalidOperationException($"{builder.GetType().FullName} has no CreateFile(PEImage) method.");

        return method.Invoke(builder, new object[] { image })
            ?? throw new InvalidOperationException("CreateFile returned null.");
    }

    private static void WriteFile(object peFile, string exePath)
    {
        var writeMethod = peFile.GetType().GetMethod("Write", new[] { typeof(string) })
            ?? throw new InvalidOperationException($"{peFile.GetType().FullName} has no Write(string) method.");

        try
        {
            writeMethod.Invoke(peFile, new object[] { exePath });
        }
        catch (Exception ex)
        {
            var real = Unwrap(ex);
            if (real is UnauthorizedAccessException)
            {
                throw new UnauthorizedAccessException(
                    $"Access denied writing to \"{exePath}\". " +
                    "Re-run this program as Administrator (right-click your terminal -> Run as administrator).");
            }
            throw real;
        }
    }

    private static object? TryCreateBuilderByName(string typeName)
    {
        var asm = typeof(PEImage).Assembly;
        var type = asm.GetTypes().FirstOrDefault(t => t.Name == typeName && !t.IsAbstract && !t.IsInterface);
        var ctor = type?.GetConstructor(Type.EmptyTypes);
        return ctor?.Invoke(null);
    }

    private static object CreateNativeCapableBuilder()
    {
        var asm = typeof(PEImage).Assembly;
        var candidates = asm.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => t.Name.EndsWith("PEFileBuilder"))
            .Where(t => t.Name != "ManagedPEFileBuilder")
            .ToList();

        var builderType = candidates.FirstOrDefault(t => t.Name.Contains("Templated"))
            ?? candidates.FirstOrDefault(t => t.Name.Contains("Unmanaged"))
            ?? candidates.FirstOrDefault();

        if (builderType == null)
        {
            throw new InvalidOperationException(
                "No native-capable PE file builder found in AsmResolver.PE. " +
                "This requires AsmResolver.PE 6.0.0 or newer.");
        }

        var ctor = builderType.GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException($"{builderType.FullName} has no parameterless constructor.");

        return ctor.Invoke(null);
    }
}