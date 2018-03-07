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

namespace ssCertDay3
{
    public class ComPortController
    {
        private CrestronCollection<ComPort> myComPorts;
        private CrestronQueue<string> rxQueue = new CrestronQueue<string>();
        private Thread rxThreadComHandler;   // thread for com port 

        public ComPortController() { }

        public ComPortController(ControlSystem cs)
        {
            myComPorts = cs.ComPorts;

            for (uint i = 1; i <= 2; i++)
            {
                myComPorts[i].SerialDataReceived += new ComPortDataReceivedEvent(ControlSystem_SerialDataReceived);
                if (myComPorts[i].Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("Error registering comport {0}", myComPorts[i].DeviceRegistrationFailureReason);
                else
                {
                    myComPorts[i].SetComPortSpec(ComPort.eComBaudRates.ComspecBaudRate19200,
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

        public void Initialize()
        {
            try
            {
                rxThreadComHandler = new Thread(Gather, null, Thread.eThreadStartOptions.Running);
            }
            catch (InvalidOperationException e)
            {
                ErrorLog.Error("===>InvalidOperationException Creating Thread in InitializeSystem: {0}", e.Message);
            }
            catch (Exception e)
            {
                ErrorLog.Error("===>Exception Creating Thread in InitializeSystem: {0}", e.Message);
            }

        }

        void ControlSystem_SerialDataReceived(ComPort ReceivingComPort, ComPortSerialDataEventArgs args)
        {
            if (ReceivingComPort == myComPorts[2])
            {
                rxQueue.Enqueue(args.SerialData);       // Put all incoming data on the queue
            }
        }

        public void Cleanup()
        {
            rxQueue.Enqueue(null);  // put null at end of Q so the gather will complete and end thread

            if (rxThreadComHandler != null)
                rxThreadComHandler.Abort();
            rxThreadComHandler = null;
        }

        // Callback method for thread
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
                    rxTemp = rxQueue.Dequeue();

                    if (rxTemp == null)
                        return null;
                    else
                        CrestronConsole.PrintLine(rxTemp);

                    rxData.Append(rxTemp);
                    rxGathered = rxData.ToString();
                    Pos = rxGathered.IndexOf("\n");
                    if (Pos >= 0)
                    {
                        rxGathered.Substring(0, Pos + 1);
                        rxData.Remove(0, Pos + 1);
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

    }
}