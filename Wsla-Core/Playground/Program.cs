using Wsla.Generator;

var builder = new CodeStringBuilder();

builder.Write("public class Example");

using (builder.CodeBlock())
{
    builder.Write("public const string Text = ");
    using (builder.StringDeclaration())
    {
        builder.Write("Hello World");
    }
    builder.EndLine();

    builder.Newline();

    builder.Write("public static int[] Array = new int[]");
    using (builder.ArrayBlock())
    {
        builder.Write("1");
        builder.EndLine(",");

        builder.Write("2");
        builder.EndLine(",");
    }

    builder.Newline();

    builder.Write("public static void Write()");
    using (builder.CodeBlock())
    {
        builder.Write("return Text");
        builder.EndLine();
    }
}

Console.Write(builder.ToString());

while (true)
    Console.ReadKey();