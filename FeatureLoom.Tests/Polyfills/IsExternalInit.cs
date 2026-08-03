// Polyfill required so that C# records / init-only setters compile on down-level targets
// (e.g. .NET Framework 4.8) that do not ship System.Runtime.CompilerServices.IsExternalInit.
#if NETFRAMEWORK || NETSTANDARD2_0
using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
#endif
