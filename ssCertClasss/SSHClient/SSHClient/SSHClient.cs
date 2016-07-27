using System;
using System.Text;
using Crestron.SimplSharp;      // For Basic SIMPL# Classes
using Crestron.SimplSharp.Ssh;
using System.Text.RegularExpressions;

namespace SSHClient
{
    public delegate void CommandEventHandler(string stringVal);

    public class SSHClientDevice
    {
        private SshClient myClient;
        private ShellStream myStream;
        // Event to send command response to SSP
        public event CommandEventHandler myEventToSsp = delegate { };  // gives default delegate so it cant be null

        /// <summary>
        /// SIMPL+ can only execute the default constructor. If you have variables that require initialization, please
        /// use an Initialize method
        /// </summary>
        public SSHClientDevice()
        {
            CrestronInvoke.BeginInvoke(ProcessResponses);
        }


        public void parseData(String str)
        {
            String matchString = @"(^.*$)*DHCP.*:\ ON.*$\n(^.*$\n)*^.*IP\ Address.*:\ (?<ipaddress>.*$).*\n(^.*$\n)*^.*DHCP\ Server.*:\ (?<DHCPServer>.*$).*\n(^.*$\n)*^.*Lease\ Expires On.*:\ (?<LeaseDate>.*$\n)";
            RegexOptions myRegexOptions = new RegexOptions();
            myRegexOptions = RegexOptions.IgnoreCase | RegexOptions.Multiline;
            Regex myRegex = new Regex(matchString, myRegexOptions);
            MatchCollection myCollection;
            try
            {
                myCollection = myRegex.Matches(str);
                foreach (Match m in myCollection)
                {
                    // Altered print to single parameter to work with event
                    myEventToSsp("\r\nIP Address:          " + m.Groups["ipaddress"].Value);
                    myEventToSsp("DHCP Server:         " + m.Groups["DHCPServer"].Value);
                    myEventToSsp("Lease Expiration:    " + m.Groups["LeaseDate"].Value);
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error("System Exception: {0}", e.Message);
            }
        }

        private CrestronQueue<String> myQueue = new CrestronQueue<String>();
       
        private void ProcessResponses(object o)
        {            
            String _str;

            while (true)
            {
                try
                {
                    _str = myQueue.Dequeue();
                    //Send command response to SSP
                    myEventToSsp(_str);
                    // Look for IP, DHCP Server, and Lease Experation
                    parseData(_str);
                }
                catch (Exception ex)
                {
                    ErrorLog.Error(String.Format("ProcessResponses Error: {0}", ex.Message));
                }
            }
        }

        // Event Handler for host key receivec.
         public ushort Connect(String Host, ushort Port, String UserName, String Password)
        {
            try
            {
                if (myClient != null && myClient.IsConnected)
                    return 0;
                myClient = new SshClient(Host, (int)Port, UserName, Password);
                myClient.Connect();
                
                // if host key override needed register eventhandler myClient.HostKeyReceived
                myClient.HostKeyReceived += new EventHandler<Crestron.SimplSharp.Ssh.Common.HostKeyEventArgs>(myClient_HostKeyReceived);

                // Create a new shellstream
                try
                {
                    myStream = myClient.CreateShellStream("terminal", 80, 24, 800, 600, 1024);
                    myStream.DataReceived += new EventHandler<Crestron.SimplSharp.Ssh.Common.ShellDataEventArgs>(myStream_DataReceived);
                }
                catch (Exception e)
                {
                    ErrorLog.Exception("Exception creating stream", e);
                }
                return 1;
            }
            catch (Exception ex)
            {
                ErrorLog.Error(String.Format("Error Connecting: {0}", ex.Message));
                return 0;
            }
        }
        
        // This ONLY needs rto be implemented oif you want to check the key tio see if you trust it
        // by default SSH sets cant.trsu to true so you dont need to implment this if you dont care
        void myClient_HostKeyReceived(object sender, Crestron.SimplSharp.Ssh.Common.HostKeyEventArgs e)
        {
            e.CanTrust = true;
            /*
             if ("Man in the middle")
             {
                 e.CanTrust = false;
             }
             */

        }
         void myStream_DataReceived(object sender, Crestron.SimplSharp.Ssh.Common.ShellDataEventArgs e)
         {
             var stream = (ShellStream)sender;
             // Loop as long as there is data on the stream
             while (stream.DataAvailable)
             {
                 // Read the stream and pass it to SSP
                 myEventToSsp(stream.Read());
             }
         }

        public ushort SendCommand(String strCommand)
        {
            try
            {
                SshCommand myCmd = myClient.RunCommand(strCommand);
                myQueue.Enqueue(myCmd.Execute());
                return 1;
            }
            catch (Exception ex)
            {
                ErrorLog.Error(String.Format("Error Sending Command: {0} -- {1}", strCommand, ex.Message));
                return 0;
            }
        }
    }
}