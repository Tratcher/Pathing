using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Ready");
            Console.ReadKey();

            var request = "GET /a%2F HTTP/1.1\r\nHost: localhost:5000\r\nConnection: close\r\n\r\n";

            var client = new TcpClient();
            client.Connect(new IPEndPoint(IPAddress.Loopback, 5000));
            var stream = client.GetStream();
            stream.Write(Encoding.UTF8.GetBytes(request));
        }
    }
}
