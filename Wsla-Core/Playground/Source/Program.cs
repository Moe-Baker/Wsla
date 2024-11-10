using System.Collections;

using Wsla;

NetworkLog.UseConsole();

unchecked
{
    byte a = (byte)400;

    Console.WriteLine(a);

    for (int i = 0; i < 255 * 2; i++)
    {
        a += 1;
        Console.WriteLine(a);
    }
}

while (true)
    Console.ReadKey();