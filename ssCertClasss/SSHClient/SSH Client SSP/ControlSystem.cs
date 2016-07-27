using System;
using System.Text;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       				// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    		// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using SSHClient;

namespace SSH_Client_SSP
{
    

    public class ControlSystem : CrestronControlSystem
    {
        // Define local variables ...
        public SSHClientDevice mySshClientDevice;
        public string sshHost = "127.0.0.1", sshUser = "admin", sshPass = "crestron";
        public ushort sshPort = 22;
        
        /// <summary>
        /// Constructor of the Control System Class. Make sure the constructor always exists.
        /// If it doesn't exit, the code will not run on your 3-Series processor.
        /// </summary>
        public ControlSystem()
            : base()
        {

            CrestronConsole.AddNewConsoleCommand(new SimplSharpProConsoleCmdFunction(ConnectSSH), "SSHConnect", "Connect to the SSH server", ConsoleAccessLevelEnum.AccessProgrammer);
            CrestronConsole.AddNewConsoleCommand(new SimplSharpProConsoleCmdFunction(SendSSHCommand), "SSHCommand", "Send a string as a command to the SSH server", ConsoleAccessLevelEnum.AccessProgrammer);

            mySshClientDevice = new SSHClientDevice();
            mySshClientDevice.myEventToSsp += new CommandEventHandler(mySshClientDevice_myEventToSsp);

            // Set the number of system and user threads which you want to use in your program .
            // User threads are created using the CrestronThread class
            // System threads are used for CTimers/CrestronInvoke/Async Socket operations
            // At this point the threads cannot be created but we should
            // define the max number of threads which we will use in the system.
            // the right number depends on your project; do not make this number unnecessarily large
            Thread.MaxNumberOfUserThreads = 10;

        }

        void mySshClientDevice_myEventToSsp(string strValue)
        {
            CrestronConsole.Print(strValue);
        }

        public void ConnectSSH(string unused)
        {
            if (mySshClientDevice.Connect(sshHost, sshPort, sshUser, sshPass) == 1)
                CrestronConsole.ConsoleCommandResponse("Connection Successful");
            else
                CrestronConsole.ConsoleCommandResponse("Connection Failed");
        }

        public void SendSSHCommand(string cmd)
        { 
            if (mySshClientDevice.SendCommand(cmd) != 1)
                CrestronConsole.ConsoleCommandResponse("Command Failed");

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
        }
    }
}
