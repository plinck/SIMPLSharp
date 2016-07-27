using System;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronIO;                   // For IR files etc
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Ssh;
using Crestron.SimplSharpPro;                  			// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharpPro.Keypads;                   // Keypads
using Crestron.SimplSharpPro.UI;                    	// For UI Devices. Please include the 

namespace LinckATLSIMPLSharpPro
{
    public class CustomSFTP
    {
        SftpClient mySFTPClient;
        Crestron.SimplSharp.CrestronIO.FileStream myFileStream;

        // get file from SFTP and return string
        public void getFromSFTP(string url)
        {
            try
            {
                myFileStream = new FileStream(@"\NVRAM\temp.txt", FileMode.Create);
                mySFTPClient = new SftpClient(url, 22, "Crestron", "");

                mySFTPClient.Connect();
                mySFTPClient.DownloadFile(url, myFileStream, DownloadDone);

                return;
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Exception {0}", e);
                return;
            }
            finally
            {
                mySFTPClient.Disconnect();
                myFileStream.Close();
            }
        }

        private void DownloadDone(ulong size)
        {
            CrestronConsole.PrintLine("Download file size: {0}", size);
        }
    }
    // Define Local Variables
    public class OpenHTTPFile
    {
        public string localFilePath = @"\NVRAM\Books.xml";
        public string httpHost = @"http://textfiles.com/computers/1pt4mb.inf";
        public string htmlPageString;

        HttpClient myHttpClient;
        
        public void getHTTPPage()
        {
            myHttpClient = new HttpClient();

            CrestronConsole.PrintLine("In getHTTPPage");

            try
            {
                htmlPageString = myHttpClient.Get(httpHost);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Exception {0}", e);
                throw;
            }

            CrestronConsole.PrintLine(htmlPageString);
        }

    }

    public class MyFileReader
    {
        private FileStream myFileStream;
        private StreamReader myStreamReader;

        public string myFileStringContents { get; set; }

        public ushort OpenLocalFile(String strPath)
        {
            ushort returnvalue;

            returnvalue = 0;
            try
            {
                myFileStream = new FileStream(strPath, FileMode.Open);
            }
            catch (DirectoryNotFoundException e)
            {
                CrestronConsole.PrintLine("Directory not found {0}", e);
                throw;
            }
            catch (PathTooLongException e)
            {
                CrestronConsole.PrintLine("Path too long {0}", e);
                throw;
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Exception {0}", e);
                throw;
            }
            myStreamReader = new StreamReader(myFileStream);
            myFileStringContents = myStreamReader.ReadToEnd();

            return returnvalue;
        }

    }

    public class ControlSystem : CrestronControlSystem
    {
        // Define local variables ...
        public XpanelForSmartGraphics myXpanel;
        public IROutputPort myIROutputDevice;
        tvFamilyRoom myTVFamilyRoom;
        C2nCbdP myKeypad;

