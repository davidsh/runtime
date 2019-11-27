// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Linq;
using System.Net.Test.Common;
using System.Security.Authentication;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

using Xunit;

namespace System.Net.Security.Tests
{
    public abstract class NegotiateStreamLoopbackTest
    {
        private const int PartialBytesToRead = 5;
        private static readonly byte[] s_sampleMsg = Encoding.UTF8.GetBytes("Sample Test Message");

        private const int MaxWriteDataSize = 63 * 1024; // NegoState.MaxWriteDataSize
        private static string s_longString = new string('A', MaxWriteDataSize) + 'Z';
        private static readonly byte[] s_longMsg = Encoding.ASCII.GetBytes(s_longString);

        protected abstract Task AuthenticateAsClientAsync(NegotiateStream client, NetworkCredential credential, string targetName);
        protected abstract Task AuthenticateAsServerAsync(NegotiateStream server);

        public static readonly object[][] SuccessCasesMemberData =
        {
            new object[] { new NetworkCredential("user1", "password"), true, "HOST/localhost" },
            new object[] { new NetworkCredential("user1", "password"), true, "HOST/linuxclient.linux.contoso.com" },
            //new object[] { new NetworkCredential("user1", "password"), false, "UNKNOWNHOST/localhost" },
            //new object[] { new NetworkCredential("ntlmonly", "password"), false, "HOST/localhost" },
            //new object[] { new NetworkCredential("ntlmonly", "password"), false, "NEWSERVICE/localhost" },
            //new object[] { CredentialCache.DefaultNetworkCredentials, true, "HOST/localhost" },
            //new object[] { CredentialCache.DefaultNetworkCredentials, true, "HOST/linuxclient.linux.contoso.com" },
        };

        [Theory]
        [MemberData(nameof(SuccessCasesMemberData))]
        public async Task StreamToStream_ValidAuthentication_Success(
            NetworkCredential creds, bool isKerberos, string target)
        {
            var network = new VirtualNetwork();

            using (var clientStream = new VirtualNetworkStream(network, isServer: false))
            using (var serverStream = new VirtualNetworkStream(network, isServer: true))
            using (var client = new NegotiateStream(clientStream))
            using (var server = new NegotiateStream(serverStream))
            {
                Assert.False(client.IsAuthenticated);
                Assert.False(server.IsAuthenticated);

                Task[] auth = new Task[2];
                auth[0] = AuthenticateAsClientAsync(client, creds, target);
                auth[1] = AuthenticateAsServerAsync(server);

                await TestConfiguration.WhenAllOrAnyFailedWithTimeout(auth);

                VerifyStreamProperties(client, isServer: false, isKerberos, target);

                string remoteName = (creds == CredentialCache.DefaultNetworkCredentials) ?
                    TestConfiguration.DefaultNetworkCredentials.UserName : creds.UserName;
                if (isKerberos)
                {
                    remoteName += "@" + TestConfiguration.Realm;
                }
                else
                {
                    remoteName = TestConfiguration.Domain + "\\" + remoteName;
                }

                VerifyStreamProperties(server, isServer: true, isKerberos, remoteName);
            }
        }

        public static readonly object[][] FailureCasesMemberData =
        {
            // 'user1' is a valid Kerberos credential. But trying to connect to the server using
            // the 'NEWSERVICE/localhost' SPN is not valid. That SPN, while registered in the overall
            // Kerberos realm, is not registered on this particular server's keytab. So, this test case verifies
            // that SPNEGO won't fallback from Kerberos to NTLM. Instead, it causes an AuthenticationException.
            new object[] { new NetworkCredential("user1", "password"), "NEWSERVICE/localhost" },

            // Invalid Kerberos credential password.
            new object[] { new NetworkCredential("user1", "passwordxx"), "HOST/localhost" },
        };

