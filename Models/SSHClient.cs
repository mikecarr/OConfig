using System;
using System.IO;
using Renci.SshNet;

namespace AvaloniaApplication6;

public class SSHClient
{
    private string _username;
    private string _password;
    private string _host;

    public SSHClient(string username, string password, string host)
    {
        _username = username;
        _password = password;
        _host = host;
    }

    public void ConnectAndRunCommand(string command)
    {
        using (var client = new SshClient(_host, _username, _password))
        {
            try
            {
                client.Connect();
                Console.WriteLine("SSH Connected");

                var result = client.RunCommand(command);
                Console.WriteLine("Command output: " + result.Result);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            finally
            {
                client.Disconnect();
                Console.WriteLine("SSH Disconnected");
            }
        }
    }

    public void DownloadFile(string remotePath, string localPath)
    {
        using (var scpClient = new ScpClient(_host, _username, _password))
        {
            try
            {
                scpClient.Connect();
                Console.WriteLine("SCP Connected");

                using (var localFile = File.Create(localPath))
                {
                    scpClient.Download(remotePath, localFile);
                }
                Console.WriteLine($"File downloaded to {localPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            finally
            {
                scpClient.Disconnect();
                Console.WriteLine("SCP Disconnected");
            }
        }
    }
}