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
    public StringSettingItem(object targetObject, PropertyInfo property, string label, string description, string initialValue) 
        : base(targetObject, property, label, description)
    {
        TextValue = initialValue;
    }
    public override void Apply()
    {
        var val = ConvertText(TextValue, Property.PropertyType);
        Property.SetValue(TargetObject, val);
    }

    private static object? ConvertText(string text, Type targetType)
    {
        if (targetType == typeof(string))
            return text ?? string.Empty;

        return Convert.ChangeType(text, targetType, CultureInfo.InvariantCulture);
    }
}