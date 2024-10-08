using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;


namespace AvaloniaApplication6.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
#pragma warning disable CA1822 // Mark members as static
    public string Greeting => "Welcome to Avalonia!";
#pragma warning restore CA1822 // Mark members as static
    
    // Bindings for UI elements
    public string Username { get; set; }
    public string Password { get; set; }
    public string IPAddress { get; set; }
    public DeviceType SelectedDevice { get; set; }
    public ObservableCollection<string> Logs { get; set; }

    public enum DeviceType
    {
        None,       // Default, in case the device type isn't identified yet
        Camera,     // For devices like the GepRC Cinelog or any camera-type devices
        Radxa,      // For Radxa devices
        NVR         // For NVR (Network Video Recorder) devices
    }
    
    public MainWindowViewModel()
    {
        
    }
    
    

    private void ValidateDevice(string hostname)
    {
        if (hostname.Contains("openipc-"))
        {
            SelectedDevice = DeviceType.Camera;
        }
        else if (hostname.Contains("radxa"))
        {
            SelectedDevice = DeviceType.Radxa;
        }

        Logs.Add($"Device validated: {SelectedDevice}");
    }
}
