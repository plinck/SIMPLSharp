
using System;
using System.Text;
using Crestron.SimplSharp;                
using Crestron.SimplSharpPro;                       	
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro.Diagnostics;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.DM.Endpoints;
using Crestron.SimplSharpPro.DM.Endpoints.Receivers;
using Crestron.SimplSharpPro.DM.Endpoints.Transmitters;
using Crestron.SimplSharpPro.Keypads;
using Crestron.SimplSharp.CrestronXml.Serialization;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharpPro.UI;
using System.Collections.Generic;
using Crestron.SimplSharpPro.DM;


/** Project Summary ***********************************************************
 *		This project is for use in a small huddle room setup
 *		where there will be a single DM-TX-401-C, a single
 *		DM-RMC-200-C (or DM-RMC-SCALER-C), a single TT-100,
 *		and a single video monitor.
 *		
 *		When a user walks into a room, they are greeted by
 *		blue leds on the TT-100, which means the system is off.
 *		They can turn on the system by either plugging one of
 *		the three cables (hdmi, vga, displayport), or by pressing
 *		either of the TT-100 buttons. If a cable is plugged in,
 *		the TX switches to that input and turns on the display.
 *		The leds will be solid green now.
 *		
 *		If a button was pressed, it will check to see if there are
 *		any signals detected and if so, switch to that and turn the
 *		leds solid green. If no signal is detected, the leds will
 *		flash green for 60 seconds before turning off the system,
 *		and turning the leds solid blue. If a cable is connected
 *		while the leds flash green, it will switch to that input
 *		and turn the leds solid green.
 *		
 *		While a cable is connected and the system is on, plugging
 *		any other cable will not intrupt the active one.
 *		
 *		Pressing one of the buttons while the system is on, and only
 *		one cable connected will not do anything. If there is more
 *		than one cable connected, it will cycle through them.
 *		
 *		Most attributes of the system can be tracked via Fusion RV.
 *
 * 
 * COLOR CODE REFERENCE:
 *		Solid Blue - System is off
 *		Solid Green - System is on and has signal
 *		Blinking Green - System is on, but no inputs have signal
 *		Blinking Red - Not used
 *			
 * 
 * IPID Info:
 *		IPID 03 - DM-TX-401-C
 *		IPID 04 - DM-RMC-SCALER-C
 *		IPID 05 - Fusion RV
 *		IPID 0B - Web Xpanel
 * 
 * NOTES:
 *		Connect to the processor via Toolbox command line to get info
 *		about the current state of the system. Type "getinfo:xx ?" to see
 *		a list of available commands (where xx is the prog slot number).
 *		Example: getinfo:1 state
 *****************************************************************************/



namespace DM_TX_RX_TT100_Example
{
	public class ControlSystem : CrestronControlSystem
	{
		#region local objects

		// Define local variables ...
		public DmTx401C dmTx;
		public DmRmcScalerC dmRmc;
		public Tt1xx connectIt;
	    public XpanelForSmartGraphics xPanelUi;

		private SmartObject dmRmcOutputResList;
		private SmartObject dmRmcAspectModeList;
		private SmartObject dmRmcUnderscanList;

		#endregion




		#region local variables and constants

		// time out before the system will shut down due to no video input
		private const long DEFAULT_SHUTDOWN_TIMEOUT = 60000;
		private const long MIN_SHUTDOWN_TIMEOUT = 10000;
		private const long MAX_SHUTDOWN_TIMEOUT = 120000;

		// file name and pathway for the shutdown timeout value, to be stored as xml
		string shutdownTimerFileName;

		// variable timeout for the shutdown timer
		private long _shutdownTimeout;

		// static strings for fusion
		private string ProjectVersion = "v1.0.1";
		private string ProjectDate = "3/12/15";
		private string RoomName = "DM Room";
        private string TransmitterName = "<P align=\"left\"><FONT size=\"24\" face=\"Crestron Sans Pro\" color=\"#000000\"><B>DM-TX-401-C</B> Video Transmitter</FONT></P>";
        private string ReceiverName = "<P align=\"left\"><FONT size=\"24\" face=\"Crestron Sans Pro\" color=\"#000000\"><B>DM-RMC-SCALER-C</B> Video Receiver</FONT></P>";
        private string TT100Name = "<P align=\"left\"><FONT size=\"24\" face=\"Crestron Sans Pro\" color=\"#000000\"><B>TT-100</B> Connect-IT Interface</FONT></P>";
		/// <summary>
		/// Enumeration of the xpanel smart object ids in the project.
		/// </summary>
		private enum eSmartObjectIds
		{
			DmRmcOutputResList = 1,
			DmRmcAspectList,
			DmRmcUnderscanList
		}

		#endregion




		#region required for start up

