using System.Reflection;

namespace Hub.ViewModels.Settings;

public class BoolSettingItem : SettingItemViewModel
{
    public bool BoolValue
    {
        get => field;
        set { field = value; OnPropertyChanged(); }
    }
    public BoolSettingItem(object targetObject, PropertyInfo property, string label, string description, bool initialValue)
        : base(targetObject, property, label, description)
    {
        BoolValue = initialValue;
    }
    public override void Apply() => Property.SetValue(TargetObject, BoolValue);
}