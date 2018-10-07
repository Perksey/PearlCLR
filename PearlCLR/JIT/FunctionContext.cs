using System.Collections.Generic;
using LLVMSharp;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace PearlCLR.JIT
{
    public class FunctionContext
    {
        public MethodDefinition MethodDef { get; set; }
        public List<TypeReference> LocalVariableTypes { get; set; } = new List<TypeReference>();
        
        public Dictionary<Instruction, LLVMBasicBlockRef> BranchTo { get; set; } = new Dictionary<Instruction, LLVMBasicBlockRef>();
        public Stack<KeyValuePair<Instruction, LLVMBasicBlockRef>> BranchToProcess { get; set; } = new Stack<KeyValuePair<Instruction, LLVMBasicBlockRef>>();
        public List<Instruction> ProcessedBranch { get; set; } = new List<Instruction>();

        public LLVMBuilderRef Builder { get; set; }
        public Stack<BuilderStackItem> BuilderStack { get; set; } = new Stack<BuilderStackItem>();
        public LLVMValueRef FunctionRef { get; set; }
        public LLVMTypeRef FunctionType { get; set; }
        public List<LLVMValueRef> LocalVariables { get; set; } = new List<LLVMValueRef>();
        public LLVMBasicBlockRef CurrentBlockRef { get; set; }
        
        public bool isMain { get; set; }
        public int PrependTabs { get; set; } = 0;
        

        
    }
}