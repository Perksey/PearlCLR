namespace PearlCLR.JIT
{
    public class JITCompilerOptions
    {
        public MetadataTypeHandlingMode MetadataTypeHandlingModeOption { get; set; } =
            MetadataTypeHandlingMode.Full_Fixed;

        /// <summary>
        ///     Size of Metadata Overhead for Type Handle and Synchronization Context, this is used for indexing purpose.
        ///     Currently default to 32 bit integer.
        /// </summary>
        public uint MetadataFixedLength { get; set; } = 32;

        public StringMode CLRStringMode { get; set; } = StringMode.FixedLengthString;
    }
}