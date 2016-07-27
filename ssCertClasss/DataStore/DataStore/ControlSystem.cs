using System;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharp.CrestronDataStore;       // Method for datastore

namespace DataStore
{
    public class ControlSystem : CrestronControlSystem
    {
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

                // Console Commands
                CrestronConsole.AddNewConsoleCommand(SendToDataStore, "setField", "Stores String to DS",
                                                        ConsoleAccessLevelEnum.AccessAdministrator);
                CrestronConsole.AddNewConsoleCommand(RetrieveFromDataStore, "getField", "Gets String from DS",
                                                        ConsoleAccessLevelEnum.AccessAdministrator);
                CrestronConsole.AddNewConsoleCommand(SetupDataStore, "setDataStore", "Gets in Setup",
                                                        ConsoleAccessLevelEnum.AccessAdministrator);
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }
        }

        void SetupDataStore(string unused)
        {
            if (CrestronDataStoreStatic.InitCrestronDataStore() != CrestronDataStore.CDS_ERROR.CDS_SUCCESS)
            {
                CrestronConsole.PrintLine("Error InitCrestronDataStore: ");
            }
            else
            {
                CrestronDataStoreStatic.GlobalAccess = CrestronDataStore.CSDAFLAGS.OWNERREADWRITE & CrestronDataStore.CSDAFLAGS.OWNERREADWRITE;
            }
        }

        void RetrieveFromDataStore(string store)
        {
            string str;

            if (CrestronDataStoreStatic.GetGlobalStringValue("Local String", out str) != CrestronDataStore.CDS_ERROR.CDS_SUCCESS)
            {
                CrestronConsole.PrintLine("Error RetrieveFromDataStore: ");
            }
            else
            {
                CrestronConsole.PrintLine(str);
            }
        }

        void SendToDataStore(string store)
        {
            if (CrestronDataStoreStatic.SetGlobalStringValue("Local String", store) != CrestronDataStore.CDS_ERROR.CDS_SUCCESS)
            {
                CrestronConsole.PrintLine("Error SendToDataStore: ");
            }
        }

        public override void InitializeSystem()
        {
            try
            {

            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
        }
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
                    //The program has been stopped.
                    //Close all threads. 
                    //Shutdown all Client/Servers in the system.
                    //General cleanup.
                    //Unsubscribe to all System Monitor events
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
}