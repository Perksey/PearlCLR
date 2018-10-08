using System;
using LLVMSharp;
using Mono.Cecil.Cil;

namespace PearlCLR.JIT
{
    public class ComparsionOpcodeModule : OpcodeHandlerModule
    {
        public ComparsionOpcodeModule(JITContext context) : base(context)
        {
        }

        public override bool CanRun(Instruction instruction)
        {
            return instruction.OpCode == OpCodes.Clt ||
                   instruction.OpCode == OpCodes.Cgt ||
                   instruction.OpCode == OpCodes.Ceq;
        }

        public override BuildResult Build(Instruction instruction, FunctionContext funcContext)
        {
            if (instruction.OpCode == OpCodes.Clt)
            {
                var rval = funcContext.BuilderStack.Pop().ValRef.Value;
                var lval = funcContext.BuilderStack.Pop().ValRef.Value;
                var rvalType = rval.TypeOf();
                var lvalType = lval.TypeOf();
                if (!rvalType.Equals(lvalType))
                    rval = TypeConversionHelper.AutoCast(funcContext.Builder, rval, lvalType);

                LLVMValueRef cmp;
                if (lvalType.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
                    cmp = LLVM.BuildICmp(funcContext.Builder, LLVMIntPredicate.LLVMIntSLT, lval, rval, "clt");
                else if (lvalType.TypeKind == LLVMTypeKind.LLVMFloatTypeKind)
                    cmp = LLVM.BuildFCmp(funcContext.Builder, LLVMRealPredicate.LLVMRealOLT, lval, rval, "clt");
                else if (lvalType.TypeKind == LLVMTypeKind.LLVMDoubleTypeKind)
                    cmp = LLVM.BuildFCmp(funcContext.Builder, LLVMRealPredicate.LLVMRealOLT, lval, rval, "clt");
                else
                    throw new NotImplementedException(
                        $"No comparision supported for those types: {lval} < {rval}");

                funcContext.BuilderStack.Push(new BuilderStackItem(MiniBCL.Int32Type, cmp));
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Cgt)
            {
                var rval = funcContext.BuilderStack.Pop().ValRef.Value;
                var lval = funcContext.BuilderStack.Pop().ValRef.Value;
                var rvalType = rval.TypeOf();
                var lvalType = lval.TypeOf();
                if (!rvalType.Equals(lvalType))
                    rval = TypeConversionHelper.AutoCast(funcContext.Builder, rval, lvalType);

                LLVMValueRef cmp;
                if (lvalType.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
                    cmp = LLVM.BuildICmp(funcContext.Builder, LLVMIntPredicate.LLVMIntSGT, lval, rval, "cgt");
                else if (lvalType.TypeKind == LLVMTypeKind.LLVMFloatTypeKind)
                    cmp = LLVM.BuildFCmp(funcContext.Builder, LLVMRealPredicate.LLVMRealOGT, lval, rval, "cgt");
                else if (lvalType.TypeKind == LLVMTypeKind.LLVMDoubleTypeKind)
                    cmp = LLVM.BuildFCmp(funcContext.Builder, LLVMRealPredicate.LLVMRealUGT, lval, rval, "cgt");
                else
                    throw new NotImplementedException(
                        $"No comparision supported for those types: {lval} < {rval}");

                funcContext.BuilderStack.Push(new BuilderStackItem(MiniBCL.Int32Type, cmp));
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Ceq)
            {
                var rval = funcContext.BuilderStack.Pop().ValRef.Value;
                var lval = funcContext.BuilderStack.Pop().ValRef.Value;
                var rvalType = rval.TypeOf();
                var lvalType = lval.TypeOf();
                if (!rvalType.Equals(lvalType))
                    rval = TypeConversionHelper.AutoCast(funcContext.Builder, rval, lvalType);

                LLVMValueRef cmp;
                if (lvalType.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
                    cmp = LLVM.BuildICmp(funcContext.Builder, LLVMIntPredicate.LLVMIntEQ, lval, rval, "ceq");
                else if (lvalType.TypeKind == LLVMTypeKind.LLVMFloatTypeKind)
                    cmp = LLVM.BuildFCmp(funcContext.Builder, LLVMRealPredicate.LLVMRealOEQ, lval, rval, "ceq");
                else if (lvalType.TypeKind == LLVMTypeKind.LLVMDoubleTypeKind)
                    cmp = LLVM.BuildFCmp(funcContext.Builder, LLVMRealPredicate.LLVMRealOEQ, lval, rval, "ceq");
                else
                    throw new NotImplementedException(
                        $"No comparision supported for those types: {lval} == {rval}");

                funcContext.BuilderStack.Push(new BuilderStackItem(MiniBCL.Int32Type, cmp));
                return new BuildResult(true);
            }

            return new BuildResult(false);
        }
    }
}