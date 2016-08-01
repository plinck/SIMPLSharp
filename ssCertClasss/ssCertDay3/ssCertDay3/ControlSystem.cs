using System;
using System.Text;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronIO;                   // for directory()
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharpPro.Keypads;

namespace ssCertDay3
{
    public static class GV
    {
        public static ControlSystem MyControlSystem;
    }

    public class ControlSystem : CrestronControlSystem
    {
        // Define local variables ...
        private C2nCbdP myKeypad;
        private ButtonInterfaceController actionBIC;
        private IROutputPort myIRPort1;
        private CrestronQueue<string> rxQueue = new CrestronQueue<string>();
        private Thread rxHandler;   // thread for com port 

        public ControlSystem()
            : base()
        {
            try
            {
                GV.MyControlSystem = this;      // To Access ControlSystem (this) outside of ControlSystem Classs

                Thread.MaxNumberOfUserThreads = 20;

                //Subscribe to the controller events (System, Program, and Ethernet)
                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControlSystem_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);
                CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(ControlSystem_ControllerEthernetEventHandler);

                // Injects a new console command for use in text console 
                CrestronConsole.AddNewConsoleCommand(UpPress, "UpPress", "Presses the UP Button", ConsoleAccessLevelEnum.AccessOperator);
                CrestronConsole.AddNewConsoleCommand(UpRelease, "UpRelease", "Releases the UP Button", ConsoleAccessLevelEnum.AccessOperator);
                CrestronConsole.AddNewConsoleCommand(DnPress, "DnPress", "Presses the DN Button", ConsoleAccessLevelEnum.AccessOperator);
                CrestronConsole.AddNewConsoleCommand(DnRelease, "DnRelease", "Releases the DN Button", ConsoleAccessLevelEnum.AccessOperator);

                #region Keypad
                if (this.SupportsCresnet)
                {
                    myKeypad = new C2nCbdP(0x25, this);

                    myKeypad.ButtonStateChange += new ButtonEventHandler(myKeypad_ButtonStateChange);

                    // Set versi port handlers for the keypad buttons
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
                    }
                    else
                    {
                        myKeypad.ButtonStateChange -= new ButtonEventHandler(myKeypad_ButtonStateChange);
                        myKeypad.Button[1].Name = eButtonName.Up;
                        myKeypad.Button[2].Name = eButtonName.Down;

                    }

                    // List all the cresnet devices - note: Query might not work for duplicate devices
                    PllHelperClass.DisplayCresnetDevices();

                }
                #endregion

