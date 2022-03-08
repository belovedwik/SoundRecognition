using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DeltaM.DeltaTell.Amd.Helpers;
using NAudio.Wave;

/// <summary>
/// NEW
/// </summary>
namespace DeltaM.DeltaTell.Amd
{
    public class AudioComparer
    {
        public volatile List<KeyValuePair<AutoAnswerFile, float[]>> EnvelopeBaseList = new List<KeyValuePair<AutoAnswerFile, float[]>>();

        public bool IsShowConsoleDebug = false; //indicates is need to show debug info
        //public bool IsDetectSilence = false; //indicates is need to detect silence before auto recognition
        public string BaseAudioDir { get; set; } //base directory with audio

        private object m_Sync = new object(); //field to sync threads when writes in file
        private static Detector m_Detector; //msspeech speech recognition
        private int m_EnvelopeStep { get; set; }
        private float m_PercentageMatch { get; set; } //needed auto percentmatch to be "sure" that audio is auto

        private const string AMD_RECORDS_PATH = @"AmdRecords";
        private readonly string PATH_TO_TRIMMED = Path.Combine(AMD_RECORDS_PATH, "Trimmed"); //path to trimmed audio

        public bool IsCanUseMSSpeech //indicates is MSSpeech available on PC
        {
            get
            {
                return Detector.IsHasInstalledRecognizers();
            }
        }

        public bool IsCanUseCurrentGrammar //indicates is Grammar from App.config available
        {
            get
            {
                return Detector.IsCanPerformRecognitionWithCurrentCulture();
            }
        }

        //public float[] EnvelopeIn { get; set; }

        #region initialize

        /// <summary>
        /// Class constructor
        /// </summary>
        /// <param name="percentageMatch">Needed detect percentmatch</param>
        /// <param name="envelopeStep"></param>
        /// <param name="logger">Where to write logs</param>
        public AudioComparer(float percentageMatch = 0.9f, int envelopeStep = 20, LogTarget logger = LogTarget.Console)
        {
            LogHelper.Init(logger); //where to write logs

            //initialize class fields
            m_PercentageMatch = percentageMatch;
            m_EnvelopeStep = envelopeStep;

            //if MSSpeech detector is not created
            if (ReferenceEquals(m_Detector, null))
                m_Detector = Detector.CreateDetector(); //try to create detector

            InitializeDirectories(); //initialize need direcories
        }

        /// <summary>
        /// initialize directories for trimmed values
        /// </summary>
        private void InitializeDirectories()
        {
            //if path to amd rectords is not exists
            if (!Directory.Exists(AMD_RECORDS_PATH))
                Directory.CreateDirectory(AMD_RECORDS_PATH); //create path to amd records

            //check is directory for trimmed files is not exist
            if (!Directory.Exists(PATH_TO_TRIMMED))
                Directory.CreateDirectory(PATH_TO_TRIMMED); //if directory for trimmedd file is not exist - create it
        }

        #endregion initialize