		/// <summary>
		/// Constructor of the Control System Class. Make sure the constructor always exists.
		/// If it doesn't exit, the code will not run on your 3-Series processor.
		/// </summary>
		public ControlSystem() : base()
		{
			// subscribe to control system events
			CrestronEnvironment.SystemEventHandler += new SystemEventHandler(CrestronEnvironment_SystemEventHandler);
			CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(CrestronEnvironment_ProgramStatusEventHandler);
			CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(CrestronEnvironment_EthernetEventHandler);
			
			// Set the number of threads which you want to use in your program - At this point the threads cannot be created but we should
			// define the max number of threads which we will use in the system.
			// the right number depends on your project; do not make this number unnecessarily large
			Thread.MaxNumberOfUserThreads = 20;

			// ensure this processor has ethernet
			if (this.SupportsEthernet)
			{
				// create new dmtx401c object and subscribe to its events
				dmTx = new DmTx401C(0x03, this);	// IPID for the dmtx is 3
				dmTx.BaseEvent += dmTx_BaseEvent;
				dmTx.HdmiInput.InputStreamChange += HdmiInput_InputStreamChange;
				dmTx.HdmiInput.VideoAttributes.AttributeChange += Hdmi_AttributeChange;
				dmTx.VgaInput.InputStreamChange += VgaInput_InputStreamChange;
				dmTx.VgaInput.VideoAttributes.AttributeChange += Vga_AttributeChange;
				dmTx.DisplayPortInput.InputStreamChange += DisplayPortInput_InputStreamChange;
				dmTx.DisplayPortInput.VideoAttributes.AttributeChange += DisplayPort_AttributeChange;
				dmTx.OnlineStatusChange += Device_OnlineStatusChange;

				// create new tt100 object using the dmtx401 constructor, and subscribe to its events
				connectIt = new Tt1xx(dmTx);
				connectIt.ButtonStateChange += connectIt_ButtonStateChange;
				connectIt.OnlineStatusChange += Device_OnlineStatusChange;

				// register the dmtx to this program, the tt100 will be registered as part of the dmtx
				if (dmTx.Register() == eDeviceRegistrationUnRegistrationResponse.Success)
					ErrorLog.Notice(">>> The DM-TX-401-c has been registered successfully");
				else
					ErrorLog.Error(">>> The DM-TX-401-C was not registered: {0}", dmTx.RegistrationFailureReason);



				// create new dmrmc100c object and subscribe to its events
				dmRmc = new DmRmcScalerC(0x04, this);	// IPID for the dmtx is 4
				dmRmc.DmInput.InputStreamChange += DmRmc_InputStreamChange;
				dmRmc.ComPorts[1].SerialDataReceived += DmRmc_SerialDataReceived;
				dmRmc.OnlineStatusChange += Device_OnlineStatusChange;
				dmRmc.Scaler.OutputChange += Scaler_OutputChange;

				// register device with the control system
				if (dmRmc.Register() == eDeviceRegistrationUnRegistrationResponse.Success)
					ErrorLog.Notice(">>> The DM-RMC-Scaler-C has been registered successfully");
				else
					ErrorLog.Error(">>> The DM-RMC-Scaler-C was not registered: {0}", dmRmc.RegistrationFailureReason);



				// create a new xpanel room object and subscribe to its events
				xPanelUi = new XpanelForSmartGraphics(0x0b, this);
				xPanelUi.SigChange += xPanelUi_SigChange;
				xPanelUi.OnlineStatusChange += Device_OnlineStatusChange;

				// pathway to the SGD file for this ui project
				string xPanelSgdFilePath = string.Format("{0}\\Config.Standalone.sgd", Directory.GetApplicationDirectory());

				// make sure file exists in the application directory
				if (File.Exists(xPanelSgdFilePath))
				{
					// load the SGD file for this ui project
					xPanelUi.LoadSmartObjects(xPanelSgdFilePath);

					// create reference for the various smart objects in the ui project
					dmRmcOutputResList = xPanelUi.SmartObjects[(uint)eSmartObjectIds.DmRmcOutputResList];
					dmRmcAspectModeList = xPanelUi.SmartObjects[(uint)eSmartObjectIds.DmRmcAspectList];
					dmRmcUnderscanList = xPanelUi.SmartObjects[(uint)eSmartObjectIds.DmRmcUnderscanList];

					// subscribe to the smart object sig events
					dmRmcOutputResList.SigChange += dmRmcOutputResList_SigChange;
					dmRmcAspectModeList.SigChange += dmRmcAspectModeList_SigChange;
					dmRmcUnderscanList.SigChange += dmRmcUnderscanList_SigChange;
				}
				else
				{
					ErrorLog.Error(">>> Could not find xpanel SGD file. SmartObjects will not work at this time");
				}

				// register device with the control system
				if (xPanelUi.Register() == eDeviceRegistrationUnRegistrationResponse.Success)
					ErrorLog.Notice(">>> xPanel has been registered successfully");
				else
					ErrorLog.Error(">>> xPanel was not registered: {0}", xPanelUi.RegistrationFailureReason);
			}
			else
			{
				ErrorLog.Error(">>> This processor does not support ethernet, so this program will not run");
			}

			// create a new timer object to track system inactivity or unplugged cables
			ShutdownTimer = new CTimer(ShutownTimerCallback, Timeout.Infinite, Timeout.Infinite);
		}




		/// <summary>
		/// Overridden function... Invoked before any traffic starts flowing back and forth between the devices and the 
		/// user program. 
		/// This is used to start all the user threads and create all events / mutexes etc.
		/// This function should exit ... If this function does not exit then the program will not start
		/// </summary>
		public override void InitializeSystem()
		{
			// set the initial system state
			SetSystemState(eSystemStates.PoweredOff);

			// configure the comport spec to control the display
			if (dmRmc.Registered)
				dmRmc.ComPorts[1].SetComPortSpec(ComPort.eComBaudRates.ComspecBaudRate9600,
												 ComPort.eComDataBits.ComspecDataBits8,
												 ComPort.eComParityType.ComspecParityNone,
												 ComPort.eComStopBits.ComspecStopBits1,
												 ComPort.eComProtocolType.ComspecProtocolRS232,
												 ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeNone,
												 ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeNone,
												 false);

			// disable free run video support on the vga input
			if (dmTx.Registered)
				dmTx.VgaInput.FreeRun = Crestron.SimplSharpPro.DM.eDmFreeRunSetting.Disabled;

			

			// make sure the rmc and xpanel are registered and if so, update the ui to reflect the current values
			if (xPanelUi.Registered)
			{
				if (dmRmc.Registered && dmRmc.IsOnline)
					UpdateRmcConfig();

				xPanelUi.StringInput[(uint)eXpanelFeedbacks.StringRoomName].StringValue = RoomName;
				xPanelUi.StringInput[(uint)eXpanelFeedbacks.StringProjectVersion].StringValue = ProjectVersion;
				xPanelUi.StringInput[(uint)eXpanelFeedbacks.StringProjectDate].StringValue = ProjectDate;
                xPanelUi.StringInput[(uint)eXpanelFeedbacks.StringTransmitterName].StringValue = TransmitterName;
                xPanelUi.StringInput[(uint)eXpanelFeedbacks.StringReceiverName].StringValue = ReceiverName;
                xPanelUi.StringInput[(uint)eXpanelFeedbacks.StringTT100Name].StringValue = TT100Name;
				xPanelUi.UShortInput[(uint)eXpanelFeedbacks.UShortTimeout].UShortValue = (ushort)(_shutdownTimeout / 1000);
			}

			// assign the filename the shutdown timer value
			shutdownTimerFileName = string.Format("{0}\\shutdowntimer.xml", Directory.GetApplicationDirectory());

			// read shutdown timeout info from disk
			if (File.Exists(shutdownTimerFileName))
			{
				// file exists, deserialize it from the xml file
				_shutdownTimeout = CrestronXMLSerialization.DeSerializeObject<long>(shutdownTimerFileName);

				ErrorLog.Notice("shutdown timer file found, setting to {0}", _shutdownTimeout);
			}
			else
			{
				// start with a default value of 60 seconds
				_shutdownTimeout = DEFAULT_SHUTDOWN_TIMEOUT;

				// write this value to disk
				CrestronXMLSerialization.SerializeObject(shutdownTimerFileName, _shutdownTimeout);

				ErrorLog.Notice("shutdown timer file NOT found, setting to {0}", _shutdownTimeout);
			}

			// update fusion with the time
		
			xPanelUi.UShortInput[(uint)eXpanelFeedbacks.UShortTimeout].UShortValue = (ushort)(_shutdownTimeout / 1000);

			// add new custom commands to the console
			CrestronConsole.AddNewConsoleCommand(Info, "getinfo", "Displays information about the system. Use \"getinfo ?\" for more info.", ConsoleAccessLevelEnum.AccessOperator);
			CrestronConsole.AddNewConsoleCommand(SetNewTimeoutValueFromConsole, "sdtime", "Displays / sets the shutdown timer value. Use \"sdtime ?\" for more info.", ConsoleAccessLevelEnum.AccessOperator);
		}

