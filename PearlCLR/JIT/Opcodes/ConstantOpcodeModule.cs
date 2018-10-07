using LLVMSharp;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace PearlCLR.JIT
{
    public class ConstantOpcodeModule : OpcodeHandlerModule
    {
        public ConstantOpcodeModule(JITContext context) : base(context)
        {
        }

        public override bool CanRun(Instruction instruction) =>
            instruction.OpCode == OpCodes.Ldind_U1 ||
            instruction.OpCode == OpCodes.Ldind_I ||
            instruction.OpCode == OpCodes.Ldc_I4 ||
            instruction.OpCode == OpCodes.Ldc_I4_S ||
            instruction.OpCode == OpCodes.Ldc_I4_0 ||
            instruction.OpCode == OpCodes.Ldc_I4_1 ||
            instruction.OpCode == OpCodes.Ldc_I4_2 ||
            instruction.OpCode == OpCodes.Ldc_I4_3 ||
            instruction.OpCode == OpCodes.Ldc_I4_4 ||
            instruction.OpCode == OpCodes.Ldc_I4_5 ||
            instruction.OpCode == OpCodes.Ldc_I4_6 ||
            instruction.OpCode == OpCodes.Ldc_I4_7 ||
            instruction.OpCode == OpCodes.Ldc_I4_8 ||
            instruction.OpCode == OpCodes.Ldc_I4_M1 ||
            instruction.OpCode == OpCodes.Ldc_I8 ||
            instruction.OpCode == OpCodes.Ldc_R4 ||
            instruction.OpCode == OpCodes.Ldc_R8 ||
            instruction.OpCode == OpCodes.Ldnull ||
            instruction.OpCode == OpCodes.Ldftn ||
            instruction.OpCode == OpCodes.Ldstr;


        public override BuildResult Build(Instruction instruction, FunctionContext funcContext)
        {
            if (instruction.OpCode == OpCodes.Ldind_U1)
            {
                var val = funcContext.BuilderStack.Pop();
                LLVMValueRef cast;
                if (val.Type.IsPointer)
                    cast = LLVM.BuildPtrToInt(funcContext.Builder,
                        LLVM.BuildLoad(funcContext.Builder, val.ValRef.Value, ""),
                        LLVM.Int32Type(), "");
                else
                    cast = LLVM.BuildZExt(funcContext.Builder,
                        LLVM.BuildLoad(funcContext.Builder, val.ValRef.Value, ""),
                        LLVM.Int32Type(), "");
                funcContext.BuilderStack.Push(new BuilderStackItem
                {
                    Type = MiniBCL.Int32Type,
                    TypeRef = LLVM.Int32Type(),
                    ValRef = cast
                });
                Context.CLRLogger.Debug(
                    $"[Ldind_U1] -> Popped Stack Item {val.ValRef.Value}, Loaded and Casted to Int32 Type {cast}");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldind_I)
            {
                var val = funcContext.BuilderStack.Pop();
                // TODO: Support Native Integer conversion
                LLVMValueRef cast;
                if (val.Type.IsPointer)
                {
                    cast = LLVM.BuildLoad(funcContext.Builder, val.ValRef.Value, "");
                    funcContext.BuilderStack.Push(new BuilderStackItem
                    {
                        Type = ((PointerType) val.Type).ElementType,
                        TypeRef = cast.TypeOf(),
                        ValRef = cast
                    });
                }
                else
                {
                    cast = LLVM.BuildZExt(funcContext.Builder,
                        LLVM.BuildLoad(funcContext.Builder, val.ValRef.Value, $"Load_{val.Type.Name}"),
                        LLVM.Int64Type(), "");
                    funcContext.BuilderStack.Push(new BuilderStackItem
                    {
                        Type = MiniBCL.Int64Type,
                        TypeRef = LLVM.Int64Type(),
                        ValRef = cast
                    });
                }

                Context.CLRLogger.Debug(
                    $"[Ldind_I] -> Popped Stack Item {val.ValRef.Value} and Casted to Int64 Type {cast}");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldc_I4)
            {
                var operand = (int) instruction.Operand;
                var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(Context.ContextRef), (ulong) operand,
                    true);
                funcContext.BuilderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                    stackItem));
                Context.CLRLogger.Debug($"[Ldc_I4 {operand}] -> Pushed {stackItem} to Stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldc_I4_S)
            {
                var operand = (sbyte) instruction.Operand;
                var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(Context.ContextRef), (ulong) operand,
                    true);
                funcContext.BuilderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                    stackItem));
                Context.CLRLogger.Debug($"[Ldc_I4_S {operand}] -> Pushed {stackItem} to Stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldc_I4_0)
            {
                var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(Context.ContextRef), 0, true);
                funcContext.BuilderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                    stackItem));
                Context.CLRLogger.Debug($"[Ldc_I4_0] -> Pushed {stackItem} to Stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldc_I4_1)
            {
                var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(Context.ContextRef), 1, true);
                funcContext.BuilderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                    stackItem));
                Context.CLRLogger.Debug($"[Ldc_I4_1] -> Pushed {stackItem} to Stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldc_I4_2)
            {
                var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(Context.ContextRef), 2, true);
                funcContext.BuilderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                    stackItem));
                Context.CLRLogger.Debug($"[Ldc_I4_2] -> Pushed {stackItem} to Stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldc_I4_3)
            {
                var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(Context.ContextRef), 3, true);
                funcContext.BuilderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                    stackItem));
                Context.CLRLogger.Debug($"[Ldc_I4_3] -> Pushed {stackItem} to Stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldc_I4_4)
            {
                var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(Context.ContextRef), 4, true);
                funcContext.BuilderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                    stackItem));
                Context.CLRLogger.Debug($"[Ldc_I4_4] -> Pushed {stackItem} to Stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldc_I4_5)
            {
                var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(Context.ContextRef), 5, true);
                funcContext.BuilderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                    stackItem));
                Context.CLRLogger.Debug($"[Ldc_I4_5] -> Pushed {stackItem} to Stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldc_I4_6)
            {
                var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(Context.ContextRef), 6, true);
                funcContext.BuilderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                    stackItem));
                Context.CLRLogger.Debug($"[Ldc_I4_6] -> Pushed {stackItem} to Stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldc_I4_7)
            {
                var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(Context.ContextRef), 7, true);
                funcContext.BuilderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                    stackItem));
                Context.CLRLogger.Debug($"[Ldc_I4_7] -> Pushed {stackItem} to Stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldc_I4_8)
            {
                var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(Context.ContextRef), 8, true);
                funcContext.BuilderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                    stackItem));
                Context.CLRLogger.Debug($"[Ldc_I4_8] -> Pushed {stackItem} to Stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldc_I4_M1)
            {
                unchecked
                {
                    var stackItem = LLVM.ConstInt(LLVM.Int32TypeInContext(Context.ContextRef), (ulong) -1,
                        true);
                    funcContext.BuilderStack.Push(new BuilderStackItem(MiniBCL.Int32Type,
                        stackItem));
                    Context.CLRLogger.Debug($"[Ldc_I4_M1] -> Pushed {stackItem} to Stack");
                    return new BuildResult(true);
                }
            }
            else if (instruction.OpCode == OpCodes.Ldc_I8)
            {
                var stackItem = new BuilderStackItem(MiniBCL.Int64Type,
                    LLVM.ConstInt(LLVM.Int64TypeInContext(Context.ContextRef), (ulong) instruction.Operand,
                        new LLVMBool(1)));
                funcContext.BuilderStack.Push(stackItem);
                Context.CLRLogger.Debug($"[Ldc_I8] -> Pushed {stackItem.ValRef.Value} to Stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldc_R4)
            {
                var stackItem = new BuilderStackItem(MiniBCL.FloatType,
                    LLVM.ConstReal(LLVM.FloatTypeInContext(Context.ContextRef), (double) instruction.Operand));
                funcContext.BuilderStack.Push(stackItem);
                Context.CLRLogger.Debug($"[Ldc_R4] -> Pushed {stackItem.ValRef.Value} to Stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldc_R8)
            {
                var stackItem = new BuilderStackItem(MiniBCL.DoubleType,
                    LLVM.ConstReal(LLVM.DoubleTypeInContext(Context.ContextRef),
                        (double) instruction.Operand));
                funcContext.BuilderStack.Push(stackItem);
                Context.CLRLogger.Debug($"[Ldc_R8] -> Pushed {stackItem.ValRef.Value} to Stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldnull)
            {
                var stackItem = new BuilderStackItem(MiniBCL.Int64Type,
                    LLVM.ConstNull(LLVM.PointerType(LLVM.Int8Type(), 0)));
                funcContext.BuilderStack.Push(stackItem);
                Context.CLRLogger.Debug($"[Ldnull] -> Pushed {stackItem.ValRef.Value} to Stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldftn)
            {
                var operand = (MethodDefinition) instruction.Operand;
                var symbol = SymbolHelper.GetCSToLLVMSymbolName(operand);
                if (!Context.SymbolToCallableFunction.ContainsKey(symbol))
                    Context.CLR.ProcessFunction(operand.Resolve(), false);

                var stackItem =
                    new BuilderStackItem(operand.ReturnType.Resolve(),
                        Context.SymbolToCallableFunction[symbol]);
                funcContext.BuilderStack.Push(stackItem);
                Context.CLRLogger.Debug($"[Ldftn {stackItem.Type}] -> Pushed {stackItem.ValRef.Value} to Stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ldstr)
            {
                var val = (string) instruction.Operand;
                var ldstr = LLVM.BuildGlobalStringPtr(funcContext.Builder, val, "");
                var stackItem = new BuilderStackItem(MiniBCL.StringType, ldstr);
                funcContext.BuilderStack.Push(stackItem);
                Context.CLRLogger.Debug($"[Ldstr {val}] -> Pushed {ldstr.TypeOf()} to Stack");
                return new BuildResult(true);
            }

            return new BuildResult(false);
        }
    }
}