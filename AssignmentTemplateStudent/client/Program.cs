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

 
    static string configFile = @"../Setting.json";
    static Setting? setting;
    private static IPEndPoint? serverEndPoint;
    private static Socket? clientSocket;
    
    //TODO: [Deserialize Setting.json]  
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
        try
        {
            LoadSettings();
            

            // SENDING
            if (setting == null || string.IsNullOrEmpty(setting.ServerIPAddress) || string.IsNullOrEmpty(setting.ClientIPAddress))
            {
                Console.WriteLine("[Client] Invalid settings, exiting.");
                return;
            }
            
        //TODO: [Create endpoints and socket]
            IPEndPoint clientEndPoint = new(IPAddress.Any, setting.ClientPortNumber); // De client luistert nu op alle beschikbare netwerkinterfaces.
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            clientSocket.Bind(clientEndPoint);
            
        //TODO: [Create and send HELLO]
            serverEndPoint = new IPEndPoint(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber);
            Console.WriteLine("[Client] Sending HELLO message...");
            
            var helloMessage = new Message { MsgId = 1, MsgType = MessageType.Hello, Content = "Hello from client" };
            string helloMessageJson = JsonSerializer.Serialize(helloMessage);
            byte[] helloMessageBytes = Encoding.ASCII.GetBytes(helloMessageJson);
            clientSocket.SendTo(helloMessageBytes, serverEndPoint);

        // TODO: [Create and send DNSLookup Message]
            var dnsLookupMessage = new Message { MsgId = 2, MsgType = MessageType.DNSLookup, Content = new DNSRecord { Type = "A", Name = "www.test.com" } };
            byte[] dnsLookupBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dnsLookupMessage));
            clientSocket.SendTo(dnsLookupBytes, serverEndPoint);




        // RECEIVING

        //TODO: [Receive and print Welcome from server]
            byte[] buffer = new byte[1024];
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0); // accepteert berichten van elk IP-adres
            int receivedBytes = clientSocket.ReceiveFrom(buffer, ref remoteEP); // Ontvangt het bericht en slaat het op in buffer.
            string receivedMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
            Console.WriteLine("[Client] Received: " + receivedMessage);            

        // TODO: [Receive and print DNSLookupReply from server]
            receivedBytes = clientSocket.ReceiveFrom(buffer, ref remoteEP);
            string dnsLookupReply = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
            Console.WriteLine("[Client] Received DNSLookupReply: " + dnsLookupReply);

        //TODO: [Send Acknowledgment to Server]

        // TODO: [Send next DNSLookup to server]
        // repeat the process until all DNSLoopkups (correct and incorrect onces) are sent to server and the replies with DNSLookupReply

        //TODO: [Receive and print End from server]
            clientSocket.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Client] Error: " + ex.Message);
        }







    }
}