		#endregion




		#region xpanel ui methods

		/// <summary>
		/// Method to handle sig change (joins) events.
		/// </summary>
		/// <param name="currentDevice">Reference to the device raising this event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void xPanelUi_SigChange(BasicTriList currentDevice, SigEventArgs args)
		{
			switch (args.Sig.Type)
			{
				case eSigType.Bool:
					if (args.Sig.BoolValue == true)
					{
						// press event
					}
					else
					{
						// release event
						switch (args.Sig.Number)
						{
							case (uint)eXpanelButtons.TimeoutPress:
								SetNewTimeoutValue(xPanelUi.UShortOutput[(uint)eXpanelFeedbacks.UShortTimeout].UShortValue * 1000);
                                break;

                            case (uint)eXpanelButtons.ShowScalerPage:
                                xPanelUi.BooleanInput[(uint)eXpanelFeedbacks.BoolShowScalerPage].BoolValue = true;
								break;

                            case (uint)eXpanelButtons.ClearScalerSubpage:
                                xPanelUi.BooleanInput[(uint)eXpanelFeedbacks.BoolShowScalerPage].BoolValue = false;
                                break;

							default:
								break;
						}
					}
					break;

				case eSigType.String:                    
					break;

				case eSigType.UShort:
					break;

				case eSigType.NA:
				default:
					break;
			}
		}




		/// <summary>
		/// Method to handle smart object events from the underscan list.
		/// </summary>
		/// <param name="currentDevice">Reference to the device raising this event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void dmRmcUnderscanList_SigChange(GenericBase currentDevice, SmartObjectEventArgs args)
		{
			try
			{
				if (args.Sig.Name == "Item Clicked")
					dmRmc.Scaler.UnderscanMode = (eDmScanMode)(args.Sig.UShortValue - 1);
			}
			catch (Exception)
			{
				ErrorLog.Error(">>> invalid dm rmc output underscan selection");
			}
		}




		/// <summary>
		/// Method to handle smart object events from the aspect list.
		/// </summary>
		/// <param name="currentDevice">Reference to the device raising this event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void dmRmcAspectModeList_SigChange(GenericBase currentDevice, SmartObjectEventArgs args)
		{
			try
			{
			if (args.Sig.Name == "Item Clicked")
				dmRmc.Scaler.DisplayMode = (EndpointScalerOutput.eDisplayMode)(args.Sig.UShortValue - 1);
			}
			catch (Exception)
			{
				ErrorLog.Error(">>> invalid dm rmc output aspect selection");
			}
		}




		/// <summary>
		/// Method to handle smart object events from the resolution list.
		/// </summary>
		/// <param name="currentDevice">Reference to the device raising this event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void dmRmcOutputResList_SigChange(GenericBase currentDevice, SmartObjectEventArgs args)
		{
			try
			{
				if (args.Sig.Name == "Item Clicked")
					dmRmc.Scaler.Resolution = (EndpointScalerOutput.eResolution)(args.Sig.UShortValue - 1);
			}
			catch (Exception)
			{
				ErrorLog.Error(">>> invalid dm rmc output resolution selection");
			}
		}




		/// <summary>
		/// Enumeration of the various xpanel buttons (digitals).
		/// </summary>
		private enum eXpanelButtons
		{
			TimeoutPress = 10,
            ShowScalerPage,
            ClearScalerSubpage,
		}




		/// <summary>
		/// Enumeration of the various xpanel feedback joins.
		/// </summary>
		private enum eXpanelFeedbacks
		{
			StringRoomName = 1,
			StringProjectDate,
			StringProjectVersion,
			StringSystemState,
            StringTransmitterName = 5,
            StringReceiverName = 6,
            StringTT100Name = 7,
            StringFusionName = 8,
			StringHdmiRes = 11,
			StringDisplayPortRes,
			STringVgaRes,
            BoolShowScalerPage = 11,
            
			BoolDmTxOnline = 30,
			BoolDmRmcOnline = 31,
			BoolTt100Online = 31,
			BoolFusionOnline = 33,
			BoolHdmiSync = 40,
			BoolDisplayPortSync,
			BoolVgaSync,

			UShortTimeout = 10
		}

		#endregion




		#region dm tx event handlers

		/// <summary>
		/// Method to handle base events raised by the Dm Tx.
		/// </summary>
		/// <param name="device">Reference to the device raising this event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void dmTx_BaseEvent(GenericBase device, BaseEventArgs args)
		{
			// determine what event has been raised
			switch (args.EventId)
			{
				case BaseDmTx401.VideoSourceFeedbackEventId:
					break;

				default:
					break;
			}
		}




