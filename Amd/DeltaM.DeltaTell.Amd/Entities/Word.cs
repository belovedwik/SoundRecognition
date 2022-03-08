using System.Collections.Generic;

namespace CompareAudioWav.Entities
{
    public class Word
    {
        public int StartWord { get; set; }
        public IEnumerable<float> Data { get; set; }
        public int EndWord { get; set; }
    }
}
