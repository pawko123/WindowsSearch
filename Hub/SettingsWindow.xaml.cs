using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Hub.Models.Settings;
using Hub.Services.Settings;
using WindowsSearch.Common.Models;

// Hub also has UseWindowsForms=true (for the tray icon), so these WPF types collide with
// System.Windows.Forms/System.Drawing equivalents unless aliased explicitly.
using Panel = System.Windows.Controls.Panel;
using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;
using Control = System.Windows.Controls.Control;
using DataGrid = System.Windows.Controls.DataGrid;
using DataGridRow = System.Windows.Controls.DataGridRow;
using DataGridCell = System.Windows.Controls.DataGridCell;
using DataGridColumnHeader = System.Windows.Controls.Primitives.DataGridColumnHeader;
using DataGridTextColumn = System.Windows.Controls.DataGridTextColumn;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Binding = System.Windows.Data.Binding;
using Style = System.Windows.Style;
using Setter = System.Windows.Setter;

namespace Hub;

public partial class SettingsWindow : Window
{
    private sealed record SettingField(PropertyInfo Property, string Label, Func<object?> GetValue);

    private readonly AppSettingsService settingsService;
    private readonly ProviderSettingsService providerSettingsService;
    private readonly Action<AppSettings> onSaved;
    private readonly ObservableCollection<ProviderSettingsModel> providers = new();
    private readonly List<SettingField> hubFields = new();
    private readonly List<SettingField> providerFields = new();

