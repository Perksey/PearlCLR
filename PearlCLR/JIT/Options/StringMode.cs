namespace PearlCLR.JIT
{
    public enum StringMode
    {
        /// <summary>
        ///     Immutable string and is null terminated, requires strlen to obtain the size of string.
        /// </summary>
        CString,

        /// <summary>
        ///     Immutable string and is null terminated, but also includes Int32 length as metadata
        /// </summary>
        FixedLengthString
    }
}