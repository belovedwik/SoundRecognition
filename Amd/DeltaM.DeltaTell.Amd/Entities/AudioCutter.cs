using NAudio.Wave;
using System;
using System.IO;

namespace CompareAudioWav.Entities
{
    public class AudioСutter
    {
        private const string outPath = @"TrimAudio\";
        public string resultPath;
        public AudioСutter()
        {
            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }
        }

        public string TrimWavFile(string inPath, TimeSpan cutFromStart, TimeSpan cutFromEnd)
        {
            resultPath = $"{outPath}{Path.GetFileName(inPath)}";
            using (WaveFileReader reader = new WaveFileReader(inPath))
            {
                using (WaveFileWriter writer = new WaveFileWriter(resultPath, reader.WaveFormat))
                {
                    int bytesPerMillisecond = reader.WaveFormat.AverageBytesPerSecond / 1000;

                    int startPos = (int)cutFromStart.TotalMilliseconds * bytesPerMillisecond;
                    startPos = startPos - startPos % reader.WaveFormat.BlockAlign;

                    int endBytes = (int)cutFromEnd.TotalMilliseconds * bytesPerMillisecond;
                    endBytes = endBytes - endBytes % reader.WaveFormat.BlockAlign;
                    int endPos = (int)reader.Length - endBytes;
                    return TrimWavFile(reader, writer, startPos, endPos);
                }
            }
        }

        private string TrimWavFile(WaveFileReader reader, WaveFileWriter writer, int startPos, int endPos)
        {
            reader.Position = startPos;
            byte[] buffer = new byte[1024];
            while (reader.Position < endPos)
            {
                int bytesRequired = (int)(endPos - reader.Position);
                if (bytesRequired > 0)
                {
                    int bytesToRead = Math.Min(bytesRequired, buffer.Length);
                    int bytesRead = reader.Read(buffer, 0, bytesToRead);
                    if (bytesRead > 0)
                    {
                        writer.Write(buffer, 0, bytesRead);
                    }
                }
            }
            return resultPath;
        }
    }
}
