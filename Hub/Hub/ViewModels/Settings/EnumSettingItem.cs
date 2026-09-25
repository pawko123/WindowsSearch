using System;
using System.Collections.Generic;
using System.Reflection;

namespace Hub.ViewModels.Settings;

public class EnumSettingItem : SettingItemViewModel
{
    public IEnumerable<string> Options { get; }
    
    public string SelectedOption
    {
        get => field;
        set { field = value; OnPropertyChanged(); }
    }

    public EnumSettingItem(PropertyInfo property, string label, string description, IEnumerable<string> options, string initialValue)
        : base(property, label, description)
    {
        Options = options;
        SelectedOption = initialValue;
    }

    public override void ApplyTo(object target) => Property.SetValue(target, Enum.Parse(Property.PropertyType, SelectedOption));
}