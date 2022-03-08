
using CompareAudioWav.Entities;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CompareAudioWav.AudioAnalizers
{
    public class AudioAnalizer
    {
        private int _bytesPerMillisecond;
        private const int _callDuration = 1000;
        public int counter = 0;
        private const string _pathDirectory = @"Audio\";
        private const string _pathTmpDirectory = @"Tmp\";



        private int bytesPerMillisecond { get => _bytesPerMillisecond; set => _bytesPerMillisecond = value; }
        public AudioAnalizer(int bytesPerMillisecond)
        {
            this.bytesPerMillisecond = bytesPerMillisecond;
            if (!Directory.Exists(_pathDirectory))
            {
                Directory.CreateDirectory(_pathDirectory);
            }
            if (!Directory.Exists(_pathDirectory))
            {
                Directory.CreateDirectory(_pathTmpDirectory);
            }
        }
        public IEnumerable<float> getSample(AudioFileReader stream)
        {
            int buf_len = stream.WaveFormat.AverageBytesPerSecond;
            var buffer = new float[buf_len];

            while (stream.Position < stream.Length)
            {
                var count = stream.Read(buffer, 0, buffer.Length);
                for (int i = 0; i < count; i += 2)
                    yield return buffer[i];
            }
        }
        public float[] getData(string path)
        {
            using (AudioFileReader audioStream = new AudioFileReader(path))
            {
                float[] data = getSample(audioStream).ToArray();
                return data;
            }
        }


        public TimeSpan detectSilence(float[] samples, int blockAlign, int streamLength)
        {

            if (samples.Length == 0)
            {
                return new TimeSpan();
            }
            int k = streamLength / samples.Length;
            int sampleLength = samples.Length;
            int skipElements = 0;
            int generalSkipItems = 0;
            TimeSpan generalSkipTime = new TimeSpan();
            while (counter < 8)
            {
                counter++;
                float[] localSamples = new float[sampleLength - generalSkipItems];
                Array.Copy(samples, generalSkipItems, localSamples, 0, localSamples.Length);
                generalSkipTime = generalSkipTime.Add(getTimeTrimRecurse(localSamples, generalSkipTime, k, blockAlign, ref skipElements));
                generalSkipItems = generalSkipItems + skipElements;
            }

            return generalSkipTime;
        }
        
        private TimeSpan getTimeTrimRecurse(float[] samples, TimeSpan time, int k, int blockAlign, ref int skipElements)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                if (Math.Abs(samples[i]) > 0.01f)
                {
                    int countms = (i / bytesPerMillisecond) * k;
                    countms = countms - countms % blockAlign;
                    time = new TimeSpan(0, 0, 0, 0, countms);
                    time = time.Add(detectCall(samples, blockAlign, bytesPerMillisecond, i, k, time, ref skipElements));
                    skipElements = skipElements + i;
                    return time;
                }
            }
            return new TimeSpan();
        }
        public TimeSpan detectCall(float[] samples, int blockAlign, int bytePerMiliSeconds, int index, int k, TimeSpan skipTime, ref int skipElemnts)
        {
            try
            {
                int countCallBytes = (_callDuration * bytePerMiliSeconds) / k;
                skipElemnts = 0;
                float[] call = new float[countCallBytes];
                Array.Copy(samples, index, call, 0, countCallBytes);
                float max = call.Max();
                float min = call.Min();
                float a = (max - min) / 2;
                float s = a * a;
                float lower = 0.20f;
                float higher = 0.30f;
                if (s > lower && s < higher)
                {
                    skipElemnts = countCallBytes;
                    return new TimeSpan(0, 0, 1);
                }
                else
                {
                    //Console.WriteLine(s);
                    return new TimeSpan();
                }
            }
            catch (System.ArgumentException)
            {
                return new TimeSpan();
            }
        }

        public IEnumerable<Word> GetSamlpesSilence(float[] samples)
        {
            int counter = 0;
            int endWordPosition = 0;
            List<Word> words = new List<Word>();
            Word firstWord = new Word();
            firstWord.StartWord = 0;
            firstWord.EndWord = getEndWord(samples, ref endWordPosition);
            //word.Data = getDataWord(samples,firstWord.StartWord,firstWord.EndWord);
            words.Add(firstWord);
            counter++;
            for (int i = endWordPosition; i < samples.Length;)
            {
                if (Math.Abs(samples[i]) > 0.1)
                {
                    counter++;
                    Word word = new Word();
                    word.StartWord = i;
                    word.EndWord = getEndWord(samples, ref i);
                    //word.Data = getDataWord(samples, word.StartWord, word.EndWord);
                    words.Add(word);
                }
                else
                {
                    i++;
                }
            }
            return words;
        }
        public int getEndWord(float[] samples, ref int endWordPosition)
        {
            for (int i = endWordPosition; i < samples.Length; i++)
            {
                if (Math.Abs(samples[i]) < 0.1)
                {
                    endWordPosition = i;
                    return i;
                }
            }
            return samples.Length;
        }




        public string NormilizeAudio(string path)
        {
            string normilizeAudio;
            float max = 0;
            using (var reader = new AudioFileReader(path))
            {
                // find the max peak
                float[] buffer = new float[reader.WaveFormat.SampleRate];
                int read;
                do
                {
                    read = reader.Read(buffer, 0, buffer.Length);
                    for (int n = 0; n < read; n++)
                    {
                        var abs = Math.Abs(buffer[n]);
                        if (abs > max) max = abs;
                    }
                } while (read > 0);
                //Console.WriteLine($"Max sample value: {max}");
                try
                {

                    if (max == 0 || max > 1.0f)
                    {
                        normilizeAudio = $@"{_pathDirectory}Normilized{Path.GetFileNameWithoutExtension(path)}.wav";
                        return normilizeAudio;
                    }


                    // rewind and amplify
                    reader.Position = 0;
                    reader.Volume = 1.0f / max;
                    normilizeAudio = $@"{_pathDirectory}Normilized{Path.GetFileNameWithoutExtension(path)}.wav";
                    WaveFileWriter.CreateWaveFile16(normilizeAudio, reader);
                }
                catch (InvalidOperationException)
                {
                    normilizeAudio = $@"{_pathDirectory}Normilized{Path.GetFileNameWithoutExtension(path)}.wav";
                    WaveFileWriter.CreateWaveFile16(normilizeAudio, reader);
                }

                return normilizeAudio;
            }

        }

        public List<AudioSetting> getListWaves(string _pathDirectory)
        {
            if (!Directory.Exists(_pathDirectory))
            {
                Console.WriteLine("List is empty");
                return new List<AudioSetting>();
            }
            DirectoryInfo dir = new DirectoryInfo(_pathDirectory);
            List<AudioSetting> audios = new List<AudioSetting>();

            foreach (var item in dir.GetFiles("*.wav"))
            {
                audios.Add(new AudioSetting()
                {
                    Audio = new AudioRecordVaw() { Name = item.Name, FullPath = item.FullName },
                    AudioFile = new WaveFileReader(item.FullName)
                });
            }
            return audios;
        }
        /// <summary>
        /// Пока хз что тут будет
        /// </summary>
        private static void useFFT()
        {
            //NormilizeAudio(ListAudio[0].Audio.FullPath);
            //Complex[] firstAudio = new Complex[dataFisrtAudio.Length];
            //Complex[] secondAudio = new Complex[dataSecondAudio.Length];
            //for (int i = 0; i < firstAudio.Length; i++)
            //{
            //    firstAudio[i].X = dataFisrtAudio[i];
            //    firstAudio[i].Y = 0;
            //}
            //FastFourierTransform.FFT(true, firstAudio.Length / 2, firstAudio);
        }
    }
}