        [Theory]
        [MemberData(nameof(FailureCasesMemberData))]
        public async Task StreamToStream_InvalidAuthentication_Failure(NetworkCredential creds, string target)
        {
            var network = new VirtualNetwork();

            using (var clientStream = new VirtualNetworkStream(network, isServer: false))
            using (var serverStream = new VirtualNetworkStream(network, isServer: true))
            using (var client = new NegotiateStream(clientStream))
            using (var server = new NegotiateStream(serverStream))
            {
                Assert.False(client.IsAuthenticated);
                Assert.False(server.IsAuthenticated);

                Task clientTask = client.AuthenticateAsClientAsync(creds, target);

                await Assert.ThrowsAsync<AuthenticationException>(() => server.AuthenticateAsServerAsync());
            }
        }

        [Fact]
        public async Task NegotiateStream_StreamToStream_Successive_ClientWrite_Sync_Success()
        {
            byte[] recvBuf = new byte[s_sampleMsg.Length];
            VirtualNetwork network = new VirtualNetwork();
            int bytesRead = 0;

            using (var clientStream = new VirtualNetworkStream(network, isServer: false))
            using (var serverStream = new VirtualNetworkStream(network, isServer: true))
            using (var client = new NegotiateStream(clientStream))
            using (var server = new NegotiateStream(serverStream))
            {
                Assert.False(client.IsAuthenticated);
                Assert.False(server.IsAuthenticated);

                Task[] auth = new Task[2];
                auth[0] = AuthenticateAsClientAsync(client, TestConfiguration.DefaultNetworkCredentials, TestConfiguration.TargetName);
                auth[1] = AuthenticateAsServerAsync(server);

                await TestConfiguration.WhenAllOrAnyFailedWithTimeout(auth);

                client.Write(s_sampleMsg, 0, s_sampleMsg.Length);
                server.Read(recvBuf, 0, s_sampleMsg.Length);

                Assert.True(s_sampleMsg.SequenceEqual(recvBuf));

                client.Write(s_sampleMsg, 0, s_sampleMsg.Length);

                // Test partial sync read.
                bytesRead = server.Read(recvBuf, 0, PartialBytesToRead);
                Assert.Equal(PartialBytesToRead, bytesRead);

                bytesRead = server.Read(recvBuf, PartialBytesToRead, s_sampleMsg.Length - PartialBytesToRead);
                Assert.Equal(s_sampleMsg.Length - PartialBytesToRead, bytesRead);

                Assert.True(s_sampleMsg.SequenceEqual(recvBuf));
            }
        }

        [Fact]
        public async Task NegotiateStream_StreamToStream_Successive_ClientWrite_Async_Success()
        {
            byte[] recvBuf = new byte[s_sampleMsg.Length];
            VirtualNetwork network = new VirtualNetwork();
            int bytesRead = 0;

            using (var clientStream = new VirtualNetworkStream(network, isServer: false))
            using (var serverStream = new VirtualNetworkStream(network, isServer: true))
            using (var client = new NegotiateStream(clientStream))
            using (var server = new NegotiateStream(serverStream))
            {
                Assert.False(client.IsAuthenticated);
                Assert.False(server.IsAuthenticated);

                Task[] auth = new Task[2];
                auth[0] = AuthenticateAsClientAsync(client, TestConfiguration.DefaultNetworkCredentials, TestConfiguration.TargetName);
                auth[1] = AuthenticateAsServerAsync(server);

                await TestConfiguration.WhenAllOrAnyFailedWithTimeout(auth);

                auth[0] = client.WriteAsync(s_sampleMsg, 0, s_sampleMsg.Length);
                auth[1] = server.ReadAsync(recvBuf, 0, s_sampleMsg.Length);
                await TestConfiguration.WhenAllOrAnyFailedWithTimeout(auth);
                Assert.True(s_sampleMsg.SequenceEqual(recvBuf));

                await client.WriteAsync(s_sampleMsg, 0, s_sampleMsg.Length);

                // Test partial async read.
                bytesRead = await server.ReadAsync(recvBuf, 0, PartialBytesToRead);
                Assert.Equal(PartialBytesToRead, bytesRead);

                bytesRead = await server.ReadAsync(recvBuf, PartialBytesToRead, s_sampleMsg.Length - PartialBytesToRead);
                Assert.Equal(s_sampleMsg.Length - PartialBytesToRead, bytesRead);

                Assert.True(s_sampleMsg.SequenceEqual(recvBuf));
            }
        }

