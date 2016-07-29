using System;
using System.Collections.Generic;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronIO;                   // IO
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Ssh;
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharpPro.GeneralIO;
using Crestron.SimplSharpPro.Keypads;                   // Keypads
using Crestron.SimplSharpPro.UI;                        // Touchpanels
using Crestron.SimplSharp.Reflection;                   // Reflection

namespace ssCertMain
{
    public static class GV
    {
        public static ControlSystem MyControlSystem;
    }

    /***************************************************************
    // Main control system class
    *****************************************************************/
    public class ControlSystem : CrestronControlSystem
    {

        // Define local variables ...
        private C2nCbdP myKeypad;
        private XpanelForSmartGraphics myXpanel;
        private SigGroup mySigGroup;
        private ButtonInterfaceController actionBIC;
        private Assembly myAssembly;
        private CType myCType;
        private object myInstance;

        // Entry point
        public ControlSystem()
            : base()
        {
            CrestronConsole.PrintLine("ssCertMain started ...");

            GV.MyControlSystem = this;

            try
            {
                Thread.MaxNumberOfUserThreads = 20;

                //Subscribe to the controller events (System, Program, and Etherent)
                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControlSystem_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);
                CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(ControlSystem_ControllerEthernetEventHandler);

                // Injects a new console command for use in text console 
                // I am thinking this may be handy to help debug - e.g. fire events to test (like doorbell ringing )...
                CrestronConsole.AddNewConsoleCommand(UpperCase, "ToUpper", "Converts string to UPPER case", ConsoleAccessLevelEnum.AccessOperator);
            }
            catch (Exception e)
            {
                ErrorLog.Error("ControlSystem() - Error in constructor: {0}", e.Message);
            }

            #region Keypad
            // Register Keypad
            if (this.SupportsCresnet)
            {
                myKeypad = new C2nCbdP(0x24, this);
                // myKeypad = new C2nCbdP(0x25, this);

                myKeypad.ButtonStateChange += new ButtonEventHandler(myKeypad_ButtonStateChange);

                if (myKeypad.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("myKeypad failed registration. Cause: {0}", myKeypad.RegistrationFailureReason);
				
				// List all the cresnet devices - note: Query might not work for duplicate devices
				PllHelperClass.DisplayCresnetDevices();
            }
            #endregion

            #region Xpanel
            // Register Xpanel
            if (this.SupportsEthernet)
            {
                myXpanel = new XpanelForSmartGraphics(0x03, this);

                myXpanel.SigChange += new SigEventHandler(myXpanel_SigChange);

                if (myXpanel.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("myXpanel failed registration. Cause: {0}", myKeypad.RegistrationFailureReason);
                else
                {
                    myXpanel.LoadSmartObjects(@"\NVRAM\Xpnl.sgd");
                    CrestronConsole.PrintLine("sgd");
                    foreach (KeyValuePair<uint, SmartObject>mySmartObject in myXpanel.SmartObjects)
                    {
                        mySmartObject.Value.SigChange += new SmartObjectSigChangeEventHandler(Value_SigChange);
                    }

                    // Typically you would create the group in init and then add items throughout the program
                    mySigGroup = CreateSigGroup(1, myXpanel.StringInput[1]);
                    mySigGroup.Add(myXpanel.StringInput[2]);
                }
            }
            #endregion
        }

        // Initialize system
        public override void InitializeSystem()
        {
            myAssembly = Assembly.GetExecutingAssembly();  // This is just to put *something*/*anything* in so not null

            // This statement defines the userobject for this signal as a delegate to run the class method
            // So, when this particular signal is invoked the delegate function invokes the class method
            // I have demonstrated 3 different ways to assign the action with and without parms as well
            // as lambda notation vs simplified - need to test to see whagt does and does not work
            actionBIC = new ButtonInterfaceController();
            myKeypad.Button[1].UserObject = new System.Action<Button>(p => actionBIC.BReadFile(p));
            myKeypad.Button[2].UserObject = new System.Action<Button>(actionBIC.BGetHTTPFile);
            myKeypad.Button[3].UserObject = new System.Action(actionBIC.GetSFTPFile);

            return;
        }

        // Smart Object
        // Use reflection to see what's inside
        void Value_SigChange(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            mySigGroup.StringValue = String.Format("Event Type: {0}, Signal {1}, from SmartObject: {2}",
                                                    args.Sig.Type, args.Sig.Name, args.SmartObjectArgs.ID);
            if (args.SmartObjectArgs.ID == 2)
            {
                if (args.Sig.Name == "1" && args.Sig.BoolValue) //
                {
                    myAssembly = Assembly.LoadFrom(@"\NVRAM\ReflectionLib1.dll");
                    PrintContents();
                }
                if (args.Sig.Name == "2" && args.Sig.BoolValue)
                {
                    myAssembly = Assembly.LoadFrom(@"\NVRAM\ReflectionLib2.dll");
                    PrintContents();
                }
            }

            if (args.SmartObjectArgs.ID == 1)
            {
                if (args.Sig.Name.ToUpper() == "OK") //
                {
                    if (args.Sig.BoolValue)
                    {
                        if (myAssembly.FullName.ToUpper().Contains("REFLECTIONLIB2"))
                        {
                            myInstance = myAssembly.CreateInstance("ReflectionLib2.PrintToConsole");
                            myCType = myInstance.GetType();
                            MethodInfo method = myCType.GetMethod("PrintSomething");
                            method.Invoke(myInstance, new object[] { "Hello World From Reflection \n" });
                        }
                        if (myAssembly.FullName.ToUpper().Contains("REFLECTIONLIB1"))
                        {
                            myInstance = myAssembly.CreateInstance("ReflectionLib1.RelayClicks");
                            myCType = myInstance.GetType();
                            FieldInfo field = myCType.GetField("cs");
                            field.SetValue(myInstance, this);

                            MethodInfo method = myCType.GetMethod("Initialize");
                            method.Invoke(myInstance, new object[] { });

                            method = myCType.GetMethod("StartClicking");
                            method.Invoke(myInstance, new object[] { 500 });
                        }
                    }
                    else
                    {
                        if (myAssembly.FullName.ToUpper().Contains("REFLECTIONLIB1"))
                        {
                            MethodInfo method = myCType.GetMethod("StopClicking");
                            method.Invoke(myInstance, new object[] { });
                        }
                    }
                }
            }
        }

        void PrintContents()
        {
            foreach (CType type in myAssembly.GetTypes())
            {
                CrestronConsole.PrintLine("CType: {0} ", type.FullName);
                foreach (ConstructorInfo constructor in type.GetConstructors())
                {
                    CrestronConsole.Print("{0} (", constructor.Name);
                    foreach (ParameterInfo parameter in constructor.GetParameters())
                    {
                        CrestronConsole.Print("{0} {1}, ", parameter.ParameterType, parameter.Name);
                    }
                    CrestronConsole.PrintLine(")");
                }
                foreach (MethodInfo method in type.GetMethods())
                {
                    CrestronConsole.Print("{0} {1} (", method.ReturnType.Name, method.Name);
                    foreach (ParameterInfo parameter in method.GetParameters())
                    {
                        CrestronConsole.Print("{0} {1}, ", parameter.ParameterType, parameter.Name);
                    }
                    CrestronConsole.PrintLine(")");
                }
                foreach (PropertyInfo property in type.GetProperties())
                {
                    CrestronConsole.PrintLine("{0} {1} {2} {3}", property.Name,
                                            property.PropertyType.Name,
                                            property.CanRead,
                                            property.CanWrite);
                }
                foreach (FieldInfo field in type.GetFields())
                {
                    CrestronConsole.PrintLine("{0} {1};", field.Name,
                                            field.FieldType.Name);
                }
            }
        }

        void myXpanel_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            var sig = args.Sig;
            var uo = sig.UserObject;

            CrestronConsole.PrintLine("Event sig: {0}, Type: {1}", sig.Number, sig.GetType());

        }

