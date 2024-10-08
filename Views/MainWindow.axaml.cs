using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using System.Text.Json;
using System.Threading.Tasks;
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

        await ConnectAndReadFilesAsync(username, password, ipAddress);
    
    }

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
        string fileName = System.IO.Path.GetFileName(filePath);
        var existingTab = _tabControl.Items.OfType<TabItem>()
            .FirstOrDefault(tab => tab.Header.ToString() == fileName);

        if (existingTab != null)
        {
            UpdateTabContent(existingTab, fileContent);
        }
        else
        {
            var headerTextBlock = new TextBlock
            {
                Text = fileName,
                FontSize = 14,
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            Control tabContent;

            if (fileContent is Dictionary<string, object> parsedData)
            {
                var deserializer = new YamlDotNet.Serialization.SerializerBuilder().Build();
                var yamlString = deserializer.Serialize(parsedData);

                tabContent = new TextBox
                {
                    Text = yamlString,
                    AcceptsReturn = true,
                    IsReadOnly = false,
                    TextWrapping = TextWrapping.Wrap
                };
            }
            else if (fileContent is string plainText)
            {
                tabContent = new TextBox
                {
                    Text = plainText,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap
                };
            }
            else
            {
                throw new InvalidOperationException("Unsupported file type");
            }

            // Wrap the content in a ScrollViewer to enable scrolling
            var scrollViewer = new ScrollViewer
            {
                Content = tabContent,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 400, // Set a fixed height for the ScrollViewer
            };

            // Create buttons
            var saveButton = new Button { Content = "Save" };
            var cancelButton = new Button { Content = "Cancel" };

            saveButton.Click += (sender, e) => { SaveFile(filePath, tabContent); };
            cancelButton.Click += (sender, e) =>
            {
                CancelChanges(tabContent, tabContent is TextBox textBox ? textBox.Text : null);
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);

            // Add the scroll viewer and buttons to the stack panel
            stackPanel.Children.Add(scrollViewer);
            stackPanel.Children.Add(buttonPanel);

            // Create a new tab with the header and the stack panel
            var newTab = new TabItem
            {
                Header = headerTextBlock,
                Content = stackPanel
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