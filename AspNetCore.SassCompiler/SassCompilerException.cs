using System;

namespace AspNetCore.SassCompiler;

public class SassCompilerException : Exception
{
    public SassCompilerException(string message)
        : base(message)
    {
    }

    public SassCompilerException(string message, string errorOutput)
        : base(message)
    {
        ErrorOutput = errorOutput;
    }

    public string ErrorOutput { get; }
}
