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
using GHI.Processor;
using Microsoft.SPOT.Hardware;
using GHI.Glide;
using System.Text;
using GHI.Glide.Geom;
using Json.NETMF;

namespace OximeterReader
{
    public partial class Program
    {
        //lora init
        private static SimpleSerial _loraSerial;
        private static string[] _dataInLora;
        //lora reset pin
        static OutputPort _restPort = new OutputPort(GHI.Pins.FEZRaptor.Socket10.Pin6, true);
        private static string rx;
        //UI
        GHI.Glide.UI.Image img = null;
        GHI.Glide.UI.TextBlock txtLora = null;
        GHI.Glide.UI.TextBlock txtStatus = null;
        GHI.Glide.UI.TextBlock txtSPO2 = null;
        GHI.Glide.UI.TextBlock txtSignal = null;
        GHI.Glide.UI.TextBlock txtPulseRate = null;
        GHI.Glide.UI.TextBlock txtDesc = null;
        GHI.Glide.Display.Window window = null;
        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            multicolorLED.BlinkOnce(GT.Color.Red);
            //7" Displays
            Display.Width = 800;
            Display.Height = 480;
            Display.OutputEnableIsFixed = false;
            Display.OutputEnablePolarity = true;
            Display.PixelPolarity = false;
            Display.PixelClockRateKHz = 30000;
            Display.HorizontalSyncPolarity = false;
            Display.HorizontalSyncPulseWidth = 48;
            Display.HorizontalBackPorch = 88;
            Display.HorizontalFrontPorch = 40;
            Display.VerticalSyncPolarity = false;
            Display.VerticalSyncPulseWidth = 3;
            Display.VerticalBackPorch = 32;
            Display.VerticalFrontPorch = 13;
            Display.Type = Display.DisplayType.Lcd;
            if (Display.Save())      // Reboot required?
            {
                PowerState.RebootDevice(false);
            }
            //set up touch screen
            CapacitiveTouchController.Initialize(GHI.Pins.FEZRaptor.Socket13.Pin3);

