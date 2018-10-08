using LLVMSharp;
using Mono.Cecil.Cil;

namespace PearlCLR.JIT
{
    public class ParameterOpcodeModule : OpcodeHandlerModule
    {
        public ParameterOpcodeModule(JITContext context) : base(context)
        {
        }

        public override bool CanRun(Instruction instruction)
        {
            return instruction.OpCode == OpCodes.Ldarg_0 ||
                   instruction.OpCode == OpCodes.Ldarg_1 ||
                   instruction.OpCode == OpCodes.Ldarg_2 ||
                   instruction.OpCode == OpCodes.Ldarg_3 ||
                   instruction.OpCode == OpCodes.Ldarg;
        }

        public override BuildResult Build(Instruction instruction, FunctionContext funcContext)
        {
            if (instruction.OpCode == OpCodes.Ldarg_0)
            {
                if (!funcContext.MethodDef.HasThis)
                    funcContext.BuilderStack.Push(new BuilderStackItem(
                        funcContext.MethodDef.Parameters[0].ParameterType,
                        LLVM.GetFirstParam(funcContext.FunctionRef)));
                else
                    funcContext.BuilderStack.Push(new BuilderStackItem(funcContext.MethodDef.DeclaringType,
                        LLVM.GetFirstParam(funcContext.FunctionRef)));

                Context.CLRLogger.Debug($"[Ldarg_0] -> {LLVM.GetFirstParam(funcContext.FunctionRef)} pushed to Stack");
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Ldarg_1)
            {
                funcContext.BuilderStack.Push(
                    new BuilderStackItem(
                        funcContext.MethodDef.Parameters[funcContext.MethodDef.HasThis ? 0 : 1].ParameterType,
                        LLVM.GetParam(funcContext.FunctionRef, 1)));
                Context.CLRLogger.Debug($"[Ldarg_1] -> {LLVM.GetParams(funcContext.FunctionRef)[1]} pushed to Stack");
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Ldarg_2)
            {
                funcContext.BuilderStack.Push(
                    new BuilderStackItem(
                        funcContext.MethodDef.Parameters[funcContext.MethodDef.HasThis ? 0 : 1].ParameterType,
                        LLVM.GetParam(funcContext.FunctionRef, 2)));
                Context.CLRLogger.Debug($"[Ldarg_2] -> {LLVM.GetParams(funcContext.FunctionRef)[2]} pushed to Stack");
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Ldarg_3)
            {
                funcContext.BuilderStack.Push(
                    new BuilderStackItem(
                        funcContext.MethodDef.Parameters[funcContext.MethodDef.HasThis ? 0 : 1].ParameterType,
                        LLVM.GetParam(funcContext.FunctionRef, 3)));
                Context.CLRLogger.Debug($"[Ldarg_3] -> {LLVM.GetParams(funcContext.FunctionRef)[3]} pushed to Stack");
                return new BuildResult(true);
            }

            if (instruction.OpCode == OpCodes.Ldarg)
            {
                var varDef = (VariableDefinition) instruction.Operand;
                funcContext.BuilderStack.Push(new BuilderStackItem(
                    funcContext.MethodDef.Parameters[funcContext.MethodDef.HasThis ? varDef.Index - 1 : varDef.Index]
                        .ParameterType,
                    LLVM.GetParam(funcContext.FunctionRef, (uint) varDef.Index)));
                Context.CLRLogger.Debug(
                    $"[Ldarg {varDef.Index}] -> {LLVM.GetParams(funcContext.FunctionRef)[1]} pushed to Stack");
                return new BuildResult(true);
            }

            return new BuildResult(false);
        }
    }
}