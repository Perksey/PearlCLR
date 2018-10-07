using System;
using System.Collections.Generic;
using LLVMSharp;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace PearlCLR.JIT
{
    public class MetaOpcodeModule : OpcodeHandlerModule
    {
        public MetaOpcodeModule(JITContext context) : base(context)
        {
        }

        public override bool CanRun(Instruction instruction) =>
            instruction.OpCode == OpCodes.Nop ||
            instruction.OpCode == OpCodes.Initobj ||
            instruction.OpCode == OpCodes.Dup ||
            instruction.OpCode == OpCodes.Newobj ||
            instruction.OpCode == OpCodes.Callvirt ||
            instruction.OpCode == OpCodes.Call ||
            instruction.OpCode == OpCodes.Box ||
            instruction.OpCode == OpCodes.Br ||
            instruction.OpCode == OpCodes.Br_S ||
            instruction.OpCode == OpCodes.Beq ||
            instruction.OpCode == OpCodes.Beq_S ||
            instruction.OpCode == OpCodes.Bge ||
            instruction.OpCode == OpCodes.Bge_S ||
            instruction.OpCode == OpCodes.Bge_Un ||
            instruction.OpCode == OpCodes.Bge_Un_S ||
            instruction.OpCode == OpCodes.Bgt ||
            instruction.OpCode == OpCodes.Bgt_S ||
            instruction.OpCode == OpCodes.Bgt_Un ||
            instruction.OpCode == OpCodes.Bgt_Un_S ||
            instruction.OpCode == OpCodes.Ble ||
            instruction.OpCode == OpCodes.Ble_S ||
            instruction.OpCode == OpCodes.Ble_Un ||
            instruction.OpCode == OpCodes.Ble_Un_S ||
            instruction.OpCode == OpCodes.Blt ||
            instruction.OpCode == OpCodes.Blt_S ||
            instruction.OpCode == OpCodes.Blt_Un ||
            instruction.OpCode == OpCodes.Blt_Un_S ||
            instruction.OpCode == OpCodes.Bne_Un ||
            instruction.OpCode == OpCodes.Bne_Un_S ||
            instruction.OpCode == OpCodes.Brfalse ||
            instruction.OpCode == OpCodes.Brfalse_S ||
            instruction.OpCode == OpCodes.Brtrue ||
            instruction.OpCode == OpCodes.Brtrue_S;

        /// <summary>
        /// Build LLVM instructions for provided CIL instruction
        /// </summary>
        /// <param name="instruction"></param>
        /// <param name="funcContext"></param>
        /// <returns>Boolean flag for breaking out of loop for processing function body</returns>
        public override BuildResult Build(Instruction instruction, FunctionContext funcContext)
        {
            if (instruction.OpCode == OpCodes.Nop)
            {
                Context.CLRLogger.Debug("[Nop]");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Initobj)
            {
                var item = funcContext.BuilderStack.Pop();
                Context.CLRLogger.Debug($"[Initobj] -> Popped Stack Item {item.ValRef.Value}");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Dup)
            {
                Context.CLRLogger.Debug("Attempting Dup");
                var stackItem = funcContext.BuilderStack.Peek();
                funcContext.BuilderStack.Push(stackItem);
                Context.CLRLogger.Debug($"[Dup] -> Pushed a Duplicate of {stackItem.ValRef.Value} onto Stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Newobj)
            {
                var operand = (MethodDefinition) instruction.Operand;
                LLVMValueRef valRef;
                if (operand.DeclaringType.IsClass)
                    valRef = LLVM.BuildMalloc(funcContext.Builder,
                        Context.FullSymbolToTypeRef[operand.DeclaringType.FullName].StructTypeRef,
                        $"Malloc_{operand.DeclaringType.Name}");
                else
                    valRef = LLVM.BuildAlloca(funcContext.Builder,
                        Context.FullSymbolToTypeRef[operand.DeclaringType.FullName].StructTypeRef,
                        $"Alloc_{operand.DeclaringType.Name}");
                funcContext.BuilderStack.Push(new BuilderStackItem(operand.DeclaringType, valRef));
                ProcessCall(instruction, funcContext.Builder, funcContext.BuilderStack);
                funcContext.BuilderStack.Push(new BuilderStackItem(operand.DeclaringType, valRef));
                Context.CLRLogger.Debug(
                    $"[Newobj {operand.FullName}] -> Called Ctor and pushed {funcContext.BuilderStack.Peek().ValRef.Value} to stack");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Callvirt ||
                     instruction.OpCode == OpCodes.Call)
            {
                ProcessCall(instruction, funcContext.Builder, funcContext.BuilderStack);
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Box)
            {
                var operand = (TypeReference) instruction.Operand;
                Context.CLRLogger.Debug("[Box] -> Allocated as Reference {0}");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Br ||
                     instruction.OpCode == OpCodes.Br_S)
            {
                var operand = (Instruction) instruction.Operand;
                var branchToBlock = funcContext.BranchTo[operand];
                LLVM.BuildBr(funcContext.Builder, branchToBlock);
                Context.CLRLogger.Debug(
                    $"[{instruction.OpCode} {operand}] -> Conditionally Redirect Control Context to Branch, terminating this block.");
                return new BuildResult(true, true);
            }


            if (instruction.OpCode == OpCodes.Beq ||
                instruction.OpCode == OpCodes.Beq_S ||
                instruction.OpCode == OpCodes.Bge ||
                instruction.OpCode == OpCodes.Bge_S ||
                instruction.OpCode == OpCodes.Bge_Un ||
                instruction.OpCode == OpCodes.Bge_Un_S ||
                instruction.OpCode == OpCodes.Bgt ||
                instruction.OpCode == OpCodes.Bgt_S ||
                instruction.OpCode == OpCodes.Bgt_Un ||
                instruction.OpCode == OpCodes.Bgt_Un_S ||
                instruction.OpCode == OpCodes.Ble ||
                instruction.OpCode == OpCodes.Ble_S ||
                instruction.OpCode == OpCodes.Ble_Un ||
                instruction.OpCode == OpCodes.Ble_Un_S ||
                instruction.OpCode == OpCodes.Blt ||
                instruction.OpCode == OpCodes.Blt_S ||
                instruction.OpCode == OpCodes.Blt_Un ||
                instruction.OpCode == OpCodes.Blt_Un_S ||
                instruction.OpCode == OpCodes.Bne_Un ||
                instruction.OpCode == OpCodes.Bne_Un_S)
            {
                LLVMIntPredicate intCmp;

                if (instruction.OpCode == OpCodes.Beq ||
                    instruction.OpCode == OpCodes.Beq_S)
                    intCmp = LLVMIntPredicate.LLVMIntEQ;
                else if (instruction.OpCode == OpCodes.Bne_Un ||
                         instruction.OpCode == OpCodes.Bne_Un_S)
                    intCmp = LLVMIntPredicate.LLVMIntNE;
                else if (instruction.OpCode == OpCodes.Bge ||
                         instruction.OpCode == OpCodes.Bge_S)
                    intCmp = LLVMIntPredicate.LLVMIntSGE;
                else if (instruction.OpCode == OpCodes.Bge_Un ||
                         instruction.OpCode == OpCodes.Bge_Un_S)
                    intCmp = LLVMIntPredicate.LLVMIntUGE;
                else if (instruction.OpCode == OpCodes.Bgt ||
                         instruction.OpCode == OpCodes.Bgt_S)
                    intCmp = LLVMIntPredicate.LLVMIntSGT;
                else if (instruction.OpCode == OpCodes.Bgt_Un ||
                         instruction.OpCode == OpCodes.Bgt_Un_S)
                    intCmp = LLVMIntPredicate.LLVMIntUGT;
                else if (instruction.OpCode == OpCodes.Ble ||
                         instruction.OpCode == OpCodes.Ble_S)
                    intCmp = LLVMIntPredicate.LLVMIntSLE;
                else if (instruction.OpCode == OpCodes.Ble_Un ||
                         instruction.OpCode == OpCodes.Ble_Un_S)
                    intCmp = LLVMIntPredicate.LLVMIntULE;
                else if (instruction.OpCode == OpCodes.Blt ||
                         instruction.OpCode == OpCodes.Blt_S)
                    intCmp = LLVMIntPredicate.LLVMIntSLT;
                else if (instruction.OpCode == OpCodes.Blt_Un ||
                         instruction.OpCode == OpCodes.Blt_Un_S)
                    intCmp = LLVMIntPredicate.LLVMIntULT;
                else
                    throw new BadImageFormatException();

                var rval = funcContext.BuilderStack.Pop();
                var lval = funcContext.BuilderStack.Pop();
                var operand = (Instruction) instruction.Operand;
                var branchToBlock = funcContext.BranchTo[operand];
                funcContext.CurrentBlockRef = LLVM.AppendBasicBlock(funcContext.CurrentBlockRef, "branch");

                var cmp = LLVM.BuildICmp(funcContext.Builder, intCmp, lval.ValRef.Value,
                    rval.ValRef.Value, instruction.OpCode.ToString());

                LLVM.BuildCondBr(funcContext.Builder,
                    cmp,
                    funcContext.CurrentBlockRef, branchToBlock);

                if (funcContext.BranchTo.ContainsKey(instruction.Next))
                    return new BuildResult(true, true);

                LLVM.PositionBuilderAtEnd(funcContext.Builder, funcContext.CurrentBlockRef);
                AddBlockToProcess(funcContext, instruction);

                Context.CLRLogger.Debug(
                    $"[{instruction.OpCode} {operand}] -> Popped {lval} and {rval} and pushed {cmp} and branched to {funcContext.BranchTo}");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Brfalse ||
                     instruction.OpCode == OpCodes.Brfalse_S ||
                     instruction.OpCode == OpCodes.Brtrue ||
                     instruction.OpCode == OpCodes.Brtrue_S)
            {
                var lval = funcContext.BuilderStack.Pop();
                var operand = (Instruction) instruction.Operand;
                var branchToBlock = funcContext.BranchTo[operand];
                funcContext.CurrentBlockRef = LLVM.AppendBasicBlock(funcContext.CurrentBlockRef, "branch");
                LLVMValueRef rval;
                if (instruction.OpCode == OpCodes.Brtrue ||
                    instruction.OpCode == OpCodes.Brtrue_S)
                    rval = LLVM.ConstInt(lval.ValRef.Value.TypeOf(), 1, new LLVMBool(0));
                else
                    rval = LLVM.ConstInt(lval.ValRef.Value.TypeOf(), 0, new LLVMBool(0));

                var cmp = LLVM.BuildICmp(funcContext.Builder, LLVMIntPredicate.LLVMIntEQ, lval.ValRef.Value,
                    rval, instruction.OpCode.ToString());

                LLVM.BuildCondBr(funcContext.Builder,
                    cmp,
                    funcContext.CurrentBlockRef, branchToBlock);
                if (funcContext.BranchTo.ContainsKey(instruction.Next))
                {
                    return new BuildResult(true, true);
                }

                LLVM.PositionBuilderAtEnd(funcContext.Builder, funcContext.CurrentBlockRef);
                AddBlockToProcess(funcContext, instruction);

                Context.CLRLogger.Debug(
                    $"[{instruction.OpCode} {operand}] -> Created Branch");
                return new BuildResult(true);
            }
            else if (instruction.OpCode == OpCodes.Ret)
            {
                if (funcContext.MethodDef.ReturnType.FullName == "System.Void")
                {
                    LLVM.BuildRetVoid(funcContext.Builder);
                    Context.CLRLogger.Debug($"[Ret {funcContext.MethodDef.ReturnType.FullName}] -> Build Return.");
                    return new BuildResult(true, true);
                }

                var val = funcContext.BuilderStack.Pop();
                var ret = LLVM.BuildRet(funcContext.Builder, val.ValRef.Value);
                Context.CLRLogger.Debug(
                    $"[Ret {funcContext.MethodDef.ReturnType.FullName}] -> Popped {val.ValRef.Value} from Stack and Build Return with {ret}");
                return new BuildResult(true, true);
            }

            return new BuildResult(false);
        }

        internal void ProcessCall(Instruction instruction, LLVMBuilderRef builder,
            Stack<BuilderStackItem> builderStack)
        {
            var methodToCall = (MethodReference) instruction.Operand;
            Context.CLRLogger.Debug(methodToCall.ToString());
            var resolvedMethodToCall = methodToCall.Resolve();
            if (methodToCall.HasThis && resolvedMethodToCall.DeclaringType?.BaseType != null &&
                (resolvedMethodToCall.DeclaringType.BaseType.FullName == "System.Delegate" ||
                 resolvedMethodToCall.DeclaringType.BaseType.FullName == "System.MulticastDelegate"))
            {
                var refs = new LLVMValueRef[methodToCall.Parameters.Count];
                if (refs.Length > 0)
                    for (var i = methodToCall.Parameters.Count - 1; i > -1; --i)
                        refs[i] = builderStack.Pop().ValRef.Value;

                var reference = builderStack.Pop();
                var stackItem = new BuilderStackItem(methodToCall.ReturnType.Resolve(),
                    LLVM.BuildCall(builder, reference.ValRef.Value, refs, ""));
                // We know this is a delegate, now to call it!
                builderStack.Push(stackItem);
                Context.CLRLogger.Debug(
                    $"[{instruction.OpCode.Name} {methodToCall.FullName}] -> Determined as Delegate, Popped Reference {reference} and Push {stackItem.ValRef} to Stack");
            }
            else
            {
                var refs = new LLVMValueRef[methodToCall.HasThis
                    ? methodToCall.Parameters.Count + 1
                    : methodToCall.Parameters.Count];
                var symbol = SymbolHelper.GetCSToLLVMSymbolName(methodToCall);

                if (refs.Length > (methodToCall.HasThis ? 1 : 0))
                    for (var i = methodToCall.HasThis
                            ? methodToCall.Parameters.Count
                            : methodToCall.Parameters.Count - 1;
                        i > (methodToCall.HasThis ? 0 : -1);
                        --i)
                        refs[i] = builderStack.Pop().ValRef.Value;

                if (!Context.SymbolToCallableFunction.ContainsKey(SymbolHelper.GetCSToLLVMSymbolName(methodToCall)))
                {
                    Context.CLRLogger.Debug("Resolving Function");
                    Context.CLR.ProcessFunction(methodToCall.Resolve(), false);
                }

                if (methodToCall.HasThis)
                {
                    var reference = builderStack.Pop();
                    if (methodToCall.DeclaringType.FullName != reference.Type.FullName)
                        refs[0] = LLVM.BuildPointerCast(builder, reference.ValRef.Value,
                            Context.SymbolToCallableFunction[symbol].GetFirstParam().TypeOf(), "");
                    else
                        refs[0] = reference.ValRef.Value;
                }

                if (methodToCall.ReturnType.FullName != "System.Void")
                {
                    var stackItem = new BuilderStackItem(methodToCall.ReturnType.Resolve(),
                        LLVM.BuildCall(builder,
                            Context.SymbolToCallableFunction[SymbolHelper.GetCSToLLVMSymbolName(methodToCall)],
                            refs, ""));
                    builderStack.Push(stackItem);
                    Context.CLRLogger.Debug(
                        $"[{instruction.OpCode.Name} {methodToCall.FullName}] -> Push {stackItem.ValRef} to Stack");
                    return;
                }

                var call = LLVM.BuildCall(builder,
                    Context.SymbolToCallableFunction[SymbolHelper.GetCSToLLVMSymbolName(methodToCall)],
                    refs, "");
                Context.CLRLogger.Debug($"[{instruction.OpCode.Name} {methodToCall.FullName}] -> Called {call}");
            }
        }

        internal void AddBlockToProcess(FunctionContext funcContext, Instruction instruction)
        {
            if (!funcContext.ProcessedBranch.Contains(instruction.Next)) return;
            funcContext.BranchTo.Add(instruction.Next, funcContext.CurrentBlockRef);
            funcContext.BranchToProcess.Push(
                new KeyValuePair<Instruction, LLVMBasicBlockRef>(instruction.Next, funcContext.CurrentBlockRef));
        }
    }
}