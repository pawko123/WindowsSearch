using System.Reflection;

namespace Hub.ViewModels.Settings;

public class CategoryHeaderItem : SettingItemViewModel
{
    public CategoryHeaderItem(string label, string description) : base(null!, label, description)
    {
    }

    public override System.Windows.Thickness LeftMargin => new(IndentDepth * 24, 0, 0, 4);

    public override void ApplyTo(object target)
    {
        // No-op for category headers
    }
}