		/// <summary>
		/// Method to handle display port input events on the Dm Tx.
		/// </summary>
		/// <param name="inputStream">Reference to the input raising this event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void DisplayPortInput_InputStreamChange(EndpointInputStream inputStream, EndpointInputStreamEventArgs args)
		{
			// determine what event has been raised
			switch (args.EventId)
			{
				// sync event, could be detected or lost
				case EndpointInputStreamEventIds.SyncDetectedFeedbackEventId:
					// update fusion with the sync status for this input
					
					xPanelUi.BooleanInput[(uint)eXpanelFeedbacks.BoolDisplayPortSync].BoolValue = dmTx.HdmiInput.SyncDetectedFeedback.BoolValue;
					
					// determine if sync was detected
					if (dmTx.DisplayPortInput.SyncDetectedFeedback.BoolValue)
					{
						// determine what state the system is in
						switch (SystemState)
						{
							case eSystemStates.PoweredOff:
							case eSystemStates.WaitingForInput:
								CrestronConsole.PrintLine("display port sync has been detected, switching to that input");

								if (SystemState == eSystemStates.PoweredOff)
									SetDisplayPower(true);

								SetSystemState(eSystemStates.InUse);
								dmTx.VideoSource = BaseDmTx401.eSourceSelection.DisplayPort;
								break;

							case eSystemStates.InUse:
								CrestronConsole.PrintLine("display port sync has been detected, but another input is active");
								break;

							default:
								break;
						}
						break;
					}
					else // sync was lost
					{
						switch (SystemState)
						{
							case eSystemStates.InUse:
								// check to see if this input the active input
								if (dmTx.VideoSourceFeedback == BaseDmTx401.eSourceSelection.DisplayPort)
								{
									CrestronConsole.PrintLine("display port sync has been lost while it was the active input");
									// if it was the active input, check the next logical input for sync and switch if found
									if (dmTx.HdmiInput.SyncDetectedFeedback.BoolValue)
										dmTx.VideoSource = BaseDmTx401.eSourceSelection.HDMI;
									else if (dmTx.VgaInput.SyncDetectedFeedback.BoolValue)
										dmTx.VideoSource = BaseDmTx401.eSourceSelection.VGA;
									else
									{
										// no other inputs had sync, start timer
										dmTx.VideoSource = BaseDmTx401.eSourceSelection.Disabled;
										SetSystemState(eSystemStates.WaitingForInput);
									}
								}
								else
								{
									CrestronConsole.PrintLine("display port sync has been lost, but another input is active");
								}
								break;

							case eSystemStates.WaitingForInput:
							case eSystemStates.PoweredOff:
							default:
								break;
						}
					}

					break;

				default:
					break;
			}
		}





		/// <summary>
		/// Method to handle hdmi input events on the Dm Tx.
		/// </summary>
		/// <param name="inputStream">Reference to the input raising this event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void HdmiInput_InputStreamChange(EndpointInputStream inputStream, EndpointInputStreamEventArgs args)
		{
			// determine what event has been raised
			switch (args.EventId)
			{
				// sync event, could be detected or lost
				case EndpointInputStreamEventIds.SyncDetectedFeedbackEventId:
					// update fusion with the sync status for this input
					xPanelUi.BooleanInput[(uint)eXpanelFeedbacks.BoolHdmiSync].BoolValue = dmTx.HdmiInput.SyncDetectedFeedback.BoolValue;
					
					// determine if sync was detected
					if (dmTx.HdmiInput.SyncDetectedFeedback.BoolValue)
					{
						// determine what state the system is in
						switch (SystemState)
						{
							case eSystemStates.PoweredOff:
							case eSystemStates.WaitingForInput:
								CrestronConsole.PrintLine("hdmi sync has been detected, switching to that input");

								if (SystemState == eSystemStates.PoweredOff)
									SetDisplayPower(true);

								SetSystemState(eSystemStates.InUse);
								dmTx.VideoSource = BaseDmTx401.eSourceSelection.HDMI;
								break;

							case eSystemStates.InUse:
								CrestronConsole.PrintLine("hdmi sync has been detected, but another input is active");
								break;

							default:
								break;
						}
						break;
					}
					else // sync was lost
					{
						switch (SystemState)
						{
							case eSystemStates.InUse:
								// check to see if this input the active input
								if (dmTx.VideoSourceFeedback == BaseDmTx401.eSourceSelection.HDMI)
								{
									CrestronConsole.PrintLine("hdmi sync has been lost while it was the active input");
									// if it was the active input, check the next logical input for sync and switch if found
									if (dmTx.HdmiInput.SyncDetectedFeedback.BoolValue)
										dmTx.VideoSource = BaseDmTx401.eSourceSelection.DisplayPort;
									else if (dmTx.VgaInput.SyncDetectedFeedback.BoolValue)
										dmTx.VideoSource = BaseDmTx401.eSourceSelection.VGA;
									else
									{
										// no other inputs had sync, start timer
										dmTx.VideoSource = BaseDmTx401.eSourceSelection.Disabled;
										SetSystemState(eSystemStates.WaitingForInput);
									}
								}
								else
								{
									CrestronConsole.PrintLine("hdmi sync has been lost, but another input is active");
								}
								break;

							case eSystemStates.WaitingForInput:
							case eSystemStates.PoweredOff:
							default:
								break;
						}
					}

					break;

				default:
					break;
			}
		}




		/// <summary>
		/// Method to handle vga input events on the Dm Tx.
		/// </summary>
		/// <param name="inputStream">Reference to the input raising this event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void VgaInput_InputStreamChange(EndpointInputStream inputStream, EndpointInputStreamEventArgs args)
		{
			// determine what event has been raised
			switch (args.EventId)
			{
				// sync event, could be detected or lost
				case EndpointInputStreamEventIds.SyncDetectedFeedbackEventId:
					// update fusion with the sync status for this input
					xPanelUi.BooleanInput[(uint)eXpanelFeedbacks.BoolVgaSync].BoolValue = dmTx.HdmiInput.SyncDetectedFeedback.BoolValue;
					
					// determine if sync was detected
					if (dmTx.VgaInput.SyncDetectedFeedback.BoolValue)
					{
						// determine what state the system is in
						switch (SystemState)
						{
							case eSystemStates.PoweredOff:
							case eSystemStates.WaitingForInput:
								CrestronConsole.PrintLine("vga sync has been detected, switching to that input");

								if (SystemState == eSystemStates.PoweredOff)
									SetDisplayPower(true);

								SetSystemState(eSystemStates.InUse);
								dmTx.VideoSource = BaseDmTx401.eSourceSelection.VGA;
								break;

							case eSystemStates.InUse:
								CrestronConsole.PrintLine("vga sync has been detected, but another input is active");
								break;

							default:
								break;
						}
						break;
					}
					else // sync was lost
					{
						switch (SystemState)
						{
							case eSystemStates.InUse:
								// check to see if this input the active input
								if (dmTx.VideoSourceFeedback == BaseDmTx401.eSourceSelection.VGA)
								{
									CrestronConsole.PrintLine("vga sync has been lost while it was the active input");
									// if it was the active input, check the next logical input for sync and switch if found
									if (dmTx.HdmiInput.SyncDetectedFeedback.BoolValue)
										dmTx.VideoSource = BaseDmTx401.eSourceSelection.HDMI;
									else if (dmTx.VgaInput.SyncDetectedFeedback.BoolValue)
										dmTx.VideoSource = BaseDmTx401.eSourceSelection.DisplayPort;
									else
									{
										// no other inputs had sync, start timer
										dmTx.VideoSource = BaseDmTx401.eSourceSelection.Disabled;
										SetSystemState(eSystemStates.WaitingForInput);
									}
								}
								else
								{
									CrestronConsole.PrintLine("vga sync has been lost, but another input is active");
								}
								break;

							case eSystemStates.WaitingForInput:
							case eSystemStates.PoweredOff:
							default:
								break;
						}
					}

					break;

				default:
					break;
			}
		}




