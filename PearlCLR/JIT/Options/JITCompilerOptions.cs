namespace PearlCLR.JIT
{
    public class JITCompilerOptions
    {
        public MetadataTypeHandlingMode MetadataTypeHandlingModeOption { get; set; } =
            MetadataTypeHandlingMode.Full_Fixed;

        public uint MetadataFixedLength { get; set; } = 32;

        public StringMode CLRStringMode { get; set; } = StringMode.FixedLengthString;
    }
}