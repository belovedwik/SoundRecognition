using NAudio.Wave;

namespace CompareAudioWav.Entities
{
    public class AudioSetting
    {
        public AudioRecordVaw Audio { get; set; }
        public WaveFileReader AudioFile { get; set; }
    }
}
