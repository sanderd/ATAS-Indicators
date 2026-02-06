namespace OFT.Attributes;

/// <summary>
/// Mock HelpLink attribute matching ATAS signature
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class HelpLinkAttribute : Attribute
{
    public string Url { get; }

    public HelpLinkAttribute(string url)
    {
        Url = url;
    }
}
