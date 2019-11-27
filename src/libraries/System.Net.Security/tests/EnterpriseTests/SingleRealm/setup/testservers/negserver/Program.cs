using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace negserver
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("usage: negserver <listeningPort>");
                return;
            }

            int listeningPort = int.Parse(args[0]);

            Console.WriteLine($"Listening on {IPAddress.Any}:{listeningPort} for clients...");
            var listener = new TcpListener(IPAddress.Any, listeningPort);
            listener.Start();

            while (true)
            {
                TcpClient clientRequest = null;
                Console.Write("Waiting for client...");
                clientRequest = listener.AcceptTcpClient();
                Console.WriteLine("client connected.");

                Task ignored = Task.Run(() => AuthenticateClient(clientRequest));
            }
        }

        public static void AuthenticateClient(TcpClient clientRequest)
        {
            NetworkStream stream = clientRequest.GetStream();
            var authStream = new NegotiateStream(stream, false);
            var builder = new StringBuilder();

            try
            {
                authStream.AuthenticateAsServer();
                DisplayProperties(authStream);

                var buffer = new byte[65536];
                int bytesRead;

                while (true)
                {
                    bytesRead = authStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    builder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                }

                IIdentity id = authStream.RemoteIdentity;
                string message = builder.ToString();
                Console.WriteLine("{0} sent a message of length: {1}", id.Name, message.Length);
                Console.WriteLine("Client disconnected.");
                Console.WriteLine();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                authStream.Close();
                clientRequest.Close();
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
    }
}
