using LLVMSharp;
using Mono.Cecil;

namespace PearlCLR.JIT
{
    /// <summary>
    ///     Contains the information of type of value pushed into stack and it's value for either Value or Type.
    /// </summary>
    public struct BuilderStackItem
    {
        public BuilderStackItem(TypeReference type, LLVMValueRef valref)
        {
            Type = type;
            ValRef = valref;
            TypeRef = null;
        }

        public BuilderStackItem(TypeReference type, LLVMTypeRef typeref)
        {
            Type = type;
            ValRef = null;
            TypeRef = typeref;
        }

        public TypeReference Type;
        public LLVMValueRef? ValRef;
        public LLVMTypeRef? TypeRef;
    }
}