using System;
using System.Collections.Generic;
using LLVMSharp;
using Mono.Cecil;

namespace PearlCLR.JIT
{
    internal class LLVMTypeResolver
    {
        private readonly bool _cStringMode;
        private readonly Dictionary<string, StructDefinition> _fullSymbolToTypeRef;
        private readonly Dictionary<string, Func<LLVMTypeRef>> _mapping;

        public LLVMTypeResolver(bool cStringMode, Dictionary<string, StructDefinition> fullSymbolToTypeRef)
        {
            _cStringMode = cStringMode;
            _fullSymbolToTypeRef = fullSymbolToTypeRef;

            _mapping = new Dictionary<string, Func<LLVMTypeRef>>()
            {
                {TypeReferenceName.Boolean, LLVM.Int8Type},
                {TypeReferenceName.SByte, LLVM.Int8Type},
                {TypeReferenceName.Int8, LLVM.Int8Type},
                {TypeReferenceName.Int16, LLVM.Int16Type},
                {TypeReferenceName.Int32, LLVM.Int32Type},
                {TypeReferenceName.Int64, LLVM.Int64Type},
                {TypeReferenceName.Byte, LLVM.Int8Type},
                {TypeReferenceName.UInt8, LLVM.Int8Type},
                {TypeReferenceName.UInt16, LLVM.Int16Type},
                {TypeReferenceName.UInt32, LLVM.Int32Type},
                {TypeReferenceName.UInt64, LLVM.Int64Type},
                {TypeReferenceName.Float, LLVM.FloatType},
                {TypeReferenceName.Double, LLVM.DoubleType},
                //TODO, StopbeinglazywithFP128Type
                {TypeReferenceName.Decimal, LLVM.FP128Type}
            };
        }

        public LLVMTypeRef Resolve(TypeReference def)
        {
            if (def.IsPointer)
            {
                var definedType = Resolve(def.Resolve());
                for (var i = def.Name.Length - 1; i > -1; --i)
                    if (def.Name[i] == '*')
                        definedType = LLVM.PointerType(definedType, 0);
                    else
                        break;
                return definedType;
            }

            if (def.FullName == TypeReferenceName.Void || def.FullName == TypeReferenceName.Type)
            {
                return LLVM.VoidType();
            }

            if (def.IsPrimitive)
            {
                if (!_mapping.ContainsKey(def.FullName)) throw new Exception("Unhandled Type for Primitive!" + def.FullName);
                return _mapping[def.FullName]();
            }

            if (def.FullName == TypeReferenceName.String)
            {
                if (_cStringMode)
                    // This is a special case, all string in this CLR is null terminated.
                    return LLVM.PointerType(LLVM.Int8Type(), 0);
            }

            if (_fullSymbolToTypeRef.TryGetValue(def.FullName, out var val))
            {
                return def.Resolve().IsClass ? LLVM.PointerType(val.StructTypeRef, 0) : val.StructTypeRef;
            }

            throw new NotSupportedException($"Unhandled Type. {def.FullName} {def.DeclaringType}");
        }


        private static class TypeReferenceName
        {
            public const string Void = "System.Void";
            public const string Type = "System.Type";
            public const string String = "System.String";
            public const string Boolean = "System.Boolean";
            public const string SByte = "System.SByte";
            public const string Int8 = "System.Int8";
            public const string Int16 = "System.Int16";
            public const string Int32 = "System.Int32";
            public const string Int64 = "System.Int64";
            public const string Byte = "System.Byte";
            public const string UInt8 = "System.UInt8";
            public const string UInt16 = "System.UInt16";
            public const string UInt32 = "System.UInt32";
            public const string UInt64 = "System.UInt64";
            public const string Float = "System.Float";
            public const string Double = "System.Double";
            public const string Decimal = "System.Decimal";
        }
    }
}