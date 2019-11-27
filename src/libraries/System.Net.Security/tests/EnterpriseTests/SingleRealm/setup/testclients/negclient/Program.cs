using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;

namespace negclient
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length < 3 || args.Length == 4)
            {
                DisplayUsage();
                return;
            }

            string server = args[0];
            int port = int.Parse(args[1]);
            string target = args[2];
            string userName = null;
            string password = null;
            string domain = null;

            if (args.Length > 3)
            {
                userName = args[3];
                password = args[4];
                if (args.Length == 6) domain = args[5];
            }

            try
            {
                var client = new TcpClient();
                client.Connect(server, port);
                Console.WriteLine($"Client connected to {server}:{port}");

                // Ensure the client does not close when there is still data to be sent to the server.
                client.LingerState = new LingerOption(true, 0);

                // Request authentication.
                NetworkStream clientStream = client.GetStream();
                var authStream = new NegotiateStream(clientStream, false);
                var credential = (userName == null) ? CredentialCache.DefaultNetworkCredentials :
                    new NetworkCredential(userName, password, domain);
                Console.Write("Client waiting for authentication...");
                authStream.AuthenticateAsClient(
                    credential,
                    target,
                    ProtectionLevel.EncryptAndSign,
                    TokenImpersonationLevel.Identification);
                Console.WriteLine("done.");
                DisplayProperties(authStream);

                // Send a message to the server.
                var writer = new StreamWriter(authStream);
                var clientMessage = new string('A', 65536);
                byte[] message = Encoding.UTF8.GetBytes(clientMessage);
                authStream.Write(message, 0, message.Length);
                Console.WriteLine("Sent {0} bytes.", message.Length);

                // Close the client connection.
                authStream.Close();
                Console.WriteLine("Closing client.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        public static void DisplayProperties(NegotiateStream stream)
        {
            Console.WriteLine("IsAuthenticated: {0}", stream.IsAuthenticated);
            Console.WriteLine("IsMutuallyAuthenticated: {0}", stream.IsMutuallyAuthenticated);
            Console.WriteLine("IsEncrypted: {0}", stream.IsEncrypted);
            Console.WriteLine("IsSigned: {0}", stream.IsSigned);
            Console.WriteLine("IsServer: {0}", stream.IsServer);
            Console.WriteLine("ImpersonationLevel: {0}", stream.ImpersonationLevel);
            Console.WriteLine("ServerIdentity.AuthenticationType: {0}", stream.RemoteIdentity.AuthenticationType);
            Console.WriteLine("ServerIdentity.IsAuthenticated: {0}", stream.RemoteIdentity.IsAuthenticated);
            Console.WriteLine("ServerIdentity.Name: {0}", stream.RemoteIdentity.Name);
        }

        public static void DisplayUsage()
        {
            Console.WriteLine("usage: negclient <serverHostNameOrIPAddress> <port> <targetName> [ <username> <password> [<domain>] ]");
            Console.WriteLine("       if no username/password is specified then DefaultNetworkCredentials is used.");
        }
    }
}
