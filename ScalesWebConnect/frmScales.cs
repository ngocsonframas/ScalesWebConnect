using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace ScalesWebConnect
{
    public partial class frmScales : Form
    {
        private SerialPort _serialPort;         //<-- declares a SerialPort Variable to be used throughout the form
        private const int BaudRate = 9600;      //<-- BaudRate Constant. 9600 seems to be the scale-units default value

        public frmScales()
        {
            InitializeComponent();
        }

        private void frmScales_Load(object sender, EventArgs e)
        {
            string[] portNames = SerialPort.GetPortNames();     //<-- Reads all available comPorts
            foreach (var portName in portNames)
            {
                cbPortCom.Items.Add(portName);                  //<-- Adds Ports to combobox
            }
            cbPortCom.SelectedIndex = 0;                        //<-- Selects first entry (convenience purposes)

            //<-- This block ensures that no exceptions happen
            if (_serialPort != null && _serialPort.IsOpen)
                _serialPort.Close();
            if (_serialPort != null)
                _serialPort.Dispose();
            //<-- End of Block

            _serialPort = new SerialPort(cbPortCom.Text, BaudRate, Parity.None, 8, StopBits.One);       //<-- Creates new SerialPort using the name selected in the combobox
            _serialPort.DataReceived += SerialPortOnDataReceived;       //<-- this event happens everytime when new data is received by the ComPort
            _serialPort.Open();     //<-- make the comport listen
            txtWeight.Text = "Listening on " + _serialPort.PortName + "...\r\n";

        }

        private delegate void Closure();
        private void SerialPortOnDataReceived(object sender, SerialDataReceivedEventArgs serialDataReceivedEventArgs)
        {
            if (InvokeRequired)     //<-- Makes sure the function is invoked to work properly in the UI-Thread
                BeginInvoke(new Closure(() => { SerialPortOnDataReceived(sender, serialDataReceivedEventArgs); }));     //<-- Function invokes itself
            else
            {
                int dataLength = _serialPort.BytesToRead;

                byte[] data = new byte[dataLength];
                int nbrDataRead = _serialPort.Read(data, 0, dataLength);
                if (nbrDataRead == 0)
                    return;
                string str = Encoding.UTF8.GetString(data);

                //Buffers values in a file
                File.AppendAllText("buffer1", str);

                //Read from buffer and write into "strnew" String
                string strnew = File.ReadLines("buffer1").Last();

                //Shows actual true value coming from scale
                txtWeight.Text = strnew;
                Regex digits = new Regex(@"^\D*?((-?(\d+(\.\d+)?))|(-?\.\d+)).*");
                Match mx = digits.Match(txtWeight.Text);
                decimal strValue1 = mx.Success ? Convert.ToDecimal(mx.Groups[1].Value) : 0;
                txtWeight.Text = strValue1.ToString();

                //Connect and send data for web app 
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://localhost:57760/Mixing/Index");
                request.Method = "POST";
                byte[] byteArray = Encoding.UTF8.GetBytes(strValue1.ToString());
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = byteArray.Length;
                Stream dataStream = request.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();
                WebResponse response = request.GetResponse();
                dataStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                string responseFromServer = HttpUtility.UrlDecode(reader.ReadToEnd());
                //You may need HttpUtility.HtmlDecode depending on the response
                reader.Close();
                dataStream.Close();
                response.Close();
            }
        }
    }
}
