// Copyright (c) 2017 TrakHound Inc, All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace TrakHound.MTConnectSniffer
{
    /// <summary>
    /// MTConnect Sniffer used to find MTConnect Devices on a network
    /// </summary>
    public class Sniffer
    {
        public delegate void DeviceHandler(MTConnectDevice device);
        public delegate void RequestStatusHandler(long milliseconds);

        /// <summary>
        /// The timeout used for requests
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// The range of ports to scan for MTConnect Agents
        /// </summary>
        public int[] PortRange { get; set; }

        /// <summary>
        /// Event raised when an MTConnect Device has been found
        /// </summary>
        public event DeviceHandler DeviceFound;

        /// <summary>
        /// Event raised when all requests have completed whether successful or not
        /// </summary>
        public event RequestStatusHandler RequestsCompleted;

        private Stopwatch stopwatch;

        private object _lock = new object();

        int sentPingRequests = 0;
        int receivedPingRequests = 0;

        int sentProbeRequests = 0;
        int receivedProbeRequests = 0;

        public Sniffer()
        {
            Timeout = 500;

            int start = 5000;
            var size = 20;
            var portRange = new int[size];
            for (var i = 0; i < size; i++) portRange[i] = start++;
            PortRange = portRange;
        }

        /// <summary>
        /// Start the Sniffer to find MTConnect Devices on the network
        /// </summary>
        public void Start()
        {
            stopwatch = new Stopwatch();
            stopwatch.Start();

            var hosts = GetHostAddresses();
            foreach (var host in hosts)
            {
                SendPingRequests(host);
            }
        }

        private void CheckRequestsStatus()
        {
            if (receivedPingRequests >= sentPingRequests && receivedProbeRequests >= sentProbeRequests)
            {
                long m = 0;

                if (stopwatch != null)
                {
                    stopwatch.Stop();
                    m = stopwatch.ElapsedMilliseconds;
                }

                RequestsCompleted?.Invoke(m);
            }
        }

        /// <summary>
        /// Get an array of Host Addresses for each Network Interface
        /// </summary>
        private IPAddress[] GetHostAddresses()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            if (interfaces != null)
            {
                var addresses = new List<IPAddress>();

                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    {
                        foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                addresses.Add(ip.Address);
                            }
                        }
                    }
                }

                return addresses.ToArray();
            }

            return null;
        }

        private bool TestPort(IPAddress address, int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(address, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(Timeout);
                    if (!success)
                    {
                        return false;
                    }

                    client.EndConnect(result);
                }
            }
            catch
            {
                return false;
            }
            return true;
        }


        #region "Ping"

        private void SendPingRequests(IPAddress host)
        {
            IPNetwork ip;
            if (IPNetwork.TryParse(host.ToString(), out ip))
            {
                var addresses = IPNetwork.ListIPAddress(ip);
                if (addresses != null)
                {
                    foreach (var address in addresses)
                    {
                        var p = new Ping();
                        p.PingCompleted += PingCompleted;
                        sentPingRequests++;
                        p.SendAsync(address, address);
                    }
                }
            }
        }

        private void PingCompleted(object sender, PingCompletedEventArgs e)
        {
            // Set flag to know when all sent Ping requests have been received
            lock (_lock)
            {
                receivedPingRequests++;
                CheckRequestsStatus();
            }

            var ip = e.UserState as IPAddress;
            if (ip != null && e.Reply.Status == IPStatus.Success)
            {
                foreach (int port in PortRange)
                {
                    if (TestPort(ip, port))
                    {
                        SendProbe(ip, port);
                    }
                }
            }
        }

        #endregion

        #region "MTConnect Probe"

        private class ProbeSender
        {
            public ProbeSender(IPAddress address, int port)
            {
                Address = address;
                Port = port;
            }

            public IPAddress Address { get; set; }
            public int Port { get; set; }
        }


        private void SendProbe(IPAddress address, int port)
        {
            var uri = new UriBuilder("http", address.ToString(), port);

            var probe = new MTConnect.Clients.Probe(uri.ToString());
            probe.UserObject = new ProbeSender(address, port);
            probe.Successful += Probe_Successful;
            probe.Error += Probe_Error;
            probe.ConnectionError += Probe_ConnectionError;
            sentProbeRequests++;
            probe.ExecuteAsync();          
        }

        private void Probe_ConnectionError(Exception ex) { IncrementProbeRequests(); }

        private void Probe_Error(MTConnect.MTConnectError.Document errorDocument) { IncrementProbeRequests(); }

        private void Probe_Successful(MTConnect.MTConnectDevices.Document document)
        {
            IncrementProbeRequests();

            if (document.UserObject != null)
            {
                var sender = document.UserObject as ProbeSender;
                if (sender != null)
                {
                    // Get the MAC Address of the sender
                    var macAddress = GetMacAddress(sender.Address);

                    foreach (var device in document.Devices)
                    {
                        DeviceFound?.Invoke(new MTConnectDevice(sender.Address, sender.Port, macAddress, device.Name));
                    }
                }
            }
        }

        private void IncrementProbeRequests()
        {
            lock (_lock)
            {
                receivedProbeRequests++;
                CheckRequestsStatus();
            }
        }

        #endregion

        #region "MAC Address"

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        static extern int SendARP(int DestIP, int SrcIP, byte[] pMacAddr, ref int PhyAddrLen);

        /// <summary>
        /// Gets the MAC address (<see cref="PhysicalAddress"/>) associated with the specified IP.
        /// </summary>
        /// <param name="ipAddress">The remote IP address.</param>
        /// <returns>The remote machine's MAC address.</returns>
        private static PhysicalAddress GetMacAddress(IPAddress ipAddress)
        {
            const int MacAddressLength = 6;
            int length = MacAddressLength;
            var macBytes = new byte[MacAddressLength];
            if (SendARP(BitConverter.ToInt32(ipAddress.GetAddressBytes(), 0), 0, macBytes, ref length) == 0)
            {
                return new PhysicalAddress(macBytes);
            }

            return null;
        }

        #endregion
    }
}
