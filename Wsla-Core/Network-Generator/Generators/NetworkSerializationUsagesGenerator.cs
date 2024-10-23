using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace Wsla.Generator
{
    [Generator]
    public class NetworkSerializationUsagesGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var compilation = context.CompilationProvider.Select(CompilationData.Create);

            var methods = context.SyntaxProvider
                .CreateSyntaxProvider(IsInvocationSnytax, GetInvocationMethodDefinition)
                .Where(IsNotNull);

            var usages = methods.Combine(compilation)
                .Select(GetGeneratorUsageType)
                .Where(IsNotNull)
                .Collect()
                .Combine(compilation);

            context.RegisterSourceOutput(usages, WriteUsages);
        }

        public struct CompilationData : IEquatable<CompilationData>
        {
            public IAssemblySymbol Assembly;
            public INamedTypeSymbol MarkerAttribute;

            public INamedTypeSymbol ManualResolver;
            public INamedTypeSymbol ManualContract;

            public INamedTypeSymbol AutoResolver;
            public INamedTypeSymbol AutoContract;

            public INamedTypeSymbol BlittableResolver;
            public INamedTypeSymbol BlittableAttribute;

            (IAssemblySymbol, INamedTypeSymbol) GetComparableFields() => (Assembly, MarkerAttribute);

            public override bool Equals(object obj)
            {
                if (obj is CompilationData other)
                    return Equals(other);

                return false;
            }
            public bool Equals(CompilationData other) => GetComparableFields() == other.GetComparableFields();

            public override int GetHashCode() => GetComparableFields().GetHashCode();

            public static CompilationData Create(Compilation compilation, CancellationToken cancellation)
            {
                var data = new CompilationData()
                {
                    Assembly = compilation.Assembly,
                    MarkerAttribute = compilation.GetTypeByMetadataName(Constants.NetworkSerializationMarkerAttribute),

                    ManualResolver = compilation.GetGenericTypeByMetadataName(Constants.ManualNetworkSerializationResolver, 1),
                    ManualContract = compilation.GetTypeByMetadataName(Constants.IManualNetworkSerialization),

                    AutoResolver = compilation.GetGenericTypeByMetadataName(Constants.AutoNetworkSerializationResolver, 1),
                    AutoContract = compilation.GetTypeByMetadataName(Constants.IAutoNetworkSerialization),

                    BlittableResolver = compilation.GetGenericTypeByMetadataName(Constants.BlittableNetworkSerializationResolver, 1),
                    BlittableAttribute = compilation.GetTypeByMetadataName(Constants.NetworkBlittableAttribute),
                };

                return data;
            }
        }

        static bool IsInvocationSnytax(SyntaxNode node, CancellationToken token) => node is InvocationExpressionSyntax;
        static IMethodSymbol GetInvocationMethodDefinition(GeneratorSyntaxContext context, CancellationToken token)
        {
            var info = context.SemanticModel.GetSymbolInfo(context.Node, token);

            return info.Symbol as IMethodSymbol;
        }

        static INamedTypeSymbol GetGeneratorUsageType((IMethodSymbol Method, CompilationData Compilation) input, CancellationToken token)
        {
            var parameters = input.Method.TypeParameters;
            var arguments = input.Method.TypeArguments;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (CodeUtility.HasAttribute(parameters[i], input.Compilation.MarkerAttribute))
                {
                    if (arguments[i] is INamedTypeSymbol type)
                        return type;
                }
            }

            return default;
        }

        static bool IsNotNull<T>(T item) where T : class => ReferenceEquals(item, null) is false;

        static void WriteUsages(SourceProductionContext context, (ImmutableArray<INamedTypeSymbol> Usages, CompilationData Compilation) source)
        {
            try
            {
                var builder = new CodeStringBuilder(512);

                WriteResolverRegisteration(builder, source.Compilation, "Usages", source.Usages);

                CodeUtility.Log(builder.ToString());

                context.AddSource("UsagesNetworkSerializationRegisteration.g.cs", builder.ToString());
            }
            catch (Exception ex)
            {
                CodeUtility.Log(ex);
            }

            CodeUtility.Log("DONE");
        }

        public static void WriteResolverRegisteration(CodeStringBuilder builder, CompilationData compilation, string prefix, ImmutableArray<INamedTypeSymbol> usages)
        {
            //Assembly attribute
            builder.Write("[assembly: ");
            builder.Write(Constants.NetworkSerializationResolverRegisterationAttribute);
            builder.Write("(typeof(");
            WriteNamespaceName();
            builder.Write(".");
            WriteClassName();
            builder.Write("), 0, \"Register\")]");

            builder.Newline(2);

            //Namespace
            builder.Write("namespace ");
            WriteNamespaceName();

            using (builder.CodeBlock())
            {
                //Class Declaration
                builder.Write("public class ");
                WriteClassName();

                using (builder.CodeBlock())
                {
                    //Registeration Method
                    builder.Write("public static void Register()");
                    using (builder.CodeBlock())
                    {
                        foreach (var usage in usages)
                        {
                            if (Resolvers.TryCreate(compilation, usage, out var resolver))
                            {
                                builder.Write(Constants.NetworkSerializationResolver);
                                builder.Write(".Register");

                                using (builder.GenericArguments())
                                {
                                    builder.Write(usage);
                                    builder.Write(", ");
                                    builder.Write(resolver);
                                }

                                using (builder.Parameters()) { }

                                builder.EndLine();
                            }
                            else
                            {
                                builder.Write("//");
                                builder.Write(usage);
                                builder.Write(" -- > ");
                                builder.Write("// No Resolver found for Type");
                            }

                            builder.Newline();
                        }
                    }
                }
            }

            void WriteClassName()
            {
                CodeUtility.WriteAssemblyAsClass(compilation.Assembly, builder);
                builder.Write("_");
                builder.Write(prefix);
                builder.Write("SerializationRegisteration");
            }
            void WriteNamespaceName()
            {
                builder.Write(Constants.Name);
                builder.Write(".Generated");
            }
        }

        public class Constants : GlobalNetworkGenerator.Constants
        {
            public static readonly string Namespace = $"{Name}.Serialization";

            public static readonly string NetworkSerializationResolver = $"{Namespace}.{nameof(NetworkSerializationResolver)}";

            public static readonly string NetworkSerializationMarkerAttribute = $"{Namespace}.{nameof(NetworkSerializationMarkerAttribute)}";

            public static readonly string ArrayNetworkSerializationResolver = $"{Namespace}.{nameof(ArrayNetworkSerializationResolver)}";
            public static readonly string ArraySegmentNetworkSerializationResolver = $"{Namespace}.{nameof(ArraySegmentNetworkSerializationResolver)}";
            public static readonly string ListNetworkSerializationResolver = $"{Namespace}.{nameof(ListNetworkSerializationResolver)}";

            public static readonly string ManualNetworkSerializationResolver = $"{Namespace}.{nameof(ManualNetworkSerializationResolver)}";
            public static readonly string IManualNetworkSerialization = $"{Namespace}.{nameof(IManualNetworkSerialization)}";

            public static readonly string AutoNetworkSerializationResolver = $"{Namespace}.{nameof(AutoNetworkSerializationResolver)}";
            public static readonly string IAutoNetworkSerialization = $"{Namespace}.{nameof(IAutoNetworkSerialization)}";

            public static readonly string BlittableNetworkSerializationResolver = $"{Namespace}.{nameof(BlittableNetworkSerializationResolver)}";
            public static readonly string NetworkBlittableAttribute = $"{Namespace}.{nameof(NetworkBlittableAttribute)}";

            public static readonly string NetworkSerializationResolverRegisterationAttribute = $"{Namespace}.{nameof(NetworkSerializationResolverRegisterationAttribute)}";
        }

        public class Resolvers
        {
            public static bool TryCreate(CompilationData compilation, INamedTypeSymbol usage, out INamedTypeSymbol resolver)
            {
                if (TryResolveBlittable(compilation, usage, out resolver))
                    return true;

                if (TryResolveManual(compilation, usage, out resolver))
                    return true;

                if (TryResolveAuto(compilation, usage, out resolver))
                    return true;

                resolver = default;
                return false;
            }

            static bool TryResolveManual(CompilationData compilation, INamedTypeSymbol usage, out INamedTypeSymbol resolver)
            {
                if (usage.ImplementsInterface(compilation.ManualContract) is false)
                {
                    resolver = default;
                    return false;
                }

                resolver = compilation.ManualResolver.Construct(usage);
                return true;
            }

            static bool TryResolveAuto(CompilationData compilation, INamedTypeSymbol usage, out INamedTypeSymbol resolver)
            {
                if (usage.ImplementsInterface(compilation.AutoContract) is false)
                {
                    resolver = default;
                    return false;
                }

                resolver = compilation.AutoResolver.Construct(usage);
                return true;
            }

            static bool TryResolveBlittable(CompilationData compilation, INamedTypeSymbol usage, out INamedTypeSymbol resolver)
            {
                if (CodeUtility.HasAttribute(usage, compilation.BlittableAttribute) is false)
                {
                    resolver = default;
                    return false;
                }

                resolver = compilation.BlittableResolver.Construct(usage);
                return true;
            }
        }
    }
}