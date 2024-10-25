using Microsoft.CodeAnalysis;

using System;
using System.IO;

namespace Wsla.Generator
{
    public static class CodeUtility
    {
        public static SymbolEqualityComparer SymbolEquality => SymbolEqualityComparer.Default;

        public static void Log(object target)
        {
            try
            {
                File.AppendAllText($"C:/Files/Log.txt", target.ToString() + Environment.NewLine);
            }
            catch (Exception)
            {

            }
        }

        public static bool DefaultEquality(this ISymbol right, ISymbol left) => SymbolEquality.Equals(right, left);

        public static bool HasAttribute(this ISymbol parameter, INamedTypeSymbol attribute)
        {
            var collection = parameter.GetAttributes();

            foreach (var data in collection)
                if (SymbolEquality.Equals(attribute, data.AttributeClass))
                    return true;

            return false;
        }

        public static void WriteAssemblyAsClass(IAssemblySymbol assembly, CodeStringBuilder builder)
        {
            foreach (var character in assembly.Name)
            {
                if (char.IsLetter(character))
                    builder.Write(character);
                else
                    builder.Write("_");
            }
        }

        public static INamedTypeSymbol GetGenericTypeByMetadataName(this Compilation compilation, string name, int generics)
        {
            return compilation.GetTypeByMetadataName($"{name}`{generics}");
        }

        public static bool ImplementsInterface(this ITypeSymbol type, INamedTypeSymbol target)
        {
            return type.AllInterfaces.Contains(target);
        }
    }
}