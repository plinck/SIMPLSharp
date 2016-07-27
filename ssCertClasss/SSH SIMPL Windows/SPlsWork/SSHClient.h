namespace SSHClient;
        // class declarations
         class SSHClientDevice;
     class SSHClientDevice 
    {
        // class delegates
        delegate FUNCTION SerialDelegate ( SIMPLSHARPSTRING strValue );

        // class events

        // class functions
        FUNCTION parseData ( STRING str );
        INTEGER_FUNCTION Connect ( STRING Host , INTEGER Port , STRING UserName , STRING Password );
        INTEGER_FUNCTION SendCommand ( STRING strCommand );
        STRING_FUNCTION ToString ();
        SIGNED_LONG_INTEGER_FUNCTION GetHashCode ();

        // class variables
        INTEGER __class_id__;

        // class properties
        DelegateProperty SerialDelegate SerialDataReceived;
    };

