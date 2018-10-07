using System;
using LLVMSharp;

namespace PearlCLR
{
    public static class TypeConversionHelper
    {
        public static LLVMValueRef AutoCast(LLVMBuilderRef builder, LLVMValueRef val, LLVMTypeRef type)
        {
            // Int to Int
            if (val.TypeOf().TypeKind == LLVMTypeKind.LLVMIntegerTypeKind &&
                type.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
                return LLVM.BuildZExt(builder, val, type, "");

            // Int to Floating Point
            if (val.TypeOf().TypeKind == LLVMTypeKind.LLVMIntegerTypeKind &&
                type.TypeKind == LLVMTypeKind.LLVMFloatTypeKind)
                return LLVM.BuildSIToFP(builder, val, type, "");

            // Floating Point to Int
            if (val.TypeOf().TypeKind == LLVMTypeKind.LLVMIntegerTypeKind &&
                type.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
                return LLVM.BuildFPToSI(builder, val, type, "");

            // Floating Point to Floating Point
            if (val.TypeOf().TypeKind == LLVMTypeKind.LLVMIntegerTypeKind &&
                type.TypeKind == LLVMTypeKind.LLVMFloatTypeKind)
                return LLVM.BuildFPCast(builder, val, type, "");

            throw new Exception($"Unsupported cast for {val.TypeOf().TypeKind} to {type.TypeKind}");
        }
    }
}