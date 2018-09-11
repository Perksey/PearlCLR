using System.Collections.Generic;
using LLVMSharp;
using Mono.Cecil;

namespace PearlCLR.JIT
{
    public struct StructDefinition
    {
        /// <summary>
        /// Struct Name on C# Side
        /// </summary>
        public string CS_StructName { get; set; }
        
        /// <summary>
        /// Struct Name on LLVM Side
        /// </summary>
        public string LL_StructName { get; set; }
        
        public LLVMTypeRef StructTypeRef { get; set; }
        
        /// <summary>
        /// Field Definitions Parsed from C# Side
        /// </summary>
        public List<FieldDefinition> CS_FieldDefs { get; set; }
        
        /// <summary>
        /// Field Type References for LLVM
        /// </summary>
        public LLVMFieldDefAndRef[] LL_FieldTypeRefs { get; set; }
    }

    public struct LLVMFieldDefAndRef
    {
        public LLVMTypeRef FieldTypeRef { get; set; }
        public TypeReference StackType { get; set; }

        public LLVMFieldDefAndRef(TypeReference stacktype, LLVMTypeRef typeref)
        {
            StackType = stacktype;
            FieldTypeRef = typeref;
        }
    }
}