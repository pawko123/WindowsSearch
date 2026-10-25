namespace WindowsSearch.Common.Validation;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
public sealed class ProviderActionAttribute : Attribute
{
    private string? _pollAction;
    private string? _cancelAction;

    public string Action { get; }
    public string ButtonText { get; }
    public string Description { get; set; } = string.Empty;
    public int PollIntervalMs { get; set; } = 1000;

    public object? PollAction
    {
        get => _pollAction;
        set => _pollAction = value?.ToString();
    }

    public object? CancelAction
    {
        get => _cancelAction;
        set => _cancelAction = value?.ToString();
    }

    public ProviderActionAttribute(object action, string buttonText)
    {
        Action = action?.ToString() ?? string.Empty;
        ButtonText = buttonText;
    }
}