        public bool AddToBase(string baseFileName, AutoAnswerType aaType = AutoAnswerType.AutoAnswer)
        {
            var item = new KeyValuePair<AutoAnswerFile, float[]>(new AutoAnswerFile(baseFileName, aaType), GetEnvelope(baseFileName, out TimeSpan time));
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

        private float[] GetEnvelope(string fileName, out TimeSpan time, AutoDetectMethod amdAlg = AutoDetectMethod.Hard)
        {
            time = TimeSpan.Zero;

            if (!File.Exists(fileName))
                return new float[0];

            float[] result;

            using (var stream = new AudioFileReader(fileName))
            {
                result = GetEnvelope2(GetSamples(stream).ToList(), m_EnvelopeStep, amdAlg).ToArray();
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
        #region Get Graph

        private StringBuilder GetGraph(string fileName, bool writeLog, float[] envelopeIn)
        {
            var sbGraph = new StringBuilder();

            if (writeLog)
            {
                if (!Directory.Exists("graph"))
                    Directory.CreateDirectory("graph");

                foreach (var val in envelopeIn)
                    sbGraph.AppendLine(val.ToString());

                lock (m_Sync)
                    File.WriteAllText("graph/" + Path.GetFileName(fileName) + ".txt", sbGraph.ToString());

                sbGraph.Clear();
            }
            return sbGraph;
        }

        private void GetBaseGraph(StringBuilder sbGraph, float[] envBase, string baseFilename, bool writeLog)
        {
            if (IsShowConsoleDebug)
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
                lock (m_Sync)
                    File.WriteAllText("graph/" + baseFilename + ".txt", sbGraph.ToString());

                sbGraph.Clear();
            }

        }

        #endregion Get Graph

        #region Result Check

        private bool CheckResult(int t, ref int maxTao, float result, ref float maxPercent, string baseFilename)
        {
            maxTao = result > maxPercent ? t : maxTao;
            maxPercent = Math.Max(maxPercent, result);

            if (m_PercentageMatch <= result)
            {
                LogHelper.AppendLine($" .. OK!! tao:{t}; {maxPercent:P}");

                return true;
            }

            return false;
        }

        private bool CheckResult75(int t, ref int maxTao, float result, ref float maxPercent, string baseFilename)
        {
            maxTao = result > maxPercent ? t : maxTao;
            maxPercent = Math.Max(maxPercent, result);

            Debug.WriteLine("tao2:{1}, res2:{0:P}", result, t);

            if (m_PercentageMatch * 93 / 100 <= result)
            {
                LogHelper.AppendLine($" .. OK2!! tao:{t}; {maxPercent:P}");

                return true;
            }
            return false;
        }

        #endregion Result Check

        #region Detect Silence

        /// <summary>
        /// Perform silence recognition base on the received silence detect method
        /// </summary>
        /// <param name="res">Silence detection result</param>
        /// <param name="silenceDetectMethod">Method to perform silence recognition</param>
        /// <param name="filename">file to check</param>
        /// <returns>true if we have recognized silence; false otherwise</returns>
        private bool IsSilence(float[] envIn, TimeSpan time, ref AudioComparerResult res, SilenceDetectMethod silenceDetectMethod, string filename)
        {
            var isSilence = false; //indicate is silence detected

            switch (silenceDetectMethod)
            {
                //perform default silence detection
                case SilenceDetectMethod.Default:

                    isSilence = !(envIn.Length > 10 || time.TotalSeconds < 2.5 || (envIn.Length > 0 && envIn.Max() > 0.2));

                    break;

                //perform system speech silence detection
                case SilenceDetectMethod.SystemSpeech:

                    isSilence = CheckIsSilenceSystemRecognition(filename);

                    break;

                //perform MSSpeech silence detection
                case SilenceDetectMethod.MSSpeech:

                    if (ReferenceEquals(m_Detector, null)) //check is there is installed MSSpeech recognition "libraries" on PC
                        isSilence = false; //indicate that there is no silence because we can't perform recognition
                    else
                        isSilence = m_Detector.IsSilence(filename); //perform msspeech silence recognition

                    break;
            }

            //if we detected silence
            if (isSilence)
                res = new AudioComparerResult($"Silence_{silenceDetectMethod.ToString()}", 0, 0, AutoAnswerType.Silence); //write silence result

            return isSilence; //return silence detection result

        }

        /// !!!!!!! causing dial drop even if there speech; consider remove
        private bool DetectSilenceMSSpeech(float[] samples, long length, int duration, string fileName, ref AudioComparerResult res)
        {
            if (!(samples.Length > 0 && samples.Max() < 0.2 && (length > 46080 || duration > 2500)))
                return false;

            res = new AudioComparerResult(fileName, 100, 100.0f, AutoAnswerType.Silence);

            return true;
        }

        /// <summary>
        /// Perform silence recognition with System.Speech library
        /// </summary>
        /// <param name="pathToAudio">audio to check</param>
        /// <returns>true if there is only silence; false - otherwise</returns>
        private bool CheckIsSilenceSystemRecognition(string pathToAudio)
        {
            try
            {
                //get SpeechRecognition with US culture to assign dictation grammars
                using (var sre = new System.Speech.Recognition.SpeechRecognitionEngine(new System.Globalization.CultureInfo("en-US"))) 
                {
                    using (Stream fileStream = new FileStream(pathToAudio, FileMode.Open)) //open file to recognize
                    {
                        var isSilence = true; //indicate is silence detected in audio

                        var stateCount = 0;
                        var silenceCount = 0;

                        sre.AudioStateChanged += (sender, e) =>
                        {
                            if (e.AudioState != System.Speech.Recognition.AudioState.Speech)
                                silenceCount++;

                            stateCount++;
                        };

                        //subscribe to change state event
                        sre.SpeechDetected += (sender, e) =>
                        {
                            isSilence = false;
                        };

                        sre.LoadGrammar(new System.Speech.Recognition.DictationGrammar()); //load default dictation grammar

                        sre.SetInputToWaveStream(fileStream); //set input to recognize

                        sre.Recognize(); //launch phrases recognition

                        return silenceCount == stateCount; //indicate is all state was silence
                    }
                }
            }
            catch (Exception ex) //unexpected error occured
            {

            }

            return false; //error occured
        }

        #endregion Detect Silenece

        #region Comparing methods
        private delegate bool RecognitionPerform(string filename, out AudioComparerResult res, SilenceDetectMethod silenceDetectMethod, bool writeLog = false); //body of recognition methods

        /// <summary>
        /// Main method to perform auto recognition
        /// </summary>
        /// <param name="detectMethod">needed recognition type</param>
        /// <param name="filename">path to audio to check</param>
        /// <param name="res">recognition result</param>
        /// <param name="writeLog">is need to write log</param>
        /// <returns>recognition result: true - is auto; false - is human</returns>
        public bool PerformRecognition(AutoDetectMethod detectMethod, string filename, out AudioComparerResult res,
            bool isNeedTrim, int needTrimValue, SilenceDetectMethod silenceDetectMethod, bool writeLog = false)
        {
            res = null; //initialize comparer result

            var trimResult = TrimAudio(filename, isNeedTrim, needTrimValue); //try to trim audio
            filename = trimResult.fileName; //assign new path to audio

            RecognitionPerform funcToPerform = null; //method to perfrom recognition

            //get needed recognition
            switch (detectMethod)
            {
                case AutoDetectMethod.Hard: //EquivalentHard recognition
                    funcToPerform = HasEquivalentHard;
                    break;

                case AutoDetectMethod.Partial: //EquivalentPartial recognition
                    funcToPerform = HasEquivalentPartial;
                    break;

                case AutoDetectMethod.Partial75: //EquivalentPartial75 recognition
                    funcToPerform = HasEquivalentPartial75;
                    break;

                case AutoDetectMethod.MSSpeech: //EquivalentMSSpeech recognition
                    funcToPerform = HasEquivalentMSSpeech;
                    break;

                case AutoDetectMethod.MSSpeechBefore: //HasEquivalentMSSpeechBefore recognition
                    funcToPerform = HasEquivalentMSSpeechBefore;
                    break;

                case AutoDetectMethod.SystemSpeechBefore: //HasEquivalentSystemSpeechBefore recognition
                    funcToPerform = HasEquivalentSystemSpeechBefore;
                    break;

                default:
                    funcToPerform = HasEquivalent; //Equivalent recognition
                    break;
            }

            var recognitionResult = funcToPerform?.Invoke(filename, out res, silenceDetectMethod, writeLog) //try to perform recognition
                                                                                                            //if funcToPerform wasn't initialize - throw exception that needed recognition is not exists
                                        ?? throw new ArgumentException($"AudioComparer.PerformRecognition: Couldn't perform recognition.{Environment.NewLine} Because there is no - {detectMethod.ToString()} recognition;");
            //if file path was change 
            if (trimResult.isChanged)
            {
                RemoveAudio(trimResult.fileName); //remove trimmed audio
            }

            return recognitionResult; //return recognition result
        }

        /// <summary>
        /// Сравнение файла со списком загруженных файлов
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="hardCompare"></param>
        /// <returns></returns>
        private bool HasEquivalent(string filename, out AudioComparerResult res, SilenceDetectMethod silenceDetectMethod, bool writeLog = false)
        {
            res = null;
            TimeSpan time;

            var envInBase = GetEnvelope(filename, out time);

            if (IsShowConsoleDebug)
                LogHelper.AppendLine($"Compare file: {Path.GetFileName(filename)} [{envInBase.Length}]");

            if (IsSilence(envInBase, time, ref res, silenceDetectMethod, filename))
                return true;

            var sbGraph = GetGraph(filename, writeLog, envInBase);

            foreach (var EnvBase in EnvelopeBaseList)
            {

                var envInLen = envInBase.Length;
                var envBaseLen = EnvBase.Value.Length;
                var minLen = Math.Min(envInLen, envBaseLen);
                string baseFilename = Path.GetFileName(EnvBase.Key.FilePath);

                GetBaseGraph(sbGraph, EnvBase.Value, baseFilename, writeLog);

                /*
                 если размер файла слишком маленький - игнорим
                 или входящая запись слишком херовенькая ...
                 */
                if (minLen < 230 || envInBase.Max() < 0.1)
                {
                    if (IsShowConsoleDebug)
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

                    if (!CheckResult(t, ref maxTao, result, ref maxPercent, baseFilename))
                        continue;
                    res = new AudioComparerResult(baseFilename, t, maxPercent, EnvBase.Key.AnswerType);
                    return true;
                }

                if (IsShowConsoleDebug)
                    LogHelper.AppendLine($" ..false, {maxPercent:P} tao:{maxTao}");
            }

            LogHelper.AppendLine($" TOTAL - false");

            return false;
        }

        private bool HasEquivalentHard(string filename, out AudioComparerResult res, SilenceDetectMethod silenceDetectMethod, bool writeLog = false)
        {
            res = null;
            TimeSpan time;

            var envIn = GetEnvelope(filename, out time, AutoDetectMethod.Hard);
            var envInLen = envIn.Length;

            if (IsShowConsoleDebug)
                LogHelper.AppendLine($"Compare file: {Path.GetFileName(filename)} [{envInLen}]");

            if (IsSilence(envIn, time, ref res, silenceDetectMethod, filename))
                return true;

            var sbGraph = GetGraph(filename, writeLog, envIn);
            var baseList = EnvelopeBaseList.ToList();

            foreach (var EnvBase in baseList)
            {
                var envBaseLen = EnvBase.Value.Length;
                var minLen = Math.Min(envInLen, envBaseLen);
                var maxLen = Math.Max(envInLen, envBaseLen);
                string baseFilename = Path.GetFileName(EnvBase.Key.FilePath);

                GetBaseGraph(sbGraph, EnvBase.Value, baseFilename, writeLog);
                /*
                 если размер файла слишком маленький - игнорим
                 или входящая запись слишком херовенькая ...
                 */
                if (minLen < 230 || envIn.Max() < 0.1)
                {
                    if (IsShowConsoleDebug)
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

                    if (!CheckResult(t, ref maxTao, result, ref maxPercent, baseFilename)) continue;
                    res = new AudioComparerResult(baseFilename, t, maxPercent, EnvBase.Key.AnswerType);
                    return true;
                }

                if (IsShowConsoleDebug)
                    LogHelper.AppendLine($" .. false, {maxPercent:P} tao:{maxTao}");
            }

            LogHelper.AppendLine($" TOTAL - false");

            return false;
        }

        private bool HasEquivalentPartial75(string filename, out AudioComparerResult res, SilenceDetectMethod silenceDetectMethod, bool writeLog = false)
        {
            res = null;
            TimeSpan time;

            var envInBase = GetEnvelope(filename, out time, AutoDetectMethod.Partial);

            if (IsShowConsoleDebug)
                LogHelper.AppendLine($"Compare file: {Path.GetFileName(filename)} [{envInBase.Length}]");

            if (IsSilence(envInBase, time, ref res, silenceDetectMethod, filename))
                return true;

            var sbGraph = GetGraph(filename, writeLog, envInBase);

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

                GetBaseGraph(sbGraph, envelopeBase.Value, baseFilename, writeLog);

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
                    if (IsShowConsoleDebug)
                        LogHelper.AppendLine($"..len:{minLen} < minlen");
                    continue;
                }


                var leftX = minLen * 10 / 100; // Смещение влево  leftX   -10% ролика
                var rightX = minLen * 10 / 100; // мещение вправо rightX  +10% ролика


                for (int t = -leftX; t < (maxLen - minLen + rightX); t += 1)
                {
                    // сравниваем первые 75% файла
                    var resultPart1 = Correlation_Hard(t, envBase, envIn);
                    if (!CheckResult(t, ref maxTao, resultPart1, ref maxPercent, baseFilename)) continue;

                    res = new AudioComparerResult(baseFilename, t, maxPercent, envelopeBase.Key.AnswerType);
                    return true;

                }

                if (IsShowConsoleDebug)
                    LogHelper.AppendLine($"..f, {maxPercent:P} t:{maxTao}");
            }

            LogHelper.AppendLine($" TOTAL - false");

            return false;
        }

