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
    // ***************************************************************
    // CustomConsoleCommands static class to add console commands
    // ******************************************************************
    public class CustomConsoleCommands
    {
        static public void AddCustomConsoleCommands()
        {
            CrestronConsole.AddNewConsoleCommand(UpPress, "UpPress", "Presses the UP Button", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(UpRelease, "UpRelease", "Releases the UP Button", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(DnPress, "DnPress", "Presses the DN Button", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(DnRelease, "DnRelease", "Releases the DN Button", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(PrintCN, "PrintCN", "Prints Cresnet Devices", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(PrintIRDeviceFunctions, "PrintIR", "Prints IR Device Functions", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(SwampPZ, "SwampPZ", "Prints Zones for Swamp", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(SwampCZS, "SwampCZS", "Changes source for Swamp Zone", ConsoleAccessLevelEnum.AccessOperator);
        }

        static public void UpPress(string s)
        {
            ButtonInterfaceController bic = new ButtonInterfaceController();

            bic.PressUp_Pressed(Convert.ToUInt32(s));
        }
        static public void UpRelease(string s)
        {
            ButtonInterfaceController bic = new ButtonInterfaceController();

            bic.PressUp_Released(Convert.ToUInt32(s));
        }
        static public void DnPress(string s)
        {
            ButtonInterfaceController bic = new ButtonInterfaceController();

            bic.PressDn_Pressed(Convert.ToUInt32(s));
        }
        static public void DnRelease(string s)
        {
            ButtonInterfaceController bic = new ButtonInterfaceController();

            bic.PressDn_Released(Convert.ToUInt32(s));
        }
        static public void PrintCN(string s)
        {
            // List all the cresnet devices - note: Query might not work for duplicate devices
            CSHelperClass.DisplayCresnetDevices();
        }
        static public void PrintIRDeviceFunctions(string s)
        {
            CSHelperClass.PrintIRDeviceFunctions(GV.MyControlSystem.IROutputPorts[1]);
        }

        static public void SwampPZ(string s)
        {
            GV.MyControlSystem.mySwampController.PrintAllZonesSources();
        }
        static public void SwampCZS(string parms)
        {
            ushort z = 0;
            ushort src = 0;
            string[] strParams = parms.Split(' ');

            z = Convert.ToUInt16(strParams[0]);
            src = Convert.ToUInt16(strParams[1]);

            GV.MyControlSystem.mySwampController.SetSourceForRoom(z, src);
        }
    }
}