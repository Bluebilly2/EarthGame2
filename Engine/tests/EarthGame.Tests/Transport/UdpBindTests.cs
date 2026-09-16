using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Transport
{
    /// <summary>
    /// Which addresses a UDP socket binds (M1.Ba). A socket bound to every address is one Windows' firewall asks
    /// about, once for each new program path, with a box on the owner's screen; by 2026-09-14 he had answered it
    /// for eight test builds and the test suite twice. An end for this machine binds the loopback address alone:
    /// a server told <see cref="UdpOptions.LocalOnly"/>, and a client joining a loopback address by itself.
    ///
    /// The witness is the system's own table of bound UDP sockets (what netstat prints), read through
    /// <see cref="IPGlobalProperties"/>, not anything the transport says about itself; each rule has its contrast,
    /// a socket left open for friends, so a pass says something.
    /// </summary>
    public sealed class UdpBindTests
    {
        /// <summary>The IPv4 addresses the system says are bound on the port.</summary>
        private static List<IPAddress> BoundOn(int port) =>
            IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners()
                .Where(e => e.Port == port && e.AddressFamily == AddressFamily.InterNetwork)
                .Select(e => e.Address)
                .ToList();

        [Test]
        public void AServerToldLocalOnlyIsOnThisMachinesAddressAlone()
        {
            using (UdpServerTransport st = new UdpServerTransport(new UdpOptions { LocalOnly = true }))
            {
                st.Listen(0);
                Assert.That(st.Port, Is.GreaterThan(0), "no port was bound");
                Assert.That(st.BoundLocalOnly, Is.True);
                List<IPAddress> bound = BoundOn(st.Port);
                Assert.That(bound, Is.Not.Empty, "the system's table has nothing on port " + st.Port);
                Assert.That(bound, Is.All.EqualTo(IPAddress.Loopback), "bound on " + string.Join(", ", bound));
            }
        }

        /// <summary>
        /// A server left open for friends would bind every address, read from the transport's choice and not from a
        /// socket: a socket bound on every address is the firewall's box on the owner's screen once for every path the
        /// suite runs from, and on 2026-09-16 the suite ran from three new paths in ten minutes and raised three (M1.Bb).
        /// Until then this test bound one to read the system's table. The default is closed.
        /// </summary>
        [Test]
        public void AServerLeftOpenForFriendsWouldBindEveryAddressAndTheDefaultIsClosed()
        {
            Assert.That(new UdpOptions().LocalOnly, Is.True, "closed unless opened");
            Assert.That(UdpTransportBase.BindAddressFor(false), Is.EqualTo(IPAddress.Any));
            Assert.That(UdpTransportBase.BindAddress6For(false), Is.EqualTo(IPAddress.IPv6Any));
            Assert.That(UdpTransportBase.BindAddressFor(true), Is.EqualTo(IPAddress.Loopback));
            Assert.That(UdpTransportBase.BindAddress6For(true), Is.EqualTo(IPAddress.IPv6Loopback));
        }

        [TestCase("127.0.0.1")]
        [TestCase("localhost")]
        public void AClientJoiningThisMachineIsOnItsAddressAlone(string address)
        {
            // Told nothing about locality: the address it joins decides.
            using (UdpClientTransport ct = new UdpClientTransport(new UdpOptions { LocalOnly = false }))
            {
                ct.Connect(address, 28917);
                Assert.That(ct.LocalPort, Is.GreaterThan(0), "no port was bound");
                Assert.That(ct.BoundLocalOnly, Is.True);
                List<IPAddress> bound = BoundOn(ct.LocalPort);
                Assert.That(bound, Is.Not.Empty, "the system's table has nothing on port " + ct.LocalPort);
                Assert.That(bound, Is.All.EqualTo(IPAddress.Loopback), "bound on " + string.Join(", ", bound));
            }
        }

        /// <summary>A client joining another machine would bind every address unless told local only; read, not bound (M1.Bb).</summary>
        [Test]
        public void AClientJoiningAnotherMachineWouldBindEveryAddressUnlessToldLocalOnly()
        {
            // 192.0.2.1 is documentation's address (RFC 5737); nothing is sent to it and nothing is bound.
            using (UdpClientTransport open = new UdpClientTransport(new UdpOptions { LocalOnly = false }))
            using (UdpClientTransport closed = new UdpClientTransport())
            {
                Assert.That(open.BindsLocalOnlyFor("192.0.2.1"), Is.False);
                Assert.That(open.BindsLocalOnlyFor("127.0.0.1"), Is.True, "this machine's address decides by itself");
                Assert.That(closed.BindsLocalOnlyFor("192.0.2.1"), Is.True, "the default is closed");
            }
        }

        [Test]
        public void AClientToldLocalOnlyIsOnThisMachinesAddressWhoeverItJoins()
        {
            using (UdpClientTransport ct = new UdpClientTransport(new UdpOptions { LocalOnly = true }))
            {
                ct.Connect("192.0.2.1", 28919);
                Assert.That(ct.BoundLocalOnly, Is.True);
                Assert.That(BoundOn(ct.LocalPort), Is.All.EqualTo(IPAddress.Loopback));
            }
        }

        [TestCase("127.0.0.1", true)]
        [TestCase("127.0.0.2", true)]
        [TestCase("localhost", true)]
        [TestCase("LOCALHOST", true)]
        [TestCase("::1", true)]
        [TestCase("192.168.1.5", false)]
        [TestCase("192.0.2.1", false)]
        [TestCase("example.org", false)]
        [TestCase("", false)]
        public void TheAddressesThatNameThisMachine(string address, bool loopback)
        {
            Assert.That(UdpTransportBase.IsLoopback(address), Is.EqualTo(loopback));
        }
    }
}
