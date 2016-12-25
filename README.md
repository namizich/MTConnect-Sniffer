![MTConnect Sniffer](mtc-sniffer-logo-50px.png)

[![Travis branch](https://img.shields.io/travis/TrakHound/MTConnect-Sniffer.svg?style=flat-square)](https://travis-ci.org/TrakHound/MTConnect-Sniffer) [![NuGet](https://img.shields.io/nuget/v/MTConnect-Sniffer.svg?style=flat-square)](https://www.nuget.org/packages/MTConnect-Sniffer/)

.NET library for finding MTConnect® Devices on a network.

# How It Works
- The local IP Address is read for each Network Interface
- A list of IP Addresses is found for the local subnet
- A ping request is sent to each IP Address in the subnet
- Upon each successful request, each port in the PortRange property is tested to see if open
- If the port is open then an MTConnect Probe request is sent
- If the MTConnect Probe request is successful then a MTConnectDevice object is created and included in the DeviceFound event

# Example

```c#
void Start()
{
  // Create new MTConnect Sniffer
  var sniffer = new TrakHound.MTConnectSniffer.Sniffer();

  // Attach Event Handlers
  sniffer.DeviceFound += Sniffer_DeviceFound;
  sniffer.RequestsCompleted += Sniffer_RequestsCompleted;

  // Start Sniffer
  sniffer.Start();
}

private static void Sniffer_RequestsCompleted(long milliseconds)
{
  Console.WriteLine("Requests Completed in " + milliseconds + "ms");
}

private static void Sniffer_DeviceFound(TrakHound.MTConnectSniffer.MTConnectDevice device)
{
  Console.WriteLine(device.DeviceName + " Found @ " + device.IpAddress + ":" + device.Port + " (" + device.MacAddress + ")");
}
```

# License
This library is licensed under the Apache 2.0 License.
