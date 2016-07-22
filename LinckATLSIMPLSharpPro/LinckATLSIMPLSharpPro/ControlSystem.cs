//Please uncomment the #define line below if you want to include the sample code 
// in the compiled output.
// for the sample to work, you'll have to add a reference to the SimplSharpPro.UI dll to your project.
//#define IncludeSampleCode

using System;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       				// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    		// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support

#if IncludeSampleCode
using Crestron.SimplSharpPro.UI;                    			// For UI Devices. Please include the 
																// Crestron.SimplSharpPro.UI DLL as a reference to your project.
#endif

namespace LinckATLSIMPLSharpPro
{
    public class ControlSystem : CrestronControlSystem
    {
        // Define local variables ...
#if IncludeSampleCode
        public Tsw750 My750;
        public Tsw550 My550;
        public XpanelForSmartGraphics MyXpanel;
        public ComPort MyCOMPort;

        private CrestronQueue<String> RxQueue = new CrestronQueue<string>();
        private Thread RxHandler;
#endif

        /// <summary>
        /// Constructor of the Control System Class. Make sure the constructor always exists.
        /// If it doesn't exit, the code will not run on your 3-Series processor.
        /// </summary>
        public ControlSystem()
            : base()
        {

            // Set the number of threads which you want to use in your program - At this point the threads cannot be created but we should
            // define the max number of threads which we will use in the system.
            // the right number depends on your project; do not make this number unnecessarily large
            Thread.MaxNumberOfUserThreads = 20;

#if IncludeSampleCode
            //Subscribe to the controller events (System, Program, and Etherent)
            CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControlSystem_ControllerSystemEventHandler);
            CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);
            CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(ControlSystem_ControllerEthernetEventHandler);

