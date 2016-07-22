using System;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       				// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    		// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharpPro.Lighting;
using Crestron.SimplSharpPro.UI;                    			// For UI Devices. Please include the 
using Crestron.SimplSharpPro.Gateways;
using System.Collections.Generic;

namespace DYoung
{
    public class ControlSystem : CrestronControlSystem
    {
        // Define local variables ...
        public XpanelForSmartGraphics xp;
        public CenRfgwEx gateway;
        public ClwLdimex1 lampDimmer;
        public ClwDelvexE wallDimmer;

        /// <summary>
        /// Constructor of the Control System Class. Make sure the constructor always exists.
        /// If it doesn't exit, the code will not run on your 3-Series processor.
        /// </summary>
        public ControlSystem()
            : base()
        {

            // Set the number of threads which you want to use in your program - At this point the threads cannot be created but we should
            // define the max number of threads which we will use in the system.
            // the right number depends on your project; do not make this number unnecessarily large
            Thread.MaxNumberOfUserThreads = 20;

            xp = new XpanelForSmartGraphics(0xaa, this);
            xp.SigChange += new SigEventHandler(xp_SigChange);
            xp.Register();

            gateway = new CenRfgwEx(0x03, this);
            lampDimmer = new ClwLdimex1(0x03, gateway);
            lampDimmer.Description = "Bedroom lamp dimmer";		// this is what shows in the IP table description field
            lampDimmer.LoadStateChange += new LoadEventHandler(lampDimmer_LoadStateChange);
            lampDimmer.ParameterRaiseLowerRate = SimplSharpDeviceHelper.SecondsToUshort(4.0f);
            //        ^^^^^^^^^^^^^^^^^^^^ - this is a very helpful class

            wallDimmer = new ClwDelvexE(0x04, gateway);
            wallDimmer.DimmerRemoteButtonSettings.ParameterBargraphBehavior = ClwDimExDimmerRemoteButtonSettings.eBargraphBehavior.AlwaysOn;
            wallDimmer.DimmerRemoteButtonSettings.ParameterBargraphTimeout = SimplSharpDeviceHelper.SecondsToUshort(2.0f);
            wallDimmer.DimmerRemoteButtonSettings.ParameterRemoteDoubleTapTime = SimplSharpDeviceHelper.SecondsToUshort(0.5f);
            wallDimmer.DimmerRemoteButtonSettings.ParameterRemoteHoldTime = SimplSharpDeviceHelper.SecondsToUshort(0.5f);
            wallDimmer.DimmerRemoteButtonSettings.ParameterRemoteWaitForDoubleTap = eRemoteWaitForDoubleTap.No;
            wallDimmer.DimmerRemoteButtonSettings.ParameterReservedButtonForLocalMode = 0;
            wallDimmer.DimmerUISettings.ParameterButtonLogic = eButtonLogic.Remote;
            wallDimmer.DimmerUISettings.ParameterLEDOnLevel = ushort.MaxValue;
            wallDimmer.DimmerUISettings.ParameterNightLightLevel = SimplSharpDeviceHelper.PercentToUshort(10.0f);
            wallDimmer.ParameterDimmerDelayedOffTime = SimplSharpDeviceHelper.SecondsToUshort(1.0f);
            wallDimmer.ParameterOffFadeTime = SimplSharpDeviceHelper.SecondsToUshort(0.5f);
            wallDimmer.ParameterPresetFadeTime = SimplSharpDeviceHelper.SecondsToUshort(1.0f);
            wallDimmer.ParameterRaiseLowerRate = SimplSharpDeviceHelper.SecondsToUshort(3.0f);
            wallDimmer.DimmingLoads[1].ParameterDimmerMinLevel = ushort.MinValue;
            wallDimmer.DimmingLoads[1].ParameterDimmerMaxLevel = ushort.MaxValue;
            wallDimmer.ButtonStateChange += new ButtonEventHandler(wallDimmer_ButtonStateChange);
            wallDimmer.LoadStateChange += new LoadEventHandler(wallDimmer_LoadStateChange);

            // registering the gateway after adding all of the devices will register everything at once
            gateway.Register();
        }

        void wallDimmer_LoadStateChange(LightingBase lightingObject, LoadEventArgs args)
        {
            if (EventIds.ContainsKey(args.EventId))
                CrestronConsole.PrintLine("Wall dimmer: {0}", EventIds[args.EventId]);

            // see below
        }

