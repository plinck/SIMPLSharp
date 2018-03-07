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
    // ButtonInterfaceContoller - Handles Button functionality
    // ***************************************************************
    class ButtonInterfaceController
    {
        public ButtonInterfaceController() { }

        // Setup all the joins for this Keypad
        public ButtonInterfaceController(C2nCbdP myKP)  // overloaded constructor
        {
            myKP.Button[1].UserObject = new System.Action<Button>((p) => this.BPressUp(p));
            myKP.Button[2].UserObject = new System.Action<Button>((p) => this.BPressDn(p));
        }

        public void BPressUp(Button btn)
        {
            if (btn.State == eButtonState.Pressed)
                PressUp_Pressed(btn.Number);
            else if (btn.State == eButtonState.Released)
                PressUp_Released(btn.Number);
        }

        public void PressUp_Pressed(uint i)
        {
            if (GV.MyControlSystem.SupportsVersiport && GV.MyControlSystem.NumberOfVersiPorts >= i)
            {
                Versiport myVersiport = GV.MyControlSystem.VersiPorts[i];
                myVersiport.DigitalOut = true;
            }

            if (GV.MyControlSystem.SupportsIROut && GV.MyControlSystem.NumberOfIROutputPorts >= 1)
            {
                IROutputPort myIRPort = GV.MyControlSystem.IROutputPorts[1];
                myIRPort.Press("UP_ARROW");
            }

            if (GV.MyControlSystem.SupportsComPort && GV.MyControlSystem.NumberOfComPorts >= i)
            {
                ComPort myComPort = GV.MyControlSystem.ComPorts[i];
                myComPort.Send("Test transmition, please ignore");
            }
        }

        public void PressUp_Released(uint i)
        {
            if (GV.MyControlSystem.SupportsVersiport && GV.MyControlSystem.NumberOfVersiPorts >= i)
            {
                Versiport myVersiport = GV.MyControlSystem.VersiPorts[i];
                myVersiport.DigitalOut = false;
            }

            if (GV.MyControlSystem.SupportsIROut && GV.MyControlSystem.NumberOfIROutputPorts >= 1)
            {
                IROutputPort myIRPort = GV.MyControlSystem.IROutputPorts[1];
                myIRPort.Release();
            }

            if (GV.MyControlSystem.SupportsComPort && GV.MyControlSystem.NumberOfComPorts >= i)
            {
                ComPort myComPort = GV.MyControlSystem.ComPorts[i];
                myComPort.Send(" ");
            }
        }

        public void BPressDn(Button btn)
        {
            if (btn.State == eButtonState.Pressed)
                PressDn_Pressed(btn.Number);
            else if (btn.State == eButtonState.Released)
                PressDn_Released(btn.Number);
        }

        public void PressDn_Pressed(uint i)
        {
            if (GV.MyControlSystem.SupportsVersiport && GV.MyControlSystem.NumberOfVersiPorts >= i)
            {
                Versiport myVersiport = GV.MyControlSystem.VersiPorts[i];
                myVersiport.DigitalOut = true;
            }

            if (GV.MyControlSystem.SupportsIROut && GV.MyControlSystem.NumberOfIROutputPorts >= 1)
            {
                IROutputPort myIRPort = GV.MyControlSystem.IROutputPorts[1];
                myIRPort.Press("DN_ARROW");
            }

            if (GV.MyControlSystem.SupportsComPort && GV.MyControlSystem.NumberOfComPorts >= i)
            {
                ComPort myComPort = GV.MyControlSystem.ComPorts[i];
                myComPort.Send("\n");
            }
        }

        public void PressDn_Released(uint i)
        {
            if (GV.MyControlSystem.SupportsVersiport && GV.MyControlSystem.NumberOfVersiPorts >= i)
            {
                Versiport myVersiport = GV.MyControlSystem.VersiPorts[i];
                myVersiport.DigitalOut = false;
            }

            if (GV.MyControlSystem.SupportsIROut && GV.MyControlSystem.NumberOfIROutputPorts >= 1)
            {
                IROutputPort myIRPort = GV.MyControlSystem.IROutputPorts[1];
                myIRPort.Release();
            }

            if (GV.MyControlSystem.SupportsComPort && GV.MyControlSystem.NumberOfComPorts >= i)
            {
                ComPort myComPort = GV.MyControlSystem.ComPorts[i];
                // myComPort.Send("\n");
            }

        }
    } // class
}