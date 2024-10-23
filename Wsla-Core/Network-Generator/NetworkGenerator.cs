using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Wsla.Generator
{
    [Generator]
    public class NetworkGenerator : IIncrementalGenerator
    {
        public static class DiagnosticCodes
        {
            public static readonly DiagnosticDescriptor Example = new DiagnosticDescriptor("WSLA-1", "Example Title", "Example Message", "Example Category", DiagnosticSeverity.Error, true);
        }

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var marker = context.CompilationProvider.Select(LocateGeneratorAttribute);

            var methods = context.SyntaxProvider
                .CreateSyntaxProvider(IsInvocationSnytax, GetInvocationMethodDefinition)
                .Where(IsNotNull);

            var usages = methods.Combine(marker)
                .Select(GetGeneratorUsageType)
                .Where(IsNotNull)
                .Collect();

            context.RegisterSourceOutput(usages, WriteUsages);
        }

        void WriteUsages(SourceProductionContext context, ImmutableArray<INamedTypeSymbol> source)
        {
            var set = source.ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var type in set)
                CodeUtility.Log(type.constr);

            var builder = new StringBuilder();

            try
            {
                builder.AppendLine("public class NetworkGeneratedCode");
                builder.AppendLine("{");
                {
                    builder.Append("public const string Text = ");

                    builder.Append('"');

                    foreach (var type in set)
                    {
                        builder.Append(type.Name);
                        builder.Append(", ");
                    }

                    builder.Append('"');
                    builder.Append(';');
                }
                builder.AppendLine("}");

                context.AddSource("NetworkGeneratedCode.cs", builder.ToString());
            }
            catch (Exception ex)
            {
                CodeUtility.Log(ex);
                throw;
            }

            CodeUtility.Log("DONE");
        }

        static INamedTypeSymbol LocateGeneratorAttribute(Compilation compilation, CancellationToken token)
        {
            return compilation.GetTypeByMetadataName(CodeConstants.NetworkSerializerGeneraterAttribute);
        }

        static bool IsInvocationSnytax(SyntaxNode node, CancellationToken token) => node is InvocationExpressionSyntax;
        static IMethodSymbol GetInvocationMethodDefinition(GeneratorSyntaxContext context, CancellationToken token)
        {
            var info = context.SemanticModel.GetSymbolInfo(context.Node, token);

            return info.Symbol as IMethodSymbol;
        }

        static INamedTypeSymbol GetGeneratorUsageType((IMethodSymbol Method, INamedTypeSymbol Marker) input, CancellationToken token)
        {
            var parameters = input.Method.TypeParameters;
            var arguments = input.Method.TypeArguments;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (CodeUtility.HasAttribute(parameters[i], input.Marker))
                {
                    if (arguments[i] is INamedTypeSymbol type)
                        return type;
                }
            }

            return default;
        }

        static bool IsNotNull<T>(T item) where T : class => ReferenceEquals(item, null) is false;
    }

    public static class CodeConstants
    {
        public const string NetworkSerializerGeneraterAttribute = "Wsla.Serialization.NetworkSerializationMarkerAttribute";

        public const string ArrayNetworkSerializationResolver = "Wsla.Serialization.ArrayNetworkSerializationResolver";
        public const string ArraySegmentNetworkSerializationResolver = "Wsla.Serialization.ArraySegmentNetworkSerializationResolver";
        public const string ListNetworkSerializationResolver = "Wsla.Serialization.ListNetworkSerializationResolver";

        public const string ManualNetworkSerializationResolver = "Wsla.Serialization.ManualNetworkSerializationResolver";
        public const string AutoNetworkSerializationResolver = "Wsla.Serialization.AutoNetworkSerializationResolver";
        public const string BlittableNetworkSerializationResolver = "Wsla.Serialization.BlittableNetworkSerializationResolver";
    }

    public static class CodeUtility
    {
        public static SymbolEqualityComparer SymbolEquality => SymbolEqualityComparer.Default;

        public static bool HasAttribute(ITypeParameterSymbol parameter, INamedTypeSymbol attribute)
        {
            var collection = parameter.GetAttributes();

            foreach (var data in collection)
                if (SymbolEquality.Equals(attribute, data.AttributeClass))
                    return true;

            return false;
        }

        public static void Log(object target) => File.AppendAllText($"C:/Files/Log.txt", target.ToString() + Environment.NewLine);
    }
}