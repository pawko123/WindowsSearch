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

    public EnumSettingItem(object targetObject, PropertyInfo property, string label, string description, IEnumerable<string> options, string initialValue)
        : base(targetObject, property, label, description)
    {
        Options = options;
        SelectedOption = initialValue;
    }

    public override void Apply() => Property.SetValue(TargetObject, Enum.Parse(Property.PropertyType, SelectedOption));
}