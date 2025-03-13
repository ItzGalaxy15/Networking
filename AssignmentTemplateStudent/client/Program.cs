using System.Collections.Immutable;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LibData;

// SendTo();
class Program
{
    static void Main(string[] args)
    {
        ClientUDP.start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}

class ClientUDP
{

    //TODO: [Deserialize Setting.json]   
    static string configFile = @"../Setting.json";
    static Setting? setting;
    private static IPEndPoint? serverEndPoint;
    private static Socket? clientSocket;

    static void LoadSettings()
    {
        try
        {
            string configContent = File.ReadAllText(configFile);
            setting = JsonSerializer.Deserialize<Setting>(configContent);
            Console.WriteLine("[Client] Configuration loaded successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Client] Error loading settings: " + ex.Message);
            Environment.Exit(1);
        }
    }

        public static void start()
    {
        //TODO: [Create endpoints and socket]
        try
        {
            LoadSettings();
            
            if (setting == null || setting.ServerIPAddress == null || setting.ClientIPAddress == null)
            {
                Console.WriteLine("[Client] Invalid settings, exiting.");
                return;
            }
            
            IPEndPoint clientEndPoint = new(IPAddress.Any, setting.ClientPortNumber);
            serverEndPoint = new IPEndPoint(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber);
            
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            clientSocket.Bind(clientEndPoint);
            
            Console.WriteLine("[Client] Sending HELLO message...");
            
            var helloMessage = new Message { MsgId = 1, MsgType = MessageType.Hello, Content = "Hello from client" };
            byte[] helloBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(helloMessage));
            clientSocket.SendTo(helloBytes, serverEndPoint);
            
            byte[] buffer = new byte[1024];
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            int receivedBytes = clientSocket.ReceiveFrom(buffer, ref remoteEP);
            string receivedMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
            Console.WriteLine("[Client] Received: " + receivedMessage);
            
            clientSocket.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Client] Error: " + ex.Message);
        }

        


        //TODO: [Create and send HELLO]

        //TODO: [Receive and print Welcome from server]

        // TODO: [Create and send DNSLookup Message]


        //TODO: [Receive and print DNSLookupReply from server]


        //TODO: [Send Acknowledgment to Server]

        // TODO: [Send next DNSLookup to server]
        // repeat the process until all DNSLoopkups (correct and incorrect onces) are sent to server and the replies with DNSLookupReply

        //TODO: [Receive and print End from server]





    }
}