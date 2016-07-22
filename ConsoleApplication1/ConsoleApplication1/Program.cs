using System;

namespace ConsoleApplication1
{
    namespace SimpleEvent
    {
        public class EventTest
        {
            private int value;

            // define delegate
            public delegate void NumManipulationHandler();
            // define event
            public event NumManipulationHandler ChangeNum;
            
            protected virtual void OnNumChanged()
            {
                if (ChangeNum != null)
                    ChangeNum();
                else
                     Console.WriteLine("Event fired!");
            }

            public EventTest(int n)
            {
                SetValue(n);
            }

            public void SetValue(int n)
            {
                if (value != n)
                {
                    value = n;
                    OnNumChanged();
                }
            }
        }
        
        class classReadOnlyAge
        {
            readonly int myReadOnlyYear;  // declare year as read-only so can only be set in constructor

            classReadOnlyAge(int year)
            {
                myReadOnlyYear = year;
            }

            void ChangeYear()
            {
                // myReadOnlyYear = 1965; // Compile error if uncommented.
            }
        }

        class classReadWriteAge
        {
            int myReadWriteYear;  // declare year as normal

            classReadWriteAge(int year)
            {
                myReadWriteYear = year;
            }

            void ChangeYear()
            {
                myReadWriteYear = 1965; // OK.
            }
        }

        class RangeClass
        {
            uint total = 0;

            public uint AddRange(uint iFrom, uint iTo)
            {
                for (uint a = iFrom; a <= iTo; a = a + 1)
                {
                    total = total + a;
                }
                return (total);
            }
        }

        public class MainClass
        {
            public static void Main()
            {
                EventTest e = new EventTest(5);
                e.SetValue(7);
                e.SetValue(11);
                Console.ReadKey();

                RangeClass myRangeObject = new RangeClass();
                uint myTotal;

                // Question #15 - jhow to initialize a strng without needing backslash each \ character - use the @ modifier
                //Initialize with a regular string literal.
                string oldPath = "c:\\Program Files\\Microsoft Visual Studio 8.0";
                // Initialize with a verbatim string literal.
                string newPath = @"c:\Program Files\Microsoft Visual Studio 9.0";

                myTotal = myRangeObject.AddRange(1, 4);
                Console.WriteLine("Total from {0} to {1} is: {2}", 1, 4, myTotal);
                Console.ReadKey();
            }
        }
    }
}
