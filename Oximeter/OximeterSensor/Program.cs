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
            GlideTouch.Initialize();
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
