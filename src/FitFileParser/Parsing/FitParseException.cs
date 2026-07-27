namespace FitFileParser.Parsing;

/// <summary>Raised when a FIT file cannot be parsed.</summary>
public sealed class FitParseException : Exception
{
    public FitParseException(string message) : base(message) { }
    public FitParseException(string message, Exception inner) : base(message, inner) { }
}
