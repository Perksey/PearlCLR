using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using jit = PearlCLR.JIT.PearlCLR;
using LLVMSharp;
namespace PearlCLR
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Attempting to Process Demo.dll");
            var jitter = new jit("YourDllPathHere");
            jitter.ProcessMainModule();
            Console.WriteLine("Application Completed!");
        }

        private static void Handler(string reason)
        {
            Console.WriteLine(reason);
        }
    }
}
