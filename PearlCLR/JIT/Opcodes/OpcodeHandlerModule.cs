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
        public abstract bool CanRun(Instruction opcode);
        public abstract void Build(Instruction opcode, FunctionContext context);
    }
}