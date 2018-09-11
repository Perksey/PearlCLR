using Mono.Cecil;

namespace PearlCLR.JIT
{
    public static class MiniBCL
    {
        public static TypeDefinition Int8Type = new TypeDefinition("System", "Int8", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static TypeDefinition Int16Type = new TypeDefinition("System", "Int16", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static TypeDefinition Int32Type = new TypeDefinition("System", "Int32", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static TypeDefinition Int64Type = new TypeDefinition("System", "Int64", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static TypeDefinition UInt8Type = new TypeDefinition("System", "UInt8", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static TypeDefinition UInt16Type = new TypeDefinition("System", "UInt16", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static TypeDefinition UInt32Type = new TypeDefinition("System", "UInt32", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static TypeDefinition UInt64Type = new TypeDefinition("System", "UInt64", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static TypeDefinition FloatType = new TypeDefinition("System", "Float", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static TypeDefinition DoubleType = new TypeDefinition("System", "Double", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static TypeDefinition StringType = new TypeDefinition("System", "String", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static TypeDefinition NativeIntType = new TypeDefinition("System", "IntPtr", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
    }
}