using System;
using System.IO;

namespace PearlCLRRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Attempting to Process Demo.dll");
            var jitter = new PearlCLR.JIT.PearlCLR(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Demo.dll"));
            jitter.ProcessMainModule();
            Console.WriteLine("Application Completed!");
        }
    }
}