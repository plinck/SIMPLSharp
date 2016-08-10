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
        private Swamp24x8 mySwamp;

        public SwampController() { }

        public SwampController(Swamp24x8 pSwamp)
        {
            mySwamp = pSwamp;               // Save crestron swamp in my wrapper object

            mySwamp.SourcesChangeEvent += new SourceEventHandler(mySwamp_SourcesChangeEvent);

            // Register and if fails, get rid of event handler
            if (mySwamp.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
            {
                ErrorLog.Error("mySwamp failed registration. Cause: {0}", mySwamp.RegistrationFailureReason);
            }
            else
            {
                mySwamp.SourcesChangeEvent -= new SourceEventHandler(mySwamp_SourcesChangeEvent);
            }
        }

        void mySwamp_SourcesChangeEvent(object sender, SourceEventArgs args)
        {
            //
        }

        public void SetSourceForRoom(ushort zoneNbr, uint sourceNbr)
        {
            // mySwamp.Zones[zoneNbr].Name.Name = "Family Room";
        }

        public void PrintAllZonesSources()
        {
            foreach (Zone  zone in mySwamp.Zones)
            {
                CrestronConsole.PrintLine("Zone Name:{0},Zone Nbr:{1},Source Name:{2},Source Number:{3}",
                                            zone.Name.Name, zone.Name.Number, zone.Source.Name, zone.Source.Number);
            }
        }
    }
}