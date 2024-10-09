using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaApplication6.ViewModels;
using Renci.SshNet; // For handling SSH connections
using YamlDotNet.Serialization; // For handling YAML

namespace AvaloniaApplication6.Views;

public partial class MainWindow : Window
{
    private TabControl _tabControl;
    private AppConfig _appConfig;

    public MainWindow()
    {
        InitializeComponent();

        this.SizeToContent = SizeToContent.Height; // This allows the window to resize based on content

        // Set a fixed height
        this.Height = 600; // or whatever height is appropriate for your design

        this.WindowState = WindowState.Normal; // Ensure the window is normal
        this.Activate(); // Activate the window
        this.Topmost = true; // Keep the window in front
        // this.Icon = new WindowIcon("Assets/openipc.ico"); 

        // Set the logs TextBox and ScrollViewer for the Logger
        Logger.Instance.SetLogComponents(LogsTextBox, LogsScrollViewer);

        _tabControl = this.FindControl<TabControl>("MainTabControl");

        // Load the application configuration
        _appConfig = AppConfig.Load();

        // Populate fields with loaded config
        PopulateFields();
    }

    // Populates the text fields and radio buttons with values from the loaded configuration
    private void PopulateFields()
    {
        UsernameTextBox.Text = _appConfig.Username;
        PasswordTextBox.Text = _appConfig.Password;
        IPAddressTextBox.Text = _appConfig.IPAddress;

        // Set the device type radio button based on loaded config
        if (_appConfig.DeviceType == MainWindowViewModel.DeviceType.Camera)
        {
            CameraRadioButton.IsChecked = true;
        }
        else if (_appConfig.DeviceType == MainWindowViewModel.DeviceType.Radxa)
        {
            RadxaRadioButton.IsChecked = true;
        }
        else if (_appConfig.DeviceType == MainWindowViewModel.DeviceType.None)
        {
            // Set other device type radio button
        }
    }

    // Handles the click event for the Connect button
    private async void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        Logger.Instance.Log("Attempting to connect...");

        // Retrieve the values from the text boxes
        _appConfig.Username = UsernameTextBox.Text;
        _appConfig.Password = PasswordTextBox.Text;
        _appConfig.IPAddress = IPAddressTextBox.Text;

        if (CameraRadioButton.IsChecked == true)
        {
            _appConfig.DeviceType = MainWindowViewModel.DeviceType.Camera;
        }
        else if (RadxaRadioButton.IsChecked == true)
        {
            _appConfig.DeviceType = MainWindowViewModel.DeviceType.Radxa;
        }

        // Save the configuration
        _appConfig.Save();

        string username = _appConfig.Username;
        string password = _appConfig.Password;
        string ipAddress = _appConfig.IPAddress;
        
        // Read the hostname
        string hostname = await GetHostnameAsync(ipAddress, username, password);
        
        // Check if hostname matches the expected pattern
        if (_appConfig.DeviceType == MainWindowViewModel.DeviceType.Camera && !hostname.StartsWith("openipc-"))
        {
            // Show dialog indicating the mismatch
            await ShowErrorDialogAsync("Target does not match the device type.");
            return; // Stop processing
        }
        
