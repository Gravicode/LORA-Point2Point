using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Gadgeteer.Modules.GHIElectronics;
using GHI.Glide;
using Microsoft.SPOT.Hardware;
using System.Text;
using GHI.Processor;
using Microsoft.SPOT.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;

namespace OximeterSensor
{
    public partial class Program
    {
        //UI init..
        GHI.Glide.UI.TextBlock txtLora = null;
        GHI.Glide.UI.TextBlock txtStatus = null;
        GHI.Glide.UI.TextBlock txtSPO2 = null;
        GHI.Glide.UI.TextBlock txtSignal = null;
        GHI.Glide.UI.TextBlock txtPulseRate = null;
        GHI.Glide.Display.Window window = null;
        //LORA setting..
        private static SimpleSerial _loraSerial;
        private static string[] _dataInLora;
        static OutputPort _restPort = new OutputPort(GHI.Pins.FEZSpiderII.Socket11.Pin6, true);
        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            //set display
            this.videoOut.SetDisplayConfiguration(VideoOut.Resolution.Vga800x600);
            //set glide
            window = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.MyForm));

            txtLora = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtLora");
            txtStatus = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtStatus");
            txtSPO2 = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtSPO2");
            txtSignal = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtSignal");
            txtPulseRate = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtPulseRate");
            Glide.MainWindow = window;
          
            //LORA init
            _loraSerial = new SimpleSerial(GHI.Pins.FEZSpiderII.Socket11.SerialPortName, 57600);
            _loraSerial.Open();
            _loraSerial.DataReceived += _loraSerial_DataReceived;
            //reset lora
            _restPort.Write(false);
            Thread.Sleep(1000);
            _restPort.Write(true);
            Thread.Sleep(1000);
            //setup lora for point to point
            //get lora version
            _loraSerial.WriteLine("sys get ver");
            Thread.Sleep(1000);
            //pause join
            _loraSerial.WriteLine("mac pause");
            Thread.Sleep(1000);
            //set antena power
            _loraSerial.WriteLine("radio set pwr 14");
            Thread.Sleep(1000);

            //var DT = new DateTime(2016, 6, 19, 12, 43, 0); // This will set the clock to 9:30:00 on 9/15/2014
            //RealTimeClock.SetDateTime(DT); //This will set the hardware Real-time Clock to what is in DT
            //Debug.Print("New Real-time Clock " + RealTimeClock.GetDateTime().ToString());

            new Thread(SendData).Start();
            //connect wifi
            SetupNetwork();

            //sync time
            var result = Waktu.UpdateTimeFromNtpServer("time.nist.gov", 7);  // Eastern Daylight Time
            Debug.Print(result ? "Time successfully updated" : "Time not updated");


        }

        void SendData()
        {
            //loop forever
            for (; ; )
            {
                if (pulseOximeter.IsProbeAttached)
                {
                    //get data from oximeter
                    var RefreshedSensor = new DataSensor() { SPO2 = pulseOximeter.LastReading.SPO2, PulseRate = pulseOximeter.LastReading.PulseRate, SignalStrength = pulseOximeter.LastReading.SignalStrength, Tanggal = DateTime.Now };
                    string data = Json.NETMF.JsonSerializer.SerializeObject(RefreshedSensor);
                    byte[] b = Encoding.UTF8.GetBytes(data);
                    string hex = "radio tx " + ToHexString(b, 0, b.Length); // TX payload needs to be HEX
                    //send data via lora
                    _loraSerial.WriteLine(hex);
                    txtLora.Text = "Lora Status : OK";
                    txtStatus.Text = "Sending data : " + RefreshedSensor.Tanggal.ToString("dd MMM yy HH:mm:ss");
                    txtSPO2.Text = "SPO2 : " + RefreshedSensor.SPO2;
                    txtSignal.Text = "Signal : " + RefreshedSensor.SignalStrength;
                    txtPulseRate.Text = "Pulse Rate : " + RefreshedSensor.PulseRate;
                    //refresh
                    window.Invalidate();
                    txtLora.Invalidate();
                    txtStatus.Invalidate();
                    txtSPO2.Invalidate();
                    txtSignal.Invalidate();
                    txtPulseRate.Invalidate();
                    Thread.Sleep(2000);
                }
            }
        }
        static void _loraSerial_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            //just for debug
            _dataInLora = _loraSerial.Deserialize();
            for (int index = 0; index < _dataInLora.Length; index++)
            {
                // Debug.Print(_dataInLora[index]);
            }
        }
        //convert byte to hex
        public static string ToHexString(byte[] value, int index, int length)
        {
            char[] c = new char[length * 3];
            byte b;

            for (int y = 0, x = 0; y < length; ++y, ++x)
            {
                b = (byte)(value[index + y] >> 4);
                c[x] = (char)(b > 9 ? b + 0x37 : b + 0x30);
                b = (byte)(value[index + y] & 0xF);
                c[++x] = (char)(b > 9 ? b + 0x37 : b + 0x30);
            }
            return new string(c, 0, c.Length - 1);
        }
        void SetupNetwork()
        {
            string SSID = "wifi berbayar";
            string KeyWifi = "123qweasd";
            //NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
            //NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;

            wifiRS21.NetworkInterface.Open();
            wifiRS21.NetworkInterface.EnableDhcp();
            wifiRS21.NetworkInterface.EnableDynamicDns();
            wifiRS21.NetworkInterface.Join(SSID, KeyWifi);

            while (wifiRS21.NetworkInterface.IPAddress == "0.0.0.0")
            {
                Debug.Print("Waiting for DHCP");
                Thread.Sleep(250);
            }
            ListNetworkInterfaces();
            //The network is now ready to use.

            //set RTC
            //DateTime DT = new DateTime(2016, 4, 18, 13, 13, 50); // This will set a time for the Real-time Clock clock to 1:01:01 on 1/1/2014
            //RealTimeClock.SetDateTime(DT); //This will set the hardware Real-time Clock to what is in DT

        }
        
        void ListNetworkInterfaces()
        {
            var settings = wifiRS21.NetworkSettings;

            Debug.Print("------------------------------------------------");
            Debug.Print("MAC: " + ByteExt.ToHexString(settings.PhysicalAddress, "-"));
            Debug.Print("IP Address:   " + settings.IPAddress);
            Debug.Print("DHCP Enabled: " + settings.IsDhcpEnabled);
            Debug.Print("Subnet Mask:  " + settings.SubnetMask);
            Debug.Print("Gateway:      " + settings.GatewayAddress);
            Debug.Print("------------------------------------------------");

        }


        private static void NetworkChange_NetworkAddressChanged(object sender, Microsoft.SPOT.EventArgs e)
        {
            Debug.Print("Network address changed");
        }

        private static void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            Debug.Print("Network availability: " + e.IsAvailable.ToString());
        }
    }

    public static class ByteExt
    {
        private static char[] _hexCharacterTable = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

#if MF_FRAMEWORK_VERSION_V4_1
    public static string ToHexString(byte[] array, string delimiter = "-")
#else
        public static string ToHexString(this byte[] array, string delimiter = "-")
#endif
        {
            if (array.Length > 0)
            {
                // it's faster to concatenate inside a char array than to
                // use string concatenation
                char[] delimeterArray = delimiter.ToCharArray();
                char[] chars = new char[array.Length * 2 + delimeterArray.Length * (array.Length - 1)];

                int j = 0;
                for (int i = 0; i < array.Length; i++)
                {
                    chars[j++] = (char)_hexCharacterTable[(array[i] & 0xF0) >> 4];
                    chars[j++] = (char)_hexCharacterTable[array[i] & 0x0F];

                    if (i != array.Length - 1)
                    {
                        foreach (char c in delimeterArray)
                        {
                            chars[j++] = c;
                        }

                    }
                }

                return new string(chars);
            }
            else
            {
                return string.Empty;
            }
        }
    }

    public class Waktu
    {
        public static bool UpdateTimeFromNtpServer(string server, int timeZoneOffset)
        {
            try
            {
                var currentTime = GetNtpTime(server, timeZoneOffset);
                Microsoft.SPOT.Hardware.Utility.SetLocalTime(currentTime);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get DateTime from NTP Server
        /// Based on:
        /// http://weblogs.asp.net/mschwarz/archive/2008/03/09/wrong-datetime-on-net-micro-framework-devices.aspx
        /// </summary>
        /// <param name="timeServer">Time Server (NTP) address</param>
        /// <param name="timeZoneOffset">Difference in hours from UTC</param>
        /// <returns>Local NTP Time</returns>
        private static DateTime GetNtpTime(String timeServer, int timeZoneOffset)
        {
            // Find endpoint for TimeServer
            var ep = new IPEndPoint(Dns.GetHostEntry(timeServer).AddressList[0], 123);

            // Make send/receive buffer
            var ntpData = new byte[48];

            // Connect to TimeServer
            using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                // Set 10s send/receive timeout and connect
                s.SendTimeout = s.ReceiveTimeout = 10000; // 10,000 ms
                s.Connect(ep);

                // Set protocol version
                ntpData[0] = 0x1B;

                // Send Request
                s.Send(ntpData);

                // Receive Time
                s.Receive(ntpData);

                // Close the socket
                s.Close();
            }

            const byte offsetTransmitTime = 40;

            ulong intpart = 0;
            ulong fractpart = 0;

            for (var i = 0; i <= 3; i++)
                intpart = (intpart << 8) | ntpData[offsetTransmitTime + i];

            for (var i = 4; i <= 7; i++)
                fractpart = (fractpart << 8) | ntpData[offsetTransmitTime + i];

            ulong milliseconds = (intpart * 1000 + (fractpart * 1000) / 0x100000000L);

            var timeSpan = TimeSpan.FromTicks((long)milliseconds * TimeSpan.TicksPerMillisecond);
            var dateTime = new DateTime(1900, 1, 1);
            dateTime += timeSpan;

            var offsetAmount = new TimeSpan(timeZoneOffset, 0, 0);
            var networkDateTime = (dateTime + offsetAmount);

            return networkDateTime;
        }
    }

    //sensor class
    public class DataSensor
    {
        public DateTime Tanggal { set; get; }
        public int PulseRate { set; get; }
        public int SignalStrength { set; get; }
        public int SPO2 { set; get; }
    }
}
