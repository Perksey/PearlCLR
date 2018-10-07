using Mono.Cecil;

namespace PearlCLR
{
    public static class SymbolHelper
    {
        public static string GetCSToLLVMSymbolName(MethodReference method) =>
            $"{method.DeclaringType.FullName}::{method.Name}";
    }
}