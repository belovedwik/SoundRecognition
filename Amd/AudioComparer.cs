using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NAudio.Wave;

namespace DeltaM.DeltaTell.Amd
{
    public class AudioComparer
    {

        public volatile List<KeyValuePair<AutoAnswerFile, float[]>> EnvelopeBaseList = new List<KeyValuePair<AutoAnswerFile, float[]>>();

        public bool showConsoleDebug = false;
        public bool detectSilence = false;
        private readonly object sync = new object();
      
        int _envelopeStep { get; set; }
        float _percentageMatch { get; set; }
        public string BaseAudioDir { get; set; }
        public bool CanUseMsSpeech = false;

        //public float[] EnvelopeIn { get; set; }

        public AudioComparer(float percentageMatch = 0.9f, int envelopeStep = 20, LogTarget logger = LogTarget.Console)
        {
            LogHelper.Init(logger);
            _percentageMatch = percentageMatch;
            _envelopeStep = envelopeStep;

        }

        public bool AddToBase(string baseFileName, AutoAnswerType aaType = AutoAnswerType.AutoAnswer)
        {
            TimeSpan time;

            var item = new KeyValuePair<AutoAnswerFile, float[]>(new AutoAnswerFile(baseFileName, aaType), getEnvelope(baseFileName, out time));
            EnvelopeBaseList.Add(item);

            return true;
        }

        /// <summary>
        /// Проверяет в цикле на совпадение входящего файла с файлами загруженынми ранее.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>

        public void ClearBase()
        {
            EnvelopeBaseList.Clear();
        }

        private float[] getEnvelope(string fileName, out TimeSpan time, AMDAlgorithm amdAlg = AMDAlgorithm.Hard)
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

        /*
        private float[] GetSamplesFromFile(string FileName, bool trimSilence = true)
        {
            if (!File.Exists(FileName))
                return new float[0];

            float[] result;
            using (var stream = new AudioFileReader(FileName))
            {
                result = GetEnvelope2(GetSamples(stream).ToArray(), _envelopeStep, AMDAlgorithm.Normal).ToArray();
            }
            return result;
        }
        */

        private StringBuilder getGraph(string fileName, bool writeLog, float[] envelopeIn)
        {
            var sbGraph = new StringBuilder();

            if (writeLog)
            {
                if (!Directory.Exists("graph"))
                    Directory.CreateDirectory("graph");

                foreach (var val in envelopeIn)
                    sbGraph.AppendLine(val.ToString());

                lock (sync)
                    File.WriteAllText("graph/" + Path.GetFileName(fileName) + ".txt", sbGraph.ToString());

                sbGraph.Clear();
            }
            return sbGraph;
        }