            window = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.MyForm));
            //glide init
            GlideTouch.Initialize();

            GHI.Glide.UI.Button btn = (GHI.Glide.UI.Button)window.GetChildByName("btnTest");
            img = (GHI.Glide.UI.Image)window.GetChildByName("img1");
            txtLora = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtLora");
            txtStatus = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtStatus");
            txtSPO2 = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtSPO2");
            txtSignal = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtSignal");
            txtPulseRate = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtPulseRate");
            txtDesc = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtDesc");
            img.Visible = false;

            btn.TapEvent += btn_TapEvent;

            Glide.MainWindow = window;
            //reset lora
            _restPort.Write(false);
            Thread.Sleep(1000);
            _restPort.Write(true);
            Thread.Sleep(1000);

            _loraSerial = new SimpleSerial(GHI.Pins.FEZRaptor.Socket10.SerialPortName, 57600);
            _loraSerial.Open();
            _loraSerial.DataReceived += _loraSerial_DataReceived;
            //get version
            _loraSerial.WriteLine("sys get ver");
            Thread.Sleep(1000);
            //pause join
            _loraSerial.WriteLine("mac pause");
            Thread.Sleep(1000);
            //antena power
            _loraSerial.WriteLine("radio set pwr 14");
            Thread.Sleep(1000);
            //set device to receive
            _loraSerial.WriteLine("radio rx 0"); //set module to RX

        }
        //button on tap
        void btn_TapEvent(object sender)
        {
            Bitmap bmp = new Bitmap(Resources.GetBytes(Resources.BinaryResources.setan), Bitmap.BitmapImageType.Jpeg);
            img.Visible = true;
            img.Bitmap = bmp;

            img.Invalidate();
            Thread.Sleep(2000);
            img.Visible = false;
            img.Invalidate();
            window.Invalidate();
        }
        //convert hex to string
        string HexStringToString(string hexString)
        {
            if (hexString == null || (hexString.Length & 1) == 1)
            {
                throw new ArgumentException();
            }
            var sb = new StringBuilder();
            for (var i = 0; i < hexString.Length; i += 2)
            {
                var hexChar = hexString.Substring(i, 2);
                sb.Append((char)Convert.ToByte(hexChar));
            }
            return sb.ToString();
        }
        //convert hex to ascii
        private string HexString2Ascii(string hexString)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i <= hexString.Length - 2; i += 2)
            {
                int x = Int32.Parse(hexString.Substring(i, 2));
                sb.Append(new string(new char[] { (char)x }));
            }
            return sb.ToString();
        }
        //lora data received
        void _loraSerial_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            _dataInLora = _loraSerial.Deserialize();
            for (int index = 0; index < _dataInLora.Length; index++)
            {
                rx = _dataInLora[index];
                //if error
                if (_dataInLora[index].Length > 7)
                {
                    if (rx.Substring(0, 9) == "radio_err")
                    {
                        Debug.Print("!!!!!!!!!!!!! Radio Error !!!!!!!!!!!!!!");
                        _restPort.Write(false);
                        Thread.Sleep(1000);
                        _restPort.Write(true);
                        Thread.Sleep(1000);
                        _loraSerial.WriteLine("mac pause");
                        Thread.Sleep(1000);
                        _loraSerial.WriteLine("radio rx 0");
                        return;

                    }
                    //if receive data
                    if (rx.Substring(0, 8) == "radio_rx")
                    {
                        string hex = _dataInLora[index].Substring(10);
                        multicolorLED.BlinkRepeatedly(GT.Color.Blue);

                        //Debug.Print(hex);
                        //Debug.Print(Unpack(hex));
                        //update display
                        PrintToLCD(Unpack(hex));
                        Thread.Sleep(100);
                        // set module to RX
                        _loraSerial.WriteLine("radio rx 0"); 
                    }
                }
            }

        }
        //extract hex to string
        public static string Unpack(string input)
        {
            byte[] b = new byte[input.Length / 2];

            for (int i = 0; i < input.Length; i += 2)
            {
                b[i / 2] = (byte)((FromHex(input[i]) << 4) | FromHex(input[i + 1]));
            }
            return new string(Encoding.UTF8.GetChars(b));
        }
        public static int FromHex(char digit)
        {
            if ('0' <= digit && digit <= '9')
            {
                return (int)(digit - '0');
            }

            if ('a' <= digit && digit <= 'f')
                return (int)(digit - 'a' + 10);

            if ('A' <= digit && digit <= 'F')
                return (int)(digit - 'A' + 10);

            throw new ArgumentException("digit");
        }
        void PrintToLCD(string message)
        {
            //cek message
            if (message !=null && message.Length > 0)
            {
                try
                {
                    var msg="";
                    //parse json message
                    Hashtable hashtable = JsonSerializer.DeserializeString(message) as Hashtable;
                    foreach (DictionaryEntry item in hashtable)
                    {
                        switch (item.Key.ToString())
                        {
                            case "Tanggal":
                                txtStatus.Text = "Get data : " + DateTimeExtensions.FromIso8601(item.Value.ToString()).ToString("dd MMM yyyy HH:mm:ss");
                                break;
                            case "SPO2":
                                txtSPO2.Text = "SPO2 : " + item.Value;
                                if ((long)item.Value >= 95)
                                    msg += "alhamdulilah sehat bang! ";
                                else
                                    msg += "antum kurang tidur nih, kurang oksigen. ";
                                break;

                            case "SignalStrength":
                                txtSignal.Text = "Signal : " + item.Value;
                                break;

                            case "PulseRate":
                                txtPulseRate.Text = "Pulse Rate : " + item.Value;
                                if ((long)item.Value >= 60 && (long)item.Value <= 100)
                                    msg += "detak jantung normal. ";
                                else
                                    msg += "detak jantung abnormal. ";
                                break;

                        }
                        
                    }
                    //update display
                    txtDesc.Text = msg;
                    txtLora.Text = "Lora Status : OK";
                    window.Invalidate();
                    txtLora.Invalidate();
                    txtStatus.Invalidate();
                    txtSPO2.Invalidate();
                    txtSignal.Invalidate();
                    txtPulseRate.Invalidate();
                    txtDesc.Invalidate();
                }
                catch (Exception ex)
                {
                    txtPulseRate.Text=message+"_"+ex.Message+"_"+ex.StackTrace;
                    txtPulseRate.Invalidate();
                }
            }
 
        }
    }

    //driver for touch screen
    public class CapacitiveTouchController
    {
        private InterruptPort touchInterrupt;
        private I2CDevice i2cBus;
        private I2CDevice.I2CTransaction[] transactions;
        private byte[] addressBuffer;
        private byte[] resultBuffer;

        private static CapacitiveTouchController _this;

        public static void Initialize(Cpu.Pin PortId)
        {
            if (_this == null)
                _this = new CapacitiveTouchController(PortId);
        }

        private CapacitiveTouchController()
        {
        }

        private CapacitiveTouchController(Cpu.Pin portId)
        {
            transactions = new I2CDevice.I2CTransaction[2];
            resultBuffer = new byte[1];
            addressBuffer = new byte[1];
            i2cBus = new I2CDevice(new I2CDevice.Configuration(0x38, 400));
            touchInterrupt = new InterruptPort(portId, false, Port.ResistorMode.Disabled, Port.InterruptMode.InterruptEdgeBoth);
            touchInterrupt.OnInterrupt += (a, b, c) => this.OnTouchEvent();
        }

        private void OnTouchEvent()
        {
            for (var i = 0; i < 5; i++)
            {
                var first = this.ReadRegister((byte)(3 + i * 6));
                var x = ((first & 0x0F) << 8) + this.ReadRegister((byte)(4 + i * 6));
                var y = ((this.ReadRegister((byte)(5 + i * 6)) & 0x0F) << 8) + this.ReadRegister((byte)(6 + i * 6));

                if (x == 4095 && y == 4095)
                    break;

                if (((first & 0xC0) >> 6) == 1)
                    GlideTouch.RaiseTouchUpEvent(null, new GHI.Glide.TouchEventArgs(new Point(x, y)));
                else
                    GlideTouch.RaiseTouchDownEvent(null, new GHI.Glide.TouchEventArgs(new Point(x, y)));
            }
        }

        private byte ReadRegister(byte address)
        {
            this.addressBuffer[0] = address;

            this.transactions[0] = I2CDevice.CreateWriteTransaction(this.addressBuffer);
            this.transactions[1] = I2CDevice.CreateReadTransaction(this.resultBuffer);

            this.i2cBus.Execute(this.transactions, 1000);

            return this.resultBuffer[0];
        }
    }

}