        [Fact]
        public async Task NegotiateStream_ReadWriteLongMsgSync_Success()
        {
            byte[] recvBuf = new byte[s_longMsg.Length];
            var network = new VirtualNetwork();
            int bytesRead = 0;

            using (var clientStream = new VirtualNetworkStream(network, isServer: false))
            using (var serverStream = new VirtualNetworkStream(network, isServer: true))
            using (var client = new NegotiateStream(clientStream))
            using (var server = new NegotiateStream(serverStream))
            {
                await TestConfiguration.WhenAllOrAnyFailedWithTimeout(
                    client.AuthenticateAsClientAsync(TestConfiguration.DefaultNetworkCredentials, TestConfiguration.TargetName),
                    server.AuthenticateAsServerAsync());

                client.Write(s_longMsg, 0, s_longMsg.Length);

                while (bytesRead < s_longMsg.Length)
                {
                    bytesRead += server.Read(recvBuf, bytesRead, s_longMsg.Length - bytesRead);
                }

                Assert.True(s_longMsg.SequenceEqual(recvBuf));
            }
        }

        [Fact]
        public async Task NegotiateStream_ReadWriteLongMsgAsync_Success()
        {
            byte[] recvBuf = new byte[s_longMsg.Length];
            var network = new VirtualNetwork();
            int bytesRead = 0;

            using (var clientStream = new VirtualNetworkStream(network, isServer: false))
            using (var serverStream = new VirtualNetworkStream(network, isServer: true))
            using (var client = new NegotiateStream(clientStream))
            using (var server = new NegotiateStream(serverStream))
            {
                await TestConfiguration.WhenAllOrAnyFailedWithTimeout(
                    client.AuthenticateAsClientAsync(TestConfiguration.DefaultNetworkCredentials, TestConfiguration.TargetName),
                    server.AuthenticateAsServerAsync());

                await client.WriteAsync(s_longMsg, 0, s_longMsg.Length);

                while (bytesRead < s_longMsg.Length)
                {
                    bytesRead += await server.ReadAsync(recvBuf, bytesRead, s_longMsg.Length - bytesRead);
                }

                Assert.True(s_longMsg.SequenceEqual(recvBuf));
            }
        }

        [Fact]
        public void NegotiateStream_StreamToStream_Flush_Propagated()
        {
            VirtualNetwork network = new VirtualNetwork();

            using (var stream = new VirtualNetworkStream(network, isServer: false))
            using (var negotiateStream = new NegotiateStream(stream))
            {
                Assert.False(stream.HasBeenSyncFlushed);
                negotiateStream.Flush();
                Assert.True(stream.HasBeenSyncFlushed);
            }
        }

        [Fact]
        public void NegotiateStream_StreamToStream_FlushAsync_Propagated()
        {
            VirtualNetwork network = new VirtualNetwork();

            using (var stream = new VirtualNetworkStream(network, isServer: false))
            using (var negotiateStream = new NegotiateStream(stream))
            {
                Task task = negotiateStream.FlushAsync();

                Assert.False(task.IsCompleted);
                stream.CompleteAsyncFlush();
                Assert.True(task.IsCompleted);
            }
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
            Assert.Equal(remoteName, remoteIdentity.Name);
        }
    }

    public sealed class NegotiateStreamStreamToStreamTest_Async : NegotiateStreamLoopbackTest
    {
        protected override Task AuthenticateAsClientAsync(NegotiateStream client, NetworkCredential credential, string targetName) =>
            client.AuthenticateAsClientAsync(credential, targetName);

        protected override Task AuthenticateAsServerAsync(NegotiateStream server) =>
            server.AuthenticateAsServerAsync();
    }