        private void getBaseGraph(StringBuilder sbGraph, float[] envBase, string baseFilename, bool writeLog)
        {
            if (showConsoleDebug)
            {
                var shortName = baseFilename.Length > 40 ? baseFilename.Substring(0, 40) : baseFilename;
                LogHelper.Append($"{shortName,42}[{envBase.Length}]");
            }

            if (writeLog)
            {
                foreach (var val in envBase)
                {
                    sbGraph.AppendLine(val.ToString());
                }
                lock (sync)
                    File.WriteAllText("graph/" + baseFilename + ".txt", sbGraph.ToString());

                sbGraph.Clear();
            }

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
        private bool DetectSilenceMSSpeech(float[] samples, long length, int duration)
        {
            return samples.Length > 0 && samples.Max() < 0.2 && (length > 46080 || duration > 2500);
        }




        /// <summary>
        /// Сравнение файла со списком загруженных файлов
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="hardCompare"></param>
        /// <returns></returns>
        public bool HasEquivalent(string filename, out AudioComparerResult res, bool writeLog = false)
        {
            res = null;
            TimeSpan time;

            var envInBase = getEnvelope(filename, out time);

            if (showConsoleDebug)
                LogHelper.AppendLine($"Compare file: {Path.GetFileName(filename)} [{envInBase.Length}]");

            if (DetectSilence(envInBase, time, ref res))
                return true;

            var sbGraph = getGraph(filename, writeLog, envInBase);

            foreach (var EnvBase in EnvelopeBaseList)
            {

                var envInLen = envInBase.Length;
                var envBaseLen = EnvBase.Value.Length;
                var minLen = Math.Min(envInLen, envBaseLen);
                string baseFilename = Path.GetFileName(EnvBase.Key.FilePath);

                getBaseGraph(sbGraph, EnvBase.Value, baseFilename, writeLog);

                /*
                 если размер файла слишком маленький - игнорим
                 или входящая запись слишком херовенькая ...
                 */
                if (minLen < 230 || envInBase.Max() < 0.1)
                {
                    if (showConsoleDebug)
                        LogHelper.AppendLine($"..len:{minLen} < minlen");
                    continue;
                }

                float maxPercent = 0f;
                int maxTao = 0;

                int leftX = 0; // базовое смещение по оси Х 
                if (envBaseLen > envInLen)
                    leftX = minLen * 2 / 10; // Смещение влево  leftX   -20% ролика

                var rightX = minLen * 3 / 10; // мещение вправо rightX  +30% ролика

                for (int t = -leftX; t < rightX; t += 1)
                {
                    var result = Correlation_NM2(t, EnvBase.Value, envInBase);

                    if (!checkResult(t, ref maxTao, result, ref maxPercent, baseFilename))
                        continue;
                    res = new AudioComparerResult(baseFilename, t, maxPercent, EnvBase.Key.AnswerType);
                    return true;
                }

                if (showConsoleDebug)
                    LogHelper.AppendLine($" ..false, {maxPercent:P} tao:{maxTao}");
            }

            LogHelper.AppendLine($" TOTAL - false");

            return false;
        }

        public bool HasEquivalentHard(string filename, out AudioComparerResult res, bool writeLog = false)
        {
            res = null;
            TimeSpan time;

            var envIn = getEnvelope(filename, out time, AMDAlgorithm.Hard);
            var envInLen = envIn.Length;

            if (showConsoleDebug)
                LogHelper.AppendLine($"Compare file: {Path.GetFileName(filename)} [{envInLen}]");

            if (DetectSilence(envIn, time, ref res))
                return true;

            var sbGraph = getGraph(filename, writeLog, envIn);
            var baseList = EnvelopeBaseList.ToList();

            foreach (var EnvBase in baseList)
            {
                var envBaseLen = EnvBase.Value.Length;
                var minLen = Math.Min(envInLen, envBaseLen);
                var maxLen = Math.Max(envInLen, envBaseLen);
                string baseFilename = Path.GetFileName(EnvBase.Key.FilePath);

                getBaseGraph(sbGraph, EnvBase.Value, baseFilename, writeLog);
                /*
                 если размер файла слишком маленький - игнорим
                 или входящая запись слишком херовенькая ...
                 */
                if (minLen < 230 || envIn.Max() < 0.1)
                {
                    if (showConsoleDebug)
                        LogHelper.AppendLine($"..len:{minLen} < minlen");
                    continue;
                }

                float maxPercent = 0f;
                int maxTao = 0;



                var leftX = minLen * 10 / 100; // Смещение влево  leftX   -10% ролика
                var rightX = minLen * 15 / 100; // мещение вправо rightX  +15% ролика

                for (int t = -leftX; t < maxLen - minLen + rightX; t += 1)
                {

                    var result = Correlation_Hard(t, EnvBase.Value, envIn);

                    if (!checkResult(t, ref maxTao, result, ref maxPercent, baseFilename)) continue;
                    res = new AudioComparerResult(baseFilename, t, maxPercent, EnvBase.Key.AnswerType);
                    return true;
                }

                if (showConsoleDebug)
                    LogHelper.AppendLine($" .. false, {maxPercent:P} tao:{maxTao}");
            }

            LogHelper.AppendLine($" TOTAL - false");

            return false;
        }

        public bool HasEquivalentPartial75(string filename, out AudioComparerResult res, bool writeLog = false)
        {
            res = null;
            TimeSpan time;

            var envInBase = getEnvelope(filename, out time, AMDAlgorithm.Partial);

            if (showConsoleDebug)
                LogHelper.AppendLine($"Compare file: {Path.GetFileName(filename)} [{envInBase.Length}]");

            if (DetectSilence(envInBase, time, ref res))
                return true;

            var sbGraph = getGraph(filename, writeLog, envInBase);

            var envBase = new float[0];
            var envIn = new float[0];

            foreach (var envelopeBase in EnvelopeBaseList)
            {

                var envInLen = envInBase.Length;
                var envBaseLen = envelopeBase.Value.Length;

                var baseFilename = Path.GetFileName(envelopeBase.Key.FilePath);

                /*
                 если размер файла слишком маленький - игнорим
                 или входящая запись слишком херовенькая ...
                 */

                float maxPercent = 0f;
                int maxTao = 0;

                getBaseGraph(sbGraph, envelopeBase.Value, baseFilename, writeLog);

                if (envInLen > envBaseLen)
                {
                    envBase = envelopeBase.Value.Take(envBaseLen * 75 / 100).ToArray();
                    envIn = envInBase;
                }
                else
                {
                    envBase = envelopeBase.Value;
                    envIn = envInBase.Take(envInLen * 75 / 100).ToArray();
                }

                var minLen = Math.Min(envInBase.Length, envBase.Length);
                var maxLen = Math.Max(envInBase.Length, envBase.Length);

                if (minLen < 215 || envInBase.Max() < 0.1)
                {
                    if (showConsoleDebug)
                        LogHelper.AppendLine($"..len:{minLen} < minlen");
                    continue;
                }


                var leftX = minLen * 10 / 100; // Смещение влево  leftX   -10% ролика
                var rightX = minLen * 10 / 100; // мещение вправо rightX  +10% ролика


                for (int t = -leftX; t < (maxLen - minLen + rightX); t += 1)
                {
                    // сравниваем первые 75% файла
                    var resultPart1 = Correlation_Hard(t, envBase, envIn);
                    if (!checkResult(t, ref maxTao, resultPart1, ref maxPercent, baseFilename)) continue;

                    res = new AudioComparerResult(baseFilename, t, maxPercent, envelopeBase.Key.AnswerType);
                    return true;

                }

                if (showConsoleDebug)
                    LogHelper.AppendLine($"..f, {maxPercent:P} t:{maxTao}");
            }

            LogHelper.AppendLine($" TOTAL - false");

            return false;
        }

        public bool HasEquivalentPartial(string filename, out AudioComparerResult res, bool writeLog = false)
        {
            res = null;
            TimeSpan time;

            var envInBase = getEnvelope(filename, out time, AMDAlgorithm.Partial);

            if (showConsoleDebug)
                LogHelper.AppendLine($"Compare file: {Path.GetFileName(filename)} [{envInBase.Length}]");

            if (DetectSilence(envInBase, time, ref res))
                return true;

            var sbGraph = getGraph(filename, writeLog, envInBase);

            var envBase = new float[0];
            var envIn = new float[0];
            var envNextBase = new float[0];
            var envNextIn = new float[0];

            foreach (var envelopeBase in EnvelopeBaseList)
            {

                var envInLen = envInBase.Length;
                var envBaseLen = envelopeBase.Value.Length;

                var minLen = Math.Min(envInLen, envBaseLen);
                var maxLen = Math.Max(envInLen, envBaseLen);
                var baseFilename = Path.GetFileName(envelopeBase.Key.FilePath);

                getBaseGraph(sbGraph, envelopeBase.Value, baseFilename, writeLog);

                /*
                 если размер файла слишком маленький - игнорим
                 или входящая запись слишком херовенькая ...
                 */
                if (minLen < 230 || envInBase.Max() < 0.1)
                {
                    if (showConsoleDebug)
                        LogHelper.AppendLine($"..len:{minLen} < minlen");
                    continue;
                }

                float maxPercent = 0f;
                int maxTao = 0;
                float maxPercent2 = 0f;
                int maxTao2 = 0;



                var leftX = minLen * 5 / 100; // Смещение влево  leftX   -5% ролика
                var rightX = minLen * 5 / 100; // мещение вправо rightX  +5% ролика
                var next2start = 0;

                if (envInLen > envBaseLen)
                {
                    envNextIn = envIn = envInBase;
                    envBase = envelopeBase.Value.Take(envBaseLen * 60 / 100).ToArray();
                    envNextBase = envelopeBase.Value.Skip(envBaseLen * 60 / 100).ToArray();
                    next2start = envBase.Length;
                }
                else
                {
                    envIn = envInBase.Take(envInLen * 60 / 100).ToArray();
                    envNextIn = envelopeBase.Value.Skip(envInLen * 60 / 100).ToArray();
                    envNextBase = envBase = envelopeBase.Value;
                    next2start = envIn.Length;
                }

                for (var t = -leftX; t < (maxLen - minLen); t += 1)
                {
                    // сравниваем первые 60% файла
                    var resultPart1 = Correlation_Hard(t, envBase, envIn);
                    if (!checkResult(t, ref maxTao, resultPart1, ref maxPercent, baseFilename)) continue;

                    // первые 60% совпало, сравниваем остальые 40%
                    var minLen2 = Math.Min(envNextBase.Length, envNextIn.Length);
                    var leftXY = minLen2 * 7 / 100; ; // Смещение leftXY   -7% ролика      


                    for (int n = -leftXY; n < leftXY; n += 1)
                    {
                        // Если первая часть совпадает на 80%, то вторая часть должна совпадать минимум на 75%
                        var resultPart2 = Correlation_Hard(t + next2start + n, envNextBase, envNextIn);
                        if (!checkResult75(t + next2start + n, ref maxTao2, resultPart2, ref maxPercent2, baseFilename))
                            continue;

                        res = new AudioComparerResult(baseFilename, n, maxPercent2, envelopeBase.Key.AnswerType);
                        return true;
                    }

                    break;

                }

                if (showConsoleDebug)
                    LogHelper.AppendLine($"..f, {maxPercent:P} t:{maxTao}, {maxPercent2:P} t2:{maxTao2}");
            }

            LogHelper.AppendLine($" TOTAL - false");

            return false;
        }
        /*
        public bool HasEquivalentMSSpeech(string filename, out AudioComparerResult res, bool superLog)
        {
            bool result = false;
            if (CanUseMsSpeech)
            {
                res = null;
                TimeSpan time;
                var envInBase = getEnvelope(filename, out time);
                if (DetectSilence(envInBase, time, ref res)) return true;
                using (AudioFileReader reader = new AudioFileReader(filename))
                {
                    float[] samples = GetSamples(reader).ToArray();
                    FileInfo fi = new FileInfo(filename);
                    if (DetectSilenceMSSpeech(samples, fi.Length, reader.TotalTime.Milliseconds))
                    {
                        res = new AudioComparerResult(filename, 100, 100f, AutoAnswerType.Silence);
                    }
                }

                result = detector.Start(filename, superLog);
                res = new AudioComparerResult(filename, 0, 100.0f, AutoAnswerType.AutoAnswer);
                return result;
            }
            Console.WriteLine("Can use ms speech!");
            res = new AudioComparerResult(filename, 0, 0.0f, AutoAnswerType.AutoAnswerInvalid);
            return result;
        }
        */

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

        IEnumerable<float> GetEnvelope2(IEnumerable<float> samples, int step, AMDAlgorithm alg)
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
            if (alg == AMDAlgorithm.Partial)
            {

            }
            return result;
        }

