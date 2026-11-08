namespace Stabilt.TellusTalk.Client;

/// <summary>How TellusTalk encodes a text, which sets how many characters each part holds.</summary>
public enum SmsCharacterSet
{
    /// <summary>The encoding that needs the fewest parts.</summary>
    Optimize,

    /// <summary>The GSM 7-bit alphabet, 160 characters in a single part; the text must be in that alphabet.</summary>
    Gsm,

    /// <summary>UCS-2, for any text, 70 characters in a single part.</summary>
    Unicode,
}