        await ConnectAndReadFilesAsync(username, password, ipAddress);
    }

    // Displays an error dialog with a specified message
    private async Task ShowErrorDialogAsync(string message)
    {
        var dialog = new Window
        {
            Title = "Error",
            Width = 300,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            Content = new StackPanel
            {
                Margin = new Thickness(10),
                Children =
                {
                    new TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 10) },
                    new Button
                    {
                        Content = "OK",
                        Name = "OK",
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            }
        };

        dialog.WindowState = WindowState.Normal;

        var okButton = (Button)((StackPanel)dialog.Content).Children[1];
        okButton.Click += (s, e) => dialog.Close();

        // Show the dialog as a modal window
        await dialog.ShowDialog(this);
    }

    // Retrieves the hostname from the specified IP address using SSH
    private async Task<string> GetHostnameAsync(string ipAddress, string username, string password)
    {
        using (var client = new SshClient(ipAddress, username, password))
        {
            try
            {
                await Task.Run(() => client.Connect());
                Logger.Instance.Log("SSH Connected to get hostname");

                // Run command to get the hostname
                var command = await Task.Run(() => client.RunCommand("hostname"));
                var hostname = command.Result.Trim(); // Remove any whitespace

                return hostname;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Error getting hostname: {ex.Message}");
                return string.Empty; // Return an empty string in case of an error
            }
            finally
            {
                client.Disconnect();
                Logger.Instance.Log("SSH Disconnected");
            }
        }
    }

    // Connects via SSH and reads specified files based on the selected device type
    private async Task ConnectAndReadFilesAsync(string username, string password, string ipAddress)
    {
        using (var client = new SshClient(ipAddress, username, password))
        {
            try
            {
                client.Connect();
                Logger.Instance.Log("SSH Connected");

                // List of files to read
                string[] filesToRead = new []{""};
                if (CameraRadioButton.IsChecked == true)
                {
                    filesToRead = new[] { "/etc/wfb.conf", "/etc/majestic.yaml", "/etc/telemetry.conf" };
                }
                else if (RadxaRadioButton.IsChecked == true)
                {
                    filesToRead = new[]
                    {
                        "/config/stream.sh" ,
                        "/config/autoload-wfb-nics.sh",
                        "/config/rec-fps",
                        "/config/screen-mode"
                    };
                }

                foreach (var filePath in filesToRead)
                {
                    // Run the command to get file contents
                    Logger.Instance.Log("Reading " + filePath);
                    var command = await Task.Run(() => client.RunCommand($"cat {filePath}"));
                    var fileContent = command.Result;

                    // Switch to the UI thread to add the tab
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        // Based on the file extension, parse and display
                        if (filePath.EndsWith(".yaml") || filePath.EndsWith(".yml"))
                        {
                            var yamlParsed = ParseYaml(fileContent);
                            AddFileTab(filePath, yamlParsed);
                        }
                        else if (filePath.EndsWith(".json"))
                        {
                            var jsonParsed = ParseJson(fileContent);
                            AddFileTab(filePath, jsonParsed);
                        }
                        else
                        {
                            // Handle other formats as plain text
                            AddFileTab(filePath, fileContent);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() => { Logger.Instance.Log($"SSH Error: {ex.Message}"); });
            }
            finally
            {
                client.Disconnect();
                Logger.Instance.Log("SSH Disconnected");
            }
        }
    }

    // Connects via SSH and reads specified files based on the selected device type (synchronous version)
    private void ConnectAndReadFiles(string username, string password, string ipAddress)
    {
        using (var client = new SshClient(ipAddress, username, password))
        {
            try
            {
                client.Connect();
                Logger.Instance.Log("SSH Connected");

                // List of files to read
                var filesToRead = new[] { "/etc/wfb.conf", "/etc/majestic.yaml", "/etc/telemetry.conf" };

                foreach (var filePath in filesToRead)
                {
                    // Run the command to get file contents
                    var command = client.RunCommand($"cat {filePath}");
                    var fileContent = command.Result;

                    // Switch to the UI thread to add the tab
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        // Based on the file extension, parse and display
                        if (filePath.EndsWith(".yaml") || filePath.EndsWith(".yml"))
                        {
                            var yamlParsed = ParseYaml(fileContent);
                            AddFileTab(filePath, yamlParsed);
                        }
                        else if (filePath.EndsWith(".json"))
                        {
                            var jsonParsed = ParseJson(fileContent);
                            AddFileTab(filePath, jsonParsed);
                        }
                        else
                        {
                            // Handle other formats as plain text
                            AddFileTab(filePath, fileContent);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.InvokeAsync(() => { Logger.Instance.Log($"SSH Error: {ex.Message}"); });
            }
            finally
            {
                client.Disconnect();
                Logger.Instance.Log("SSH Disconnected");
            }
        }
    }

    // Parses YAML content and returns a dictionary representation
    private Dictionary<string, object> ParseYaml(string yamlContent)
    {
        var deserializer = new Deserializer();
        return deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
    }

    // Parses JSON content and returns a dictionary representation
    private Dictionary<string, object> ParseJson(string jsonContent)
    {
        return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
    }

    // Adds a new tab to the TabControl with the file's content
    private void AddFileTab(string filePath, object fileContent)
    {
        var tabItem = new TabItem
        {
            Header = Path.GetFileName(filePath), // Use the file name as the tab header
            Content = new TextBlock { Text = fileContent.ToString(), TextWrapping = TextWrapping.Wrap }, // Display the content
            IsSelected = true // Select the new tab
        };
        _tabControl.Items.Add(tabItem);
    }

    // Saves the configuration upon closing the application
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _appConfig.Save(); // Save current configuration
        base.OnClosing(e);
    }
}