        float Correlation_NM(int tao, float[] EnvelopeBase, float[] EnvelopeIn, int bShift = 0)
        {

            int maxlen = Math.Min(EnvelopeBase.Length, EnvelopeIn.Length);

            float a1 = 0;
            float a2 = 0;
            float a3 = 0;
            float a4 = 0;
            float a5 = 0;
            float a6 = 0;
            float a7 = 0;

            for (int t = 0; t < maxlen - tao; t++)
            {
                a1 = a1 + EnvelopeBase[t + bShift] * EnvelopeIn[t + tao];
                a2 = a2 + EnvelopeBase[t + bShift];
                a3 = a3 + EnvelopeIn[t + tao];

                a4 = a4 + EnvelopeBase[t + bShift] * EnvelopeBase[t + bShift];
                a5 = a5 + EnvelopeBase[t + bShift];

                a6 = a6 + EnvelopeIn[t + tao] * EnvelopeIn[t + tao];
                a7 = a7 + EnvelopeIn[t + tao];
            }

            var result = ((maxlen - tao) * a1 - a2 * a3) /
                         (float)Math.Sqrt(((maxlen - tao) * a4 - a5 * a5) * ((maxlen - tao) * a6 - a7 * a7));
            if (float.IsNaN(result) || float.IsInfinity(result))
                return 0;
            return result;
        }

