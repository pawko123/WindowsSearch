using System.ComponentModel.DataAnnotations;
using System.Windows.Controls;
using Hub.ViewModels.Settings;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using Hub.Models.Settings;
using Hub.Services.Settings;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Validation;

namespace Hub;

public partial class SettingsWindow : Window
{

    private readonly AppSettingsService settingsService;
    private readonly ProviderSettingsService providerSettingsService;
    private readonly Action<AppSettings> onSaved;
    private readonly ObservableCollection<ProviderSettingsModel> providers = new();
    private readonly ObservableCollection<SettingItemViewModel> hubFields = new();
    private readonly ObservableCollection<SettingItemViewModel> providerFields = new();

    public SettingsWindow(AppSettingsService settingsService, AppSettings currentSettings, Action<AppSettings> onSaved)
    {
        InitializeComponent();
        this.settingsService = settingsService;
        providerSettingsService = new ProviderSettingsService();
        this.onSaved = onSaved;
        HubSettingsList.ItemsSource = hubFields;
        ProviderSettingsList.ItemsSource = providerFields;

        BuildSettingsForm(currentSettings, hubFields,
            typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance));

        foreach (var provider in providerSettingsService.LoadAll())
        {
            providers.Add(provider);
        }

        ProviderComboBox.ItemsSource = providers;
        ProviderComboBox.DisplayMemberPath = nameof(ProviderSettingsModel.ProviderName);

        if (providers.Count > 0)
        {
            ProviderComboBox.SelectedIndex = 0;
        }
    }

    private void ProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProviderComboBox.SelectedItem is not ProviderSettingsModel selectedProvider) return;
        
        BuildSettingsForm(selectedProvider.Settings, providerFields,
            selectedProvider.Settings.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance));
    }

    /// <summary>
    /// Renders one field per writable property of <paramref name="settings"/> - works for both
    /// AppSettings (Hub panel) and any ProviderSettingsBase-derived type (provider panel). No XAML
    /// is needed per settings class; [Display] drives the label/hint and validation runs on Save.
    /// </summary>
    private static void BuildSettingsForm(object settings, ObservableCollection<SettingItemViewModel> fields, IEnumerable<PropertyInfo> properties)
    {
        fields.Clear();

        foreach (var property in properties.Where(p => p.CanWrite))
        {
            var display = property.GetCustomAttribute<DisplayAttribute>();
            var label = display?.GetName() ?? property.Name;
            var description = display?.GetDescription() ?? string.Empty;
            var value = property.GetValue(settings);

            SettingItemViewModel viewModel = property.PropertyType switch
            {
                Type t when t == typeof(Dictionary<string, string>) => new DictionarySettingItem(
                    property, label, description, 
                    new ObservableCollection<ProviderSettingsEntry>(
                        ((Dictionary<string, string>)(value ?? new Dictionary<string, string>()))
                            .Select(pair => new ProviderSettingsEntry { Key = pair.Key, Value = pair.Value }))),
                            
                Type t when t.IsEnum => new EnumSettingItem(
                    property, label, description, Enum.GetNames(t), value?.ToString() ?? string.Empty),
                    
                Type t when t == typeof(bool) => new BoolSettingItem(
                    property, label, description, value as bool? ?? false),
                    
                _ => new StringSettingItem(
                    property, label, description, value?.ToString() ?? string.Empty)
            };

            fields.Add(viewModel);
        }
    }
    private static bool TryApplyFields(object target, IEnumerable<SettingItemViewModel> fields)
    {
        bool hasErrors = false;
        foreach (var field in fields)
        {
            field.ErrorText = null;
            try
            {
                field.ApplyTo(target);
            }
            catch
            {
                field.ErrorText = "Invalid format.";
                hasErrors = true;
            }
        }
        return !hasErrors;
    }

    private void SaveHub_Click(object sender, RoutedEventArgs e)
    {
        bool hasAnyErrors = false;
        var updatedSettings = new AppSettings();
        
        if (!TryApplyFields(updatedSettings, hubFields))
        {
            hasAnyErrors = true;
        }
        else
        {
            var hubValidationErrors = SettingsValidationHelper.ValidateDetailed(updatedSettings);
            if (hubValidationErrors.Count > 0)
            {
                MapValidationErrors(hubFields, hubValidationErrors);
                hasAnyErrors = true;
            }
        }

        if (hasAnyErrors)
        {
            MessageBox.Show("Please correct the highlighted errors before saving.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        settingsService.Save(updatedSettings);
        onSaved(updatedSettings);
        
        MessageBox.Show("Hub settings saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveProvider_Click(object sender, RoutedEventArgs e)
    {
        if (ProviderComboBox.SelectedItem is not ProviderSettingsModel selectedProvider) return;

        bool hasAnyErrors = false;
        if (!TryApplyFields(selectedProvider.Settings, providerFields))
        {
            hasAnyErrors = true;
        }
        else
        {
            var providerValidationErrors = SettingsValidationHelper.ValidateDetailed(selectedProvider.Settings);
            if (providerValidationErrors.Count > 0)
            {
                MapValidationErrors(providerFields, providerValidationErrors);
                hasAnyErrors = true;
            }
        }

        if (hasAnyErrors)
        {
            MessageBox.Show("Please correct the highlighted errors before saving.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var saveErrors = providerSettingsService.Save(selectedProvider);
        if (saveErrors.Count > 0)
        {
            MessageBox.Show(string.Join(Environment.NewLine, saveErrors), "Error saving provider", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show($"{selectedProvider.ProviderName} settings saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static void MapValidationErrors(IEnumerable<SettingItemViewModel> fields, IReadOnlyList<System.ComponentModel.DataAnnotations.ValidationResult> validationResults)
    {
        foreach (var result in validationResults)
        {
            foreach (var memberName in result.MemberNames)
            {
                var field = fields.FirstOrDefault(f => f.Property.Name == memberName);
                if (field != null)
                {
                    field.ErrorText = result.ErrorMessage;
                }
            }
        }
    }

}
