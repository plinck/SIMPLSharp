using System;
using System.Text;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharpPro.Keypads;
using Crestron.SimplSharp.CrestronIO;                  // for directory()
using Crestron.SimplSharp.Reflection;

namespace ssCertDay3
{
    public class ControlSystem : CrestronControlSystem
    {
        // Define local variables ...
        private C2nCbdP myKeypad;
        private ButtonInterfaceController actionBIC;
        private IROutputPort myIRPort;
        private CrestronQueue<string> rxQueue = new CrestronQueue<string>();
        private Thread rxHandler;   // thread for com port 

        public ControlSystem()
            : base()
        {
            try
            {
                Thread.MaxNumberOfUserThreads = 20;

                //Subscribe to the controller events (System, Program, and Ethernet)
                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControlSystem_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);
                CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(ControlSystem_ControllerEthernetEventHandler);

                #region Keypad
                if (this.SupportsCresnet)
                {
                    myKeypad = new C2nCbdP(0x25, this);

                    myKeypad.ButtonStateChange += new ButtonEventHandler(myKeypad_ButtonStateChange);
                    if (myKeypad.NumberOfVersiPorts > 0)
                    {
                        for (uint i = 1; i <= myKeypad.NumberOfVersiPorts; i++)
                        {
                            myKeypad.VersiPorts[i].SetVersiportConfiguration(eVersiportConfiguration.DigitalInput);
                            myKeypad.VersiPorts[i].VersiportChange += new VersiportEventHandler(ControlSystem_VersiportChange);
                        }
                    }

                    if (myKeypad.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    {
                        ErrorLog.Error("myKeypad failed registration. Cause: {0}", myKeypad.RegistrationFailureReason);
                        myKeypad.ButtonStateChange -= new ButtonEventHandler(myKeypad_ButtonStateChange);
                    }
                    else
                    {
                        myKeypad.Button[1].Name = eButtonName.Up;
                        myKeypad.Button[2].Name = eButtonName.Down;

                    }

                    // List all the cresnet devices - note: Query might not work for duplicate devices
                    PllHelperClass.DisplayCresnetDevices();

                }
                #endregion

                #region IR
                CrestronConsole.PrintLine("IR Check");
                if (this.SupportsIROut)
                {
                    CrestronConsole.PrintLine("if this.SupportsIROut");
                    if (ControllerIROutputSlot.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                        ErrorLog.Error("Error Registering IR Slot {0}", ControllerIROutputSlot.DeviceRegistrationFailureReason);
                    else
                    {
                        myIRPort = IROutputPorts[1];
                        // myIRPort.LoadIRDriver(String.Format(@"{0}\IR\AppleTV.ir", Directory.GetApplicationDirectory()));
                        myIRPort.LoadIRDriver(@"\NVRAM\AppleTV.ir");
                        foreach (String s in myIRPort.AvailableStandardIRCmds())
                        {
                            CrestronConsole.PrintLine("AppleTV Std: {0}", s);
                        }
                        foreach (String s in myIRPort.AvailableIRCmds())
                        {
                            CrestronConsole.PrintLine("AppleTV Available: {0}", s);
                        }
                    }
                }
                #endregion

                #region Versiports
                CrestronConsole.PrintLine("IR Check");
                if (this.SupportsVersiport)
                {
                    CrestronConsole.PrintLine("if this.SupportsVersiport");
                    for (uint i = 1; i <= 2; i++)
                    {
                        if (this.VersiPorts[i].Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                        {
                            ErrorLog.Error("Error Registering Versiport {0}", this.VersiPorts[i].DeviceRegistrationFailureReason);
                        }
                        else
                            this.VersiPorts[i].SetVersiportConfiguration(eVersiportConfiguration.DigitalOutput);
                    }
               }
                #endregion

                #region ComPorts
                if (this.SupportsComPort)
                {
                    for (uint i = 1; i <= 2; i++)
                    {
                        this.ComPorts[i].SerialDataReceived += new ComPortDataReceivedEvent(ControlSystem_SerialDataReceived);
                        if (this.ComPorts[i].Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                            ErrorLog.Error("Error registering omport {0}", this.ComPorts[i].DeviceRegistrationFailureReason);
                        else
                        {
                            this.ComPorts[i].SetComPortSpec(ComPort.eComBaudRates.ComspecBaudRate19200,
                                                            ComPort.eComDataBits.ComspecDataBits8,
                                                            ComPort.eComParityType.ComspecParityNone,
                                                            ComPort.eComStopBits.ComspecStopBits1,
                                                            ComPort.eComProtocolType.ComspecProtocolRS232,
                                                            ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeNone,
                                                            ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeNone,
                                                            false);
                        }
                    }
                }
                #endregion
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }
        }

        public override void InitializeSystem()
        {
            object myObj = null;

            try
            {
                rxHandler = new Thread(Gather, myObj, Thread.eThreadStartOptions.Running);
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
            // This statement defines the userobject for this signal as a delegate to run the class method
            // So, when this particular signal is invoked the delegate function invokes the class method
            // I have demonstrated 3 different ways to assign the action with and without parms as well
            // as lambda notation vs simplified - need to test to see whagt does and does not work
            actionBIC = new ButtonInterfaceController();
            myKeypad.Button[1].UserObject = new System.Action<Button, IROutputPort>((p, i) => actionBIC.BPressUp(p, i));
            myKeypad.Button[2].UserObject = new System.Action<Button, IROutputPort>((p, i) => actionBIC.BPressDn(p, i));
        }

        // Data coming in from ComPort
        void ControlSystem_SerialDataReceived(ComPort ReceivingComPort, ComPortSerialDataEventArgs args)
        {
            if (ReceivingComPort == ComPorts[2])
            {
                rxQueue.Enqueue(args.SerialData);
            }
        }

        object Gather(object o)
        {
            StringBuilder rxData = new StringBuilder();
            String rxGathered = String.Empty;

            int Pos = -1;
            while (true)
            {
                try
                {
                    string rxTemp = rxQueue.Dequeue();
                    if (rxTemp == null)
                        return null;
                    CrestronConsole.PrintLine("Gathering");
                    rxData.Append(rxTemp);
                    rxGathered = rxData.ToString();
                    Pos = rxGathered.IndexOf("\n");
                    if (Pos >= 0)
                    {
                        rxGathered.Substring(0, Pos + 1);
                        CrestronConsole.PrintLine("Gather: {0}", rxGathered);
                        rxData.Remove(0, Pos + 1);
                    }
                }
                catch (Exception e)
                {
                    ErrorLog.Error("Error gathering: {0}", e);
                    throw;
                }
            }
        }

        void ControlSystem_VersiportChange(Versiport port, VersiportEventArgs args)
        {
            if (port == myKeypad.VersiPorts[1])
                CrestronConsole.PrintLine("Port 1: {0}", port.DigitalIn);
            if (port == myKeypad.VersiPorts[2])
                CrestronConsole.PrintLine("Port 2: {0}", port.DigitalIn);

        }


        // Keypad event handling
        void myKeypad_ButtonStateChange(GenericBase device, ButtonEventArgs args)
        {
            var btn = args.Button;
            var uo = btn.UserObject;

            // CrestronConsole.PrintLine("Event sig: {0}, Type: {1}, State: {2}", btn.Number, btn.GetType(), btn.State);

            #region UserObject Action<> invocation
            /*
            // I have a big issue to contend with in these methods - what do you pass as arguments to the methods
            // I want to keep things decoupled but how do you get access to devices created, resgitered in control system
            // class when you in in classes that are outside the scope  e.g. Versiports, IRPorts etc.
            // Need to fix this section so the action is not fired more than once on button press - for now it does.
            // for some reason
            if (btn.State == eButtonState.Pressed)
            {
                if (uo is System.Action<Button, IROutputPort>) //if this userObject has been defined and is correct type
                    (uo as System.Action<Button, IROutputPort>)(btn, myIRPort);
                else if (uo is System.Action<Button, Versiport>) //if this userObject has been defined and is correct type
                    (uo as System.Action<Button, Versiport>)(btn, this.VersiPorts[btn.Number]);
                else if (uo is System.Action)
                    (uo as System.Action)();
            }
            */
            ButtonInterfaceController myBIC = new ButtonInterfaceController();
            #endregion

            #region "Hardcoded* button invocation
            // Call direction until UserObject stuff working
            if (btn.State == eButtonState.Pressed)
            {
                this.VersiPorts[args.Button.Number].DigitalOut = true;
                switch (btn.Number)
                {
                    case 1:
                        myIRPort.Press("UP_ARROW");
                        ComPorts[1].Send("Test transmition, please ignore");
                        break;
                    case 2:
                        myIRPort.Press("DN_ARROW");
                        ComPorts[1].Send("\n");
                        break;
                    default:
                        CrestronConsole.PrintLine("Key Not Programmed: {0}", args.Button.Number);
                        break;
                }
            }

            if (args.Button.State == eButtonState.Released)
            {
                myIRPort.Release();
                this.VersiPorts[args.Button.Number].DigitalOut = false;
            }
            #endregion
        } // Event Handler

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
                    rxQueue.Enqueue(null);  // so the gather will complete
                    break;
            }

        }
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
            // Keypad event handler

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
    /***************************************************************
    // ButtonInterfaceContoller - Handles Button functionality
    *****************************************************************/
    class ButtonInterfaceController
    {
        public void BPressUp(Button btn, IROutputPort myIR)
        {

            if (btn.State == eButtonState.Pressed)
            {
                PressUp(myIR);
            }
        }

        public void BPressDn(Button btn, IROutputPort myIR)
        {
            if (btn.State == eButtonState.Pressed)
            {
                PressDn(myIR);
            }
        }
        
        public void PressUp(IROutputPort myIR)
        {

            myIR.PressAndRelease("UP_ARROW", 10);
        }

 
        public void PressDn(IROutputPort myIR)
        {

            myIR.PressAndRelease("DN_ARROW", 10);
            
        }
 
    } // class

}