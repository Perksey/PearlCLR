using System;

internal class Program
{
    static void Main()
    {
        BadStuff();
        Console.WriteLine("Hello!");
    }

    static unsafe void BadStuff()
    {
        const string s = "Hello!";
        fixed (char* c = s)
        {
            c[2] = 'n';
        }
    }
}