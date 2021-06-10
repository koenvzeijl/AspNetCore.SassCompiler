using System;

namespace AspNetCore.SassCompiler
{
    public class SassCompilerAttribute : Attribute
    {
        public SassCompilerAttribute(string sassBinary)
        {
            SassBinary = sassBinary;
        }

        internal string SassBinary { get; }
    }
}