        private bool HasEquivalentPartial(string filename, out AudioComparerResult res, SilenceDetectMethod silenceDetectMethod, bool writeLog = false)
        {
            res = null;
            TimeSpan time;

            var envInBase = GetEnvelope(filename, out time, AutoDetectMethod.Partial);

            if (IsShowConsoleDebug)
                LogHelper.AppendLine($"Compare file: {Path.GetFileName(filename)} [{envInBase.Length}]");

            if (IsSilence(envInBase, time, ref res, silenceDetectMethod, filename))
                return true;

            var sbGraph = GetGraph(filename, writeLog, envInBase);

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

                GetBaseGraph(sbGraph, envelopeBase.Value, baseFilename, writeLog);

                /*
                 если размер файла слишком маленький - игнорим
                 или входящая запись слишком херовенькая ...
                 */
                if (minLen < 230 || envInBase.Max() < 0.1)
                {
                    if (IsShowConsoleDebug)
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
                    if (!CheckResult(t, ref maxTao, resultPart1, ref maxPercent, baseFilename)) continue;

                    // первые 60% совпало, сравниваем остальые 40%
                    var minLen2 = Math.Min(envNextBase.Length, envNextIn.Length);
                    var leftXY = minLen2 * 7 / 100; ; // Смещение leftXY   -7% ролика      


                    for (int n = -leftXY; n < leftXY; n += 1)
                    {
                        // Если первая часть совпадает на 80%, то вторая часть должна совпадать минимум на 75%
                        var resultPart2 = Correlation_Hard(t + next2start + n, envNextBase, envNextIn);
                        if (!CheckResult75(t + next2start + n, ref maxTao2, resultPart2, ref maxPercent2, baseFilename))
                            continue;

                        res = new AudioComparerResult(baseFilename, n, maxPercent2, envelopeBase.Key.AnswerType);
                        return true;
                    }

                    break;

                }

                if (IsShowConsoleDebug)
                    LogHelper.AppendLine($"..f, {maxPercent:P} t:{maxTao}, {maxPercent2:P} t2:{maxTao2}");
            }

