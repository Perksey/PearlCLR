using System.Collections.Generic;
using LLVMSharp;
using NLog;

namespace PearlCLR.JIT
{
    public class JITContext
    {
        public LLVMContextRef ContextRef { get; set; }
        public LLVMModuleRef ModuleRef { get; set; }

        public Dictionary<string, StructDefinition> FullSymbolToTypeRef { get; } =
            new Dictionary<string, StructDefinition>();

        public Dictionary<string, LLVMValueRef> SymbolToCallableFunction { get; } =
            new Dictionary<string, LLVMValueRef>();

        public Dictionary<string, LLVMTypeRef> SymbolToFunctionPointerProto { get; } =
            new Dictionary<string, LLVMTypeRef>();

        public Logger CLRLogger { get; set; }
        public JITCompilerOptions Options { get; set; }
        public LLVMExecutionEngineRef EngineRef { get; set; }
        public LLVMTypeResolver TypeResolver { get; set; }
    }
}