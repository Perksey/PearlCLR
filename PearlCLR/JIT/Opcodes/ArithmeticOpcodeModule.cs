using System;
using LLVMSharp;
using Mono.Cecil.Cil;

namespace PearlCLR.JIT
{
    public class ArithmeticOpcodeModule : OpcodeHandlerModule
    {
        public ArithmeticOpcodeModule(JITContext context) : base(context)
        {
        }

        public override bool CanRun(Instruction instruction) =>
            instruction.OpCode == OpCodes.Add ||
            instruction.OpCode == OpCodes.Sub ||
            instruction.OpCode == OpCodes.Mul ||
            instruction.OpCode == OpCodes.Div ||
            instruction.OpCode == OpCodes.Rem;

        public override BuildResult Build(Instruction instruction, FunctionContext funcContext)
        {
            if (instruction.OpCode == OpCodes.Add)
            {
                // TODO: Support conversion between Floating Point and Integers
                var rval = funcContext.BuilderStack.Pop();
                var lval = funcContext.BuilderStack.Pop();
                if (MiniBCL.IsTypeAnInteger(lval.Type) && MiniBCL.IsTypeAnInteger(rval.Type))
                {
                    // TODO: Need to determine the size of pointer.
                    LLVMValueRef actualLVal;
                    LLVMValueRef actualRVal;
                    if (lval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                        actualLVal = LLVM.BuildPtrToInt(funcContext.Builder, lval.ValRef.Value, LLVM.Int32Type(),
                            "lval");
                    else
                        actualLVal = LLVM.BuildZExt(funcContext.Builder, lval.ValRef.Value, LLVM.Int32Type(), "lval");

                    if (rval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                        actualRVal = LLVM.BuildPtrToInt(funcContext.Builder, rval.ValRef.Value, LLVM.Int32Type(),
                            "rval");
                    else
                        actualRVal = LLVM.BuildZExt(funcContext.Builder, rval.ValRef.Value, LLVM.Int32Type(), "rval");

                    var stackItem = new BuilderStackItem(lval.Type,
                        LLVM.BuildAdd(funcContext.Builder, actualLVal, actualRVal, ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Add] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value.TypeOf()} and Pushed {stackItem.ValRef.Value}");
                }
                else if (MiniBCL.IsTypeARealNumber(lval.Type) && MiniBCL.IsTypeARealNumber(rval.Type))
                {
                    var stackItem = new BuilderStackItem(lval.Type,
                        LLVM.BuildFAdd(funcContext.Builder, lval.ValRef.Value, rval.ValRef.Value, ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Add] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                }
                else
                {
                    throw new Exception("Unknown type, thus cannot add!");
                }
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Sub)
            {
                // TODO: Support conversion between Floating Point and Integers
                var rval = funcContext.BuilderStack.Pop();
                var lval = funcContext.BuilderStack.Pop();
                if (MiniBCL.IsTypeAnInteger(lval.Type) && MiniBCL.IsTypeAnInteger(rval.Type))
                {
                    // TODO: Need to determine the size of pointer.
                    LLVMValueRef actualLVal;
                    LLVMValueRef actualRVal;
                    if (lval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                        actualLVal = LLVM.BuildPtrToInt(funcContext.Builder, lval.ValRef.Value, LLVM.Int32Type(),
                            "lval");
                    else
                        actualLVal = LLVM.BuildZExt(funcContext.Builder, lval.ValRef.Value, LLVM.Int32Type(), "lval");

                    if (rval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                        actualRVal = LLVM.BuildPtrToInt(funcContext.Builder, rval.ValRef.Value, LLVM.Int32Type(),
                            "rval");
                    else
                        actualRVal = LLVM.BuildZExt(funcContext.Builder, rval.ValRef.Value, LLVM.Int32Type(), "rval");

                    var stackItem = new BuilderStackItem(lval.Type,
                        LLVM.BuildSub(funcContext.Builder, actualLVal, actualRVal, ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Sub] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                }
                else if (MiniBCL.IsTypeARealNumber(lval.Type) && MiniBCL.IsTypeARealNumber(rval.Type))
                {
                    var stackItem = new BuilderStackItem(lval.Type,
                        LLVM.BuildFSub(funcContext.Builder, lval.ValRef.Value, rval.ValRef.Value, ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Sub] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                }
                else
                {
                    throw new Exception("Unknown type, thus cannot add!");
                }
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Mul)
            {
                // TODO: Support conversion between Floating Point and Integers
                var rval = funcContext.BuilderStack.Pop();
                var lval = funcContext.BuilderStack.Pop();
                if (MiniBCL.IsTypeAnInteger(lval.Type) && MiniBCL.IsTypeAnInteger(rval.Type))
                {
                    // TODO: Need to determine the size of pointer.
                    LLVMValueRef actualLVal;
                    LLVMValueRef actualRVal;
                    if (lval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                        actualLVal = LLVM.BuildPtrToInt(funcContext.Builder, lval.ValRef.Value, LLVM.Int32Type(),
                            "lval");
                    else
                        actualLVal = LLVM.BuildZExt(funcContext.Builder, lval.ValRef.Value, LLVM.Int32Type(), "lval");

                    if (rval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                        actualRVal = LLVM.BuildPtrToInt(funcContext.Builder, rval.ValRef.Value, LLVM.Int32Type(),
                            "rval");
                    else
                        actualRVal = LLVM.BuildZExt(funcContext.Builder, rval.ValRef.Value, LLVM.Int32Type(), "rval");

                    var stackItem = new BuilderStackItem(lval.Type,
                        LLVM.BuildMul(funcContext.Builder, actualLVal, actualRVal, ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Sub] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                }
                else if (MiniBCL.IsTypeARealNumber(lval.Type) && MiniBCL.IsTypeARealNumber(rval.Type))
                {
                    var stackItem = new BuilderStackItem(lval.Type,
                        LLVM.BuildFMul(funcContext.Builder, lval.ValRef.Value, rval.ValRef.Value, ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Sub] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                }
                else
                {
                    throw new Exception("Unknown type, thus cannot add!");
                }
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Div)
            {
                // TODO: Support conversion between Floating Point and Integers
                var rval = funcContext.BuilderStack.Pop();
                var lval = funcContext.BuilderStack.Pop();
                if (MiniBCL.IsTypeAnInteger(lval.Type) && MiniBCL.IsTypeAnInteger(rval.Type))
                {
                    // TODO: Need to determine the size of pointer.
                    LLVMValueRef actualLVal;
                    LLVMValueRef actualRVal;
                    if (lval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                        actualLVal = LLVM.BuildPtrToInt(funcContext.Builder, lval.ValRef.Value, LLVM.Int32Type(),
                            "lval");
                    else
                        actualLVal = LLVM.BuildZExt(funcContext.Builder, lval.ValRef.Value, LLVM.Int32Type(), "lval");

                    if (rval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                        actualRVal = LLVM.BuildPtrToInt(funcContext.Builder, rval.ValRef.Value, LLVM.Int32Type(),
                            "rval");
                    else
                        actualRVal = LLVM.BuildZExt(funcContext.Builder, rval.ValRef.Value, LLVM.Int32Type(), "rval");

                    var stackItem = new BuilderStackItem(lval.Type,
                        LLVM.BuildSDiv(funcContext.Builder, actualLVal, actualRVal, ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Sub] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                }
                else if (MiniBCL.IsTypeARealNumber(lval.Type) && MiniBCL.IsTypeARealNumber(rval.Type))
                {
                    var stackItem = new BuilderStackItem(lval.Type,
                        LLVM.BuildFDiv(funcContext.Builder, lval.ValRef.Value, rval.ValRef.Value, ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Sub] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                }
                else
                {
                    throw new Exception("Unknown type, thus cannot add!");
                }
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Rem)
            {
                // TODO: Support conversion between Floating Point and Integers
                var rval = funcContext.BuilderStack.Pop();
                var lval = funcContext.BuilderStack.Pop();
                if (MiniBCL.IsTypeAnInteger(lval.Type) && MiniBCL.IsTypeAnInteger(rval.Type))
                {
                    // TODO: Need to determine the size of pointer.
                    LLVMValueRef actualLVal;
                    LLVMValueRef actualRVal;
                    if (lval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                        actualLVal = LLVM.BuildPtrToInt(funcContext.Builder, lval.ValRef.Value, LLVM.Int32Type(),
                            "lval");
                    else
                        actualLVal = LLVM.BuildZExt(funcContext.Builder, lval.ValRef.Value, LLVM.Int32Type(), "lval");

                    if (rval.ValRef.Value.TypeOf().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
                        actualRVal = LLVM.BuildPtrToInt(funcContext.Builder, rval.ValRef.Value, LLVM.Int32Type(),
                            "rval");
                    else
                        actualRVal = LLVM.BuildZExt(funcContext.Builder, rval.ValRef.Value, LLVM.Int32Type(), "rval");

                    var stackItem = new BuilderStackItem(lval.Type,
                        LLVM.BuildSRem(funcContext.Builder, actualLVal, actualRVal, ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Sub] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                }
                else if (MiniBCL.IsTypeARealNumber(lval.Type) && MiniBCL.IsTypeARealNumber(rval.Type))
                {
                    var stackItem = new BuilderStackItem(lval.Type,
                        LLVM.BuildFRem(funcContext.Builder, lval.ValRef.Value, rval.ValRef.Value, ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Sub] -> Popped {rval.ValRef.Value} and {lval.ValRef.Value} and Pushed {stackItem.ValRef.Value}");
                }
                else
                {
                    throw new Exception("Unknown type, thus cannot add!");
                }
                return new BuildResult(true);
            }

            return new BuildResult(false);
        }
    }
}