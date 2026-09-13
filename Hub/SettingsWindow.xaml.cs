using System.Windows;
using System.Collections.ObjectModel;
using Hub.Models.Providers;
using Hub.Models.Settings;
using Hub.Services.Settings;

namespace Hub;

public partial class SettingsWindow : Window
{
    private readonly AppSettingsService settingsService;
    private readonly ProviderSettingsService providerSettingsService;
    private readonly Action<AppSettings> onSaved;
    private readonly ObservableCollection<ProviderSettingsModel> providers = new();
    private readonly ObservableCollection<ProviderSettingsEntry> providerEntries = new();

    public SettingsWindow(AppSettingsService settingsService, AppSettings currentSettings, Action<AppSettings> onSaved)
    {
        InitializeComponent();
        this.settingsService = settingsService;
        providerSettingsService = new ProviderSettingsService();
        this.onSaved = onSaved;

        ImageResolverComboBox.ItemsSource = Enum.GetNames<ImageResolverKind>();
        TransportComboBox.ItemsSource = Enum.GetNames<ProviderTransportKind>();
        ProviderTransportComboBox.ItemsSource = Enum.GetNames<ProviderTransportKind>();

        ProviderSettingsGrid.ItemsSource = providerEntries;

        foreach (var provider in providerSettingsService.LoadAll())
        {
            providers.Add(provider);
        }

        ProviderComboBox.ItemsSource = providers;
        ProviderComboBox.DisplayMemberPath = nameof(ProviderSettingsModel.ProviderName);

        ImageResolverComboBox.SelectedItem = currentSettings.ImageResolverKind.ToString();
        TransportComboBox.SelectedItem = currentSettings.ProviderTransportKind.ToString();
        EndpointTextBox.Text = currentSettings.ProviderEndpoint;
        TimeoutTextBox.Text = currentSettings.ProviderTimeoutSeconds.ToString();
        SearchLimitTextBox.Text = currentSettings.SearchLimit.ToString();

        if (providers.Count > 0)
        {
            ProviderComboBox.SelectedIndex = 0;
        }
    }

    private void ProviderComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ProviderComboBox.SelectedItem is not ProviderSettingsModel selected)
        {
            return;
        }

        ProviderTransportComboBox.SelectedItem = selected.Document.Transport;
        ProviderEndpointTextBox.Text = selected.Document.Endpoint;
        ProviderTimeoutTextBox.Text = selected.Document.TimeoutSeconds.ToString();

        providerEntries.Clear();
        foreach (var pair in selected.Document.Settings)
        {
            providerEntries.Add(new ProviderSettingsEntry { Key = pair.Key, Value = pair.Value });
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Enum.TryParse<ImageResolverKind>(ImageResolverComboBox.SelectedItem as string, true, out var resolverKind))
        {
            System.Windows.MessageBox.Show("Select a valid image resolver.", "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!Enum.TryParse<ProviderTransportKind>(TransportComboBox.SelectedItem as string, true, out var transportKind))
        {
            System.Windows.MessageBox.Show("Select a valid provider transport.", "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(TimeoutTextBox.Text, out var timeoutSeconds) || timeoutSeconds <= 0)
        {
            System.Windows.MessageBox.Show("Provider timeout must be a positive number.", "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(SearchLimitTextBox.Text, out var searchLimit) || searchLimit <= 0)
        {
            System.Windows.MessageBox.Show("Search limit must be a positive number.", "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var updatedSettings = new AppSettings
        {
            ImageResolverKind = resolverKind,
            ProviderTransportKind = transportKind,
            ProviderEndpoint = EndpointTextBox.Text.Trim(),
            ProviderTimeoutSeconds = timeoutSeconds,
            SearchLimit = searchLimit,
        };

        var validationErrors = settingsService.Save(updatedSettings);
        if (validationErrors.Count > 0)
        {
            System.Windows.MessageBox.Show(string.Join(Environment.NewLine, validationErrors), "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ProviderComboBox.SelectedItem is ProviderSettingsModel selectedProvider)
        {
            if (!Enum.TryParse<ProviderTransportKind>(ProviderTransportComboBox.SelectedItem as string, true, out var providerTransportKind))
            {
                System.Windows.MessageBox.Show("Select a valid provider transport.", "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(ProviderTimeoutTextBox.Text, out var providerTimeoutSeconds) || providerTimeoutSeconds <= 0)
            {
                System.Windows.MessageBox.Show("Provider timeout must be a positive number.", "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            selectedProvider.Document.Transport = providerTransportKind.ToString();
            selectedProvider.Document.Endpoint = ProviderEndpointTextBox.Text.Trim();
            selectedProvider.Document.TimeoutSeconds = providerTimeoutSeconds;
            selectedProvider.Document.Settings = providerEntries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
                .ToDictionary(entry => entry.Key.Trim(), entry => entry.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);

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