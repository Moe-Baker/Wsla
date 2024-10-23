using Microsoft.CodeAnalysis;

namespace Wsla.Generator
{
    public class GlobalNetworkGenerator
    {
        public class DiagnosticCodes
        {
            public static readonly DiagnosticDescriptor Example = new DiagnosticDescriptor("WSLA-1", "Example Title", "Example Message", "Example Category", DiagnosticSeverity.Error, true);
        }

        public class Constants
        {
            public static readonly string Name = "Wsla";
        }
    }
}