        float Correlation_NM2(int tao, float[] EnvelopeBase, float[] EnvelopeIn)
        {

            int taoABS = Math.Abs(tao);
            int maxlen = Math.Min(EnvelopeBase.Length - taoABS, EnvelopeIn.Length - taoABS);
            int bShift = 0;

            float a1 = 0;
            float a2 = 0;
            float a3 = 0;
            float a4 = 0;
            float a5 = 0;
            float a6 = 0;
            float a7 = 0;

            if (tao < 0)
            {
                bShift = taoABS;
                tao = 0;
            }

            for (int t = 0; t < maxlen - tao; t++)
            {
                a1 = a1 + EnvelopeBase[t + bShift] * EnvelopeIn[t + tao];
                a2 = a2 + EnvelopeBase[t + bShift];
                a3 = a3 + EnvelopeIn[t + tao];

                a4 = a4 + EnvelopeBase[t + bShift] * EnvelopeBase[t + bShift];
                a5 = a5 + EnvelopeBase[t + bShift];

                a6 = a6 + EnvelopeIn[t + tao] * EnvelopeIn[t + tao];
                a7 = a7 + EnvelopeIn[t + tao];
            }

            var result = ((maxlen - tao) * a1 - a2 * a3) /
                         (float)Math.Sqrt(((maxlen - tao) * a4 - a5 * a5) * ((maxlen - tao) * a6 - a7 * a7));
            if (float.IsNaN(result) || float.IsInfinity(result))
                result = 0;
            return result;
        }

