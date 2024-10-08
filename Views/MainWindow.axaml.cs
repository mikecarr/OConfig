using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Layout;
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
        
        this.WindowState = WindowState.Normal; // Ensure the window is normal
        this.Activate(); // Activate the window
        this.Topmost = true; // Keep the window in front
        // this.Icon = new WindowIcon("Assets/openipc.ico"); 

        // Set the logs TextBox and ScrollViewer for the Logger
        Logger.Instance.SetLogComponents(LogsTextBox, LogsScrollViewer);

        _tabControl = this.FindControl<TabControl>("MainTabControl");

        // Load the application configuration
        _appConfig = AppConfig.Load();
        PopulateFields(); // Populate fields with loaded config
        
        
    }

    private void PopulateFields()
    {
        // Populate text fields with the saved config values
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
    
    // When the Connect button is clicked
    private void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        Logger.Instance.Log("Attempting to connect...");

        if (CameraRadioButton.IsChecked == true)
        {
            // Retrieve the values from the text boxes
            _appConfig.Username = UsernameTextBox.Text;
            _appConfig.Password = PasswordTextBox.Text;
            _appConfig.IPAddress = IPAddressTextBox.Text;
            _appConfig.DeviceType = MainWindowViewModel.DeviceType.Camera;

            // Save the configuration
            _appConfig.Save();

            string username = _appConfig.Username;
            string password = _appConfig.Password;
            string ipAddress = _appConfig.IPAddress;
            string deviceType = _appConfig.DeviceType.ToString();

            // SSH and file retrieval in a background thread to avoid freezing the UI
            Task.Run(() => ConnectAndReadFiles(username, password, ipAddress));
        }
    }

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

                    // Based on the file extension, parse and display
                    if (filePath.EndsWith(".yaml") || filePath.EndsWith(".yml"))
                    {
                        var yamlParsed = ParseYaml(fileContent);
                        Dispatcher.UIThread.InvokeAsync(() => AddFileTab(filePath, yamlParsed));
                    }
                    else if (filePath.EndsWith(".json"))
                    {
                        var jsonParsed = ParseJson(fileContent);
                        Dispatcher.UIThread.InvokeAsync(() => AddFileTab(filePath, jsonParsed));
                    }
                    else
                    {
                        // Handle other formats as plain text
                        Dispatcher.UIThread.InvokeAsync(() => AddFileTab(filePath, fileContent));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"SSH Error: {ex.Message}");
            }
            finally
            {
                client.Disconnect();
                Logger.Instance.Log("SSH Disconnected");
            }
        }
    }

    // Parse YAML using YamlDotNet
    private Dictionary<string, object> ParseYaml(string fileContent)
    {
        var deserializer = new DeserializerBuilder().Build();
        return deserializer.Deserialize<Dictionary<string, object>>(fileContent);
    }

    // Parse JSON using System.Text.Json
    private Dictionary<string, object> ParseJson(string fileContent)
    {
        return JsonSerializer.Deserialize<Dictionary<string, object>>(fileContent);
    }

        public void AddFileTab(string filePath, object fileContent)
    {
        // Extract the file name for the tab header
        string fileName = System.IO.Path.GetFileName(filePath);

        // Check if a tab for this file already exists
        var existingTab = _tabControl.Items.OfType<TabItem>()
            .FirstOrDefault(tab => tab.Header.ToString() == fileName);

        if (existingTab != null)
        {
            // Update existing tab
            UpdateTabContent(existingTab, fileContent);
        }
        else
        {
            // Create new tab header with custom font size
            var headerTextBlock = new TextBlock
            {
                Text = fileName,
                FontSize = 14, // Set your desired font size here
                // You can set other properties like FontWeight, Foreground, etc.
            };

            // Create a StackPanel for the tab content
            var content = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            // Create editor for file content
            Control editor = fileContent switch
            {
                Dictionary<string, object> parsedData => CreateYamlOrJsonTreeView(parsedData),
                string plainText => new TextBox { Text = plainText, AcceptsReturn = true },
                _ => throw new InvalidOperationException("Unsupported file type")
            };

            // Add the editor to the content
            content.Children.Add(editor);

            // Create Save button
            var saveButton = new Button
            {
                Content = "Save",
                Width = 100,
                Margin = new Thickness(5)
            };
            saveButton.Click += (sender, e) => SaveFile(filePath, editor);

            // Create Cancel button
            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 100,
                Margin = new Thickness(5)
            };
            cancelButton.Click += (sender, e) => CancelChanges(editor, fileContent);

            // Create a StackPanel for buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);

            // Add the button panel to the content
            content.Children.Add(buttonPanel);

            // Create the new TabItem
            var newTab = new TabItem
            {
                Header = headerTextBlock, // Use the TextBlock as the header
                Content = content
            };

            // Add the new TabItem to your TabControl
            _tabControl.Items.Add(newTab);
        }
    }



    private void SaveFile(string filePath, Control editor)
    {
        string contentToSave = string.Empty;

        // Gather content from the editor
        if (editor is TextBox textBox)
        {
            contentToSave = textBox.Text;
        }
        else if (editor is TreeView treeView)
        {
            // Implement logic to convert the TreeView content back to YAML/JSON format if necessary
            // For now, we will just log it as an example
            Logger.Instance.Log("Saving content from TreeView (implement serialization logic).");
            // contentToSave = ConvertTreeViewToYamlOrJson(treeView); // Implement this method if needed
        }

        // Assuming you have username, password, and ipAddress from your connection details
        string username = UsernameTextBox.Text;
        string password = PasswordTextBox.Text;
        string ipAddress = IPAddressTextBox.Text;

        // Send content back to the server using SSH
        Task.Run(() =>
        {
            using (var client = new SshClient(ipAddress, username, password))
            {
                try
                {
                    client.Connect();
                    Logger.Instance.Log("SSH Connected");

                    // Use SFTP or command to write back the file
                    var sftp = new SftpClient(ipAddress, username, password);
                    sftp.Connect();

                    using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(contentToSave)))
                    {
                        // Upload the file to the specified path
                        sftp.UploadFile(memoryStream, filePath);
                    }

                    Logger.Instance.Log("File saved successfully");
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Error saving file: {ex.Message}");
                }
                finally
                {
                    client.Disconnect();
                    Logger.Instance.Log("SSH Disconnected");
                }
            }
        });
    }

    private void CancelChanges(Control editor, object originalContent)
    {
        if (editor is TextBox textBox)
        {
            textBox.Text = originalContent.ToString(); // Revert to original text
        }
        else if (editor is TreeView treeView)
        {
            // Implement logic to revert the TreeView content if necessary
            Logger.Instance.Log("Canceling changes for TreeView (implement revert logic).");
        }
    }

    private TreeView CreateYamlOrJsonTreeView(Dictionary<string, object> parsedData)
    {
        var treeView = new TreeView();
        var treeBuilder = new YamlTreeBuilder(treeView); // Reusing the builder for YAML and JSON
        treeBuilder.BuildTree(parsedData);
        return treeView;
    }

    private void UpdateTabContent(TabItem existingTab, object fileContent)
    {
        if (fileContent is Dictionary<string, object> parsedData)
        {
            // Update the existing TreeView
            if (existingTab.Content is TreeView treeView)
            {
                var treeBuilder = new YamlTreeBuilder(treeView);
                treeBuilder.BuildTree(parsedData); // Rebuild the tree with updated data
            }
            else
            {
                existingTab.Content = CreateYamlOrJsonTreeView(parsedData); // Replace with new TreeView
            }
        }
        else if (fileContent is string plainText)
        {
            // Update the existing TextBox content
            if (existingTab.Content is TextBox textBox)
            {
                textBox.Text = plainText; // Update the existing TextBox content
            }
            else
            {
                // Replace the content with a new TextBox
                existingTab.Content = new TextBox { Text = plainText, AcceptsReturn = true };
            }
        }
    }
}