		/// <summary>
		/// Method to handle video attribute changes on the hdmi video input.
		/// </summary>
		/// <param name="sender">Reference to the device raising this event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void Hdmi_AttributeChange(object sender, GenericEventArgs args)
		{
			switch (args.EventId)
			{
				case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.FramesPerSecondFeedbackEventId:
				case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.HorizontalResolutionFeedbackEventId:
				case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.VerticalResolutionFeedbackEventId:
				case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.InterlacedFeedbackEventId:
					ushort hres = dmTx.HdmiInput.VideoAttributes.HorizontalResolutionFeedback.UShortValue;
					ushort vres = dmTx.HdmiInput.VideoAttributes.VerticalResolutionFeedback.UShortValue;
					bool interlaced = dmTx.HdmiInput.VideoAttributes.InterlacedFeedback.BoolValue;
					ushort fps = dmTx.HdmiInput.VideoAttributes.FramesPerSecondFeedback.UShortValue;

					string resolutionInfo;

					if (hres == 0 || vres == 0 || fps == 0)
						resolutionInfo = "no signal";
					else
						resolutionInfo = string.Format("{0}x{1}{2} {3}fps", hres,
																			vres,
																			interlaced ? "i" : "p",
																			fps);

					xPanelUi.StringInput[(uint)eXpanelFeedbacks.StringHdmiRes].StringValue = resolutionInfo;
					break;

				default:
					break;
			}
		}




		/// <summary>
		/// Method to handle video attribute changes on the display port video input.
		/// </summary>
		/// <param name="sender">Reference to the device raising this event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void DisplayPort_AttributeChange(object sender, GenericEventArgs args)
		{
			switch (args.EventId)
			{
				case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.FramesPerSecondFeedbackEventId:
				case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.HorizontalResolutionFeedbackEventId:
				case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.VerticalResolutionFeedbackEventId:
				case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.InterlacedFeedbackEventId:
					ushort hres = dmTx.DisplayPortInput.VideoAttributes.HorizontalResolutionFeedback.UShortValue;
					ushort vres = dmTx.DisplayPortInput.VideoAttributes.VerticalResolutionFeedback.UShortValue;
					bool interlaced = dmTx.DisplayPortInput.VideoAttributes.InterlacedFeedback.BoolValue;
					ushort fps = dmTx.DisplayPortInput.VideoAttributes.FramesPerSecondFeedback.UShortValue;

					string resolutionInfo;

					if (hres == 0 || vres == 0 || fps == 0)
						resolutionInfo = "no signal";
					else
						resolutionInfo = string.Format("{0}x{1}{2} {3}fps", hres,
																			vres,
																			interlaced ? "i" : "p",
																			fps);

					xPanelUi.StringInput[(uint)eXpanelFeedbacks.StringDisplayPortRes].StringValue = resolutionInfo;
					break;

				default:
					break;
			}
		}




		/// <summary>
		/// Method to handle video attribute changes on the vga video input.
		/// </summary>
		/// <param name="sender">Reference to the device raising this event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void Vga_AttributeChange(object sender, GenericEventArgs args)
		{
			switch (args.EventId)
			{
				case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.FramesPerSecondFeedbackEventId:
				case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.HorizontalResolutionFeedbackEventId:
				case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.VerticalResolutionFeedbackEventId:
				case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.InterlacedFeedbackEventId:
					ushort hres = dmTx.VgaInput.VideoAttributes.HorizontalResolutionFeedback.UShortValue;
					ushort vres = dmTx.VgaInput.VideoAttributes.VerticalResolutionFeedback.UShortValue;
					bool interlaced = dmTx.VgaInput.VideoAttributes.InterlacedFeedback.BoolValue;
					ushort fps = dmTx.VgaInput.VideoAttributes.FramesPerSecondFeedback.UShortValue;

					string resolutionInfo;

					if (hres == 0 || vres == 0 || fps == 0)
						resolutionInfo = "no signal";
					else
						resolutionInfo = string.Format("{0}x{1}{2} {3}fps", hres,
																			vres,
																			interlaced ? "i" : "p",
																			fps);

					xPanelUi.StringInput[(uint)eXpanelFeedbacks.STringVgaRes].StringValue = resolutionInfo;
					break;

				default:
					break;
			}
		}

		#endregion




		#region dm rmc methods

		/// <summary>
		/// Method to handle Scaler events on the DM RMC.
		/// </summary>
		/// <param name="scalerOutput">Reference to the device raising this event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void Scaler_OutputChange(EndpointScalerOutput scalerOutput, ScalerOutputEventArgs args)
		{
			// determine what event was triggered
			switch (args.EventId)
			{
				case ScalerOutputEventIds.ResolutionFeedbackEventId:
					CrestronConsole.PrintLine("Dm Rmc output resolution is now set to {0}", dmRmc.Scaler.Resolution.ToString());

			
					// update the xpanel ui
					dmRmcOutputResList.UShortInput["Select Item"].UShortValue = (ushort)(dmRmc.Scaler.Resolution + 1);
					break;

				case ScalerOutputEventIds.DisplayModeFeedbackEventId:
					// temporary variable to hold string representation of the display (aspect) mode
					string displayMode;

					switch (dmRmc.Scaler.DisplayModeFeedback)
					{
						case EndpointScalerOutput.eDisplayMode.Maintain:
							displayMode = "Maintain";
							break;

						case EndpointScalerOutput.eDisplayMode.OneToOne:
							displayMode = "One-to-One";
							break;

						case EndpointScalerOutput.eDisplayMode.Stretch:
							displayMode = "Stretch";
							break;

						case EndpointScalerOutput.eDisplayMode.Zoom:
							displayMode = "Zoom";
							break;

						default:
							displayMode = "Unknown";
							break;
					}

					CrestronConsole.PrintLine("Dm Rmc output aspect ratio is now set to {0}", displayMode);


					// update xpanel ui
					dmRmcAspectModeList.UShortInput["Select Item"].UShortValue = (ushort)(dmRmc.Scaler.DisplayModeFeedback + 1);
					break;

				case ScalerOutputEventIds.UnderscanModeFeedbackEventId:
					// temporary variable to hold string representation of the under scan mode
					string underscanMode;

					switch (dmRmc.Scaler.UnderscanModeFeedback)
					{
						case eDmScanMode.None:
							underscanMode = "None";
							break;

						case eDmScanMode.ModeOne:
							underscanMode = "2.5%";
							break;

						case eDmScanMode.ModeTwo:
							underscanMode = "5%";
							break;

						case eDmScanMode.ModeThree:
							underscanMode = "7.5%";
							break;

						default:
							underscanMode = "Unknown";
							break;
					}

					CrestronConsole.PrintLine("Dm Rmc output underscan mode is now set to {0}", underscanMode);

					
					// update the xpanel ui
					dmRmcUnderscanList.UShortInput["Select Item"].UShortValue = (ushort)(dmRmc.Scaler.UnderscanModeFeedback + 1);
					break;

				default:
					break;
			}
		}