        void lampDimmer_LoadStateChange(LightingBase lightingObject, LoadEventArgs args)
        {
            if (EventIds.ContainsKey(args.EventId))
                CrestronConsole.PrintLine("Lamp dimmer: {0}", EventIds[args.EventId]);

            // use this structure to react to the different events
            switch (args.EventId)
            {
                case LoadEventIds.IsOnEventId:
                    xp.BooleanInput[1].BoolValue = !lampDimmer.DimmingLoads[1].IsOn;
                    xp.BooleanInput[2].BoolValue = lampDimmer.DimmingLoads[1].IsOn;
                    break;

                case LoadEventIds.LevelChangeEventId:
                    xp.UShortInput[1].UShortValue = lampDimmer.DimmingLoads[1].LevelFeedback.UShortValue;
                    break;

                case LoadEventIds.LevelInputChangedEventId:
                    xp.UShortInput[1].CreateRamp(lampDimmer.DimmingLoads[1].Level.RampingInformation);
                    break;

                default:
                    break;
            }
        }

        void wallDimmer_ButtonStateChange(GenericBase device, ButtonEventArgs args)
        {
            CrestronConsole.PrintLine("Button #{0} was {1}", args.Button.Number, args.NewButtonState.ToString());

            switch (args.Button.Number)
            {
                case 1:
                    // do something with button 1
                    break;

                case 2:
                    // etc...
                    break;

                case 3:
                    break;

                case 4:
                    break;

                default:
                    break;
            }
        }

        // this is just a quick way to correlate the event ids to a string name
        Dictionary<int, string> EventIds = new Dictionary<int, string>()
		{
			{1, "LowerEventId"},
			{2, "RaiseEventId"},
			{3, "OnPressEventId"},
			{4, "OnReleaseEventId"},
			{5, "OffPressEventId"},
			{6, "OffReleaseEventId"},
			{7, "LevelChangeEventId"},
			{8, "FullOnPressEventId"},
			{9, "FullOnReleaseEventId"},
			{10, "FastOffEventId"},
			{11, "FastFullOnEventId"},
			{12, "DelayedOffEventId"},
			{13, "IsOnEventId"},
			{14, "IsFullOnEventId"},
			{15, "PresetLoadIsAtEventId"},
			{16, "LastPresetCalledEventId"},
			{17, "NonDimmingFeedbackEventId"},
			{18, "LevelInputChangedEventId"},
			{19, "LoadAttachedEventId"},
			{20, "ToggleEventId"}
		};

        void xp_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            // determine what type of sig has changed
            switch (args.Sig.Type)
            {
                // a bool (digital) has changed
                case eSigType.Bool:
                    // determine if the bool sig is true (digital high, press) or false (digital low, release)
                    if (args.Sig.BoolValue)		// press
                    {
                        // determine what sig (join) number has chagned
                        switch (args.Sig.Number)
                        {
                            case 1:
                                // call the full off method on the load
                                lampDimmer.DimmingLoads[1].FullOff();
                                //    |         |             |
                                //    |         |             |- method on this dimmer
                                //    |         |
                                //    |         |- array of the actual dimmers on this device
                                //    |
                                //    |- the crestron device object
                                break;

                            case 2:
                                // call the full on method on the load
                                lampDimmer.DimmingLoads[1].FullOn();
                                break;

                            case 3:
                                // set the lower value to true (high) which creates a ramp
                                lampDimmer.DimmingLoads[1].Lower.BoolValue = true;
                                break;

                            case 4:
                                // set the raise value to true (high) which creates a ramp
                                lampDimmer.DimmingLoads[1].Raise.BoolValue = true;
                                break;

                            default:
                                break;
                        }
                    }
                    else						// release
                    {
                        // determine what sig (join) number has changed
                        switch (args.Sig.Number)
                        {
                            case 3:
                                // sets the lower value to false (low) to stop the ramp
                                lampDimmer.DimmingLoads[1].Lower.BoolValue = false;
                                break;

                            case 4:
                                // sets the raise value to false (low) to stop the ramp
                                lampDimmer.DimmingLoads[1].Raise.BoolValue = false;
                                break;

                            default:
                                break;
                        }
                    }

                    break;

                // a ushort (analog) has chagned
                case eSigType.UShort:
                    switch (args.Sig.Number)
                    {
                        case 1:
                            // send the slider value to the lamp dimmer
                            lampDimmer.DimmingLoads[1].Level.UShortValue = args.Sig.UShortValue;
                            break;

                        default:
                            break;
                    }
                    break;


                case eSigType.String:
                case eSigType.NA:
                default:
                    break;
            }
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