        /// <summary>
        /// Constructor of the Control System Class. Make sure the constructor always exists.
        /// If it doesn't exit, the code will not run on your 3-Series processor.
        /// </summary>
        public ControlSystem()
            : base()
        {
            CrestronConsole.PrintLine("Hello World - Program SIMPL#Pro LinckATLSIMPLSharpPro started ...");

            // Set the number of threads which you want to use in your program - At this point the threads cannot be created but we should
            // define the max number of threads which we will use in the system.
            // the right number depends on your project; do not make this number unnecessarily large
            try
            {
                Thread.MaxNumberOfUserThreads = 20;

               //Subscribe to the controller events (System, Program, and Etherent)
                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControlSystem_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);
                CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(ControlSystem_ControllerEthernetEventHandler);
                CrestronConsole.AddNewConsoleCommand(UpperCase, "ToUpper", "Replies to strings un Upper case", ConsoleAccessLevelEnum.AccessOperator);
                var returnVar = CrestronCresnetHelper.Query();
                if (returnVar == CrestronCresnetHelper.eCresnetDiscoveryReturnValues.Success)
                {
                    foreach (var item in CrestronCresnetHelper.DiscoveredElementsList)
                    {
                        CrestronConsole.PrintLine("Found Item: {0}, {1}", item.CresnetId, item.DeviceModel);
                    }
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in constructor: {0}", e.Message);
            }

 
            // Reghister Keypad
            if (this.SupportsCresnet)
            {
                myKeypad = new C2nCbdP(0x25, this);

                myKeypad.ButtonStateChange += new ButtonEventHandler(myKeypad_ButtonStateChange);

                if (myKeypad.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("myKeypad failed registration. Cause: {0}", myKeypad.RegistrationFailureReason);
            }

            // Register all devices which the program wants to use
            // Check if device supports Ethernet
            if (this.SupportsEthernet)
            {
                myXpanel = new XpanelForSmartGraphics(0xA5, this);  // Register the Xpanel on IPID 0xA5

                // Register a single eventhandler for all three UIs. This guarantees that they all operate 
                // the same way.
                myXpanel.SigChange += new SigEventHandler(MySigChangeHandler);

                // Register the devices for usage. This should happen after the 
                // eventhandler registration, to ensure no data is missed.
                if (myXpanel.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("MyXpanel failed registration. Cause: {0}", myXpanel.RegistrationFailureReason);
            }
            // Load IR DriverC:\Users\paul\Documents\GitHubWin10\SIMPLSharp\LinckATLSIMPLSharpPro\LinckATLSIMPLSharpPro\Properties\ControlSystem.cfg
            myIROutputDevice = IROutputPorts[1];
            myIROutputDevice.LoadIRDriver(String.Format(@"{0}\IR\Samsung_LNS4051.ir", Directory.GetApplicationDirectory()));

            return;
        }

        public void UpperCase(string response)
        {
            CrestronConsole.ConsoleCommandResponse("ToUpper: {0} ", response.ToUpper());

        }
        // Keypad state handler
        void myKeypad_ButtonStateChange(GenericBase device, ButtonEventArgs args)
        {
            var sig = args.Button;
            var uo = sig.UserObject;
            
            CrestronConsole.PrintLine("Event sig: {0}, Type: {1}, Name: {2}", sig.Number, sig.GetType(), sig.Name);

            // Read a file example
            CrestronConsole.PrintLine("File Read Example");
            MyFileReader myFileReader = new MyFileReader();
            string myFileContents;
            ushort i;
            
            CrestronConsole.PrintLine("Reading File example");
            i = myFileReader.OpenLocalFile("\\NVRAM\\Books.xml");
            myFileContents = myFileReader.myFileStringContents;
            CrestronConsole.PrintLine(myFileContents);

            // HTTP File Example
            CrestronConsole.PrintLine("HTTP Read Example");
            OpenHTTPFile myHTTPFile;

            myHTTPFile = new OpenHTTPFile();
            myHTTPFile.getHTTPPage();

            // SFTP{ File Example
            CrestronConsole.PrintLine("SFTP Read Example");
            CustomSFTP myCustomSFTP;

            myCustomSFTP = new CustomSFTP();
            myCustomSFTP.getFromSFTP(@"SFTP://127.0.0.1/Books.xml");
        }

        /// <summary>
        /// Overridden function... Invoked before any traffic starts flowing back and forth between the devices and the 
        /// user program. 
        /// This is used to start all the user threads and create all events / mutexes etc.
        /// This function should exit ... If this function does not exit then the program will not start
        /// </summary>
        public override void InitializeSystem()
        {

            // This statement defines the userobject for this signal as a delegate to run the class method
            // So, when this particular signal is invoked the delatge function invokes the class method
            myTVFamilyRoom = new tvFamilyRoom();
            myXpanel.BooleanOutput[5].UserObject = new System.Action<bool>(b => myTVFamilyRoom.VolumeUp(b));
            // another way to represent
            myXpanel.BooleanOutput[6].UserObject = new System.Action<bool>(myTVFamilyRoom.VolumeUp);

            return;
        }

        /// <summary>
        /// This method is an eventhandler. In this sample, it handles the signal events
        /// from all touchpanels, and the XPanel.
        /// This event will not retrigger, until you exit the currently running eventhandler.
        /// Use threads, or dispatch to a worker, to exit this function quickly.
        /// </summary>
        /// <param name="currentDevice">This is the device that is calling this function. 
        /// Use it to identify, for example, which room thebuttom press is associated with.</param>
        /// <param name="args">This is the signal event argument, it contains all the data you need
        /// to properly parse the event.</param>
        void MySigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            var sig = args.Sig;
            var uo = sig.UserObject;

            CrestronConsole.PrintLine("Event sig: {0}, Type: {1}", sig.Number, sig.GetType());

            if (uo is Action<bool>)                             // If the userobject for this signal with boolean
            {
                (uo as System.Action<bool>)(sig.BoolValue);     // cast this signal's userobject as delegate Action<bool>
                                                                // passing one parm - the value of the bool
            }
            else if (uo is Action<ushort>)
            {
                (uo as Action<ushort>)(sig.UShortValue);
            }
            else if (uo is Action<string>)
            {
                (uo as Action<string>)(sig.StringValue);
            }

            /*
            switch (args.Sig.Type)
            {
                case eSigType.Bool:
                    {
                        if (args.Sig.BoolValue) // only process the press, not the release;
                        {
                            switch (args.Sig.Number)
                            {
                                case 5:
                                    // process boolean press 5
                                    break;
                            }
                        }
                        if (args.Sig.Type == eSigType.UShort)
                        {
                            switch (args.Sig.Number)
                            {
                                case 5:
                                    // process number 5
                                    break;
                            }
                        }
                        break;
                    }

            }
            */
        }//Event Handler

        /// <summary>
        /// This event is triggered whenever an Ethernet event happens. 
        /// </summary>
        /// <param name="ethernetEventArgs">Holds all the data needed to properly parse</param>
        void ControlSystem_ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            switch (ethernetEventArgs.EthernetEventType)
            {//Determine the event type Link Up or Link Down
                case (eEthernetEventType.LinkDown):
                    //Next need to determine which adapter the event is for. 
                    //LAN is the adapter is the port connected to external networks.
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {
                        //
                    }
                    break;
                case (eEthernetEventType.LinkUp):
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {

                    }
                    break;
            }
        }// Event handler
  

