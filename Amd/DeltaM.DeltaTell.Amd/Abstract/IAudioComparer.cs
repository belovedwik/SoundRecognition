namespace DeltaM.DeltaTell.Amd.Abstract
{
    public interface IAudioComparer 
    {
         bool HasEquivalent(string filename, out AudioComparerResult res, bool writeLog = false);
    }
}
