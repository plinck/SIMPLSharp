using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;                   // for directory()
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support


namespace LinckATLMain
{
    public class IRPortController
    {
        CrestronCollection<IROutputPort> myIRPorts;

        public IRPortController()
        { }

        public IRPortController(ControlSystem cs)
        {
            myIRPorts = cs.IROutputPorts;

            if (cs.ControllerIROutputSlot.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                ErrorLog.Error("Error Registering IR Slot {0}", cs.ControllerIROutputSlot.DeviceRegistrationFailureReason);
            else
            {
                LoadIRDrivers();
            }
        }

        public void LoadIRDrivers()
        {

            // IROutputPorts[1].LoadIRDriver(String.Format(@"{0}\IR\AppleTV.ir", Directory.GetApplicationDirectory()));
            myIRPorts[1].LoadIRDriver(@"\NVRAM\AppleTV.ir");
        }

        public void PrintIRDeviceFunctions(IROutputPort myIR)
        {
            foreach (String s in myIR.AvailableStandardIRCmds())
            {
                CrestronConsole.PrintLine("AppleTV Std: {0}", s);
            }
            foreach (String s in myIR.AvailableIRCmds())
            {
                CrestronConsole.PrintLine("AppleTV Available: {0}", s);
            }
        }
    }
}