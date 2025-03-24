using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.IO;
using LibData;

class Program
{
    static void Main(string[] args)
    {
        ServerUDP.start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}

public class ServerUDP
{
    static string configFile = @"../Setting.json";
    static Setting? setting;
    static IPEndPoint serverEndpoint;
    static Socket? serverSocket;

    static void LoadSettings()
    {
        try
        {
            string configContent = File.ReadAllText(configFile);
            setting = JsonSerializer.Deserialize<Setting>(configContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading settings file: " + ex.Message);
            Environment.Exit(1);
        }
    }

    public static void start()
    {
        // TODO: [Create a socket and endpoints and bind it to the server IP address and port number]
        try
        {
            LoadSettings();
            if (setting == null || string.IsNullOrEmpty(setting.ServerIPAddress) || string.IsNullOrEmpty(setting.ClientIPAddress))
            {
                Console.WriteLine("Invalid configuration file.");
                return;
            }
            
            IPEndPoint serverEndPoint = new(IPAddress.Any, setting.ServerPortNumber);
            Socket serverSocket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverSocket.Bind(serverEndPoint);
            Console.WriteLine($"[Server] Listening on {setting.ServerIPAddress}:{setting.ServerPortNumber}");


            byte[] buffer = new byte[1024];
            EndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);

        //RECEIVING
            // TODO:[Receive and print a received Message from the client
            while (true)
            {
                int receivedBytes = serverSocket.ReceiveFrom(buffer, ref clientEP);
                string receivedMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
                Console.WriteLine($"[Server] Received: {receivedMessage}");

                // TODO:[Receive and print Hello]
                    Message receivedMessageObject = JsonSerializer.Deserialize<Message>(receivedMessage)!;
                    if (receivedMessageObject != null)
                    {
                        Console.WriteLine("[Server] Received: " + receivedMessageObject.Content);
                    }

        // SENDING
            var message = JsonSerializer.Deserialize<Message>(receivedMessage);
                if (message.MsgType == MessageType.Hello)
                {
                // TODO:[Send Welcome to the client]
                    Console.WriteLine("[Server] Sending WELCOME message...");
                
                    var helloMessage = new Message { MsgId = 1, MsgType = MessageType.Hello, Content = "Welcome from server" };
                    byte[] helloBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(helloMessage));
                    Console.WriteLine("[Server] Sent: " + "'" + helloMessage.Content + "' to Client");
                    serverSocket.SendTo(helloBytes, clientEP);
        
                }

    // TODO:[Receive and print DNSLookup]
                else if(message.MsgType == MessageType.DNSLookup)
                {
        // TODO:[Query the DNSRecord in Json file]
                    var dnsLookup = JsonSerializer.Deserialize<DNSRecord>(message.Content.ToString());
                    var dnsRecords = JsonSerializer.Deserialize<List<DNSRecord>>(File.ReadAllText(@"DNSrecords.json"));

                    var dnsRecord = dnsRecords.FirstOrDefault(record => record.Type == dnsLookup.Type && record.Name == dnsLookup.Name);
                    Message dnsReplyMessage;
                    if (dnsRecord != null)
                    {
                        dnsReplyMessage = new Message { MsgId = 2, MsgType = MessageType.DNSLookupReply, Content = dnsRecord };
                    }
        // TODO:[If not found Send Error]
                    else
                    {
                        dnsReplyMessage = new Message { MsgId = 2, MsgType = MessageType.Error, Content = "DNS record not found" };
                    }

        // TODO:[If found Send DNSLookupReply containing the DNSRecord]
                    byte[] dnsReplyBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dnsReplyMessage));
                    serverSocket.SendTo(dnsReplyBytes, clientEP);
                }
        // TODO:[Receive Ack about correct DNSLookupReply from the client]
        // TODO:[If no further requests receieved send End to the client]
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Server] Error: " + ex.Message);
        }
    }
}











