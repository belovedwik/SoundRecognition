namespace DeltaM.DeltaTell.Amd
{
    public class AudioComparerResult
    {
        private readonly string FileName;
        private readonly int Tao;
        private readonly float Percent;
        public AutoAnswerType aaType { get; }

        public AudioComparerResult(string filename, int tao, float percent, AutoAnswerType aat)
        {
            FileName = filename;
            Tao = tao;
            Percent = percent;
            aaType = aat;
        }

        public override string ToString()
        {
            return $"tao:{Tao}, {FileName}, {Percent}%, {aaType}";
        }
    }
}
