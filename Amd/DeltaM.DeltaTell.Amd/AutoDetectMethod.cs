namespace DeltaM.DeltaTell.Amd
{
    public enum AutoDetectMethod
    {
        //Normal = 1,
        Hard = 2,
        Partial = 3,
        Partial75 = 4,
        //HardNoSilence = 5,
        MSSpeech = 6,
        SystemSpeechBefore = 7,
        MSSpeechBefore = 8
    }

    public enum SilenceDetectMethod
    {
        None, //no silence detection needed
        Default, //system default silence detection
        MSSpeech, //silence detection with MSSpeech library
        SystemSpeech //silence detection with SystemSpeech library
    }
}