            LogHelper.AppendLine($" TOTAL - false");

            return false;
        }

        /// <summary>
        /// Perform Speech Recognition with MSSpeech engine
        /// </summary>
        /// <param name="fileName">Path to the wav file to recognize</param>
        /// <param name="res">Will save compare result</param>
        /// <param name="superLog">Indicates is need to log</param>
        /// <returns>True if file was recognized (silence, auto or human); False if there is not installed recognizers or can't perform MSSpeech recognition</returns>
        private bool HasEquivalentMSSpeech(string fileName, out AudioComparerResult res, SilenceDetectMethod silenceDetectMethod, bool superLog = false)
        {
            res = null; //comparing result

            if (ReferenceEquals(m_Detector, null)) //check is there is installed MSSpeech recognition "libraries" on PC
                return false; //stop performing comparing

            //if need to detect silence in audio
            if (File.Exists(fileName))
            {
                if (IsSilence(GetEnvelope(fileName, out TimeSpan time), time, ref res, silenceDetectMethod, fileName)) //check is audio "empty"
                    return true;
            }

            if (!m_Detector.PerformDetectionWithGrammars(fileName, superLog, m_PercentageMatch, out float confidence)) //if couldn't perform MSSpeech recognize
            {
                //File.Copy(fileName, $@"Audio\ToCheck\{Path.GetFileName(fileName)}");
                return false;
            }
            else
            {
                res = new AudioComparerResult(fileName, 0, confidence, AutoAnswerType.AutoAnswer); //forme result base on comparing result
                return true;
            }
        }

