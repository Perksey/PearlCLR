using Mono.Cecil.Cil;

namespace PearlCLR.JIT
{
    public abstract class OpcodeHandlerModule
    {
        protected JITContext Context { get; }

        protected OpcodeHandlerModule(JITContext context)
        {
            Context = context;
        }
        
        public abstract bool CanRun(Instruction instruction);
        public abstract BuildResult Build(Instruction instruction, FunctionContext funcContext);
    }

    public struct BuildResult
    {
        public bool Success { get; set; }
        public bool BreakLoop { get; set; }

        public BuildResult(bool success, bool breakLoop = false)
        {
            Success = success;
            BreakLoop = breakLoop;
        }
    }
}