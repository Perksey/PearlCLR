using Mono.Cecil;

namespace PearlCLR.JIT
{
    public static class MiniBCL
    {
        public static readonly TypeDefinition Int8Type = new TypeDefinition("System", "Int8",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);

        public static readonly TypeDefinition Int16Type = new TypeDefinition("System", "Int16",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);

        public static readonly TypeDefinition Int32Type = new TypeDefinition("System", "Int32",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);

        public static readonly TypeDefinition Int64Type = new TypeDefinition("System", "Int64",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);

        public static readonly TypeDefinition UInt8Type = new TypeDefinition("System", "UInt8",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);

        public static readonly TypeDefinition UInt16Type = new TypeDefinition("System", "UInt16",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);

        public static readonly TypeDefinition UInt32Type = new TypeDefinition("System", "UInt32",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);

        public static readonly TypeDefinition UInt64Type = new TypeDefinition("System", "UInt64",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);

        public static readonly TypeDefinition FloatType = new TypeDefinition("System", "Float",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);

        public static readonly TypeDefinition DoubleType = new TypeDefinition("System", "Double",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);

        public static readonly TypeDefinition DecimalType = new TypeDefinition("System", "Decimal",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);

        public static readonly TypeDefinition StringType = new TypeDefinition("System", "String",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);

        public static readonly TypeDefinition NativeIntType = new TypeDefinition("System", "IntPtr",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
        
        public static bool IsTypeAnInteger(TypeReference reference)
        {
            switch (reference.FullName)
            {
                case "System.SByte":
                case "System.Byte":
                case "System.Int16":
                case "System.UInt16":
                case "System.Int32":
                case "System.UInt32":
                case "System.Int64":
                case "System.UInt64":
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsTypeARealNumber(TypeReference reference)
        {
            switch (reference.FullName)
            {
                case "System.Float":
                case "System.Double":
                case "System.Decimal":
                    return true;
                default:
                    return false;
            }
        }
    }
}