namespace PearlCLR.JIT
{
    public class JITCompilerOptions
    {
        public MetadataTypeHandlingMode MetadataTypeHandlingModeOption { get; set; } =
            MetadataTypeHandlingMode.Full_Fixed;

        public uint MetadataFixedLength { get; set; } = 32;

        /// <summary>
        /// A switch for Null-Terminated String Mode basically, C String.
        /// Count property for string would be a getter function that run strlen on string address
        /// The string type itself is just a pointer to global string (after all, it's immutable.)
        /// </summary>
        public bool CStringMode = true;
    }
}