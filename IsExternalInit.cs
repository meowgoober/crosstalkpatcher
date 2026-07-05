// C# 9+ record types (and init-only setters) require this class, which lives in
// System.Runtime.dll on .NET 5+. It doesn't exist in .NET Framework, so we
// provide a stub here.  The compiler only needs the type to exist; there is no
// runtime behaviour attached to it.

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
