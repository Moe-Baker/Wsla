using Microsoft.CodeAnalysis;

namespace Wsla.Generator
{
    public class GlobalNetworkGenerator
    {
        public class DiagnosticCodes
        {
            public static readonly DiagnosticDescriptor BehaviourPartial
                = new DiagnosticDescriptor("WSLA1",
                    "Network Behaviour Struvture Must be Partial",
                    "All Network Behaviour and their Declaring Types Must be a Partial Type",
                    "Usage",
                    DiagnosticSeverity.Error,
                    true);

            public static readonly DiagnosticDescriptor BehaviourPublic
                = new DiagnosticDescriptor("WSLA2",
                    "Network Behaviour Structure Must be Public",
                    "All Network Behaviour and thier Declaring Types Must be Public",
                    "Usage",
                    DiagnosticSeverity.Error,
                    true);

            public static readonly DiagnosticDescriptor RpcInfoLastField
                = new DiagnosticDescriptor("WSLA3",
                    "RPC Must Have RpcInfo parameter",
                    "All Network RPCs must Have RpcInfo as the Last Parameter in its Method Declaration",
                    "Usage",
                    DiagnosticSeverity.Error,
                    true);

            public static readonly DiagnosticDescriptor MultiDimensionArraySerialization
                = new DiagnosticDescriptor("WSLA4",
                    "Multi-Dimension Array Serialization not Suppoprted",
                    "Multi-Dimension Array Serialization not Suppoprted, Only Single Dimension Arrays are Supporte",
                    "Usage",
                    DiagnosticSeverity.Error,
                    true);

            public static readonly DiagnosticDescriptor BlittlableConstraint
                = new DiagnosticDescriptor("WSLA5",
                    "Type is not Blittable",
                    "A Blittable Type must be an Unmanaged Struct",
                    "Usage",
                    DiagnosticSeverity.Error,
                    true);

            public static readonly DiagnosticDescriptor OverloadedRpcs
                = new DiagnosticDescriptor("WSLA6",
                    "Overloading RPCs is Not Supported",
                    "RPC Overloading is not Supported, Please Use Distinct Names for RPCs",
                    "Usage",
                    DiagnosticSeverity.Error,
                    true);
        }

        public class Constants
        {
            public static readonly string Name = "Wsla";
        }
    }
}