		/// <summary>
		/// Method to handle DM input events on the RMC Scaler device.
		/// </summary>
		/// <param name="inputStream">Reference to the device raising this event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void DmRmc_InputStreamChange(EndpointInputStream inputStream, EndpointInputStreamEventArgs args)
		{
			switch (args.EventId)
			{
				case EndpointInputStreamEventIds.SyncDetectedFeedbackEventId:
					CrestronConsole.PrintLine("video sync at the dm rmc has been {0}",
							dmRmc.DmInput.SyncDetectedFeedback.BoolValue ? "received" : "lost");
					break;
					
				default:
					break;
			}
		}




		/// <summary>
		/// Method to handle com port received events from the Dm Rmc. 
		/// </summary>
		/// <param name="ReceivingComPort">Reference to the com port raising this event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void DmRmc_SerialDataReceived(ComPort ReceivingComPort, ComPortSerialDataEventArgs args)
		{
			CrestronConsole.PrintLine("Received from Dm Rmc com port: {0}", args.SerialData);
		}




		/// <summary>
		/// Method to read the configuration settings on the RMC and update the xpanel UI.
		/// </summary>
		private void UpdateRmcConfig()
		{
			dmRmcOutputResList.UShortInput["Select Item"].UShortValue = (ushort)(dmRmc.Scaler.ResolutionFeedback + 1);
			dmRmcAspectModeList.UShortInput["Select Item"].UShortValue = (ushort)(dmRmc.Scaler.DisplayModeFeedback + 1);
			dmRmcUnderscanList.UShortInput["Select Item"].UShortValue = (ushort)(dmRmc.Scaler.UnderscanModeFeedback + 1);
		}

		#endregion





		#region other methods

		/// <summary>
		/// Method to manually start up the system (e.g. button press or Fusion command).
		/// </summary>
		private void ManualStartUp()
		{
			// turn on the display
			SetDisplayPower(true);

			// look to see if any inputs have signal, if so, make them the active input
			if (dmTx.HdmiInput.SyncDetectedFeedback.BoolValue)
			{
				CrestronConsole.PrintLine("hdmi sync detected at start up, switching to that input");

				// manually switch to this input
				dmTx.VideoSource = BaseDmTx401.eSourceSelection.HDMI;

				// update system state
				SetSystemState(eSystemStates.InUse);
			}
			else if (dmTx.DisplayPortInput.SyncDetectedFeedback.BoolValue)
			{
				CrestronConsole.PrintLine("display port sync detected at start up, switching to that input");

				// manually switch to this input
				dmTx.VideoSource = BaseDmTx401.eSourceSelection.DisplayPort;

				// update system state
				SetSystemState(eSystemStates.InUse);
			}
			else if (dmTx.VgaInput.SyncDetectedFeedback.BoolValue)
			{
				CrestronConsole.PrintLine("vga sync detected at start up, switching to that input");

				// manually switch to this input
				dmTx.VideoSource = BaseDmTx401.eSourceSelection.VGA;

				// update system state
				SetSystemState(eSystemStates.InUse);
			}
			else
			{
				CrestronConsole.PrintLine("no inputs have sync upon start up, starting {0}sec shutdown timer...", (_shutdownTimeout / 1000));

				// update system state
				SetSystemState(eSystemStates.WaitingForInput);
			}
		}



	
		/// <summary>
		/// Timer used to track how long the system will stay powered without a video input attached.
		/// </summary>
		private CTimer ShutdownTimer;




		/// <summary>
		/// Method called by the expiration of the ShutdownTimer object.
		/// </summary>
		/// <param name="notUsed">Not used.</param>
		private void ShutownTimerCallback(object notUsed)
		{
			switch (SystemState)
			{
				case eSystemStates.WaitingForInput:
					CrestronConsole.PrintLine("shutdown timer expired waiting for an input, turning system off now");

					// return the system to the off state
					SetSystemState(eSystemStates.PoweredOff);
					break;

				case eSystemStates.PoweredOff:
				case eSystemStates.InUse:
				default:
					break;
			}
		}




		/// <summary>
		/// Enumeration of the possible system states.
		/// </summary>
		private enum eSystemStates
		{
			PoweredOff,
			InUse,
			WaitingForInput
		}




		/// <summary>
		/// Field to hold the current system state.
		/// </summary>
		private eSystemStates SystemState;




		/// <summary>
		/// Method to set the system state, led colors and start/stop the shutdown timer
		/// </summary>
		/// <param name="newState">The state to put the system in.</param>
		void SetSystemState(eSystemStates newState)
		{
			// store parameter to the local variable
			SystemState = newState;


			switch (newState)
			{
				case eSystemStates.PoweredOff:
					// set led color
					connectIt.LedState1 = eLedStates.SolidBlue;
					connectIt.LedState2 = eLedStates.SolidBlue;

					// send power off command to the display
					SetDisplayPower(false);

					// stop the timer if it is running
					ShutdownTimer.Stop();


					// break video route
					dmTx.VideoSource = BaseDmTx401.eSourceSelection.Disabled;

					// update ui
					xPanelUi.StringInput[(uint)eXpanelFeedbacks.StringSystemState].StringValue = "off";
					break;

				case eSystemStates.InUse:
					// set led color
					connectIt.LedState1 = eLedStates.SolidGreen;
					connectIt.LedState2 = eLedStates.SolidGreen;

					// stop the timer if it is running
					ShutdownTimer.Stop();


					// update ui
					xPanelUi.StringInput[(uint)eXpanelFeedbacks.StringSystemState].StringValue = "in use";
					break;

				case eSystemStates.WaitingForInput:
					// set led color
					connectIt.LedState1 = eLedStates.BlinkingGreen;
					connectIt.LedState2 = eLedStates.BlinkingGreen;

					// start shutdown timer

					ShutdownTimer.Reset(_shutdownTimeout, Timeout.Infinite);

					// update ui
					xPanelUi.StringInput[(uint)eXpanelFeedbacks.StringSystemState].StringValue = "waiting for input";
					break;

				default:
					break;
			}
		}




