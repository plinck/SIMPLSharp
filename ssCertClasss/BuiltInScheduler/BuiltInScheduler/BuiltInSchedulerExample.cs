using System;
using System.Text;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharp.Scheduler;                    // SimplSharpTimerEventInterface.dll

namespace BuiltInScheduler
{
    public delegate void RelayEventHandler(int i);

    public class BuiltInSchedulerExample
    {
        #region Global Variables
        private ScheduledEventGroup myGroup;
        private ScheduledEvent myEvent1, myEvent2;
        public event RelayEventHandler RelayEvent;
        #endregion

        /// <summary>
        /// SIMPL+ can only execute the default constructor. If you have variables that require initialization, please
        /// use an Initialize method
        /// </summary>
        public BuiltInSchedulerExample()
        {
            myGroup = new ScheduledEventGroup("Mike");
            
        }

        public void Clear()
        {
            myGroup.ClearAllEvents();
        }

        public void InitializeStuff()
        {
            myGroup.RetrieveAllEvents();
            // foreach (System.Collections.Generic.KeyValuePair<string, ScheduledEvent> myEvent in myGroup.ScheduledEvents)
            foreach (var _event in myGroup.ScheduledEvents)
            {
                CrestronConsole.PrintLine("Event Read:{0}, {1}:{2}", _event.Value.Name,
                                                _event.Value.DateAndTime.Hour,
                                                _event.Value.DateAndTime.Minute);

            }
            myEvent1 = new ScheduledEvent("Relay 1", myGroup);
            myEvent1.Description = "Relay 1 Desc";
            myEvent1.DateAndTime.SetRelativeEventTime(0, 5);
            myEvent1.Acknowledgeable = true;
            myEvent1.Persistent = true;
            myEvent1.AcknowledgeExpirationTimeout.Hour = 10;
            myEvent1.UserCallBack += new ScheduledEvent.UserEventCallBack(myEvent1_UserCallBack);
            myEvent1.Enable();
            CrestronConsole.PrintLine("Event Created:{0}, {1}:{2}", myEvent1.Name,
                                                myEvent1.DateAndTime.Hour,
                                                myEvent1.DateAndTime.Minute);

            myEvent2 = new ScheduledEvent("Relay 2", myGroup);
            myEvent2.Description = "Relay 2 Desc";
            myEvent2.DateAndTime.SetRelativeEventTime(0, 7);
            myEvent2.Acknowledgeable = true;
            myEvent2.Persistent = false;
            myEvent2.UserCallBack += new ScheduledEvent.UserEventCallBack(myEvent1_UserCallBack);
            myEvent2.Enable();
            CrestronConsole.PrintLine("Event Created:{0}, {1}:{2}", myEvent2.Name,
                                                myEvent2.DateAndTime.Hour,
                                                myEvent2.DateAndTime.Minute);

        }

        public void Ack(int i)
        {
            if (i == 1)
            {
                if (myEvent1 != null)
                    myEvent1.Acknowledge();
            }
            else if (i == 2)
            {
                if (myEvent2 != null)
                    myEvent2.Acknowledge();
            }
        }

        void myEvent1_UserCallBack(ScheduledEvent SchEvent, ScheduledEventCommon.eCallbackReason type)
        {
            if (SchEvent.Name == "Relay 1")
            {
                CrestronConsole.PrintLine("Hitting Relay 1, {0}", DateTime.Now.ToString());
                RelayEvent(1);
            }
            else if (SchEvent.Name == "Relay 2")
            {
                CrestronConsole.PrintLine("Hitting Relay 2, {0}", DateTime.Now.ToString());
                CrestronConsole.PrintLine("Snooze Result: {0}", SchEvent.Snooze(2).ToString());
                RelayEvent(2);
            }
        }

    }
}