        // This is the console command injected into shell - its like a deletage strongly typed in crestron
        public void UpperCase(string response)
        {
            CrestronConsole.ConsoleCommandResponse("ToUpper: {0} ", response.ToUpper());

        }

        // Keypad event handler
        void myKeypad_ButtonStateChange(GenericBase device, ButtonEventArgs args)
        {
            var btn = args.Button;
            var uo = btn.UserObject;

            CrestronConsole.PrintLine("Event sig: {0}, Type: {1}, State: {2}", btn.Number, btn.GetType(), btn.State);

            #region UserObject Action<> invocation
            // Need to fix this section so the action is not fired more than once on button press - for now it does.
            // for some reason
            if (btn.State == eButtonState.Pressed)
            {
                if (uo is System.Action<Button>) //if this userObject has been defined and is correct type
                    (uo as System.Action<Button>)(btn);
                else if (uo is System.Action)
                    (uo as System.Action)();
            }
            #endregion

            #region "Hardcoded* button invocation
            /*
            // Call direction until UserObject stuff working
            ButtonInterfaceController myBIC = new ButtonInterfaceController();
            if (sig.State == eButtonState.Pressed)
            {
                switch (sig.Number)
                {
                    case 1:
                        myBIC.ReadFile();
                        break;
                    case 2:
                        myBIC.GetHTTPFile();
                        break;
                    default:
                        myBIC.GetSFTPFile();
                        break;
                }
            }
            */
            #endregion
        } // Event Handler


        // Event Handler for UIs - touchpanels etc.
        void MySigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            var sig = args.Sig;
            var uo = sig.UserObject;

        }//Event Handler

        // Ethernet event handler
        void ControlSystem_ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            switch (ethernetEventArgs.EthernetEventType)
            {
                case (eEthernetEventType.LinkDown):
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

        // Program event handler
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

        // System event handler
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
    } // Class

    /***************************************************************
    // PllHelperClass static helper methods
    *****************************************************************/
    static class PllHelperClass
	{
		public static void DisplayCresnetDevices()
		{
			var returnVar = CrestronCresnetHelper.Query();
			if (returnVar == CrestronCresnetHelper.eCresnetDiscoveryReturnValues.Success)
			{
				foreach (var item in CrestronCresnetHelper.DiscoveredElementsList)
				{
					CrestronConsole.PrintLine("Found Item: {0}, {1}", item.CresnetId, item.DeviceModel);
				}
			}
		}
	}

