// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Principal;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

namespace System.Net.Security.Tests
{
    public class NegotiateStreamTest
    {
        private readonly ITestOutputHelper _output;

        public NegotiateStreamTest(ITestOutputHelper output)
        {
            _output = output;
        }

        public static readonly object[][] SuccessCasesMemberData =
        {
            new object[] { new NetworkCredential("user1", "password"), true, "HTTP/linuxweb.linux.contoso.com" },
            new object[] { new NetworkCredential("user1", "password"), false, "UNKNOWNHOST/localhost" },
            new object[] { new NetworkCredential("ntlmonly", "password"), false, "HOST/localhost" },
            new object[] { new NetworkCredential("ntlmonly", "password"), false, "NEWSERVICE/localhost" },
            //new object[] { CredentialCache.DefaultNetworkCredentials, true, "HOST/localhost" },
            //new object[] { CredentialCache.DefaultNetworkCredentials, true, "HTTP/linuxweb.linux.contoso.com" },
        };

        [Theory]
        [MemberData(nameof(SuccessCasesMemberData))]
        public async Task Client_ValidCreds_Success(NetworkCredential creds, bool isKerberos, string target)
        {
                var client = new TcpClient();
                client.Connect(TestConfiguration.NegotiateServerHost, TestConfiguration.NegotiateServerPort);
                // Ensure the client does not close when there is still data to be sent to the server.
                client.LingerState = new LingerOption(true, 0);

                // Request authentication.
                NetworkStream clientStream = client.GetStream();
                var authStream = new NegotiateStream(clientStream, false);
                await authStream.AuthenticateAsClientAsync(
                    creds,
                    target,
                    ProtectionLevel.EncryptAndSign,
                    TokenImpersonationLevel.Identification);

                VerifyStreamProperties(authStream, isServer: false, isKerberos, target);
        }

        private void VerifyStreamProperties(NegotiateStream stream, bool isServer, bool isKerberos, string remoteName)
        {
            Assert.True(stream.IsAuthenticated);
            Assert.Equal(TokenImpersonationLevel.Identification, stream.ImpersonationLevel);
            Assert.True(stream.IsEncrypted);
            Assert.Equal(isKerberos, stream.IsMutuallyAuthenticated);
            Assert.Equal(isServer, stream.IsServer);
            Assert.True(stream.IsSigned);
            Assert.False(stream.LeaveInnerStreamOpen);

            IIdentity remoteIdentity = stream.RemoteIdentity;
            Assert.Equal(isKerberos ? "Kerberos" : "NTLM", remoteIdentity.AuthenticationType);
            Assert.True(remoteIdentity.IsAuthenticated);
            _output.WriteLine($"VerifyStreamProperties: remote '{remoteIdentity.Name}'");
            Assert.Equal(remoteName, remoteIdentity.Name);
        }

        public static readonly object[][] FailureCasesMemberData =
        {
            // 'user1' is a valid Kerberos credential. But trying to connect to the server using
            // the 'NEWSERVICE/localhost' SPN is not valid. That SPN, while registered in the overall
            // Kerberos realm, is not registered on this particular server's keytab. So, this test case verifies
            // that SPNEGO won't fallback from Kerberos to NTLM. Instead, it causes an AuthenticationException.
            new object[] { new NetworkCredential("user2", "password"), "NEWSERVICE/localhost" },

            // Invalid Kerberos credential password.
            new object[] { new NetworkCredential("user1", "passwordxx"), "HTTP/linuxweb.linux.contoso.com" },
        };

        [Theory]
        [MemberData(nameof(FailureCasesMemberData))]
        public async Task Client_Authentication_Failure(NetworkCredential creds, string target)
        {
            var client = new TcpClient();
            client.Connect(TestConfiguration.NegotiateServerHost, TestConfiguration.NegotiateServerPort);
            // Ensure the client does not close when there is still data to be sent to the server.
            client.LingerState = new LingerOption(true, 0);

            // Request authentication.
            NetworkStream clientStream = client.GetStream();
            var authStream = new NegotiateStream(clientStream, false);
            await Assert.ThrowsAsync<AuthenticationException>(() =>
                authStream.AuthenticateAsClientAsync(creds, target, ProtectionLevel.EncryptAndSign, TokenImpersonationLevel.Identification));
        }
    }
}