                #region IR
                if (this.SupportsIROut)
                {
                    if (ControllerIROutputSlot.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                        ErrorLog.Error("Error Registering IR Slot {0}", ControllerIROutputSlot.DeviceRegistrationFailureReason);
                    else
                    {
                        // IROutputPorts[1].LoadIRDriver(String.Format(@"{0}\IR\AppleTV.ir", Directory.GetApplicationDirectory()));
                        IROutputPorts[1].LoadIRDriver(@"\NVRAM\AppleTV.ir");
                        foreach (String s in IROutputPorts[1].AvailableStandardIRCmds())
                        {
                            CrestronConsole.PrintLine("AppleTV Std: {0}", s);
                        }
                        foreach (String s in IROutputPorts[1].AvailableIRCmds())
                        {
                            CrestronConsole.PrintLine("AppleTV Available: {0}", s);
                        }
                        myIRPort1 = IROutputPorts[1];
                    }
                }
                #endregion

                #region Versiports
                CrestronConsole.PrintLine("IR Check");
                if (this.SupportsVersiport)
                {
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
               // enquee someting for fun
            rxQueue.Enqueue("Test by putting something in Queue");

            try
            {
                rxHandler = new Thread(Gather, null, Thread.eThreadStartOptions.Running);
            }
            catch (InvalidOperationException e)
            {
                ErrorLog.Error("===>InvalidOperationException Creating Thread in InitializeSystem: {0}", e.Message);
            }
            catch (Exception e)
            {
                ErrorLog.Error("===>Exception Creating Thread in InitializeSystem: {0}", e.Message);
            }
            // This statement defines the userobject for this signal as a delegate to run the class method
            // So, when this particular signal is invoked the delegate function invokes the class method
            // I have demonstrated 3 different ways to assign the action with and without parms as well
            // as lambda notation vs simplified - need to test to see whagt does and does not work
            actionBIC = new ButtonInterfaceController();
            myKeypad.Button[1].UserObject = new System.Action<Button>((p) => actionBIC.BPressUp(p));
            myKeypad.Button[2].UserObject = new System.Action<Button>((p) => actionBIC.BPressDn(p));
        }

        // Data coming in from ComPort
        void ControlSystem_SerialDataReceived(ComPort ReceivingComPort, ComPortSerialDataEventArgs args)
        {
            if (ReceivingComPort == ComPorts[2])
            {
                rxQueue.Enqueue(args.SerialData);
            }
        }

        // The thread callback function.  Sit and wait for work.  release thread on program stopping
        // by placing a null string at the end of the queue
        object Gather(object o)
        {
            StringBuilder rxData = new StringBuilder();
            String rxGathered = String.Empty;
            string rxTemp = ""; // When I had the var definition for string inside the try it blew up

            int Pos = -1;
            while (true)
            {
                try
                {
                    CrestronConsole.PrintLine("Gathering -  Gettng off queue");
                    rxTemp = rxQueue.Dequeue();
                    CrestronConsole.PrintLine("Grabbed \"{0}\" off the Queue", rxTemp);

                    if (rxTemp == null)
                        return null;

                    rxData.Append(rxTemp);
                    rxGathered = rxData.ToString();
                    Pos = rxGathered.IndexOf("\n");
                    if (Pos >= 0)
                    {
                        rxGathered.Substring(0, Pos +1);
                        CrestronConsole.PrintLine("Gather: {0}", rxGathered);
                        rxData.Remove(0, Pos +1);
                    }
                }
                catch (System.ArgumentOutOfRangeException e)
                {
                    ErrorLog.Error("Error gathering - ArgumentOutOfRangeException: {0}", e);
                }
                catch (Exception e)
                {
                    ErrorLog.Error("Error gathering: {0}", e);
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
            // I have a big issue to contend with in these methods - what do you pass as arguments to the methods
            // I want to keep things decoupled but how do you get access to devices created, resgitered in control system
            // class when you in in classes that are outside the scope  e.g. Versiports, IRPorts etc.
            // Need to fix this section so the action is not fired more than once on button press - for now it does.
            // for some reason
            if (uo is System.Action<Button>)            //if this userObject has been defined and is correct type
                (uo as System.Action<Button>)(btn);
            else if (uo is System.Action)
                (uo as System.Action)();

            ButtonInterfaceController myBIC = new ButtonInterfaceController();
            #endregion

            #region "Hardcoded* button invocation
            /*
            // Call direction until UserObject stuff working
            if (btn.State == eButtonState.Pressed)
            {
                this.VersiPorts[args.Button.Number].DigitalOut = true;
                switch (btn.Number)
                {
                    case 1:
                        myIRPort1.Press("UP_ARROW");
                        ComPorts[1].Send("Test transmition, please ignore");
                        break;
                    case 2:
                        myIRPort1.Press("DN_ARROW");
                        ComPorts[1].Send("\n");
                        break;
                    default:
                        CrestronConsole.PrintLine("Key Not Programmed: {0}", args.Button.Number);
                        break;
                }
            }

            if (args.Button.State == eButtonState.Released)
            {
                myIRPort1.Release();
                this.VersiPorts[args.Button.Number].DigitalOut = false;
            }
            */
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
    
        #region Console Commands
        public void UpPress(string s)
        {
            ButtonInterfaceController bic = new ButtonInterfaceController();

            bic.PressUp_Pressed(1);
        }
        public void UpRelease(string s)
        {
            ButtonInterfaceController bic = new ButtonInterfaceController();

            bic.PressUp_Released(1);
        }
        public void DnPress(string s)
        {
            ButtonInterfaceController bic = new ButtonInterfaceController();

            bic.PressDn_Pressed(2);
        }
        public void DnRelease(string s)
        {
            ButtonInterfaceController bic = new ButtonInterfaceController();

            bic.PressDn_Released(2);
        }
        #endregion
    }


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
        public void BPressUp(Button btn)
        {
            if (btn.State == eButtonState.Pressed)
                PressUp_Pressed(btn.Number);
            else if (btn.State == eButtonState.Released)
                PressUp_Released(btn.Number);
       }

        public void PressUp_Pressed(uint i)
        {
            IROutputPort myIRPort1 = GV.MyControlSystem.IROutputPorts[1];
            Versiport myVersiport = GV.MyControlSystem.VersiPorts[i];
            ComPort myComPort = GV.MyControlSystem.ComPorts[i];

            myVersiport.DigitalOut = true;
            myIRPort1.Press("UP_ARROW");
            myComPort.Send("Test transmition, please ignore");
        }

        public void PressUp_Released(uint i)
        {
            IROutputPort myIRPort1 = GV.MyControlSystem.IROutputPorts[1];
            Versiport myVersiport = GV.MyControlSystem.VersiPorts[i];
            ComPort myComPort = GV.MyControlSystem.ComPorts[i];

            myVersiport.DigitalOut = false;
            myIRPort1.Release();
            myComPort.Send("\n");
        }

        public void BPressDn(Button btn)
        {
            if (btn.State == eButtonState.Pressed)
                PressDn_Pressed(btn.Number);
            else if (btn.State == eButtonState.Released)
                PressDn_Released(btn.Number);
        }

        public void PressDn_Pressed(uint i)
        {
            IROutputPort myIRPort1 = GV.MyControlSystem.IROutputPorts[1];
            Versiport myVersiport = GV.MyControlSystem.VersiPorts[i];
            ComPort myComPort = GV.MyControlSystem.ComPorts[i];

            myVersiport.DigitalOut = true;
            myIRPort1.Press("DN_ARROW");
            myComPort.Send("\n");
        }

        public void PressDn_Released(uint i)
        {
            IROutputPort myIRPort1 = GV.MyControlSystem.IROutputPorts[1];
            Versiport myVersiport = GV.MyControlSystem.VersiPorts[i];
            ComPort myComPort = GV.MyControlSystem.ComPorts[i];

            myVersiport.DigitalOut = false;
            myIRPort1.Release();
        }
    } // class

}