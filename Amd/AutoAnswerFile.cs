
namespace DeltaM.DeltaTell.Amd
{
    public class AutoAnswerFile
    {
        public AutoAnswerFile(string path, AutoAnswerType answerType = AutoAnswerType.AutoAnswer)
        {
            FilePath = path;
            AnswerType = answerType;
        }

        public string FilePath { get; set; }
        public AutoAnswerType AnswerType { get; set; }
    }
}