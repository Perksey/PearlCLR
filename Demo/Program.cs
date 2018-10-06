using System;

namespace Demo
{
    class Program
    {
        static unsafe void Main()
        {
            var ptr = 5;
            int Val = 5;
            Val += ptr;
            Console.WriteLine("%i\n", Val);
        }
    }
}