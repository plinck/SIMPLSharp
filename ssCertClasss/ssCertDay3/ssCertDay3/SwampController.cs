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
    public class SwampController
    {
        const Int32 C_SWAMP_IPID = 0x99;
        private Swamp24x8 mySwamp;

        public SwampController() { }

        public SwampController(ControlSystem cs)
        {
            mySwamp = new Swamp24x8(C_SWAMP_IPID, cs);

            mySwamp.SourcesChangeEvent += new SourceEventHandler(mySwamp_SourcesChangeEvent);
            mySwamp.ZoneChangeEvent += new ZoneEventHandler(mySwamp_ZoneChangeEvent);

            // Register and if fails, get rid of event handler
            if (mySwamp.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
            {
                ErrorLog.Error("mySwamp failed registration. Cause: {0}", mySwamp.RegistrationFailureReason);
            }
            else
            {
                mySwamp.SourcesChangeEvent -= new SourceEventHandler(mySwamp_SourcesChangeEvent);
                mySwamp.ZoneChangeEvent -= new ZoneEventHandler(mySwamp_ZoneChangeEvent);
                Initialize();
            }
        }

        private void Initialize()
        {
            mySwamp.TemperatureReportingEnable();
            mySwamp.AllZonesDoorbellSource.UShortValue = 16;
            mySwamp.Zones[1].Name.StringValue = "Main Guest Bedroom";
            mySwamp.Zones[2].Name.StringValue = "Master Bedroom";
            mySwamp.Zones[3].Name.StringValue = "Upper Deck";
            mySwamp.Zones[4].Name.StringValue = "Dining Room";
            mySwamp.Zones[5].Name.StringValue = "Basement Rec Room";
            mySwamp.Zones[6].Name.StringValue = "Workout Room";
            mySwamp.Zones[7].Name.StringValue = "Kitchen";
            mySwamp.Zones[8].Name.StringValue = "Library";
            mySwamp.Sources[1].Name.StringValue = "VCR51 - Apple TV";
            mySwamp.Sources[2].Name.StringValue = "SAT52 - Shared TIVO";
            mySwamp.Sources[3].Name.StringValue = "<unused>";
            mySwamp.Sources[4].Name.StringValue = "SAT53 - Shared TIVO";
            mySwamp.Sources[5].Name.StringValue = "SAT51 - Shared TIVO";
            mySwamp.Sources[6].Name.StringValue = "DVD51 - Shared KScape";
            mySwamp.Sources[7].Name.StringValue = "CAM1 - CAM1";
            mySwamp.Sources[8].Name.StringValue = "<unused>";
            mySwamp.Sources[16].Name.StringValue = "Doorbell Chime";

            foreach (Zone zone in mySwamp.Zones)
            {
                zone.Balance.UShortValue = 0;           // -50% to +50%
                zone.Bass.UShortValue = 1;              // -12dB to +12dB  -- This is UShort - how do I set UShort to negative value?
                zone.CrestronDRCOff();
                zone.DoorbellEnableOn();
                zone.DoorbellVolume.UShortValue = SimplSharpDeviceHelper.PercentToUshort(60);
                zone.LoudnessOn();
                zone.MaxVolume.UShortValue = SimplSharpDeviceHelper.PercentToUshort(100);
                zone.MinVolume.UShortValue = SimplSharpDeviceHelper.PercentToUshort(0);
                zone.MonoOff();
                zone.MuteOff();
                zone.Source.UShortValue = 0;
                zone.StartupVolume.UShortValue = SimplSharpDeviceHelper.PercentToUshort(40);
                zone.Treble.UShortValue = 0;            // -12dB to +12dB  -- This is UShort - how do I set UShort to negative value?
            }
        }


        public void SetSourceForRoom(ushort zoneNbr, ushort sourceNbr)
        {
            mySwamp.Zones[zoneNbr].Source.UShortValue = sourceNbr;
        }

        public void PrintAllZonesSources()
        {
            foreach (Zone zone in mySwamp.Zones)
            {
                CrestronConsole.PrintLine("Zone Name:{0},Zone Nbr:{1},Source Number:{2}",
                                            zone.Name.StringValue,
                                            zone.Number,
                                            zone.Source.UShortValue);
            }
            foreach (Source src in mySwamp.Sources)
            {
                CrestronConsole.PrintLine("Source Name:{0},Source Number:{1}",
                                            src.Name.StringValue,
                                            src.Number);
            }
        }
        #region event handlers
        void mySwamp_SourcesChangeEvent(object sender, SourceEventArgs args)
        {
            int e = args.EventId;
            Source src = args.Source;
            
            // EventIds areCrestron.SimplSharpPro.AudioDistribution.SourceEventIds;
            // NameFeedbackEventId
            if (e == SourceEventIds.NameFeedbackEventId)
            {
                // Source Name Changed?
            }
            else if (e == SourceEventIds.CompensationFeedbackEventId)
            {
                // Who Cares
            }

        }
        
        void mySwamp_ZoneChangeEvent(object sender, ZoneEventArgs args)
        {
            int e = args.EventId;
            Zone z = args.Zone;

            // Crestron.SimplSharpPro.AudioDistribution.Swamp.AllZonesDoorbellAudioOnFeedbackEventId?
            // EventIds are in Crestron.SimplSharpPro.AudioDistribution.ZoneEventIds

            if (e == ZoneEventIds.SourceFeedbackEventId)
             {
                 // Source Changed?
             }
            else if (e == ZoneEventIds.DoorbellEnableFeedbackEventId)
             {
                 // Doorbell enable
             }
        }
        #endregion
   }
}