using Mono.Cecil;

namespace PearlCLR.JIT
{
    public static class MiniBCL
    {
        public static readonly TypeDefinition Int8Type = new TypeDefinition("System", "Int8", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static readonly TypeDefinition Int16Type = new TypeDefinition("System", "Int16", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static readonly TypeDefinition Int32Type = new TypeDefinition("System", "Int32", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static readonly TypeDefinition Int64Type = new TypeDefinition("System", "Int64", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static readonly TypeDefinition UInt8Type = new TypeDefinition("System", "UInt8", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static readonly TypeDefinition UInt16Type = new TypeDefinition("System", "UInt16", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static readonly TypeDefinition UInt32Type = new TypeDefinition("System", "UInt32", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static readonly TypeDefinition UInt64Type = new TypeDefinition("System", "UInt64", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static readonly TypeDefinition FloatType = new TypeDefinition("System", "Float", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static readonly TypeDefinition DoubleType = new TypeDefinition("System", "Double", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static readonly TypeDefinition DecimalType = new TypeDefinition("System", "Decimal", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static readonly TypeDefinition StringType = new TypeDefinition("System", "String", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        public static readonly TypeDefinition NativeIntType = new TypeDefinition("System", "IntPtr", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
    }
}