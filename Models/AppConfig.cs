using AvaloniaApplication6.ViewModels;

namespace AvaloniaApplication6;

using System.IO;
using System.Text.Json;

public class AppConfig
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string IPAddress { get; set; } = string.Empty;
    
    public MainWindowViewModel.DeviceType DeviceType { get; set; }

    private const string ConfigFilePath = "appconfig.json"; // Define the path for the config file

    public static AppConfig Load()
    {
        if (File.Exists(ConfigFilePath))
        {
            var json = File.ReadAllText(ConfigFilePath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        return new AppConfig(); // Return a new config if file doesn't exist
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this);
        File.WriteAllText(ConfigFilePath, json);
    }
}
