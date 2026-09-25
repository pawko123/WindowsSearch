using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Hub.Models.Settings;

namespace Hub.ViewModels.Settings;

public class DictionarySettingItem : SettingItemViewModel
{
    public ObservableCollection<ProviderSettingsEntry> Entries { get; }

    public DictionarySettingItem(PropertyInfo property, string label, string description, ObservableCollection<ProviderSettingsEntry> entries)
        : base(property, label, description)
    {
        Entries = entries;
    }

    public override void ApplyTo(object target)
    {
        var dict = Entries.Where(e => !string.IsNullOrWhiteSpace(e.Key))
                          .ToDictionary(e => e.Key.Trim(), e => e.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        Property.SetValue(target, dict);
    }
}