using System.Runtime.InteropServices;

/// <summary>
///     This is for PearlCLR specific functionalities that include, but not limited to manual memory manipulation
///     and management.
/// </summary>
public class PearlCLRExtensions
{
    /// <summary>
    ///     Free Allocated Memory - For any boxed/reference type objects. Would do nothing for value types.
    /// </summary>
    /// <param name="instance">Reference Type Instance</param>
    [DllImport("__internal")]
    public static extern void Free(object instance);
}