using System.Collections.Generic;
using LLVMSharp;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace PearlCLR.JIT
{
    public class FunctionContext
    {
        public  Stack<BuilderStackItem> BuilderStack { get; set; } = new Stack<BuilderStackItem>();
        public List<LLVMValueRef> LocalVariables { get; set; } = new List<LLVMValueRef>();
        public List<TypeReference> LocalVariableTypes { get; set; } = new List<TypeReference>();
        public Dictionary<Instruction, LLVMBasicBlockRef> BranchTo { get; set; } = new Dictionary<Instruction, LLVMBasicBlockRef>();
        public Stack<KeyValuePair<Instruction, LLVMBasicBlockRef>> BranchToProcess { get; set; } = new Stack<KeyValuePair<Instruction, LLVMBasicBlockRef>>();
        public List<Instruction> ProcessedBranch { get; set; } = new List<Instruction>();
        public LLVMTypeRef FunctionType { get; set; }
    }
}