        private bool HasEquivalentMSSpeechBefore(string fileName, out AudioComparerResult res, SilenceDetectMethod silenceDetectMethod, bool superLog = false)
        {
            res = null; //comparing result

            if (ReferenceEquals(m_Detector, null)) //check is there is installed MSSpeech recognition "libraries" on PC
                return false; //stop performing comparing

            if (!m_Detector.PerformDetectionOfSpeech(fileName, superLog, m_PercentageMatch, out float confidence)) //if couldn't perform MSSpeech recognize
            {
                //File.Copy(fileName, $@"Audio\ToCheck\{Path.GetFileName(fileName)}");
                return false;
            }
            else
            {
                res = new AudioComparerResult(fileName, 0, confidence, AutoAnswerType.AutoAnswer); //forme result base on comparing result
                return true;
            }
        }

        /// <summary>
        /// Perform Speech Recognition with System.Speech engine
        /// </summary>
        /// <param name="fileName">Path to the wav file to recognize</param>
        /// <param name="res">Will save compare result</param>
        /// <param name="superLog">Indicates is need to log</param>
        /// <returns>True if file was recognized (silence, auto or human); False if there is not installed recognizers or can't perform MSSpeech recognition</returns>
        private bool HasEquivalentSystemSpeechBefore(string fileName, out AudioComparerResult res, SilenceDetectMethod silenceDetectMethod, bool superLog = false)
        {
            res = null; //comparing result

            var isDetected = false; //indicate if msspeech recognize phrase
            var stateCount = 0; //total states in audio
            var speechCount = 0; //total speech states in audio

            //create recognition engine with US culture (for dictation grammars)
            using (var sre = new System.Speech.Recognition.SpeechRecognitionEngine(new System.Globalization.CultureInfo("en-US")))
            {
                using (Stream fileStream = new FileStream(fileName, FileMode.Open)) //open file to recognize
                {
                    sre.LoadGrammar(new System.Speech.Recognition.DictationGrammar()); //load dictation grammars

                    //assign to state changed event
                    sre.AudioStateChanged += (sender, e) =>
                    {
                        //if current state is speech
                        if (e.AudioState != System.Speech.Recognition.AudioState.Silence )
                            speechCount++; //add speech state

                        stateCount++; //add another state
                    };

                    //assign to speech detected event
                    sre.SpeechDetected += (sender, e) =>
                    {
                        isDetected = true; //indicate that we recognized speech
                    };

                    sre.SetInputToWaveStream(fileStream); //set input to recognize

                    sre.Recognize(); //launch phrases recognition
                }
            }

            if (isDetected)
            {
                float confidence = stateCount > 0 ? (float)speechCount / (float)stateCount : 1f; //if state count is more than 0 calculate confidence percentegae; otherwise assign default value

                res = new AudioComparerResult(fileName, 0, confidence, AutoAnswerType.AutoAnswer); //forme result base on comparing result
            }

            return isDetected; //indicator is audio AMD or not
        }

