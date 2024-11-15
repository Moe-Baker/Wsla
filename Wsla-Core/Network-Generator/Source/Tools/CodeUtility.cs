using Microsoft.CodeAnalysis;

using System;
using System.ComponentModel;
using System.Globalization;
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

        public static bool InheritsFrom(this ITypeSymbol child, ITypeSymbol parent)
        {
            var current = child;

            while (true)
            {
                if (DefaultEquality(current, parent))
                    return true;

                current = current.BaseType;
                if (current == null)
                    break;
            }

            return false;
        }

        public static void WriteAssemblyAsClass(IAssemblySymbol assembly, CodeStringBuilder builder) => WriteAssemblyAsClass(assembly, builder);
        public static void WriteAssemblyAsClass(string name, CodeStringBuilder builder)
        {
            foreach (var character in name)
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

        public static bool IsOpenGenericType(this INamedTypeSymbol type)
        {
            if (type.IsGenericType is false)
                return false;

            var parameters = type.TypeArguments;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].TypeKind is TypeKind.TypeParameter)
                    return true;
            }

            return false;
        }
    }
}