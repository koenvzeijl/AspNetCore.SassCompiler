using System;

namespace AspNetCore.SassCompiler
{
    public class SassCompilerAttribute : Attribute
    {
        public SassCompilerAttribute(string sassBinary, string sassSnapshot)
        {
            SassBinary = sassBinary;
            SassSnapshot = sassSnapshot;
        }

        internal string SassBinary { get; }
        internal string SassSnapshot { get; }
    }
}
