using System;
using System.Linq;
using LLVMSharp;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace PearlCLR.JIT
{
    public class LocalVariableOpcodeModule : OpcodeHandlerModule
    {
        public LocalVariableOpcodeModule(JITContext context) : base(context)
        {
        }

        public override bool CanRun(Instruction instruction)
        {
            return instruction.OpCode == OpCodes.Ldloca_S ||
                   instruction.OpCode == OpCodes.Ldloc_S ||
                   instruction.OpCode == OpCodes.Ldloc_0 ||
                   instruction.OpCode == OpCodes.Ldloc_1 ||
                   instruction.OpCode == OpCodes.Ldloc_2 ||
                   instruction.OpCode == OpCodes.Ldloc_3 ||
                   instruction.OpCode == OpCodes.Stloc_0 ||
                   instruction.OpCode == OpCodes.Stloc_1 ||
                   instruction.OpCode == OpCodes.Stloc_2 ||
                   instruction.OpCode == OpCodes.Stloc_3 ||
                   instruction.OpCode == OpCodes.Stloc_S ||
                   instruction.OpCode == OpCodes.Stind_I1 ||
                   instruction.OpCode == OpCodes.Stfld ||
                   instruction.OpCode == OpCodes.Ldfld;
        }

        public override BuildResult Build(Instruction instruction, FunctionContext funcContext)
        {
            if (instruction.OpCode == OpCodes.Ldloca_S)
            {
                var def = (VariableDefinition) instruction.Operand;
                var stackItem = new BuilderStackItem(funcContext.MethodDef.Body.Variables[def.Index].VariableType,
                    funcContext.LocalVariables[def.Index]);
                funcContext.BuilderStack.Push(stackItem);
                Context.CLRLogger.Debug(
                    $"[Ldloca_S {def.Index}] -> Pushed Local Variable {stackItem.ValRef.Value} to Stack");
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Ldloc_S)
            {
                var def = (VariableDefinition) instruction.Operand;
                var stackItem = new BuilderStackItem(funcContext.MethodDef.Body.Variables[def.Index].VariableType,
                    LLVM.BuildLoad(funcContext.Builder, funcContext.LocalVariables[def.Index], ""));
                funcContext.BuilderStack.Push(stackItem);
                Context.CLRLogger.Debug(
                    $"[Ldloc_S {def.Index}] -> Pushed Local Variable {stackItem.ValRef.Value} to Stack");
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Ldloc_0)
            {
                var stackItem = new BuilderStackItem(funcContext.MethodDef.Body.Variables[0].VariableType,
                    LLVM.BuildLoad(funcContext.Builder, funcContext.LocalVariables[0], ""));
                funcContext.BuilderStack.Push(stackItem);
                Context.CLRLogger.Debug($"[Ldloc_0] -> Pushed Local Variable {stackItem.ValRef.Value} to Stack");
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Ldloc_1)
            {
                var stackItem = new BuilderStackItem(funcContext.MethodDef.Body.Variables[1].VariableType,
                    LLVM.BuildLoad(funcContext.Builder, funcContext.LocalVariables[1], ""));
                funcContext.BuilderStack.Push(stackItem);
                Context.CLRLogger.Debug($"[Ldloc_1] -> Pushed Local Variable {stackItem.ValRef.Value} to Stack");
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Ldloc_2)
            {
                var stackItem = new BuilderStackItem(funcContext.MethodDef.Body.Variables[2].VariableType,
                    LLVM.BuildLoad(funcContext.Builder, funcContext.LocalVariables[2], ""));
                funcContext.BuilderStack.Push(stackItem);
                Context.CLRLogger.Debug($"[Ldloc_2] -> Pushed Local Variable {stackItem.ValRef.Value} to Stack");
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Ldloc_3)
            {
                var stackItem = new BuilderStackItem(funcContext.MethodDef.Body.Variables[3].VariableType,
                    LLVM.BuildLoad(funcContext.Builder, funcContext.LocalVariables[3], ""));
                funcContext.BuilderStack.Push(stackItem);
                Context.CLRLogger.Debug($"[Ldloc_3] -> Pushed Local Variable {stackItem.ValRef.Value} to Stack");
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Stloc_0)
            {
                var val = funcContext.BuilderStack.Pop();
                ProcessStoreLoc(funcContext, val, 0);
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Stloc_1)
            {
                var val = funcContext.BuilderStack.Pop();
                ProcessStoreLoc(funcContext, val, 1);
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Stloc_2)
            {
                var val = funcContext.BuilderStack.Pop();
                ProcessStoreLoc(funcContext, val, 2);
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Stloc_3)
            {
                var val = funcContext.BuilderStack.Pop();
                ProcessStoreLoc(funcContext, val, 3);
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Stloc_S)
            {
                var index = (VariableDefinition) instruction.Operand;
                var val = funcContext.BuilderStack.Pop();
                ProcessStoreLoc(funcContext, val, index.Index);
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Stind_I1)
            {
                var val = funcContext.BuilderStack.Pop();
                var cast = LLVM.BuildZExt(funcContext.Builder, val.ValRef.Value, LLVM.Int8Type(), "");
                var address = funcContext.BuilderStack.Pop();
                var ptr = address.ValRef.Value;
                if (address.ValRef.Value.TypeOf().TypeKind != LLVMTypeKind.LLVMPointerTypeKind)
                    ptr = LLVM.BuildIntToPtr(funcContext.Builder, address.ValRef.Value,
                        LLVM.PointerType(LLVM.Int8Type(), 0),
                        "");
                var store = ProcessStore(funcContext, cast, ptr);
                Context.CLRLogger.Debug(
                    $"[Stind_I1] -> Popped {val.ValRef.Value} and {address.ValRef.Value} and Stored into address: {store}");
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Stfld)
            {
                var fieldDef = (FieldDefinition) instruction.Operand;
                var value = funcContext.BuilderStack.Pop();
                var structRef = funcContext.BuilderStack.Pop();
                if (value.ValRef.Value.IsNull())
                    throw new Exception(
                        "The Value/Reference returned as null thus cannot be stored in Struct!");
                if (structRef.ValRef.Value.IsNull())
                    throw new Exception(
                        "The Value/Reference returned as null thus cannot be stored in Struct!");

                var index = (uint) Array.IndexOf(
                    Context.FullSymbolToTypeRef[fieldDef.DeclaringType.FullName].CS_FieldDefs,
                    Context.FullSymbolToTypeRef[fieldDef.DeclaringType.FullName].CS_FieldDefs
                        .First(I => I.Name == fieldDef.Name));

                var refToStruct = structRef.ValRef.Value;
                LLVMValueRef offset;
                if (fieldDef.DeclaringType.IsClass) offset = LLVM.BuildLoad(funcContext.Builder, refToStruct, "");

                offset = LLVM.BuildStructGEP(funcContext.Builder, refToStruct, index, structRef.Type.Name);
                ProcessStore(funcContext, value.ValRef.Value, offset);
                Context.CLRLogger.Debug(
                    $"[Stfld {fieldDef.FullName}] -> Popped {value.ValRef.Value} and {refToStruct.TypeOf()} and Store {value.ValRef.Value} into {offset}");
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Ldfld)
            {
                var fieldDef = (FieldDefinition) instruction.Operand;
                var structRef = funcContext.BuilderStack.Pop();
                if (!structRef.ValRef.HasValue)
                    throw new Exception(
                        "The Value/Reference returned as null thus cannot be stored in Struct!");
                //var load = LLVM.BuildLoad(funcContext.Builder, structRef.ValRef.Value, $"Loaded_{structRef.Type.Name}");
                var load = structRef.ValRef.Value;
                var offset = LLVM.BuildStructGEP(funcContext.Builder, load,
                    (uint) Array.IndexOf(
                        Context.FullSymbolToTypeRef[fieldDef.DeclaringType.FullName].CS_FieldDefs,
                        Context.FullSymbolToTypeRef[fieldDef.DeclaringType.FullName].CS_FieldDefs
                            .First(I => I.Name == fieldDef.Name)), $"Offset_{fieldDef.Name}");
                var item = new BuilderStackItem(fieldDef.FieldType, LLVM.BuildLoad(funcContext.Builder, offset, ""));
                funcContext.BuilderStack.Push(item);
                Context.CLRLogger.Debug(
                    $"[Ldfld {fieldDef.FullName}] -> Popped {structRef.ValRef.Value} off stack and pushed {item.ValRef.Value} to stack");
                return new BuildResult(true);
            }

            return new BuildResult(false);
        }

        internal LLVMValueRef ProcessStore(FunctionContext funcContext, LLVMValueRef lval, LLVMValueRef rval)
        {
            var lvalType = lval.TypeOf();
            var rvalType = rval.TypeOf();
            if (!lvalType.Equals(LLVM.GetElementType(rvalType)))
                lval = TypeConversionHelper.AutoCast(funcContext.Builder, lval, rvalType.GetElementType());
            Context.CLRLogger.Debug(lval.ToString());
            Context.CLRLogger.Debug(rval.ToString());

            return LLVM.BuildStore(funcContext.Builder, lval, rval);
        }

        internal void ProcessStoreLoc(FunctionContext funcContext, BuilderStackItem stackItem, int localVariableIndex)
        {
            var localVariableType = funcContext.LocalVariableTypes[localVariableIndex];
            var localVariable = funcContext.LocalVariables[localVariableIndex];
            //TODO: Need a better way to compare...
            if (localVariableType.IsPointer)
            {
                var resolvePtrType = Context.TypeResolver.Resolve(localVariableType);
                var store = ProcessStore(funcContext,
                    LLVM.BuildIntToPtr(funcContext.Builder, stackItem.ValRef.Value, resolvePtrType, "IntPtr"),
                    localVariable);
                Context.CLRLogger.Debug(
                    $"[Stloc_{localVariableIndex}] -> Popped {stackItem.ValRef.Value.TypeOf()} and Stored {store}");
            }
            else
            {
                var store = ProcessStore(funcContext, stackItem.ValRef.Value, localVariable);
                Context.CLRLogger.Debug(
                    $"[Stloc_{localVariableIndex}] -> Popped {stackItem.ValRef.Value.TypeOf()} and Stored {store}");
            }
        }
    }
}