        #endregion Comparing Methods

        private IEnumerable<float> GetSamples(AudioFileReader stream)
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

        private IEnumerable<float> GetEnvelope(IEnumerable<float> samples, int step, bool trimStartEnd = false)
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

        private IEnumerable<float> GetEnvelope2(IEnumerable<float> samples, int step, AutoDetectMethod alg)
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

        private float Correlation_NM(int tao, float[] EnvelopeBase, float[] EnvelopeIn, int bShift = 0)
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

        private float Correlation_NM2(int tao, float[] EnvelopeBase, float[] EnvelopeIn)
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

        private float Correlation_Hard(int tao, float[] EnvelopeBase, float[] EnvelopeIn)
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

        public List<string> GetAvailableGrammars()
            => m_Detector?.ReloadGrammars() ?? null;

        /// <summary>
        /// Trim input audio to neede length 
        /// </summary>
        /// <param name="pathToAudio">path to audio that need to be trimmed</param>
        /// <returns></returns>
        private (string fileName, bool isChanged) TrimAudio(string pathToAudio, bool isNeedTrim, int needTrimValue)
        {
            var isFileChanged = false;

            //check is audio trim needed
            if (isNeedTrim && needTrimValue > 0)
            {
                LogHelper.AppendLine($"AudioComparer.TrimAudio: {pathToAudio} Value: {needTrimValue}");
                using (var reader = new WaveFileReader(pathToAudio)) //read audio file
                {
                    var cutLength = reader.TotalTime.Subtract(TimeSpan.FromSeconds(needTrimValue)); //get length to cut

                    //if length to cut is greater than 0
                    if (cutLength.TotalSeconds > 0)
                    {
                        //path to trimmed audio file
                        var pathToTrimmedAudio = $@"{PATH_TO_TRIMMED}\tr_{Path.GetFileName(pathToAudio)}";
                        //trim audio file to needed length
                        WavFileUtils.TrimWavFile(pathToAudio, pathToTrimmedAudio, cutLength);

                        //assign new path to audio
                        pathToAudio = pathToTrimmedAudio;
                        isFileChanged = true;
                    }
                }
            }

            //return path to audio
            return (fileName: pathToAudio, isChanged: isFileChanged);
        }

        private void RemoveAudio(string pathToAudio)
        {
            if (File.Exists(pathToAudio))
                File.Delete(pathToAudio); //remove it
        }
    }

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