    public sealed class NegotiateStreamStreamToStreamTest_Async_TestOverloadNullBinding : NegotiateStreamLoopbackTest
    {
        protected override Task AuthenticateAsClientAsync(NegotiateStream client, NetworkCredential credential, string targetName) =>
            client.AuthenticateAsClientAsync(credential, null, targetName);

        protected override Task AuthenticateAsServerAsync(NegotiateStream server) =>
            server.AuthenticateAsServerAsync(null);
    }

    public sealed class NegotiateStreamStreamToStreamTest_Async_TestOverloadProtectionLevel : NegotiateStreamLoopbackTest
    {
        protected override Task AuthenticateAsClientAsync(NegotiateStream client, NetworkCredential credential, string targetName) =>
            client.AuthenticateAsClientAsync(credential, targetName, ProtectionLevel.EncryptAndSign, TokenImpersonationLevel.Identification);

        protected override Task AuthenticateAsServerAsync(NegotiateStream server) =>
            server.AuthenticateAsServerAsync((NetworkCredential)CredentialCache.DefaultCredentials, ProtectionLevel.EncryptAndSign, TokenImpersonationLevel.Identification);
    }

    public sealed class NegotiateStreamStreamToStreamTest_Async_TestOverloadAllParameters : NegotiateStreamLoopbackTest
    {
        protected override Task AuthenticateAsClientAsync(NegotiateStream client, NetworkCredential credential, string targetName) =>
            client.AuthenticateAsClientAsync(credential, null, targetName, ProtectionLevel.EncryptAndSign, TokenImpersonationLevel.Identification);

        protected override Task AuthenticateAsServerAsync(NegotiateStream server) =>
            server.AuthenticateAsServerAsync((NetworkCredential)CredentialCache.DefaultCredentials, null, ProtectionLevel.EncryptAndSign, TokenImpersonationLevel.Identification);
    }

    public sealed class NegotiateStreamStreamToStreamTest_BeginEnd : NegotiateStreamLoopbackTest
    {
        protected override Task AuthenticateAsClientAsync(NegotiateStream client, NetworkCredential credential, string targetName) =>
            Task.Factory.FromAsync(client.BeginAuthenticateAsClient, client.EndAuthenticateAsClient, credential, targetName, null);

        protected override Task AuthenticateAsServerAsync(NegotiateStream server) =>
            Task.Factory.FromAsync(server.BeginAuthenticateAsServer, server.EndAuthenticateAsServer, null);
    }

    public sealed class NegotiateStreamStreamToStreamTest_Sync : NegotiateStreamLoopbackTest
    {
        protected override Task AuthenticateAsClientAsync(NegotiateStream client, NetworkCredential credential, string targetName) =>
            Task.Run(() => client.AuthenticateAsClient(credential, targetName));

        protected override Task AuthenticateAsServerAsync(NegotiateStream server) =>
            Task.Run(() => server.AuthenticateAsServer());
    }

    public sealed class NegotiateStreamStreamToStreamTest_Sync_TestOverloadNullBinding : NegotiateStreamLoopbackTest
    {
        protected override Task AuthenticateAsClientAsync(NegotiateStream client, NetworkCredential credential, string targetName) =>
            Task.Run(() => client.AuthenticateAsClient(credential, null, targetName));

        protected override Task AuthenticateAsServerAsync(NegotiateStream server) =>
            Task.Run(() => server.AuthenticateAsServer(null));
    }

    public sealed class NegotiateStreamStreamToStreamTest_Sync_TestOverloadAllParameters : NegotiateStreamLoopbackTest
    {
        protected override Task AuthenticateAsClientAsync(NegotiateStream client, NetworkCredential credential, string targetName) =>
            Task.Run(() => client.AuthenticateAsClient(credential, targetName, ProtectionLevel.EncryptAndSign, TokenImpersonationLevel.Identification));

        protected override Task AuthenticateAsServerAsync(NegotiateStream server) =>
            Task.Run(() => server.AuthenticateAsServer((NetworkCredential)CredentialCache.DefaultCredentials, ProtectionLevel.EncryptAndSign, TokenImpersonationLevel.Identification));
    }
}