    /*************************************************************************************
    // class DocumentSFTP - This is the SFTP class that contains the document retrived via SFTP
    // Currently it gets a file from url using SFTP, writes it to temp file and then
    // uses a delegate (Action<ulong>) called DownloadDone when its done
    **************************************************************************************/
    public class DocumentSFTP
    {
        private SftpClient mySFTPClient;
        private FileStream myFileStream;

        // get file from SFTP and return string
        public void getFromSFTP(string hostname, int port, string userName, string password, string tempFileName, string fileName)
        {
            try
            {
                mySFTPClient = new SftpClient(hostname, port, userName, password);
                myFileStream = new FileStream(tempFileName, FileMode.Create);

                mySFTPClient.Connect();
                mySFTPClient.DownloadFile(fileName, myFileStream, DownloadDone); // DownloadDone is Action<ulong>

                return;
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Document.getFromSFTP() Exception {0}", e);
                CrestronConsole.PrintLine("Document.getFromSFTP() Host {0}, Port {1}, User {2}, fileName {3}",
                                            hostname, port, userName, fileName);
                throw;
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
    } // class

    /*************************************************************************************
    // class PageHTTP - This class contains the results of an HTTP request.  It saves the
    // HTML page data in a string and returns
    **************************************************************************************/
    public class PageHTTP
    {
        private string htmlPageString;
        private HttpClient myHttpClient;

        public string getPageHTTP(string url, string localFile)
        {
            myHttpClient = new HttpClient();

            try
            {
                htmlPageString = myHttpClient.Get(url);
                return htmlPageString;
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Exception {in PageHTTP: {0}", e);
                throw;
            }
            finally
            {
            }

        }

    }

    /*************************************************************************************
    // class MyFileReader - This class contain the file contents read from control system
    **************************************************************************************/
    public class MyFileReader
    {
        private FileStream myFileStream;
        private StreamReader myStreamReader;
        private string myFileStringContents;

        public string getLocalFile(String strPath)
        {
            string sMethodName = "MyFileReader.getLocalFile";

            try
            {
                myFileStream = new FileStream(strPath, FileMode.Open);
                myStreamReader = new StreamReader(myFileStream);
                myFileStringContents = myStreamReader.ReadToEnd();
                return myFileStringContents;
            }
            catch (DirectoryNotFoundException e)
            {
                CrestronConsole.PrintLine("{0} - Directory not found {1}", sMethodName, e);
                throw;
            }
            catch (FileNotFoundException e)
            {
                CrestronConsole.PrintLine("{0} - File not found {1}", sMethodName, e);
                throw;
            }
            catch (PathTooLongException e)
            {
                CrestronConsole.PrintLine("{0} - Path too long {1}", sMethodName, e);
                CrestronConsole.PrintLine("Path too long {0}", e);
                throw;
            }
            catch (UnauthorizedAccessException e)
            {
                CrestronConsole.PrintLine("{0} - Unauthorized {1}", sMethodName, e);
                throw;
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Exception {0}", e);
                throw;
            }
            finally
            {
                myFileStream.Close();
                myStreamReader.Close();
            }
        }
    } // class

    /***************************************************************
    // ButtonInterfaceContoller - Handles Button functionality
    *****************************************************************/
    class ButtonInterfaceController
    {
        public void BReadFile(Button btn)
        {
            IROutputPort myIR = GV.MyControlSystem.IROutputPorts[1];

            ReadFile();
        }
        public void BGetHTTPFile(Button btn)
        {
            GetHTTPFile();
        }
        public void BGetSFTPFile(Button btn)
        {
            GetSFTPFile();
        }

        public void ReadFile()
        {
            // Read a file example
            CrestronConsole.PrintLine("File Read Example");
            MyFileReader myFileReader;
            string myFileContents;

            myFileReader = new MyFileReader();
            myFileContents = myFileReader.getLocalFile("\\NVRAM\\Books.xml");
            CrestronConsole.PrintLine(myFileContents);
        }

        public void GetHTTPFile()
        {
            // HTTP File Example
            CrestronConsole.PrintLine("HTTP Read Example");
            PageHTTP myHTTPFile;
            string htmlPageString;

            myHTTPFile = new PageHTTP();
            htmlPageString = myHTTPFile.getPageHTTP(@"http://textfiles.com/computers/1pt4mb.inf", @"\NVRAM\Books.xml");
            CrestronConsole.PrintLine(htmlPageString);
        }

        public void GetSFTPFile()
        {
            // SFTP File Example
            CrestronConsole.PrintLine("SFTP Read Example");
            DocumentSFTP myDocumentSFTP;

            myDocumentSFTP = new DocumentSFTP();
            myDocumentSFTP.getFromSFTP(@"127.0.0.1", 22, "Crestron", "", @"\NVRAM\temp.txt", @"/NVRAM/Books.xml");
        }

    } // class
}