            // Register all devices which the program wants to use
            // Check if device supports Ethernet
            if (this.SupportsEthernet)
            {
                My750 = new Tsw750(0x03, this);                     // Register the TSW750 on IPID 0x03
                My550 = new Tsw550(0x04, this);                     // Register the TSW550 on IPID 0x04
                MyXpanel = new XpanelForSmartGraphics(0x05, this);  // Register the Xpanel on IPID 0x05

                // Register a single eventhandler for all three UIs. This guarantees that they all operate 
                // the same way.
                My750.SigChange += new SigEventHandler(MySigChangeHandler);
                My550.SigChange += new SigEventHandler(MySigChangeHandler);
                MyXpanel.SigChange += new SigEventHandler(MySigChangeHandler);



                // Register the devices for usage. This should happen after the 
                // eventhandler registration, to ensure no data is missed.

                if (My750.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("My750 failed registration. Cause: {0}", My750.RegistrationFailureReason);
                if (My550.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("My550 failed registration. Cause: {0}", My550.RegistrationFailureReason);
                if (MyXpanel.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("MyXpanel failed registration. Cause: {0}", MyXpanel.RegistrationFailureReason);

            }

            if (this.SupportsComPort)
            {
                MyCOMPort = this.ComPorts[1];
                MyCOMPort.SerialDataReceived += new ComPortDataReceivedEvent(myComPort_SerialDataReceived);

                if (MyCOMPort.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("COM Port couldn't be registered. Cause: {0}", MyCOMPort.DeviceRegistrationFailureReason);
				
				if  (MyCOMPort.Registered)
					MyCOMPort.SetComPortSpec(ComPort.eComBaudRates.ComspecBaudRate38400,
																	 ComPort.eComDataBits.ComspecDataBits8,
																	 ComPort.eComParityType.ComspecParityNone,
																	 ComPort.eComStopBits.ComspecStopBits1,
                                         ComPort.eComProtocolType.ComspecProtocolRS232,
                                         ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeNone,
                                         ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeNone,
                                         false);
            }
#endif
        }

        /// <summary>
        /// Overridden function... Invoked before any traffic starts flowing back and forth between the devices and the 
        /// user program. 
        /// This is used to start all the user threads and create all events / mutexes etc.
        /// This function should exit ... If this function does not exit then the program will not start
        /// </summary>
        public override void InitializeSystem()
        {
            // This should always return   
#if IncludeSampleCode
            if (this.SupportsComPort && MyCOMPort.Registered)
                RxHandler = new Thread(RxMethod, null, Thread.eThreadStartOptions.Running);
#endif
        }

#if IncludeSampleCode

        /// <summary>
        /// This method will take messages of the Receive queue, and find the 
        /// delimiter. This is where you would put the parsing.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        object RxMethod(object obj)
        {
            StringBuilder RxData = new StringBuilder();
            int Pos = -1;

            String MatchString = String.Empty;
            // the Dequeue method will wait, making this an acceptable
            // while (true) implementation.
            while (true)
            {
                try
                {
                    // removes string from queue, blocks until an item is queued
                    string tmpString = RxQueue.Dequeue();

                    if (tmpString == null)
                        return null; // terminate the thread

                    RxData.Append(tmpString); //Append received data to the COM buffer
                    MatchString = RxData.ToString();

                    //find the delimiter
                    Pos = MatchString.IndexOf(Convert.ToChar("\n"));
                    if (Pos >= 0)
                    {
                        // delimiter found
                        // create temporary string with matched data.
                        MatchString = MatchString.Substring(0, Pos + 1);
                        RxData.Remove(0, Pos + 1); // remove data from COM buffer

                        // parse data here
                    }
                }
                catch (Exception ex)
                {
                    ErrorLog.Error("Exception in thread: {0}", ex.Message);
                }
            }
        }

        /// <summary>
        /// This method is an eventhandler. In this sample, it handles the signal events
        /// from the TSW750, TS550, and the XPanel.
        /// This event will not retrigger, until you exit the currently running eventhandler.
        /// Use threads, or dispatch to a worker, to exit this function quickly.
        /// </summary>
        /// <param name="currentDevice">This is the device that is calling this function. 
        /// Use it to identify, for example, which room thebuttom press is associated with.</param>
        /// <param name="args">This is the signal event argument, it contains all the data you need
        /// to properly parse the event.</param>
        void MySigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            switch (args.Sig.Type)
            {
                case eSigType.Bool:
                    {
                        if (args.Sig.BoolValue) // only process the press, not the release;
                        {
                            switch (args.Sig.Number)
                            {
                                case 10:
                                    {
                                        MyCOMPort.Send("!start\n");
                                        break;
                                    }
                                case 11:
                                    {
                                        MyCOMPort.Send("!stop\n");
                                        if (currentDevice == My550 || currentDevice == My750)
                                            ((BasicTriList)currentDevice).BooleanInput[50].BoolValue = true; // send digital value to touchscreen
                                        else
                                        {
                                            MyXpanel.BooleanInput[50].BoolValue = true;   // send digital value to xpanel
                                            MyXpanel.BooleanInput[120].BoolValue = false; // send digital value to xpanel
                                        }
                                        break;
                                    }
                            }
                        }
                        if (args.Sig.Type == eSigType.UShort)
                        {
                            switch (args.Sig.Number)
                            {
                                case (15):
                                    {
                                        MyCOMPort.Send(String.Format("!volume={0}\n", args.Sig.UShortValue));
                                        break;
                                    }
                            }
                        }
                        break;
                    }

            }
        }

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
        }

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

                    RxQueue.Enqueue(null); // The RxThread will terminate when it receives a null
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

        /// <summary>
        /// This event gets triggered whenever data comes in on the serial port. 
        /// This event will not retrigger, until you exit the currently running eventhandler.
        /// Use threads, or dispatch to a worker, to exit this function quickly.
        /// </summary>
        /// <param name="ReceivingComPort">This is a reference to the COM port sending the data</param>
        /// <param name="args">This holds all the data received.</param>
        void myComPort_SerialDataReceived(ComPort ReceivingComPort, ComPortSerialDataEventArgs args)
        {
            RxQueue.Enqueue(args.SerialData);
        }
#endif

    }
}
