namespace DeltaM.DeltaTell.Amd
{
    public class AutodetectSettings
    {
        public AutodetectConnection BeforeConnection { get; set; } = new AutodetectConnection();
        public AutodetectConnection AfterConnection { get; set; } = new AutodetectConnection();

    }
}
