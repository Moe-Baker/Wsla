using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

using System;
using System.Collections.Generic;
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

            public INamedTypeSymbol ArrayResolver;

            public INamedTypeSymbol ListResolver;
            public INamedTypeSymbol ListType;

            public INamedTypeSymbol ArraySegmentResolver;
            public INamedTypeSymbol ArraySegmentType;

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

                    ArrayResolver = compilation.GetGenericTypeByMetadataName(Constants.ArrayNetworkSerializationResolver, 1),

                    ArraySegmentResolver = compilation.GetGenericTypeByMetadataName(Constants.ArraySegmentNetworkSerializationResolver, 1),
                    ArraySegmentType = compilation.GetGenericTypeByMetadataName("System.ArraySegment", 1),

                    ListResolver = compilation.GetGenericTypeByMetadataName(Constants.ListNetworkSerializationResolver, 1),
                    ListType = compilation.GetGenericTypeByMetadataName("System.Collections.Generic.List", 1),

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

        static ITypeSymbol GetGeneratorUsageType((IMethodSymbol Method, CompilationData Compilation) input, CancellationToken token)
        {
            var parameters = input.Method.TypeParameters;
            var arguments = input.Method.TypeArguments;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (CodeUtility.HasAttribute(parameters[i], input.Compilation.MarkerAttribute))
                {
                    if (arguments[i].TypeKind is TypeKind.TypeParameter)
                        continue;

                    return arguments[i];
                }
            }

            return default;
        }

        static bool IsNotNull<T>(T item) where T : class => ReferenceEquals(item, null) is false;

        static void WriteUsages(SourceProductionContext context, (ImmutableArray<ITypeSymbol> Usages, CompilationData Compilation) source)
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

        public static bool WriteResolverRegisteration(CodeStringBuilder builder, CompilationData compilation, string prefix, ImmutableArray<ITypeSymbol> usages)
        {
            var resolvers = ResolveUsages(compilation, usages);
            if (resolvers.Count is 0)
                return false;

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
                        foreach (var pair in resolvers)
                        {
                            builder.Write(Constants.NetworkSerializationResolver);
                            builder.Write(".Register");

                            using (builder.GenericArguments())
                            {
                                builder.Write(pair.Key);
                                builder.Write(", ");
                                builder.Write(pair.Value);
                            }

                            using (builder.Parameters()) { }

                            builder.EndLine();

                            builder.Newline();
                        }
                    }
                }
            }

            return true;

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
        static Dictionary<ITypeSymbol, INamedTypeSymbol> ResolveUsages(CompilationData compilation, ImmutableArray<ITypeSymbol> usages)
        {
            var resolvers = new Dictionary<ITypeSymbol, INamedTypeSymbol>(usages.Length * 2, SymbolEqualityComparer.Default);

            foreach (var usage in usages)
                Resolvers.Resolve(compilation, usage, resolvers);

            return resolvers;
        }

        public class Constants : GlobalNetworkGenerator.Constants
        {
            public static readonly string Namespace = $"{Name}.Serialization";

            public static readonly string NetworkSerializationResolver = $"{Namespace}.{nameof(NetworkSerializationResolver)}";

            public static readonly string NetworkSerializationResolverRegisterationAttribute = $"{Namespace}.{nameof(NetworkSerializationResolverRegisterationAttribute)}";

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
        }

        public class Resolvers
        {
            public static bool Resolve(CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                //Early exit for duplicates
                if (resolvers.ContainsKey(usage))
                    return true;

                //Blittable
                if (ResolveBlittable(compilation, usage, resolvers))
                    return true;

                //Manual
                if (ResolveManual(compilation, usage, resolvers))
                    return true;

                //Auto
                if (ResolveAuto(compilation, usage, resolvers))
                    return true;

                //Array
                if (ResolveArray(compilation, usage, resolvers))
                    return true;

                //Array Segment
                if (ResolveArraySegment(compilation, usage, resolvers))
                    return true;

                //List
                if (ResolveList(compilation, usage, resolvers))
                    return true;

                return false;
            }

            static bool ResolveArray(CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                var array = usage as IArrayTypeSymbol;

                if (array is null)
                    return false;

                var element = array.ElementType;

                resolvers[usage] = compilation.ArrayResolver.Construct(element);

                Resolve(compilation, element, resolvers);

                return true;
            }
            static bool ResolveArraySegment(CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                var segment = usage as INamedTypeSymbol;

                if (segment is null)
                    return false;

                if (segment.IsGenericType is false)
                    return false;

                if (CodeUtility.SymbolEquality.Equals(segment.ConstructedFrom, compilation.ArraySegmentType) is false)
                    return false;

                var element = segment.TypeArguments[0];

                resolvers[usage] = compilation.ArraySegmentResolver.Construct(element);

                Resolve(compilation, element, resolvers);

                return true;
            }
            static bool ResolveList(CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                var list = usage as INamedTypeSymbol;

                if (list is null)
                    return false;

                if (list.IsGenericType is false)
                    return false;

                if (CodeUtility.SymbolEquality.Equals(list.ConstructedFrom, compilation.ListType) is false)
                    return false;

                var element = list.TypeArguments[0];

                resolvers[usage] = compilation.ListResolver.Construct(element);

                Resolve(compilation, element, resolvers);

                return true;
            }

            static bool ResolveManual(CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                if (usage.ImplementsInterface(compilation.ManualContract) is false)
                    return false;

                resolvers[usage] = compilation.ManualResolver.Construct(usage);

                return true;
            }

            static bool ResolveAuto(CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                if (usage.ImplementsInterface(compilation.AutoContract) is false)
                    return false;

                resolvers[usage] = compilation.AutoResolver.Construct(usage);

                return true;
            }

            static bool ResolveBlittable(CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                if (CodeUtility.HasAttribute(usage, compilation.BlittableAttribute) is false)
                    return false;

                resolvers[usage] = compilation.BlittableResolver.Construct(usage);

                return true;
            }
        }
    }
}