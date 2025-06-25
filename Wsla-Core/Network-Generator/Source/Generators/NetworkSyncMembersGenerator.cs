using Microsoft.CodeAnalysis;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Wsla.Generator
{
    [Generator]
    public class NetworkSyncMembersGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var compilation = context.CompilationProvider.Select(WaypointsData.Create);

            var isUnity = context.ParseOptionsProvider.Select(CheckIfUnityProject);

            var behaviours = compilation.Combine(isUnity).Select(NetworkBehaviourCollector.Collect);

            context.RegisterSourceOutput(compilation.Combine(behaviours), GenerateSourceCode);
        }

        public struct WaypointsData : IEquatable<WaypointsData>
        {
            public string AssemblyName;
            public INamespaceSymbol GlobalNamespace;

            public NetworkSerializationUsagesGenerator.WaypointsData SerializationWaypoints;

            public INamedTypeSymbol RpcInfo;

            public INamedTypeSymbol INetworkBehaviour;
            public INamedTypeSymbol RPCAttribute;

            public INamedTypeSymbol BaseNetworkVariable;
            public INamedTypeSymbol GenericNetworkVariable;

            public INamedTypeSymbol[] GeneralRpcBinds;
            public INamedTypeSymbol BinaryRpcBind;

            public INamedTypeSymbol BinarySource;

            (string, INamespaceSymbol) GetComparableFields() => (AssemblyName, GlobalNamespace);

            public override bool Equals(object obj)
            {
                if (obj is WaypointsData other)
                    return Equals(other);

                return false;
            }
            public bool Equals(WaypointsData other) => GetComparableFields() == other.GetComparableFields();

            public override int GetHashCode() => GetComparableFields().GetHashCode();

            public static WaypointsData Create(Compilation compilation, CancellationToken cancellation)
            {
                var data = new WaypointsData()
                {
                    AssemblyName = compilation.Assembly.Name,
                    GlobalNamespace = compilation.Assembly.GlobalNamespace,

                    SerializationWaypoints = NetworkSerializationUsagesGenerator.WaypointsData.Create(compilation, cancellation),

                    RpcInfo = compilation.GetTypeByMetadataName(Constants.RpcInfo),

                    INetworkBehaviour = compilation.GetTypeByMetadataName(Constants.INetworkBehaviour),
                    RPCAttribute = compilation.GetTypeByMetadataName(Constants.RPCAttribute),

                    BaseNetworkVariable = compilation.GetTypeByMetadataName(Constants.NetworkVariable),
                    GenericNetworkVariable = compilation.GetGenericTypeByMetadataName(Constants.NetworkVariable, 1),

                    GeneralRpcBinds = new INamedTypeSymbol[]
                    {
                        compilation.GetTypeByMetadataName(Constants.RpcBind),
                        compilation.GetGenericTypeByMetadataName(Constants.RpcBind, 1),
                        compilation.GetGenericTypeByMetadataName(Constants.RpcBind, 2),
                        compilation.GetGenericTypeByMetadataName(Constants.RpcBind, 3),
                        compilation.GetGenericTypeByMetadataName(Constants.RpcBind, 4),
                        compilation.GetGenericTypeByMetadataName(Constants.RpcBind, 5),
                        compilation.GetGenericTypeByMetadataName(Constants.RpcBind, 6),
                    },

                    BinaryRpcBind = compilation.GetTypeByMetadataName(Constants.BinaryRpcBind),

                    BinarySource = compilation.GetTypeByMetadataName(NetworkSerializationUsagesGenerator.Constants.BinarySource),
                };

                return data;
            }
        }

        bool CheckIfUnityProject(ParseOptions options, CancellationToken cancellation)
        {
            foreach (var symbol in options.PreprocessorSymbolNames)
                if (symbol.StartsWith(Constants.UnityConditionalDirectivePrefix))
                    return true;

            return false;
        }

        class NetworkBehaviourCollector : SymbolVisitor
        {
            public List<INamedTypeSymbol> List { get; }

            WaypointsData Compilation;

            void Collect(WaypointsData Compilation)
            {
                this.Compilation = Compilation;

                Visit(Compilation.GlobalNamespace);
            }

            public override void VisitNamespace(INamespaceSymbol symbol)
            {
                foreach (var member in symbol.GetMembers())
                    Visit(member);
            }
            public override void VisitNamedType(INamedTypeSymbol symbol)
            {
                foreach (var member in symbol.GetTypeMembers())
                    Visit(member);

                if (symbol.ImplementsInterface(Compilation.INetworkBehaviour))
                    List.Add(symbol);
            }

            public NetworkBehaviourCollector() : this(0) { }
            public NetworkBehaviourCollector(int capacity)
            {
                List = new List<INamedTypeSymbol>(capacity);
            }

            public static NetworkBehaviourCollector Collect((WaypointsData compilation, bool isUnity) data, CancellationToken token)
            {
                if (data.isUnity is false)
                    return new NetworkBehaviourCollector();

                var collector = new NetworkBehaviourCollector(40);

                collector.Collect(data.compilation);

                return collector;
            }
        }

        void GenerateSourceCode(SourceProductionContext context, (WaypointsData Compilation, NetworkBehaviourCollector Behaviours) data)
        {
            try
            {
                var cache = ObjectCache.Create();

                var builder = new CodeStringBuilder(512);

                foreach (var behaviour in data.Behaviours.List)
                    WriteBehaviour(context, data.Compilation, behaviour, builder, cache);

                context.AddSource("SyncMembersInterfaceImplementations.g.cs", builder.ToString());

                NetworkSerializationUsagesGenerator.WriteUsages(context, "SyncMembers", data.Compilation.SerializationWaypoints, cache.SerializedTypes);
            }
            catch (Exception)
            {
                throw;
            }
        }

        struct ObjectCache
        {
            public List<INamespaceOrTypeSymbol> Hierarchy;

            public List<IMethodSymbol> Methods;
            public List<ISymbol> Variables;

            public List<ITypeSymbol> SerializedTypes;

            public HashSet<string> RpcNames;

            public static ObjectCache Create()
            {
                return new ObjectCache()
                {
                    Hierarchy = new List<INamespaceOrTypeSymbol>(),

                    Methods = new List<IMethodSymbol>(10),
                    Variables = new List<ISymbol>(10),

                    SerializedTypes = new List<ITypeSymbol>(),

                    RpcNames = new HashSet<string>(),
                };
            }
        }

        void WriteBehaviour(SourceProductionContext context, WaypointsData compilation, INamedTypeSymbol behaviour, CodeStringBuilder builder, ObjectCache cache)
        {
            var hierarchy = cache.Hierarchy;

            //Collect Hierarchy
            {
                hierarchy.Clear();

                INamespaceOrTypeSymbol current = behaviour;

                while (true)
                {
                    if (current is ITypeSymbol type)
                    {
                        if (type.IsPartial() is false)
                        {
                            context.ReportDiagnostic(DiagnosticCodes.BehaviourPartial.Create(current));
                            return;

                        }

                        if (type.DeclaredAccessibility != Accessibility.Public)
                        {
                            context.ReportDiagnostic(DiagnosticCodes.BehaviourPublic.Create(current));
                            return;
                        }
                    }

                    hierarchy.Add(current);

                    current = (current.ContainingSymbol as INamespaceOrTypeSymbol);

                    if (current.IsNamespace && (current as INamespaceSymbol).IsGlobalNamespace)
                        break;

                    if (current == null)
                        break;
                }
            }

            var methods = cache.Methods;
            var variables = cache.Variables;

            //Collect RPCs & Network Variables
            {
                methods.Clear();
                variables.Clear();

                cache.RpcNames.Clear();

                var members = behaviour.GetMembers();

                foreach (var member in members)
                {
                    if (member is IMethodSymbol method)
                    {
                        if (method.HasAttribute(compilation.RPCAttribute) is false)
                            continue;

                        if (cache.RpcNames.Add(method.Name) is false)
                        {
                            context.ReportDiagnostic(DiagnosticCodes.OverloadedRpcs.Create(method));
                            continue;
                        }

                        if (EnsureInfoParameter(method) is false)
                            continue;

                        if (method.IsGenericMethod)
                        {
                            context.ReportDiagnostic(DiagnosticCodes.GenericRpcs.Create(method));
                            continue;
                        }

                        methods.Add(method);
                    }
                    else if (member is IPropertySymbol property)
                    {
                        if (property.Type.InheritsFrom(compilation.BaseNetworkVariable) is false)
                            continue;

                        if (property.IsReadOnly || property.IsWriteOnly)
                        {
                            context.ReportDiagnostic(DiagnosticCodes.InAccessibleNetworkVariable.Create(property));
                            continue;
                        }

                        variables.Add(property);
                    }
                    else if (member is IFieldSymbol field)
                    {
                        if (field.IsImplicitlyDeclared)
                            continue;

                        if (field.Type.InheritsFrom(compilation.BaseNetworkVariable) is false)
                            continue;

                        if (field.IsReadOnly)
                        {
                            context.ReportDiagnostic(DiagnosticCodes.InAccessibleNetworkVariable.Create(field));
                            continue;
                        }

                        variables.Add(field);
                    }
                }

                bool EnsureInfoParameter(IMethodSymbol method)
                {
                    var parameters = method.Parameters;

                    if (parameters.Length > 0 && CodeUtility.CompareSymbols(parameters[parameters.Length - 1].Type, compilation.RpcInfo))
                        return true;

                    context.ReportDiagnostic(DiagnosticCodes.RpcInfoLastField.Create(method));
                    return false;
                }
            }

            if (methods.Count is 0 && variables.Count is 0)
                return;

            for (int i = hierarchy.Count - 1; i >= 1; i--)
                GenerateContainerSyntax(builder, hierarchy[i]);

            //Write Self Type
            {
                builder.Write("partial class ");

                builder.Write(behaviour.Name);
                builder.Write(" : ");
                builder.Write(Constants.IRemoteSyncMembers);

                builder.Newline();

                builder.Write("{");

                builder.Newline();

                builder.Indent();
            }

            //Write RPCs
            {
                //Interface Implementation Header
                {
                    builder.Write("void ");
                    builder.Write(Constants.IRemoteSyncMembers);

                    builder.Write(".RegisterRPCs");

                    using (builder.Parameters())
                    {
                        builder.Write("System.Collections.Generic.List");

                        using (builder.GenericArguments())
                            builder.Write(Constants.BaseRpcBind);

                        builder.Write(" list");
                    }
                }

                if (methods.Count is 0)
                {
                    builder.Write(" {}");
                    builder.Newline();
                }
                else
                {
                    using (builder.CodeBlock())
                    {
                        foreach (var method in methods)
                        {
                            builder.Write("list.Add");

                            using (builder.Parameters())
                            {
                                builder.Write("RPCs.");

                                builder.Write(method.Name);

                                builder.Write(" ??= new");

                                using (builder.Parameters())
                                {
                                    builder.Write(method.Name);
                                }
                            }

                            builder.EndLine();
                        }
                    }

                    builder.Newline();

                    //Registration Struct
                    {
                        builder.Write("public RPCsProperty RPCs");
                        builder.EndLine();

                        builder.Write("public struct RPCsProperty");

                        using (builder.CodeBlock())
                        {
                            foreach (var method in methods)
                            {
                                builder.Write("public ");

                                var bind = GenerateType(method);
                                builder.Write(bind);
                                builder.Space();

                                builder.Write(method.Name);
                                builder.Space();

                                builder.Write("{ get; internal set; }");

                                builder.Newline();
                            }
                        }
                    }
                }

                INamedTypeSymbol GenerateType(IMethodSymbol method)
                {
                    var parameters = method.Parameters;

                    var members = parameters.Length - 1;

                    if (members is 0)
                    {
                        return compilation.GeneralRpcBinds[0];
                    }
                    else if (members is 1 && parameters[0].RefKind == RefKind.Ref && CodeUtility.CompareSymbols(parameters[0].Type, compilation.BinarySource))
                    {
                        return compilation.BinaryRpcBind;
                    }
                    else
                    {
                        var arguments = new ITypeSymbol[members];

                        for (int i = 0; i < arguments.Length; i++)
                        {
                            arguments[i] = parameters[i].Type;
                            cache.SerializedTypes.Add(arguments[i]);
                        }

                        return compilation.GeneralRpcBinds[members].Construct(arguments);
                    }
                }
            }

            //Write Network Variables
            {
                //Interface Implementation Header
                {
                    builder.Write("void ");
                    builder.Write(Constants.IRemoteSyncMembers);

                    builder.Write(".RegisterVariables");

                    using (builder.Parameters())
                    {
                        builder.Write("System.Collections.Generic.List");

                        using (builder.GenericArguments())
                            builder.Write(Constants.NetworkVariable);

                        builder.Write(" list");
                    }
                }

                if (variables.Count is 0)
                {
                    builder.Write(" {}");
                    builder.Newline();
                }
                else
                {
                    using (builder.CodeBlock())
                    {
                        foreach (var variable in variables)
                        {
                            builder.Write("list.Add(");
                            builder.Write(variable.Name);
                            builder.Write(" ??= new()");
                            builder.Write(")");

                            builder.EndLine();

                            if (variable is IPropertySymbol property)
                                cache.SerializedTypes.Add(property.Type);
                            else if (variable is IFieldSymbol field)
                                cache.SerializedTypes.Add(field.Type);
                        }
                    }
                }
            }

            for (int i = 0; i < hierarchy.Count; i++)
            {
                if (i != 0)
                    builder.Newline();

                builder.Unindent();
                builder.Write("}");
            }

            builder.Newline();
            builder.Newline();
        }

        void GenerateContainerSyntax(CodeStringBuilder builder, ISymbol symbol)
        {
            if (symbol is INamespaceSymbol)
                builder.Write("namespace ");
            else if (symbol is INamedTypeSymbol)
                builder.Write("partial class ");
            else
                throw new NotImplementedException();

            builder.Write(symbol.Name);

            builder.Newline();

            builder.Write("{");

            builder.Newline();

            builder.Indent();
        }

        public class Constants : GlobalNetworkGenerator.Constants
        {
            public const string UnityConditionalDirectivePrefix = "UNITY_";

            public static readonly string Namespace = $"{Name}.Unity";

            public static readonly string INetworkBehaviour = $"{Namespace}.{nameof(INetworkBehaviour)}";
            public static readonly string RPCAttribute = $"{Namespace}.{nameof(RPCAttribute)}";
            public static readonly string NetworkVariable = $"{Namespace}.{nameof(NetworkVariable)}";

            public static readonly string BaseRpcBind = $"{Namespace}.{nameof(BaseRpcBind)}";
            public static readonly string RpcBind = $"{Namespace}.{nameof(RpcBind)}";
            public static readonly string BinaryRpcBind = $"{Namespace}.{nameof(BinaryRpcBind)}";

            public static readonly string IRemoteSyncMembers = $"{Namespace}.{nameof(IRemoteSyncMembers)}";

            public static readonly string RpcInfo = $"{Namespace}.{nameof(RpcInfo)}";

            public static readonly string NetworkBehaviourSerializationResolver = $"{Namespace}.{nameof(NetworkBehaviourSerializationResolver)}";

            public static readonly string ISyncedAsset = $"{Namespace}.{nameof(ISyncedAsset)}";
            public static readonly string SyncedAssetSerializationResolver = $"{Namespace}.{nameof(SyncedAssetSerializationResolver)}";
        }

        public class DiagnosticCodes : GlobalNetworkGenerator.DiagnosticCodes { }
    }
}