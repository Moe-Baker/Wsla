using System;
using System.IO;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

        public static bool CompareSymbols(this ISymbol right, ISymbol left) => SymbolEquality.Equals(right, left);

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
                if (CompareSymbols(current, parent))
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

        public static bool IsOpenGenericType(this ITypeSymbol type)
        {
            if (type.TypeKind is TypeKind.TypeParameter)
                return true;

            if (type is INamedTypeSymbol named)
                return IsOpenGenericType(named);
            else if (type is IArrayTypeSymbol array)
                return IsOpenGenericType(array.ElementType);

            return false;
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

        public static bool IsPartial(this ITypeSymbol symbol, CancellationToken cancellation = default)
        {
            if (symbol.DeclaringSyntaxReferences.Length is 0)
                return false;

            var declaration = symbol.DeclaringSyntaxReferences[0].GetSyntax(cancellation) as MemberDeclarationSyntax;
            if (declaration is null)
                return false;

            foreach (var modifier in declaration.Modifiers)
                if (modifier.IsKind(SyntaxKind.PartialKeyword))
                    return true;

            return false;
        }

        public static Diagnostic Create(this DiagnosticDescriptor descriptor) => Create(descriptor, Location.None);
        public static Diagnostic Create(this DiagnosticDescriptor descriptor, ISymbol symbol) => Create(descriptor, symbol.Locations[0]);
        public static Diagnostic Create(this DiagnosticDescriptor descriptor, Location location)
        {
            return Diagnostic.Create(descriptor, location);
        }
        public static Diagnostic Create(this DiagnosticDescriptor descriptor, Location location, params object[] arguments)
        {
            return Diagnostic.Create(descriptor, location, arguments);
        }
    }
}