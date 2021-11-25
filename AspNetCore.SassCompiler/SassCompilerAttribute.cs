using System;
using System.ComponentModel;

namespace AspNetCore.SassCompiler
{
    [AttributeUsage(AttributeTargets.Assembly)]
    [EditorBrowsable(EditorBrowsableState.Never)]
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