		/// <summary>
		/// Method to send the power commands to the display via the dm rmc com port.
		/// </summary>
		/// <param name="power">Power state: TRUE = send on command, FALSE = send off command.</param>
		private void SetDisplayPower(bool power)
		{
			if (power)
			{
				CrestronConsole.PrintLine("Display power on command");

				// NEC E423 power on command
				dmRmc.ComPorts[1].Send("\u0001\u0030\u0041\u0030\u0041\u0030\u0043\u0002\u0043\u0032\u0030\u0033\u0044\u0036\u0030\u0030\u0030\u0031\u0003\u0073\u000D");

				// wait for one second before sending input command
				Thread.Sleep(2000);

				// NEC E423 hdmi input 1 
				dmRmc.ComPorts[1].Send("\u0001\u0030\u0041\u0030\u0045\u0030\u0041\u0002\u0030\u0030\u0036\u0030\u0030\u0030\u0031\u0031\u0003\u0072\u000D");
			}
			else
			{
				CrestronConsole.PrintLine("Display power off command");

				// NEC e423 power off command
				dmRmc.ComPorts[1].Send("\u0001\u0030\u0041\u0030\u0041\u0030\u0043\u0002\u0043\u0032\u0030\u0033\u0044\u0036\u0030\u0030\u0030\u0034\u0003\u0076\u000D");
			}
		}

		


		/// <summary>
		/// helper console functions.
		/// </summary>
		/// <param name="cmd">command name</param>
		private void Info(string cmd)
		{
			switch (cmd.ToLower())
			{
				case "rmcinput":
					if (dmRmc.IsOnline)
						if (dmRmc.DmInput.SyncDetectedFeedback.BoolValue)
							CrestronConsole.ConsoleCommandResponse("video resolution is {0}x{1} {2}fps\n",
									dmRmc.DmInput.VideoAttributes.HorizontalResolutionFeedback.UShortValue,
									dmRmc.DmInput.VideoAttributes.VerticalResolutionFeedback.UShortValue,
									dmRmc.DmInput.VideoAttributes.FramesPerSecondFeedback.UShortValue);
						else
							CrestronConsole.ConsoleCommandResponse("no video detected\n");
					break;

				case "state":
					CrestronConsole.PrintLine("system state is {0}\n", SystemState.ToString());
					break;

				case "txinput":
					string currentInput = string.Empty;

					switch (dmTx.VideoSourceFeedback)
					{
						case BaseDmTx401.eSourceSelection.Auto:
							currentInput = "auto";
							break;

						case BaseDmTx401.eSourceSelection.Composite:
							currentInput = "cvbs";
							break;

						case BaseDmTx401.eSourceSelection.Disabled:
							currentInput = "disabled";
							break;

						case BaseDmTx401.eSourceSelection.DisplayPort:
							currentInput = "displayport";
							break;

						case BaseDmTx401.eSourceSelection.HDMI:
							currentInput = "hdmi";
							break;

						case BaseDmTx401.eSourceSelection.VGA:
							currentInput = "vga";
							break;

						default:
							currentInput = "unknown";
							break;
					}

					CrestronConsole.ConsoleCommandResponse("the current input is {0}\n", currentInput);
					break;

				case "online":
					CrestronConsole.ConsoleCommandResponse("dm tx is {0}, rmc is {1}, tt100 is {2}",
							dmTx.IsOnline ? "online" : "offline",
							dmRmc.IsOnline ? "online" : "offline",
							connectIt.IsOnline ? "online" : "offline");
					break;

				case "?":
					CrestronConsole.ConsoleCommandResponse("available commands: ONLINE, TXINPUT, RMCINPUT, STATE");
					break;

				default:
					CrestronConsole.ConsoleCommandResponse("unknown parameter for GETINFO\n");
					break;
			}
		}




		/// <summary>
		/// Callback from the console used to set a new shutdown timeout value.
		/// </summary>
		/// <param name="cmd">Console parameter.</param>
		private void SetNewTimeoutValueFromConsole(string cmd)
		{
			switch (cmd)
			{
				case "":
					CrestronConsole.ConsoleCommandResponse("The current shutdown timeout is {0} seconds", _shutdownTimeout / 1000);
					break;

				case "?":
					CrestronConsole.ConsoleCommandResponse("Set a new shutdown timeout value (in seconds). Min = {0}, Max = {1}.",
							(MIN_SHUTDOWN_TIMEOUT / 1000), (MAX_SHUTDOWN_TIMEOUT / 1000));
					break;

				default:
					try
					{
						// parse the parameter and multiply by 1000 to get milisecons
						long tempValue = long.Parse(cmd) * 1000;

						// send value to setter method
						SetNewTimeoutValue(tempValue);
					}
					catch (Exception e)
					{
						CrestronConsole.ConsoleCommandResponse("exception raised during parse: " + e.Message);
					}
					break;
			}
		}




		/// <summary>
		/// Method to change the shutdown timeout setting via a parameter.
		/// </summary>
		/// <param name="newValue">The new value to be used as the shutdown timeout.</param>
		private void SetNewTimeoutValue(long newValue)
		{
			if (newValue <= MAX_SHUTDOWN_TIMEOUT && newValue >= MIN_SHUTDOWN_TIMEOUT)
			{
				// store the passed value
				_shutdownTimeout = newValue;

				// update the xpanel the new value
				xPanelUi.UShortInput[(uint)eXpanelFeedbacks.UShortTimeout].UShortValue = (ushort)(newValue / 1000);

				// write the new value to disk
				CrestronXMLSerialization.SerializeObject(shutdownTimerFileName, _shutdownTimeout);

				CrestronConsole.PrintLine("new shutdown timeout value of {0} seconds has been stored", newValue / 1000);
			}
			else
			{
				CrestronConsole.PrintLine("cannot not set a shutdown timeout of {0} seconds", newValue / 1000);
			}
		}

		#endregion




		#region system event handlers

