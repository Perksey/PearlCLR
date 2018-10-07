using System;
using LLVMSharp;
using Mono.Cecil.Cil;

namespace PearlCLR.JIT
{
    public class ConversionOpcodeModule : OpcodeHandlerModule
    {
        public ConversionOpcodeModule(JITContext context) : base(context)
        {
        }

        public override bool CanRun(Instruction instruction) =>
            instruction.OpCode == OpCodes.Conv_I ||
            instruction.OpCode == OpCodes.Conv_U1 ||
            instruction.OpCode == OpCodes.Conv_U2 ||
            instruction.OpCode == OpCodes.Conv_U4 ||
            instruction.OpCode == OpCodes.Conv_U8 ||
            instruction.OpCode == OpCodes.Conv_I1 ||
            instruction.OpCode == OpCodes.Conv_I2 ||
            instruction.OpCode == OpCodes.Conv_I4 ||
            instruction.OpCode == OpCodes.Conv_I8 ||
            instruction.OpCode == OpCodes.Conv_R4 ||
            instruction.OpCode == OpCodes.Conv_R8;
        

        public override BuildResult Build(Instruction instruction, FunctionContext funcContext)
        {
            if (instruction.OpCode == OpCodes.Conv_I)
            {
                var value = funcContext.BuilderStack.Pop();
                if (MiniBCL.IsTypeARealNumber(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.Int32Type,
                        LLVM.BuildFPToUI(funcContext.Builder, value.ValRef.Value, LLVM.Int8Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_U1] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                }
                else if (MiniBCL.IsTypeAnInteger(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.Int32Type,
                        LLVM.BuildZExt(funcContext.Builder, value.ValRef.Value, LLVM.Int8Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_U1] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                }
                // TODO: Support Decimal Conversion To Int64
                else
                {
                    throw new Exception(
                        "INTEGER 8 BYTES CONVERSION IS NOT SUPPORTED");
                }
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Conv_U1)
            {
                var value = funcContext.BuilderStack.Pop();
                if (MiniBCL.IsTypeARealNumber(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.UInt8Type,
                        LLVM.BuildFPToUI(funcContext.Builder, value.ValRef.Value, LLVM.Int8Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_U1] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                }
                else if (MiniBCL.IsTypeAnInteger(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.UInt8Type,
                        LLVM.BuildZExt(funcContext.Builder, value.ValRef.Value, LLVM.Int8Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_U1] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                }
                else
                {
                    throw new Exception(
                        "UNSIGNED INTEGER 1 BYTES CONVERSION IS NOT SUPPORTED");
                }
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Conv_U2)
            {
                var value = funcContext.BuilderStack.Pop();
                if (MiniBCL.IsTypeARealNumber(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.UInt16Type,
                        LLVM.BuildFPToUI(funcContext.Builder, value.ValRef.Value, LLVM.Int16Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_U2] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                }
                else if (MiniBCL.IsTypeAnInteger(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.UInt16Type,
                        LLVM.BuildZExt(funcContext.Builder, value.ValRef.Value, LLVM.Int16Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_U2] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                }
                else
                {
                    throw new Exception(
                        "UNSIGNED INTEGER 2 BYTES CONVERSION IS NOT SUPPORTED");
                }
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Conv_U4)
            {
                var value = funcContext.BuilderStack.Pop();
                if (MiniBCL.IsTypeARealNumber(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.UInt32Type,
                        LLVM.BuildFPToUI(funcContext.Builder, value.ValRef.Value, LLVM.Int32Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_U4] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                }
                else if (MiniBCL.IsTypeAnInteger(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.UInt32Type,
                        LLVM.BuildZExt(funcContext.Builder, value.ValRef.Value, LLVM.Int32Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_U4] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                }
                else
                {
                    throw new Exception(
                        "UNSIGNED INTEGER 4 BYTES CONVERSION IS NOT SUPPORTED");
                }
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Conv_U8)
            {
                var value = funcContext.BuilderStack.Pop();
                if (MiniBCL.IsTypeARealNumber(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.UInt64Type,
                        LLVM.BuildFPToUI(funcContext.Builder, value.ValRef.Value, LLVM.Int64Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_U8] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                }
                else if (MiniBCL.IsTypeAnInteger(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.UInt64Type,
                        LLVM.BuildZExt(funcContext.Builder, value.ValRef.Value, LLVM.Int64Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_U8] -> Popped {value.ValRef.Value} and Pushed As Unsigned Integer {stackItem.ValRef.Value} to Stack");
                }
                else
                {
                    throw new Exception(
                        "UNSIGNED INTEGER 8 BYTES CONVERSION IS NOT SUPPORTED");
                }
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Conv_I1)
            {
                var value = funcContext.BuilderStack.Pop();
                if (MiniBCL.IsTypeARealNumber(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.Int8Type,
                        LLVM.BuildFPToSI(funcContext.Builder, value.ValRef.Value, LLVM.Int8Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_I1] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                }
                else if (MiniBCL.IsTypeAnInteger(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.Int8Type,
                        LLVM.BuildZExt(funcContext.Builder, value.ValRef.Value, LLVM.Int8Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_I1] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                }
                else
                {
                    throw new Exception(
                        "INTEGER 1 BYTES CONVERSION IS NOT SUPPORTED");
                }
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Conv_I2)
            {
                var value = funcContext.BuilderStack.Pop();
                if (MiniBCL.IsTypeARealNumber(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.Int16Type,
                        LLVM.BuildFPToSI(funcContext.Builder, value.ValRef.Value, LLVM.Int16Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_I2] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                }
                else if (MiniBCL.IsTypeAnInteger(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.Int16Type,
                        LLVM.BuildZExt(funcContext.Builder, value.ValRef.Value, LLVM.Int16Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_I2] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                }
                else
                {
                    throw new Exception(
                        "INTEGER 2 BYTES CONVERSION IS NOT SUPPORTED");
                }
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Conv_I4)
            {
                var value = funcContext.BuilderStack.Pop();
                if (value.Type.IsPointer)
                {
                    var stackItem = new BuilderStackItem(MiniBCL.Int32Type,
                        LLVM.BuildPtrToInt(funcContext.Builder, value.ValRef.Value, LLVM.Int32Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_I4] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                }
                else if (MiniBCL.IsTypeARealNumber(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.Int32Type,
                        LLVM.BuildFPToSI(funcContext.Builder, value.ValRef.Value, LLVM.Int32Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_I4] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                }
                else if (MiniBCL.IsTypeAnInteger(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.Int32Type,
                        LLVM.BuildZExt(funcContext.Builder, value.ValRef.Value, LLVM.Int32Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_I4] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                }
                else
                {
                    throw new Exception(
                        "INTEGER 4 BYTES CONVERSION IS NOT SUPPORTED");
                }
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Conv_I8)
            {
                var value = funcContext.BuilderStack.Pop();
                if (MiniBCL.IsTypeARealNumber(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.Int64Type,
                        LLVM.BuildFPToSI(funcContext.Builder, value.ValRef.Value, LLVM.Int64Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_I8] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                }
                else if (MiniBCL.IsTypeAnInteger(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.Int64Type,
                        LLVM.BuildZExt(funcContext.Builder, value.ValRef.Value, LLVM.Int64Type(), ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_I8] -> Popped {value.ValRef.Value} and Pushed As Signed Integer {stackItem.ValRef.Value} to Stack");
                }
                else
                {
                    throw new Exception(
                        "INTEGER 8 BYTES CONVERSION IS NOT SUPPORTED");
                }
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Conv_R4)
            {
                var value = funcContext.BuilderStack.Pop();
                if (MiniBCL.IsTypeAnInteger(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.FloatType,
                        LLVM.BuildSIToFP(funcContext.Builder, value.ValRef.Value,
                            LLVM.FloatTypeInContext(Context.ContextRef),
                            ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_R4] -> Popped {value.ValRef.Value} and Pushed As Float {stackItem.ValRef.Value} to Stack");
                }
                else if (MiniBCL.IsTypeARealNumber(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.FloatType,
                        LLVM.BuildFPCast(funcContext.Builder, value.ValRef.Value,
                            LLVM.FloatTypeInContext(Context.ContextRef),
                            ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_R4] -> Popped {value.ValRef.Value} and Pushed As Float {stackItem.ValRef.Value} to Stack");
                }
                else
                {
                    throw new Exception(
                        "REAL 4 BYTES CONVERSION IS NOT SUPPORTED");
                }
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Conv_R8)
            {
                var value = funcContext.BuilderStack.Pop();
                if (MiniBCL.IsTypeAnInteger(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.DoubleType,
                        LLVM.BuildSIToFP(funcContext.Builder, value.ValRef.Value,
                            LLVM.DoubleTypeInContext(Context.ContextRef),
                            ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_R8] -> Popped {value.ValRef.Value} and Pushed As Float {stackItem.ValRef.Value} to Stack");
                }
                else if (MiniBCL.IsTypeARealNumber(value.Type))
                {
                    var stackItem = new BuilderStackItem(MiniBCL.DoubleType,
                        LLVM.BuildFPCast(funcContext.Builder, value.ValRef.Value,
                            LLVM.DoubleTypeInContext(Context.ContextRef),
                            ""));
                    funcContext.BuilderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[Conv_R8] -> Popped {value.ValRef.Value} and Pushed As Float {stackItem.ValRef.Value} to Stack");
                }
                else
                {
                    throw new Exception(
                        "REAL 8 BYTES CONVERSION IS NOT SUPPORTED");
                }
                return new BuildResult(true);
            }

            return new BuildResult(false);
        }

    }
}