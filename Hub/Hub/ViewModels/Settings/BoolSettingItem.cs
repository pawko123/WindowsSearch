using System.Reflection;

namespace Hub.ViewModels.Settings;

public class BoolSettingItem : SettingItemViewModel
{
    public bool BoolValue
    {
        get => field;
        set { field = value; OnPropertyChanged(); }
    }

    public BoolSettingItem(PropertyInfo property, string label, string description, bool initialValue)
        : base(property, label, description)
    {
        BoolValue = initialValue;
    }

    public override void ApplyTo(object target) => Property.SetValue(target, BoolValue);
}