		/// <summary>
		/// Method to handle the processor's ethernet adapter events.
		/// </summary>
		/// <param name="ethernetEventArgs">Information about the event being raised.</param>
		void CrestronEnvironment_EthernetEventHandler(EthernetEventArgs ethernetEventArgs)
		{
			// only process the main ehternet adapter's events
			if (ethernetEventArgs.EthernetAdapter != EthernetAdapterType.EthernetLANAdapter)
				return;

			// determine what type of event has been raised
			switch (ethernetEventArgs.EthernetEventType)
			{
				case eEthernetEventType.LinkUp:
					// get the processor's ip address
					var enetInfo = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, 0);

				break;

				case eEthernetEventType.LinkDown:
				default:
					break;
			}
		}




		/// <summary>
		/// Method to handle online/offline events from the devices.
		/// </summary>
		/// <param name="currentDevice">Reference to the device raising the event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void Device_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
		{
			// determine which device raised this event
			if (currentDevice == dmTx)
			{
				// update the xpanel with new online status
				xPanelUi.BooleanInput[(uint)eXpanelFeedbacks.BoolDmTxOnline].BoolValue = args.DeviceOnLine;
			}
			else if (currentDevice == dmRmc)
			{
				// update the xpanel with new online status
				xPanelUi.BooleanInput[(uint)eXpanelFeedbacks.BoolDmRmcOnline].BoolValue = args.DeviceOnLine;

				if (args.DeviceOnLine)
					UpdateRmcConfig();
			}
			
			
		}



		/// <summary>
		/// Method to handle program events on this processor.
		/// </summary>
		/// <param name="programEventType">Information about the event being raised.</param>
		void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
		{
			switch (programEventType)
			{
				case eProgramStatusEventType.Paused:
					ShutdownTimer.Stop();
					break;

				case eProgramStatusEventType.Resumed:
					break;

				case eProgramStatusEventType.Stopping:
					ShutdownTimer.Stop();
					break;

				default:
					break;
			}
		}




		/// <summary>
		/// Method to handle system events on this processor.
		/// </summary>
		/// <param name="systemEventType">Information about the event being raised.</param>
		void CrestronEnvironment_SystemEventHandler(eSystemEventType systemEventType)
		{
			switch (systemEventType)
			{
				case eSystemEventType.Rebooting:
					ShutdownTimer.Stop();
					break;

				case eSystemEventType.DiskInserted:
				case eSystemEventType.DiskRemoved:
				default:
					break;
			}
		}

		#endregion




		#region connect it event handler

		/// <summary>
		/// Enumeration of the ConnectIt buttons.
		/// </summary>
		private enum eConnectItButtons
		{
			Left = 1,
			Right
		}




		/// <summary>
		/// Method to handle button press events from the TT-100 ConnectIt.
		/// </summary>
		/// <param name="device">Reference to the device raising this event.</param>
		/// <param name="args">Information about the event being raised.</param>
		void connectIt_ButtonStateChange(GenericBase device, ButtonEventArgs args)
		{
			// only process the press of the button, ignore the release
			if (args.NewButtonState == eButtonState.Released)
				return;

			// determine which button was pressed
			switch (args.Button.Number)
			{
				// handle both buttons the same way
				case (uint)eConnectItButtons.Left:
				case (uint)eConnectItButtons.Right:
					CrestronConsole.PrintLine("{0} ConnectIt button pressed while system is in \"{1}\" mode",
											args.Button.Number == 1 ? "Left" : "Right",
											SystemState.ToString());

					// determine what the system state was when the button was pressed
					switch (SystemState)
					{
						case eSystemStates.PoweredOff:
							ManualStartUp();
							break;

						case eSystemStates.InUse:
							// determine what input is active, then check to see  if any other inputs have signal
							if (dmTx.VideoSourceFeedback == BaseDmTx401.eSourceSelection.HDMI)
							{
								// if another input has signal, cycle to that input
								if (dmTx.DisplayPortInput.SyncDetectedFeedback.BoolValue)
									dmTx.VideoSource = BaseDmTx401.eSourceSelection.DisplayPort;
								else if (dmTx.VgaInput.SyncDetectedFeedback.BoolValue)
									dmTx.VideoSource = BaseDmTx401.eSourceSelection.VGA;
								else
									CrestronConsole.PrintLine("no other inputs have sync, ignoring button press");
							}
							else if (dmTx.VideoSourceFeedback == BaseDmTx401.eSourceSelection.DisplayPort)
							{
								if (dmTx.HdmiInput.SyncDetectedFeedback.BoolValue)
									dmTx.VideoSource = BaseDmTx401.eSourceSelection.HDMI;
								else if (dmTx.VgaInput.SyncDetectedFeedback.BoolValue)
									dmTx.VideoSource = BaseDmTx401.eSourceSelection.VGA;
								else
									CrestronConsole.PrintLine("no other inputs have sync, ignoring button press");
							}
							else if (dmTx.VideoSourceFeedback == BaseDmTx401.eSourceSelection.VGA)
							{
								if (dmTx.HdmiInput.SyncDetectedFeedback.BoolValue)
									dmTx.VideoSource = BaseDmTx401.eSourceSelection.HDMI;
								else if (dmTx.DisplayPortInput.SyncDetectedFeedback.BoolValue)
									dmTx.VideoSource = BaseDmTx401.eSourceSelection.DisplayPort;
								else
									CrestronConsole.PrintLine("no other inputs have sync, ignoring button press");
							}
							else
							{
								/* the tx should never be on any input other than vga, hdmi or displayport. if is ever gets there,
								 * check to see if any of the valid inputs have signal and then switch to it */
								CrestronConsole.PrintLine("button pressed while on an invalid dm tx input ({0})", dmTx.VideoSourceFeedback.ToString());

								if (dmTx.HdmiInput.SyncDetectedFeedback.BoolValue)
									dmTx.VideoSource = BaseDmTx401.eSourceSelection.HDMI;
								else if (dmTx.DisplayPortInput.SyncDetectedFeedback.BoolValue)
									dmTx.VideoSource = BaseDmTx401.eSourceSelection.DisplayPort;
								else if (dmTx.VgaInput.SyncDetectedFeedback.BoolValue)
									dmTx.VideoSource = BaseDmTx401.eSourceSelection.VGA;
								else
									SetSystemState(eSystemStates.WaitingForInput);
							}
							break;

						case eSystemStates.WaitingForInput:
							connectIt.LedState1 = eLedStates.BlinksRedFiveTimes;
							connectIt.LedState2 = eLedStates.BlinksRedFiveTimes;
							break;

						default:
							break;
					}

					break;

				default:
					break;
			}
		}

		#endregion

	}
}