        /// <summary>
        /// This event is triggered whenever a program event happens (such as stop, pause, resume, etc.)
        /// </summary>
        /// <param name="programEventType">These event arguments hold all the data to properly parse the event</param>
        void ControlSystem_ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
        {
            switch (programStatusEventType)
            {
                case (eProgramStatusEventType.Paused):
                    //The program has been paused.  Pause all user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Resumed):
                    //The program has been resumed. Resume all the user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Stopping):
                    //The program has been stopped.
                    //Close all threads. 
                    //Shutdown all Client/Servers in the system.
                    //General cleanup.
                    //Unsubscribe to all System Monitor events
                    break;
            }

        }

        /// <summary>
        /// This handler is triggered for system events
        /// </summary>
        /// <param name="systemEventType">The event argument needed to parse.</param>
        void ControlSystem_ControllerSystemEventHandler(eSystemEventType systemEventType)
        {
            switch (systemEventType)
            {
                case (eSystemEventType.DiskInserted):
                    //Removable media was detected on the system
                    break;
                case (eSystemEventType.DiskRemoved):
                    //Removable media was detached from the system
                    break;
                case (eSystemEventType.Rebooting):
                    //The system is rebooting. 
                    //Very limited time to preform clean up and save any settings to disk.
                    break;
            }

        }

    }

    // Class for IR
    public class tvFamilyRoom
    {

        public void VolumeUp(bool b)
        {
            // test git change
            // Need to figure out how to deal with IR Devices - the correct way - seems like static class>?
            // myIROutputDevice.PressAndRelease("Volup", 10);
            CrestronConsole.PrintLine("Volume Up Triggered");
        }

    }
}