    public SettingsWindow(AppSettingsService settingsService, AppSettings currentSettings, Action<AppSettings> onSaved)
    {
        InitializeComponent();
        this.settingsService = settingsService;
        providerSettingsService = new ProviderSettingsService();
        this.onSaved = onSaved;

        BuildSettingsForm(currentSettings, HubSettingsPanel, hubFields,
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
        if (ProviderComboBox.SelectedItem is not ProviderSettingsModel selected)
        {
            return;
        }

        var baseProperties = typeof(ProviderSettingsBase)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var derivedProperties = selected.Settings.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        BuildSettingsForm(selected.Settings, ProviderSettingsPanel, providerFields, baseProperties.Concat(derivedProperties));
    }

    /// <summary>
    /// Renders one field per writable property of <paramref name="settings"/> - works for both
    /// AppSettings (Hub panel) and any ProviderSettingsBase-derived type (provider panel). No XAML
    /// is needed per settings class; [Display] drives the label/hint and validation runs on Save.
    /// </summary>
    private void BuildSettingsForm(object settings, Panel panel, List<SettingField> fields, IEnumerable<PropertyInfo> properties)
    {
        panel.Children.Clear();
        fields.Clear();

        foreach (var property in properties.Where(p => p.CanWrite))
        {
            AddField(settings, property, panel, fields);
        }
    }

    private static void AddField(object settings, PropertyInfo property, Panel panel, List<SettingField> fields)
    {
        var display = property.GetCustomAttribute<DisplayAttribute>();
        var label = display?.GetName() ?? property.Name;
        var description = display?.GetDescription();
        var value = property.GetValue(settings);

        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 4) });

        if (!string.IsNullOrWhiteSpace(description))
        {
            panel.Children.Add(new TextBlock
            {
                Text = description,
                Margin = new Thickness(0, 0, 0, 4),
                Opacity = 0.6,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (property.PropertyType == typeof(Dictionary<string, string>))
        {
            var entries = new ObservableCollection<ProviderSettingsEntry>(
                ((Dictionary<string, string>)(value ?? new Dictionary<string, string>()))
                    .Select(pair => new ProviderSettingsEntry { Key = pair.Key, Value = pair.Value }));

            panel.Children.Add(BuildDictionaryGrid(entries));
            fields.Add(new SettingField(property, label, () => entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
                .ToDictionary(entry => entry.Key.Trim(), entry => entry.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase)));
            return;
        }

        if (property.PropertyType.IsEnum)
        {
            var combo = new ComboBox { Height = 32, Margin = new Thickness(0, 0, 0, 10) };
            combo.ItemsSource = Enum.GetNames(property.PropertyType);
            combo.SelectedItem = value?.ToString();
            panel.Children.Add(combo);
            fields.Add(new SettingField(property, label, () => Enum.Parse(property.PropertyType, (string)combo.SelectedItem!)));
            return;
        }

        if (property.PropertyType == typeof(bool))
        {
            var checkBox = new CheckBox { IsChecked = value as bool? ?? false, Margin = new Thickness(0, 0, 0, 10) };
            panel.Children.Add(checkBox);
            fields.Add(new SettingField(property, label, () => checkBox.IsChecked ?? false));
            return;
        }

        var textBox = new TextBox
        {
            Height = 32,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 0, 10),
            Text = value?.ToString() ?? string.Empty
        };
        panel.Children.Add(textBox);
        fields.Add(new SettingField(property, label, () => ConvertText(textBox.Text, property.PropertyType)));
    }

    private static object? ConvertText(string text, Type targetType)
    {
        if (targetType == typeof(string))
        {
            return text ?? string.Empty;
        }

        return Convert.ChangeType(text, targetType, CultureInfo.InvariantCulture);
    }

    private static System.Windows.Controls.DataGrid BuildDictionaryGrid(ObservableCollection<ProviderSettingsEntry> entries)
    {
        var white = Brushes.White;
        var headerBackground = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
        var rowBackground = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F));
        var borderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF));
        var lineBrush = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
        var editBackground = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
        var editBorder = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));

        var headerStyle = new Style(typeof(DataGridColumnHeader));
        headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, white));
        headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, headerBackground));
        headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, borderBrush));

        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(Control.ForegroundProperty, white));
        rowStyle.Setters.Add(new Setter(Control.BackgroundProperty, rowBackground));
        rowStyle.Setters.Add(new Setter(Control.BorderBrushProperty, lineBrush));

        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(Control.ForegroundProperty, white));
        cellStyle.Setters.Add(new Setter(Control.BackgroundProperty, rowBackground));
        cellStyle.Setters.Add(new Setter(Control.BorderBrushProperty, lineBrush));
        cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 5, 8, 5)));

        var elementStyle = new Style(typeof(TextBlock));
        elementStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, white));
        elementStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));

        var editingStyle = new Style(typeof(TextBox));
        editingStyle.Setters.Add(new Setter(Control.ForegroundProperty, white));
        editingStyle.Setters.Add(new Setter(Control.BackgroundProperty, editBackground));
        editingStyle.Setters.Add(new Setter(Control.BorderBrushProperty, editBorder));

        var grid = new System.Windows.Controls.DataGrid
        {
            ItemsSource = entries,
            AutoGenerateColumns = false,
            CanUserAddRows = true,
            CanUserDeleteRows = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
            Foreground = white,
            Height = 200,
            MinRowHeight = 28,
            RowHeaderWidth = 0,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = lineBrush,
            VerticalGridLinesBrush = lineBrush,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 10),
            ColumnHeaderStyle = headerStyle,
            RowStyle = rowStyle,
            CellStyle = cellStyle,
        };

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Key",
            Binding = new Binding(nameof(ProviderSettingsEntry.Key)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            ElementStyle = elementStyle,
            EditingElementStyle = editingStyle,
        });

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(ProviderSettingsEntry.Value)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            Width = new DataGridLength(2, DataGridLengthUnitType.Star),
            ElementStyle = elementStyle,
            EditingElementStyle = editingStyle,
        });

        return grid;
    }

    private static bool TryApplyFields(object target, IReadOnlyList<SettingField> fields, out List<string> errors)
    {
        errors = new List<string>();
        foreach (var field in fields)
        {
            try
            {
                field.Property.SetValue(target, field.GetValue());
            }
            catch
            {
                errors.Add($"{field.Label} has an invalid value.");
            }
        }

        return errors.Count == 0;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var updatedSettings = new AppSettings();
        if (!TryApplyFields(updatedSettings, hubFields, out var hubFieldErrors))
        {
            System.Windows.MessageBox.Show(string.Join(Environment.NewLine, hubFieldErrors), "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var validationErrors = settingsService.Save(updatedSettings);
        if (validationErrors.Count > 0)
        {
            System.Windows.MessageBox.Show(string.Join(Environment.NewLine, validationErrors), "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ProviderComboBox.SelectedItem is ProviderSettingsModel selectedProvider)
        {
            if (!TryApplyFields(selectedProvider.Settings, providerFields, out var providerFieldErrors))
            {
                System.Windows.MessageBox.Show(string.Join(Environment.NewLine, providerFieldErrors), "Invalid provider settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var providerSaveErrors = providerSettingsService.Save(selectedProvider);
            if (providerSaveErrors.Count > 0)
            {
                System.Windows.MessageBox.Show(string.Join(Environment.NewLine, providerSaveErrors), "Invalid provider settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        onSaved(updatedSettings);
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
