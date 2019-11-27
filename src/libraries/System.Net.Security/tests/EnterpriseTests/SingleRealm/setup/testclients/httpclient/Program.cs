using System;
using System.Net;
using System.Net.Http;

namespace negclient
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0 || args.Length == 2)
            {
                DisplayUsage();
                return;
            }

            string server = args[0];
            string userName = null;
            string password = null;
            string domain = null;

            if (args.Length > 2)
            {
                userName = args[1];
                password = args[2];
                if (args.Length == 4) domain = args[3];
            }

            try
            {
                var handler = new HttpClientHandler();
                handler.Credentials = (userName == null) ? CredentialCache.DefaultNetworkCredentials :
                    new NetworkCredential(userName, password, domain);
                var client = new HttpClient(handler);

                HttpResponseMessage response = client.GetAsync(server).GetAwaiter().GetResult();
                Console.WriteLine($"{(int)response.StatusCode} {response.ReasonPhrase}");
                string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                Console.WriteLine(body);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        public static void DisplayUsage()
        {
            Console.WriteLine("usage: httpclient <serverUrl> [ <username> <password> [<domain>] ]");
            Console.WriteLine("       if no username/password is specified then DefaultNetworkCredentials is used.");
        }
    }
}
