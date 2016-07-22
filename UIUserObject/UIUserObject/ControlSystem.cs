// This is a program to demostrate the user of the UserObject
// on The signal coming from a touchpanel to assign delegate actions
// to that userobject.  Got almost all the code from Heath on crestron labs
using System;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharpPro.UI;                    	// For UI Devices. include the reference dll

namespace UIUserObject
{
    public class ControlSystem : CrestronControlSystem
    {
        // Define local class variables ...
        public XpanelForSmartGraphics myXpanel;
        UIActionClass myUIActionClass = new UIActionClass();

        /// Constructor 
        public ControlSystem()
            : base()
        {
            // Set the number of threads which you want to use in your program 
            Thread.MaxNumberOfUserThreads = 20;

            //Subscribe to the controller events (System, Program, and Etherent)
            CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControlSystem_ControllerSystemEventHandler);
            CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);
            CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(ControlSystem_ControllerEthernetEventHandler);

            // Check if device supports Ethernet
            if (this.SupportsEthernet)
            {
                myXpanel = new XpanelForSmartGraphics(0xA5, this);  // Register the Xpanel on IPID 0xA5

                // Register a single eventhandler for all UIs. 
                myXpanel.SigChange += new SigEventHandler(MySigChangeHandler);

                // Register the devices for usage, after eventhandler registration, to ensure no data is missed.
                if (myXpanel.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("MyXpanel failed registration. Cause: {0}", myXpanel.RegistrationFailureReason);
            }

            return;
        }

        /// Overridden function... Invoked before any traffic starts flowing between the devices and user program 
        /// This is used to start all the user threads and create all events / mutexes etc.
        /// This function should exit ... If this function does not exit then the program will not start
        public override void InitializeSystem()
        {
            myXpanel.BooleanInput[5].UserObject = new System.Action<bool>(b => myUIActionClass.VolumeUp(b));

            return;
        }

        /// This method is an eventhandler. 
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
        }//UI event handler

        /// This event is triggered whenever an Ethernet event happens. 
        void ControlSystem_ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            switch (ethernetEventArgs.EthernetEventType)
            {
                case (eEthernetEventType.LinkDown):
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {
                    }
                    break;
                case (eEthernetEventType.LinkUp):
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {
                    }
                    break;
            }
        }// ControlSystem_ControllerEthernetEventHandler

        /// <summary>
        /// This event is triggered whenever a program event happens (such as stop, pause, resume, etc.)
        /// </summary>
        /// <param name="programEventType">These event arguments hold all the data to properly parse the event</param>
        void ControlSystem_ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
        {
            switch (programStatusEventType)
            {
                case (eProgramStatusEventType.Paused):
                    break;
                case (eProgramStatusEventType.Resumed):
                    break;
                case (eProgramStatusEventType.Stopping):
                    break;
            }

        } //ControlSystem_ControllerProgramEventHandler

        /// <summary>
        /// This handler is triggered for system events
        /// </summary>
        /// <param name="systemEventType">The event argument needed to parse.</param>
        void ControlSystem_ControllerSystemEventHandler(eSystemEventType systemEventType)
        {
            switch (systemEventType)
            {
                case (eSystemEventType.DiskInserted):
                    break;
                case (eSystemEventType.DiskRemoved):
                    break;
                case (eSystemEventType.Rebooting):
                    break;
            }

        }//ControlSystem_ControllerSystemEventHandler

        // UIDeviceHandler - contains the methods for handling joins from UI
        public class UIActionClass
        {

            public void VolumeUp(bool b)
            {
                CrestronConsole.PrintLine("Volume Up Triggered");
            }

        }
    }//constructor
}//namespace
