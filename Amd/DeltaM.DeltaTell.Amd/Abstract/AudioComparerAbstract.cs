using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DeltaM.DeltaTell.Amd.Abstract
{
    public abstract class AudioComparerAbstract
    {
        #region Members
        public volatile List<KeyValuePair<AutoAnswerFile, float[]>> EnvelopeBaseList = new List<KeyValuePair<AutoAnswerFile, float[]>>();
        public bool showConsoleDebug = false;
        public bool detectSilence = false;
        public object sync = new object();
        #endregion Members

        #region Properties
        private int _envelopeStep { get; set; }
        private float _percentageMatch { get; set; }
        public string BaseAudioDir { get; set; }
        #endregion Properties

        #region ctor
        public AudioComparerAbstract(float percentageMatch = 0.9f, int envelopeStep = 20, LogTarget logger = LogTarget.Console)
        {
            LogHelper.Init(logger);
            _percentageMatch = percentageMatch;
            _envelopeStep = envelopeStep;
        }
        #endregion

        #region Methods
        public bool AddToBase(string baseFileName, AutoAnswerType aaType = AutoAnswerType.AutoAnswer)
        {
            TimeSpan time;

            var item = new KeyValuePair<AutoAnswerFile, float[]>(new AutoAnswerFile(baseFileName, aaType), getEnvelope(baseFileName, out time));
            EnvelopeBaseList.Add(item);

            return true;
        }

        public void ClearBase()
        {
            EnvelopeBaseList.Clear();
        }

        private float[] getEnvelope(string fileName, out TimeSpan time, AutoDetectMethod amdAlg = AutoDetectMethod.Hard)
        {
            time = TimeSpan.Zero;

            if (!File.Exists(fileName))
                return new float[0];

            float[] result;

            using (var stream = new AudioFileReader(fileName))
            {
                result = GetEnvelope2(GetSamples(stream).ToList(), _envelopeStep, amdAlg).ToArray();
                time = stream.TotalTime;
            }

            return result;
        }

        private bool checkResult(int t, ref int maxTao, float result, ref float maxPercent, string baseFilename)
        {
            maxTao = result > maxPercent ? t : maxTao;
            maxPercent = Math.Max(maxPercent, result);

            if (_percentageMatch <= result)
            {
                LogHelper.AppendLine($" .. OK!! tao:{t}; {maxPercent:P}");

                return true;
            }

            return false;
        }

        private bool checkResult75(int t, ref int maxTao, float result, ref float maxPercent, string baseFilename)
        {
            maxTao = result > maxPercent ? t : maxTao;
            maxPercent = Math.Max(maxPercent, result);

            Debug.WriteLine("tao2:{1}, res2:{0:P}", result, t);

            if (_percentageMatch * 93 / 100 <= result)
            {
                LogHelper.AppendLine($" .. OK2!! tao:{t}; {maxPercent:P}");

                return true;
            }
            return false;
        }

        private bool DetectSilence(float[] envIn, TimeSpan time, ref AudioComparerResult res)
        {
            if (!detectSilence || envIn.Length > 10 || time.TotalSeconds < 2.5 || (envIn.Length > 0 && envIn.Max() > 0.2)) // this is not Silence
                return false;

            res = new AudioComparerResult("Silence", 0, 0, AutoAnswerType.Silence);
            return true;
        }

        IEnumerable<float> GetSamples(AudioFileReader stream)
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

        IEnumerable<float> GetEnvelope(IEnumerable<float> samples, int step, bool trimStartEnd = false)
        {
            var sum = 0d;
            var counter = 0;

            foreach (var sample in samples)
            {
                /*
            если в начале тишина, sample будет близко к 0, 
            игнорим эти значения, пока sample станет немного больше 0
            */
                if (trimStartEnd && sample < 0.005)
                    continue;
                else
                    trimStartEnd = false;

                sum += Math.Abs(sample);
                counter++;

                if (counter >= step)
                {
                    yield return (float)sum;
                    sum = 0;
                    counter = 0;
                }
            }
        }

        IEnumerable<float> GetEnvelope2(IEnumerable<float> samples, int step, AutoDetectMethod alg)
        {
            List<float> res = new List<float>(samples.Count());

            var trimStartEnd = true;

            var sum = 0f;
            var counter = 0;

            foreach (var sample in samples)
            {

                if (trimStartEnd && sample < 0.01)
                    continue;
                trimStartEnd = false;

                if (sample > 0)
                    sum = Math.Max(sum, sample); //Math.Abs(sample);
                                                 //sum = sample; //Math.Abs(sample);
                counter++;

                if (counter >= step)
                {
                    res.Add(sum);
                    sum = 0;
                    counter = 0;
                }
            }

            List<float> result = new List<float>(res.Count());

            res.Reverse(0, res.Count);
            trimStartEnd = true;

            foreach (var rev in res)
            {
                if (trimStartEnd && rev < 0.01)
                    continue;
                trimStartEnd = false;
                result.Add(rev);
            }
            result.Reverse(0, result.Count());

            /*
            * Удаление гудков
            * */
            if (alg == AutoDetectMethod.Partial)
            {

            }
            return result;
        }
        #endregion

    }
}
