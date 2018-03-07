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
using Crestron.SimplSharpPro.Keypads;
using Crestron.SimplSharpPro.AudioDistribution;         // Swamp

namespace ssCertDay3
{
    // **********************************************************************
    // CSHelperClass - This class has helper methods to clean up main code
    // **********************************************************************
    static public class CSHelperClass
    {
        static public void DisplayCresnetDevices()
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

        static public void LoadIRDrivers(CrestronCollection<IROutputPort> myIRPorts)
        {

            // IROutputPorts[1].LoadIRDriver(String.Format(@"{0}\IR\AppleTV.ir", Directory.GetApplicationDirectory()));
            myIRPorts[1].LoadIRDriver(@"\NVRAM\AppleTV.ir");
        }

        static public void PrintIRDeviceFunctions(IROutputPort myIR)
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