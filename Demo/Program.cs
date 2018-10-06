using System;

namespace Demo
{
    internal class Program
    {
        private static void Main()
        {
            var ptr = 5;
            var Val = 5;
            Val += ptr;
            Console.WriteLine("%i\n", Val);
        }
    }
}