using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
                .CreateSyntaxProvider(IsInvocationSyntax, GetInvocationMethodDefinition)
                .Where(IsNotNull);

            var usages = methods.Combine(compilation)
                .SelectMany(GetGeneratorUsageType)
                .Where(IsNotNull)
                .Collect()
                .Combine(compilation);

            context.RegisterSourceOutput(usages, GenerateSourceCode);
        }

        public struct CompilationData : IEquatable<CompilationData>
        {
            public string AssemblyName;

            public INamedTypeSymbol MarkerAttribute;

            public INamedTypeSymbol ArrayResolver;

            public INamedTypeSymbol ListResolver;
            public INamedTypeSymbol ListType;

            public INamedTypeSymbol DictionaryResolver;
            public INamedTypeSymbol DictionaryType;

            public INamedTypeSymbol ArraySegmentResolver;
            public INamedTypeSymbol ArraySegmentType;

            public INamedTypeSymbol ManualResolver;
            public INamedTypeSymbol ManualContract;

            public INamedTypeSymbol AutoResolver;
            public INamedTypeSymbol AutoContract;

            public INamedTypeSymbol BlittableResolver;
            public INamedTypeSymbol BlittableAttribute;

            public INamedTypeSymbol EnumResolver;

            public INamedTypeSymbol[] TupleResolvers;
            public INamedTypeSymbol NullableResolver;

            public INamedTypeSymbol BehaviourContract;
            public INamedTypeSymbol BehaviourResolver;

            public INamedTypeSymbol ISyncedAsset;
            public INamedTypeSymbol SyncedAssetResolver;

            (string, INamedTypeSymbol) GetComparableFields() => (AssemblyName, MarkerAttribute);

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
                    AssemblyName = compilation.Assembly.Name,
                    MarkerAttribute = compilation.GetTypeByMetadataName(Constants.NetworkSerializationMarkerAttribute),

                    ArrayResolver = compilation.GetGenericTypeByMetadataName(Constants.ArrayNetworkSerializationResolver, 1),

                    ArraySegmentResolver = compilation.GetGenericTypeByMetadataName(Constants.ArraySegmentNetworkSerializationResolver, 1),
                    ArraySegmentType = compilation.GetGenericTypeByMetadataName("System.ArraySegment", 1),

                    ListResolver = compilation.GetGenericTypeByMetadataName(Constants.ListNetworkSerializationResolver, 1),
                    ListType = compilation.GetGenericTypeByMetadataName("System.Collections.Generic.List", 1),

                    DictionaryResolver = compilation.GetGenericTypeByMetadataName(Constants.DictionaryNetworkSerializationResolver, 2),
                    DictionaryType = compilation.GetGenericTypeByMetadataName("System.Collections.Generic.Dictionary", 2),

                    ManualResolver = compilation.GetGenericTypeByMetadataName(Constants.ManualNetworkSerializationResolver, 1),
                    ManualContract = compilation.GetTypeByMetadataName(Constants.IManualNetworkSerialization),

                    AutoResolver = compilation.GetGenericTypeByMetadataName(Constants.AutoNetworkSerializationResolver, 1),
                    AutoContract = compilation.GetTypeByMetadataName(Constants.IAutoNetworkSerialization),

                    BlittableResolver = compilation.GetGenericTypeByMetadataName(Constants.BlittableNetworkSerializationResolver, 1),
                    BlittableAttribute = compilation.GetTypeByMetadataName(Constants.NetworkBlittableAttribute),

                    EnumResolver = compilation.GetGenericTypeByMetadataName(Constants.EnumNetworkSerializationResolver, 1),

                    TupleResolvers = new INamedTypeSymbol[9]
                    {
                        compilation.GetTypeByMetadataName(Constants.TupleSerializationResolver),
                        compilation.GetGenericTypeByMetadataName(Constants.TupleSerializationResolver, 1),
                        compilation.GetGenericTypeByMetadataName(Constants.TupleSerializationResolver, 2),
                        compilation.GetGenericTypeByMetadataName(Constants.TupleSerializationResolver, 3),
                        compilation.GetGenericTypeByMetadataName(Constants.TupleSerializationResolver, 4),
                        compilation.GetGenericTypeByMetadataName(Constants.TupleSerializationResolver, 5),
                        compilation.GetGenericTypeByMetadataName(Constants.TupleSerializationResolver, 6),
                        compilation.GetGenericTypeByMetadataName(Constants.TupleSerializationResolver, 7),
                        compilation.GetGenericTypeByMetadataName(Constants.TupleSerializationResolver, 8),
                    },

                    NullableResolver = compilation.GetGenericTypeByMetadataName(Constants.NullableNetworkSerializationResolver, 1),

                    BehaviourContract = compilation.GetTypeByMetadataName(NetworkSyncMembersGenerator.Constants.INetworkBehaviour),
                    BehaviourResolver = compilation.GetGenericTypeByMetadataName(NetworkSyncMembersGenerator.Constants.NetworkBehaviourSerializationResolver, 1),

                    ISyncedAsset = compilation.GetTypeByMetadataName(NetworkSyncMembersGenerator.Constants.ISyncedAsset),
                    SyncedAssetResolver = compilation.GetGenericTypeByMetadataName(NetworkSyncMembersGenerator.Constants.SyncedAssetSerializationResolver, 1),
                };

                return data;
            }
        }

        static bool IsInvocationSyntax(SyntaxNode node, CancellationToken token) => node is InvocationExpressionSyntax;
        static IMethodSymbol GetInvocationMethodDefinition(GeneratorSyntaxContext context, CancellationToken token)
        {
            var info = context.SemanticModel.GetSymbolInfo(context.Node, token);

            return info.Symbol as IMethodSymbol;
        }

        static IEnumerable<ITypeSymbol> GetGeneratorUsageType((IMethodSymbol Method, CompilationData Compilation) input, CancellationToken token)
        {
            var parameters = input.Method.TypeParameters;
            var arguments = input.Method.TypeArguments;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (CodeUtility.HasAttribute(parameters[i], input.Compilation.MarkerAttribute))
                {
                    if (arguments[i].TypeKind is TypeKind.TypeParameter)
                        continue;

                    if (arguments[i] is INamedTypeSymbol named && named.IsOpenGenericType())
                        continue;

                    yield return arguments[i];
                }
            }
        }

        static bool IsNotNull<T>(T item) where T : class => ReferenceEquals(item, null) is false;

        static void GenerateSourceCode(SourceProductionContext context, (ImmutableArray<ITypeSymbol> Usages, CompilationData Compilation) source)
        {
            WriteUsages(context, "Usages", source.Usages, source.Compilation);
        }

        public static void WriteUsages(SourceProductionContext context, string id, IList<ITypeSymbol> usages, CompilationData compilation)
        {
            try
            {
                var builder = new CodeStringBuilder(512);

                var resolvers = ResolveUsages(context, compilation, usages);
                if (resolvers.Count is 0)
                    return;

                //Assembly attribute
                builder.Write("[assembly: ");
                builder.Write(Constants.NetworkSerializationResolverRegistrationAttribute);
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
                    builder.Write("class ");
                    WriteClassName();

                    using (builder.CodeBlock())
                    {
                        //Registration Method
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

                void WriteClassName()
                {
                    CodeUtility.WriteAssemblyAsClass(compilation.AssemblyName, builder);
                    builder.Write("_");
                    builder.Write(id);
                    builder.Write("SerializationRegistration");
                }
                void WriteNamespaceName()
                {
                    builder.Write(Constants.Name);
                    builder.Write(".Generated");
                }

                context.AddSource($"{id}NetworkSerializationRegistration.g.cs", builder.ToString());
            }
            catch (Exception)
            {
                throw;
            }
        }

        static Dictionary<ITypeSymbol, INamedTypeSymbol> ResolveUsages(SourceProductionContext context, CompilationData compilation, IList<ITypeSymbol> usages)
        {
            var resolvers = new Dictionary<ITypeSymbol, INamedTypeSymbol>(usages.Count * 2, SymbolEqualityComparer.Default);

            foreach (var usage in usages)
                Resolvers.Resolve(context, compilation, usage, resolvers);

            return resolvers;
        }

        public class Constants : GlobalNetworkGenerator.Constants
        {
            public static readonly string Namespace = $"{Name}.Serialization";

            public static readonly string NetworkSerializationResolver = $"{Namespace}.{nameof(NetworkSerializationResolver)}";

            public static readonly string NetworkSerializationResolverRegistrationAttribute = $"{Namespace}.{nameof(NetworkSerializationResolverRegistrationAttribute)}";

            public static readonly string NetworkSerializationMarkerAttribute = $"{Namespace}.{nameof(NetworkSerializationMarkerAttribute)}";

            public static readonly string ArrayNetworkSerializationResolver = $"{Namespace}.{nameof(ArrayNetworkSerializationResolver)}";
            public static readonly string ArraySegmentNetworkSerializationResolver = $"{Namespace}.{nameof(ArraySegmentNetworkSerializationResolver)}";
            public static readonly string ListNetworkSerializationResolver = $"{Namespace}.{nameof(ListNetworkSerializationResolver)}";
            public static readonly string DictionaryNetworkSerializationResolver = $"{Namespace}.{nameof(DictionaryNetworkSerializationResolver)}";

            public static readonly string ManualNetworkSerializationResolver = $"{Namespace}.{nameof(ManualNetworkSerializationResolver)}";
            public static readonly string IManualNetworkSerialization = $"{Namespace}.{nameof(IManualNetworkSerialization)}";

            public static readonly string AutoNetworkSerializationResolver = $"{Namespace}.{nameof(AutoNetworkSerializationResolver)}";
            public static readonly string IAutoNetworkSerialization = $"{Namespace}.{nameof(IAutoNetworkSerialization)}";

            public static readonly string BlittableNetworkSerializationResolver = $"{Namespace}.{nameof(BlittableNetworkSerializationResolver)}";
            public static readonly string NetworkBlittableAttribute = $"{Namespace}.{nameof(NetworkBlittableAttribute)}";

            public static readonly string EnumNetworkSerializationResolver = $"{Namespace}.{nameof(EnumNetworkSerializationResolver)}";

            public static readonly string TupleSerializationResolver = $"{Namespace}.{nameof(TupleSerializationResolver)}";

            public static readonly string NullableNetworkSerializationResolver = $"{Namespace}.{nameof(NullableNetworkSerializationResolver)}";

            public static readonly string BinarySource = $"{Namespace}.{nameof(BinarySource)}";
        }

        public class DiagnosticCodes : GlobalNetworkGenerator.DiagnosticCodes { }

        public class Resolvers
        {
            public static bool Resolve(SourceProductionContext context, CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                //Early exit for duplicates
                if (resolvers.ContainsKey(usage))
                    return true;

                IterateGenericParameters(context, compilation, usage, resolvers);

                if (ResolveBlittable(context, compilation, usage, resolvers))
                    return true;

                if (ResolveManual(context, compilation, usage, resolvers))
                    return true;

                if (ResolveAuto(context, compilation, usage, resolvers))
                    return true;

                if (ResolveArray(context, compilation, usage, resolvers))
                    return true;

                if (ResolveArraySegment(context, compilation, usage, resolvers))
                    return true;

                if (ResolveList(context, compilation, usage, resolvers))
                    return true;

                if (ResolveDictionary(context, compilation, usage, resolvers))
                    return true;

                if (ResolveEnum(context, compilation, usage, resolvers))
                    return true;

                if (ResolveTuple(context, compilation, usage, resolvers))
                    return true;

                if (ResolveNullable(context, compilation, usage, resolvers))
                    return true;

                if (ResolveBehaviour(context, compilation, usage, resolvers))
                    return true;

                if (ResolveSyncedAsset(context, compilation, usage, resolvers))
                    return true;

                return false;
            }

            static void IterateGenericParameters(SourceProductionContext context, CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                var type = usage as INamedTypeSymbol;
                if (type is null)
                    return;

                if (type.BaseType != null)
                    IterateGenericParameters(context, compilation, usage.BaseType, resolvers);

                if (type.IsGenericType is false)
                    return;

                var arguments = type.TypeArguments;
                var parameters = type.TypeParameters;

                for (int i = 0; i < arguments.Length; i++)
                {
                    var argument = arguments[i];
                    var parameter = parameters[i];

                    if (argument.TypeKind is TypeKind.TypeParameter)
                        continue;

                    if (CodeUtility.HasAttribute(parameter, compilation.MarkerAttribute) is false)
                        continue;

                    Resolve(context, compilation, argument, resolvers);
                }
            }

            static bool ResolveArray(SourceProductionContext context, CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                var array = usage as IArrayTypeSymbol;
                if (array is null)
                    return false;

                if (array.Rank > 1)
                {
                    context.ReportDiagnostic(DiagnosticCodes.MultiDimensionArraySerialization.Create());
                    return false;
                }

                var element = array.ElementType;

                resolvers[usage] = compilation.ArrayResolver.Construct(element);

                Resolve(context, compilation, element, resolvers);

                return true;
            }
            static bool ResolveArraySegment(SourceProductionContext context, CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
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

                Resolve(context, compilation, element, resolvers);

                return true;
            }
            static bool ResolveList(SourceProductionContext context, CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
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

                Resolve(context, compilation, element, resolvers);

                return true;
            }
            static bool ResolveDictionary(SourceProductionContext context, CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                var dictionary = usage as INamedTypeSymbol;

                if (dictionary is null)
                    return false;

                if (dictionary.IsGenericType is false)
                    return false;

                if (CodeUtility.SymbolEquality.Equals(dictionary.ConstructedFrom, compilation.DictionaryType) is false)
                    return false;

                var key = dictionary.TypeArguments[0];
                var value = dictionary.TypeArguments[1];

                resolvers[usage] = compilation.DictionaryResolver.Construct(key, value);

                Resolve(context, compilation, key, resolvers);
                Resolve(context, compilation, value, resolvers);

                return true;
            }

            static bool ResolveManual(SourceProductionContext context, CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                if (usage.ImplementsInterface(compilation.ManualContract) is false)
                    return false;

                resolvers[usage] = compilation.ManualResolver.Construct(usage);

                return true;
            }

            static bool ResolveAuto(SourceProductionContext context, CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                if (usage.ImplementsInterface(compilation.AutoContract) is false)
                    return false;

                resolvers[usage] = compilation.AutoResolver.Construct(usage);

                return true;
            }

            static bool ResolveBlittable(SourceProductionContext context, CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                if (CodeUtility.HasAttribute(usage, compilation.BlittableAttribute) is false)
                    return false;

                if (usage.IsUnmanagedType is false || usage.IsValueType is false)
                {
                    context.ReportDiagnostic(DiagnosticCodes.BlittableConstraint.Create(usage));
                    return false;
                }

                resolvers[usage] = compilation.BlittableResolver.Construct(usage);

                return true;
            }

            static bool ResolveEnum(SourceProductionContext context, CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                if (usage.TypeKind != TypeKind.Enum)
                    return false;

                var type = usage as INamedTypeSymbol;

                resolvers[usage] = compilation.EnumResolver.Construct(type);

                return true;
            }

            static bool ResolveTuple(SourceProductionContext context, CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                if (usage.IsValueType is false)
                    return false;

                if (usage.IsTupleType is false)
                    return false;

                var type = usage as INamedTypeSymbol;
                if (type is null)
                    return false;

                if (type.IsGenericType)
                {
                    var arguments = type.TypeArguments;
                    resolvers[usage] = compilation.TupleResolvers[arguments.Length].Construct(arguments, default);

                    foreach (var argument in arguments)
                        Resolve(context, compilation, argument, resolvers);
                }
                else
                {
                    resolvers[usage] = compilation.TupleResolvers[0];
                }

                return true;
            }

            static bool ResolveNullable(SourceProductionContext context, CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                if (usage.IsValueType is false)
                    return false;

                if (usage.NullableAnnotation != NullableAnnotation.Annotated)
                    return false;

                var type = usage as INamedTypeSymbol;
                if (type is null)
                    return false;

                if (type.IsGenericType is false)
                    return false;

                var arguments = type.TypeArguments;
                if (arguments.Length is 0)
                    return false;

                resolvers[usage] = compilation.NullableResolver.Construct(arguments, default);

                Resolve(context, compilation, arguments[0], resolvers);

                return true;
            }

            static bool ResolveBehaviour(SourceProductionContext context, CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                if (compilation.BehaviourContract is null || compilation.BehaviourResolver is null)
                    return false;

                if (usage.ImplementsInterface(compilation.BehaviourContract) is false)
                    return false;

                resolvers[usage] = compilation.BehaviourResolver.Construct(usage);

                return true;
            }

            static bool ResolveSyncedAsset(SourceProductionContext context, CompilationData compilation, ITypeSymbol usage, Dictionary<ITypeSymbol, INamedTypeSymbol> resolvers)
            {
                if (compilation.ISyncedAsset is null || compilation.SyncedAssetResolver is null)
                    return false;

                if (usage.ImplementsInterface(compilation.ISyncedAsset) is false)
                    return false;

                resolvers[usage] = compilation.SyncedAssetResolver.Construct(usage);

                return true;
            }
        }
    }
}