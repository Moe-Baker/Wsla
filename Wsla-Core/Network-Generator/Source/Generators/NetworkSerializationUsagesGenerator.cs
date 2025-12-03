using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wsla.Generator
{
    [Generator]
    public class NetworkSerializationUsagesGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var waypoints = context.CompilationProvider
                .Select(WaypointsData.Create);

            var usages = context.SyntaxProvider
                .CreateSyntaxProvider(IsInvocationSyntax, GetInvocationMethodDefinition)
                .Where(IsNotNull)
                .Combine(waypoints)
                .SelectMany(GetGeneratorUsageType)
                .Collect();

            context.RegisterSourceOutput(waypoints.Combine(usages), GenerateCode);
        }

        static bool IsInvocationSyntax(SyntaxNode node, CancellationToken token)
        {
            if (node is InvocationExpressionSyntax invocation && invocation.HasArgumentsOrTypeParameters())
                return true;

            return false;
        }
        static IMethodSymbol GetInvocationMethodDefinition(GeneratorSyntaxContext context, CancellationToken token)
        {
            var info = context.SemanticModel.GetSymbolInfo(context.Node, token);

            return info.Symbol as IMethodSymbol;
        }
        static IEnumerable<ITypeSymbol> GetGeneratorUsageType((IMethodSymbol Method, WaypointsData Waypoints) input, CancellationToken token)
        {
            var parameters = input.Method.TypeParameters;
            var arguments = input.Method.TypeArguments;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (CodeUtility.HasAttribute(parameters[i], input.Waypoints.MarkerAttribute))
                {
                    if (arguments[i].TypeKind is TypeKind.TypeParameter)
                        continue;

                    if (arguments[i].IsOpenGenericType())
                        continue;

                    yield return arguments[i];
                }
            }
        }

        static bool IsNotNull<T>(T item) where T : class => ReferenceEquals(item, null) is false;

        void GenerateCode(SourceProductionContext context, (WaypointsData Waypoints, ImmutableArray<ITypeSymbol> Usages) input)
        {
            WriteUsages(context, "Usages", input.Waypoints, input.Usages);
        }

        public static void WriteUsages(SourceProductionContext context, string id, WaypointsData waypoints, IList<ITypeSymbol> usages)
        {
            var builder = new CodeStringBuilder(512);

            var instances = new ResolversCache();

            var resolution = new ResolutionData(context, waypoints);

            //Check Invalid Resolvers
            for (int i = waypoints.Resolvers.Count - 1; i >= 0; i--)
            {
                if (waypoints.Resolvers[i].ValidateConfiguration(out var diagnostic) is false)
                {
                    context.ReportDiagnostic(diagnostic);
                    waypoints.Resolvers[i] = null;
                }
            }
            waypoints.Resolvers.RemoveAll(x => x == null);

            foreach (var usage in usages)
                resolution.Resolve(usage, instances);

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
                        foreach (var pair in instances)
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
                CodeUtility.WriteAssemblyAsClass(waypoints.AssemblyName, builder);
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

        public struct WaypointsData
        {
            public string AssemblyName;

            public INamedTypeSymbol MarkerAttribute;

            public List<ResolverTemplate> Resolvers;

            public SourceGeneratorData SourceGenerators;
            public struct SourceGeneratorData
            {
                public INamedTypeSymbol MarkerAttribute;

                public ConditionData Condition;
                public struct ConditionData
                {
                    public INamedTypeSymbol ImplementsInterface;
                    public INamedTypeSymbol ConstructedFrom;
                    public INamedTypeSymbol DecoratedBy;
                    public INamedTypeSymbol IsArray;
                    public INamedTypeSymbol IsEnum;

                    public ConditionData(Compilation compilation)
                    {
                        ImplementsInterface = compilation.GetTypeByMetadataName(ResolverTemplate.SourceGenerator.Condition.ImplementsInterface.SelfID);
                        ConstructedFrom = compilation.GetTypeByMetadataName(ResolverTemplate.SourceGenerator.Condition.ConstructedFrom.SelfID);
                        DecoratedBy = compilation.GetTypeByMetadataName(ResolverTemplate.SourceGenerator.Condition.DecoratedBy.SelfID);
                        IsArray = compilation.GetTypeByMetadataName(ResolverTemplate.SourceGenerator.Condition.IsArray.SelfID);
                        IsEnum = compilation.GetTypeByMetadataName(ResolverTemplate.SourceGenerator.Condition.IsEnum.SelfID);
                    }
                }

                public BuilderData Builder;
                public struct BuilderData
                {
                    public INamedTypeSymbol FromSourceType;
                    public INamedTypeSymbol FromSourceArguments;
                    public INamedTypeSymbol FromArrayType;

                    public BuilderData(Compilation compilation)
                    {
                        FromSourceType = compilation.GetTypeByMetadataName(ResolverTemplate.SourceGenerator.Builder.FromSourceType.SelfID);
                        FromSourceArguments = compilation.GetTypeByMetadataName(ResolverTemplate.SourceGenerator.Builder.FromSourceArguments.SelfID);
                        FromArrayType = compilation.GetTypeByMetadataName(ResolverTemplate.SourceGenerator.Builder.FromArrayType.SelfID);
                    }
                }

                public OptionsData Options;
                public struct OptionsData
                {
                    public INamedTypeSymbol ResolveGenericArguments;
                    public INamedTypeSymbol ResolutionOrder;

                    public OptionsData(Compilation compilation)
                    {
                        ResolveGenericArguments = compilation.GetTypeByMetadataName(ResolverTemplate.SourceGenerator.Option.ResolveGenericArguments.SelfID);
                        ResolutionOrder = compilation.GetTypeByMetadataName(ResolverTemplate.SourceGenerator.Option.ResolutionOrder.SelfID);
                    }
                }

                public SourceGeneratorData(Compilation compilation)
                {
                    MarkerAttribute = compilation.GetTypeByMetadataName(ResolverTemplate.SourceGenerator.SelfID);

                    Condition = new ConditionData(compilation);
                    Builder = new BuilderData(compilation);
                    Options = new OptionsData(compilation);
                }
            }

            public static WaypointsData Create(Compilation compilation, CancellationToken cancellation)
            {
                var data = new WaypointsData()
                {
                    AssemblyName = compilation.Assembly.Name,
                    MarkerAttribute = compilation.GetTypeByMetadataName(Constants.NetworkSerializationMarkerAttribute),
                    SourceGenerators = new SourceGeneratorData(compilation),
                };

                data.Resolvers = ResolverCollector.Collect(compilation, data);

                return data;
            }

            class ResolverCollector : SymbolVisitor
            {
                List<ResolverTemplate> Resolvers;

                public override void VisitNamespace(INamespaceSymbol symbol)
                {
                    foreach (var member in symbol.GetMembers())
                        Visit(member);
                }
                public override void VisitNamedType(INamedTypeSymbol symbol)
                {
                    foreach (var member in symbol.GetTypeMembers())
                        Visit(member);

                    if (ResolverTemplate.TryCreate(symbol, Waypoints, out var template))
                        Resolvers.Add(template);
                }

                WaypointsData Waypoints;
                ResolverCollector(WaypointsData Waypoints)
                {
                    this.Waypoints = Waypoints;

                    Resolvers = new List<ResolverTemplate>(40);
                }

                public static List<ResolverTemplate> Collect(Compilation compilation, WaypointsData waypoints)
                {
                    var collector = new ResolverCollector(waypoints);

                    collector.VisitNamespace(compilation.GlobalNamespace);

                    collector.Resolvers.Sort((x, y) => x.ResolutionOrder.CompareTo(y.ResolutionOrder));

                    return collector.Resolvers;
                }
            }
        }

        public struct ResolutionData
        {
            public SourceProductionContext Context { get; }
            public WaypointsData Waypoints { get; }

            public void Resolve(ITypeSymbol usage, ResolversCache cache)
            {
                if (cache.ContainsKey(usage))
                    return;

                IterateGenericParameters(usage, cache);

                foreach (var resolver in Waypoints.Resolvers)
                    if (resolver.Resolve(in this, usage, cache))
                        return;
            }
            void IterateGenericParameters(ITypeSymbol usage, ResolversCache cache)
            {
                var type = usage as INamedTypeSymbol;
                if (type is null)
                    return;

                if (type.BaseType != null)
                    IterateGenericParameters(usage.BaseType, cache);

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

                    if (CodeUtility.HasAttribute(parameter, Waypoints.MarkerAttribute) is false)
                        continue;

                    Resolve(argument, cache);
                }
            }

            public ResolutionData(SourceProductionContext Context, WaypointsData Waypoints)
            {
                this.Context = Context;
                this.Waypoints = Waypoints;
            }
        }

        public class DiagnosticCodes : GlobalNetworkGenerator.DiagnosticCodes { }

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
            public static readonly string HashSetNetworkSerializationResolver = $"{Namespace}.{nameof(HashSetNetworkSerializationResolver)}";
            public static readonly string QueueNetworkSerializationResolver = $"{Namespace}.{nameof(QueueNetworkSerializationResolver)}";
            public static readonly string StackNetworkSerializationResolver = $"{Namespace}.{nameof(StackNetworkSerializationResolver)}";

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

        public class ResolverTemplate
        {
            public static readonly string SelfID = $"{Constants.Namespace}.NetworkSerializationResolver";

            public readonly INamedTypeSymbol ResolverType;

            public List<SourceGenerator.Condition> Conditions;
            public SourceGenerator.Builder Builder;

            public bool ResolveGenericArguments;
            public int ResolutionOrder;

            public bool ValidateConfiguration(out Diagnostic diagnostic)
            {
                if (ResolverType.DeclaredAccessibility != Accessibility.Public)
                {
                    diagnostic = DiagnosticCodes.NotPublicResolver.Create(ResolverType);
                    return false;
                }

                if (Conditions.Count is 0)
                {
                    diagnostic = DiagnosticCodes.NoResolverCondition.Create(ResolverType);
                    return false;
                }

                if (Builder is null)
                {
                    diagnostic = DiagnosticCodes.NoResolverBuilder.Create(ResolverType);
                    return false;
                }

                diagnostic = default;
                return true;
            }

            public bool Resolve(in ResolutionData resolution, ITypeSymbol usage, ResolversCache cache)
            {
                //Check Conditions
                foreach (var condition in Conditions)
                {
                    if (condition.IsValid(in resolution, usage) is false)
                        return false;
                }

                var resolver = Builder.Construct(in resolution, usage, ResolverType);

                cache.Add(usage, resolver);

                if (ResolveGenericArguments)
                {
                    foreach (var argument in resolver.TypeArguments)
                        resolution.Resolve(argument, cache);
                }

                return true;
            }

            public class SourceGenerator
            {
                public const string Name = nameof(SourceGenerator);
                public static readonly string SelfID = $"{ResolverTemplate.SelfID}+{Name}";

                public abstract class Condition : Attribute
                {
                    public static readonly string BaseID = $"{SourceGenerator.SelfID}+{nameof(Condition)}";

                    public abstract bool IsValid(in ResolutionData resolution, ITypeSymbol usage);

                    public class ImplementsInterface : Condition
                    {
                        public const string Name = nameof(ImplementsInterface);
                        public static readonly string SelfID = $"{Condition.BaseID}+{Name}";

                        public INamedTypeSymbol Interface { get; }

                        public override bool IsValid(in ResolutionData resolution, ITypeSymbol usage)
                        {
                            return usage.ImplementsInterface(Interface);
                        }

                        public ImplementsInterface(AttributeData data)
                        {
                            Interface = data.ConstructorArguments[0].Value as INamedTypeSymbol;
                        }
                    }
                    public class ConstructedFrom : Condition
                    {
                        public const string Name = nameof(ConstructedFrom);
                        public static readonly string SelfID = $"{Condition.BaseID}+{Name}";

                        public INamedTypeSymbol Prototype { get; }

                        public override bool IsValid(in ResolutionData resolution, ITypeSymbol usage)
                        {
                            return usage is INamedTypeSymbol named
                                && named.IsGenericType
                                && CodeUtility.SymbolEquality.Equals(named.ConstructedFrom, Prototype.OriginalDefinition);
                        }

                        public ConstructedFrom(AttributeData data)
                        {
                            Prototype = data.ConstructorArguments[0].Value as INamedTypeSymbol;
                        }
                    }
                    public class DecoratedBy : Condition
                    {
                        public const string Name = nameof(DecoratedBy);
                        public static readonly string SelfID = $"{Condition.BaseID}+{Name}";

                        public INamedTypeSymbol Attribute { get; }

                        public override bool IsValid(in ResolutionData resolution, ITypeSymbol usage)
                        {
                            return CodeUtility.HasAttribute(usage, Attribute);
                        }

                        public DecoratedBy(AttributeData data)
                        {
                            Attribute = data.ConstructorArguments[0].Value as INamedTypeSymbol;
                        }
                    }
                    public class IsArray : Condition
                    {
                        public const string Name = nameof(IsArray);
                        public static readonly string SelfID = $"{Condition.BaseID}+{Name}";

                        public override bool IsValid(in ResolutionData resolution, ITypeSymbol usage)
                        {
                            return usage.TypeKind is TypeKind.Array;
                        }

                        public IsArray(AttributeData data) { }
                    }
                    public class IsEnum : Condition
                    {
                        public const string Name = nameof(IsEnum);
                        public static readonly string SelfID = $"{Condition.BaseID}+{Name}";

                        public override bool IsValid(in ResolutionData resolution, ITypeSymbol usage)
                        {
                            return usage.TypeKind is TypeKind.Enum;
                        }

                        public IsEnum(AttributeData data) { }
                    }

                    public static bool TryGet(AttributeData attribute, WaypointsData waypoints, out Condition condition)
                    {
                        if (CodeUtility.CompareSymbols(attribute.AttributeClass, waypoints.SourceGenerators.Condition.ImplementsInterface))
                        {
                            condition = new ImplementsInterface(attribute);
                            return true;
                        }

                        if (CodeUtility.CompareSymbols(attribute.AttributeClass, waypoints.SourceGenerators.Condition.ConstructedFrom))
                        {
                            condition = new ConstructedFrom(attribute);
                            return true;
                        }

                        if (CodeUtility.CompareSymbols(attribute.AttributeClass, waypoints.SourceGenerators.Condition.DecoratedBy))
                        {
                            condition = new DecoratedBy(attribute);
                            return true;
                        }

                        if (CodeUtility.CompareSymbols(attribute.AttributeClass, waypoints.SourceGenerators.Condition.IsArray))
                        {
                            condition = new IsArray(attribute);
                            return true;
                        }

                        if (CodeUtility.CompareSymbols(attribute.AttributeClass, waypoints.SourceGenerators.Condition.IsEnum))
                        {
                            condition = new IsEnum(attribute);
                            return true;
                        }

                        condition = default;
                        return false;
                    }
                }

                public abstract class Builder
                {
                    public static readonly string BaseID = $"{SourceGenerator.SelfID}+{nameof(Builder)}";

                    public abstract INamedTypeSymbol Construct(in ResolutionData resolution, ITypeSymbol usage, INamedTypeSymbol resolver);

                    public class FromSourceType : Builder
                    {
                        public const string Name = nameof(FromSourceType);
                        public static readonly string SelfID = $"{Builder.BaseID}+{Name}";

                        public override INamedTypeSymbol Construct(in ResolutionData resolution, ITypeSymbol usage, INamedTypeSymbol resolver)
                        {
                            return resolver.Construct(usage);
                        }

                        public FromSourceType(AttributeData data) { }
                    }
                    public class FromSourceArguments : Builder
                    {
                        public const string Name = nameof(FromSourceArguments);
                        public static readonly string SelfID = $"{Builder.BaseID}+{Name}";

                        public override INamedTypeSymbol Construct(in ResolutionData resolution, ITypeSymbol usage, INamedTypeSymbol resolver)
                        {
                            if (usage is INamedTypeSymbol named)
                                return resolver.Construct(named.TypeArguments, named.TypeArgumentNullableAnnotations);
                            else
                                throw new NotImplementedException();
                        }

                        public FromSourceArguments(AttributeData data) { }
                    }
                    public class FromArrayType : Builder
                    {
                        public const string Name = nameof(FromArrayType);
                        public static readonly string SelfID = $"{Builder.BaseID}+{Name}";

                        public override INamedTypeSymbol Construct(in ResolutionData resolution, ITypeSymbol usage, INamedTypeSymbol resolver)
                        {
                            if (usage is IArrayTypeSymbol array)
                                return resolver.Construct(array.ElementType);
                            else
                                throw new NotImplementedException();
                        }

                        public FromArrayType(AttributeData data) { }
                    }

                    public static bool TryGet(AttributeData attribute, WaypointsData waypoints, out Builder builder)
                    {
                        if (CodeUtility.CompareSymbols(attribute.AttributeClass, waypoints.SourceGenerators.Builder.FromSourceType))
                        {
                            builder = new FromSourceType(attribute);
                            return true;
                        }

                        if (CodeUtility.CompareSymbols(attribute.AttributeClass, waypoints.SourceGenerators.Builder.FromSourceArguments))
                        {
                            builder = new FromSourceArguments(attribute);
                            return true;
                        }

                        if (CodeUtility.CompareSymbols(attribute.AttributeClass, waypoints.SourceGenerators.Builder.FromArrayType))
                        {
                            builder = new FromArrayType(attribute);
                            return true;
                        }

                        builder = default;
                        return false;
                    }
                }

                public abstract class Option
                {
                    public static readonly string BaseID = $"{SourceGenerator.SelfID}+{nameof(Option)}";

                    public abstract void Apply(ResolverTemplate template);

                    public class ResolveGenericArguments : Option
                    {
                        public const string Name = nameof(ResolveGenericArguments);
                        public static readonly string SelfID = $"{Option.BaseID}+{Name}";

                        public override void Apply(ResolverTemplate template)
                        {
                            template.ResolveGenericArguments = true;
                        }

                        public ResolveGenericArguments(AttributeData data) { }
                    }
                    public class ResolutionOrder : Option
                    {
                        public const string Name = nameof(ResolutionOrder);
                        public static readonly string SelfID = $"{Option.BaseID}+{Name}";

                        public int Order { get; }

                        public override void Apply(ResolverTemplate template)
                        {
                            template.ResolutionOrder = Order;
                        }

                        public ResolutionOrder(AttributeData data)
                        {
                            Order = (int)data.ConstructorArguments[0].Value;
                        }
                    }

                    public static bool TryGet(AttributeData attribute, WaypointsData waypoints, out Option option)
                    {
                        if (CodeUtility.CompareSymbols(attribute.AttributeClass, waypoints.SourceGenerators.Options.ResolveGenericArguments))
                        {
                            option = new ResolveGenericArguments(attribute);
                            return true;
                        }

                        if (CodeUtility.CompareSymbols(attribute.AttributeClass, waypoints.SourceGenerators.Options.ResolutionOrder))
                        {
                            option = new ResolutionOrder(attribute);
                            return true;
                        }

                        option = default;
                        return false;
                    }
                }
            }

            public ResolverTemplate(INamedTypeSymbol ResolverType)
            {
                this.ResolverType = ResolverType;

                Conditions = new List<SourceGenerator.Condition>();

                //Apply Options Defaults
                {
                    ResolutionOrder = 0;
                    ResolveGenericArguments = false;
                }
            }

            public static bool TryCreate(INamedTypeSymbol type, WaypointsData waypoints, out ResolverTemplate template)
            {
                if (type.HasAttribute(waypoints.SourceGenerators.MarkerAttribute) is false)
                {
                    template = default;
                    return false;
                }

                template = new ResolverTemplate(type);

                var attributes = type.GetAttributes();

                foreach (var attribute in attributes)
                {
                    if (SourceGenerator.Condition.TryGet(attribute, waypoints, out var condition))
                    {
                        template.Conditions.Add(condition);
                        continue;
                    }

                    if (template.Builder is null && SourceGenerator.Builder.TryGet(attribute, waypoints, out template.Builder))
                        continue;

                    if (SourceGenerator.Option.TryGet(attribute, waypoints, out var option))
                    {
                        option.Apply(template);
                        continue;
                    }
                }

                return true;
            }
        }
        public class ResolversCache : Dictionary<ITypeSymbol, INamedTypeSymbol> { }
    }
}