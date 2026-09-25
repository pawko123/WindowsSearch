using System;
using System.Globalization;
using System.Reflection;

namespace Hub.ViewModels.Settings;

public class StringSettingItem : SettingItemViewModel
{
    public string TextValue
    {
        get => field;
        set { field = value; OnPropertyChanged(); }
    }

    public StringSettingItem(PropertyInfo property, string label, string description, string initialValue) 
        : base(property, label, description)
    {
        TextValue = initialValue;
    }

    public override void ApplyTo(object target)
    {
        var val = ConvertText(TextValue, Property.PropertyType);
        Property.SetValue(target, val);
    }

    private static object? ConvertText(string text, Type targetType)
    {
        if (targetType == typeof(string))
            return text ?? string.Empty;

        return Convert.ChangeType(text, targetType, CultureInfo.InvariantCulture);
    }
}