        float Correlation_Hard(int tao, float[] EnvelopeBase, float[] EnvelopeIn)
        {

            // определяем длинну диапазона чисел для сравнения
            int section = Math.Min(EnvelopeBase.Length, EnvelopeIn.Length); ;
            int maxLen = Math.Max(EnvelopeBase.Length, EnvelopeIn.Length);
            int taoABS = Math.Abs(tao);
            int bShift = 0;
            int iShift = 0;
            bool shiftBase = EnvelopeBase.Length > EnvelopeIn.Length; // размер базового файлика больше чем сравниваемого

            if (tao < 0) // ограничение по левому краю
            {
                section -= taoABS;
                if (shiftBase)
                    iShift = taoABS;
                else
                    bShift = taoABS;
            }

            if (tao > 0 && tao >= maxLen - section) // ограничение по правому краю
            {
                section = maxLen - taoABS;
            }

            if (tao >= 0 && tao <= maxLen - section) // средина
            {
                if (shiftBase)
                    bShift = taoABS;
                else
                    iShift = taoABS;
            }

            float a1 = 0;
            float a2 = 0;
            float a3 = 0;
            float a4 = 0;
            float a5 = 0;
            float a6 = 0;
            float a7 = 0;

            for (var t = 0; t < section; t++)
            {
                a1 = a1 + EnvelopeBase[t + bShift] * EnvelopeIn[t + iShift];
                a2 = a2 + EnvelopeBase[t + bShift];
                a3 = a3 + EnvelopeIn[t + iShift];

                a4 = a4 + EnvelopeBase[t + bShift] * EnvelopeBase[t + bShift];
                a5 = a5 + EnvelopeBase[t + bShift];

                a6 = a6 + EnvelopeIn[t + iShift] * EnvelopeIn[t + iShift];
                a7 = a7 + EnvelopeIn[t + iShift];
            }

            var result = (section * a1 - a2 * a3) /
                         (float)Math.Sqrt((section * a4 - a5 * a5) * (section * a6 - a7 * a7));
            if (float.IsNaN(result) || float.IsInfinity(result))
                result = 0;
            return result;
        }
        /*
        public List<string> GetAvalableGrammars()
        {
            return detector.ReLoadGrammars();
        }
        */


    }
}
