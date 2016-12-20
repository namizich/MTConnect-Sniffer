![MTConnect Sniffer](mtc-sniffer-logo-50px.png)<br><br>
.NET library for finding MTConnect® Devices on a network.

# How It Works
- The local IP Address is read for each Network Interface
- A list of IP Addresses is found for local subnet
- A ping request is sent to each IP Address in the subnet
- Upon successfully request, each port in the PortRange property is tested to see if open
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
