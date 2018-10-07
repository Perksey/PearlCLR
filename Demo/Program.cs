using System;

internal class Program
{
    private static void Main()
    {
        var ptr = 5;
        if (ptr > 3)
            ptr += 5;
        var Val = 5;
        if (Val < 3)
            Val += 5;
        Val += ptr;
        Console.WriteLine("%i\n", Val);
        Console.ReadLine();
    }
}