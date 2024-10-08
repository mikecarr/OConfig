using Avalonia.Threading;

namespace AvaloniaApplication6;

using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Threading;

public class Logger
{
    private static Logger _instance;
    private readonly string _logFilePath;
    private TextBox _logsTextBox; // Reference to the logs TextBox for displaying messages
    private ScrollViewer _scrollViewer; // Reference to the ScrollViewer for scrolling

    // Private constructor to prevent instantiation
    private Logger()
    {
        _logFilePath = Path.Combine(AppContext.BaseDirectory, "application.log");
        InitializeLogFile();
    }

    // Singleton instance
    public static Logger Instance => _instance ??= new Logger();

    // Method to initialize the log file
    private void InitializeLogFile()
    {
        using (File.Create(_logFilePath)) { } // Create an empty log file if it doesn't exist
    }

    // Method to set the reference to the TextBox and ScrollViewer for logs
    public void SetLogComponents(TextBox textBox, ScrollViewer scrollViewer)
    {
        _logsTextBox = textBox;
        _scrollViewer = scrollViewer;
    }

    // Method to log messages
    public void Log(string message)
    {
        string formattedMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
        
        // Log to file
        File.AppendAllText(_logFilePath, formattedMessage + Environment.NewLine);
        
        // Log to console
        Console.WriteLine(formattedMessage);
        
        // Log to TextBox (UI)
        AddLogToTextBox(formattedMessage);
    }

    // Method to update the TextBox on the UI thread
    public void AddLogToTextBox(string message)
    {
        if (_logsTextBox != null && _scrollViewer != null)
        {
            // Ensure this runs on the UI thread
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _logsTextBox.Text += message + Environment.NewLine;

                // Scroll to the bottom of the ScrollViewer
                //_scrollViewer.ScrollToVerticalOffset(_scrollViewer.ExtentHeight);
            });
        }
    }
}
