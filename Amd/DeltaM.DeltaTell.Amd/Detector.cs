using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Configuration;
using System.Linq;
using CompareAudioWav.AudioAnalizers;
using AnyLog;
using NAudio.Wave;
using Microsoft.Speech.Recognition;
using Microsoft.Speech.Recognition.SrgsGrammar;

namespace DeltaM.DeltaTell.Amd
{
    /// <summary>
    /// Анализатор автоответчиков за методом MSSPEECH
    /// </summary>
    public class Detector
    {
        #region Members
        
        private enum SpeechRecognition
        {
            Grammars,
            Speech
        }

        private static string PATH_GRAMMARS = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Grammars\Grammars To Compile"); //path to grammars
        private static string PATH_COMPILE_GRAMMARS = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Grammars\Grammars Compiled"); //path to compiled grammars

        private static CultureInfo m_CurrentCultureInfo = null;

        public static CultureInfo CurrentCultureInfo
        {
            get => m_CurrentCultureInfo;
            set
            {
                m_CurrentCultureInfo = value;
                IsCanPerformRecognitionWithCurrentCulture();
            }
        } //current culture info
        private static string PathToGrammar = null; //path to grammars to load

        //private const string PATH_INVALID_AUDIO = @"Invalid\"; //path to invalid audio
        //public static string UsingGrammar { get; private set; } = ConfigurationManager.AppSettings["UsingGrammar"]; //used grammar
        //public static List<InvalidFileInfo> InvalidAudios = new List<InvalidFileInfo>();
        //private List<Grammar> CompiledGrammars = new List<Grammar>();
        //private List<Grammar> Grammars = new List<Grammar>();
        //private DirectoryInfo diPreCompileGrammars;
        //private DirectoryInfo diGrammars;

        //public int InvalidCount { get => InvalidAudios.Count; } //amount of invalid audio files
        //public int Auto { get; private set; } //amount of auto audio files
        //public int NotAuto { get; private set; } //amount of human audio files
        //public static object Sync = new object(); //object to sync threads

        #endregion Members

        #region Initialize
        /// <summary>
        /// Создаёт экземпляр класса для определения автоответчика по алгоритму MSSPEECH
        /// </summary>
        /// <param name="pathFile"></param>
        /// <param name="cultureInfo"></param>
        private Detector()
        {
            //diPreCompileGrammars = CreateDirectoryIfNotExists(PATH_GRAMMARS); //create (if not exists) PATH_GRAMMARS directory and return PATH_GRAMMARS DirectoryInfo
            //diGrammars = CreateDirectoryIfNotExists(PATH_COMPILE_GRAMMARS); //create (if not exists) PATH_COMPILE_GRAMMARS directory and return PATH_COMPILE_GRAMMARS DirectoryInfo

            //CreateDirectoryIfNotExists(PATH_INVALID_AUDIO); //create (if not exists) PATH_INVALID_AUDIO

            //Ci = new CultureInfo(ConfigurationManager.AppSettings["UsingCulture"]); || new CultureInfo("ru-RU"); //get current culture info
            //UsingGrammar = ConfigurationManager.AppSettings["UsingGrammar"]; //get current grammar

            CompileGrammar(); //compile grammar from App.config
        }

        /// <summary>
        /// Creates detector if IsHasInstalledRecognizers and IsCanPerformRecognitionWithCurrentCulture returns true
        /// </summary>
        public static Detector CreateDetector()
        {
            try
            {
                //try to initialize static fields
                if (!InitializeStaticFields())
                    throw new Exception("Couldn't initialize Detector static fields CurrentCultureInfo and PathToGrammar");

                //check is MSSpeech installed on PC
                if (!IsHasInstalledRecognizers())
                    throw new Exception("Can't create detector instance - there is not installed recognizers on PC");

                //check is MSSpeech recognition can perform with culture from App.config
                if (!IsCanPerformRecognitionWithCurrentCulture())
                    throw new Exception("Can't create detector instance because - MSSpeech can't perform recognition with Current Culture (change culture in App.config) and/or there is no MSSPeech recognition on PC");

                return new Detector(); //return new Detector instance
            }
            catch (Exception e)
            {
                Console.WriteLine($"![{DateTime.Now.ToString("hh:mm:ss.ffff")}]Detector.CreateDetector: {e.Message}");
                Log.Error(e);
            }

            return null;
        }

        /// <summary>
        /// Trying to initialize static fields (CurrentCultureInfo and PathToGrammar) if they are reference to null
        /// </summary>
        private static bool InitializeStaticFields()
        {
            try
            {
                var cultureCfg = ConfigurationManager.AppSettings["UsingCulture"];

                //check CurrentCultureInfo and PathToGrammar are initialized
                if (!ReferenceEquals(CurrentCultureInfo, null) && !ReferenceEquals(PathToGrammar, null))
                    //if they are return true
                    return true;

                //check - is there App.config key - UsingCulture is available
                if (string.IsNullOrEmpty(cultureCfg))
                    throw new Exception("Detector.InitializeStaticFields: Can't initialize CurrentCultureInfo and PathToGrammar because there is no App.Config or keys (UsingCulture and/or UsingGrammar) is not available in it.");

                //check - is there App.config key - UsingCulture is available
                if (string.IsNullOrEmpty(ConfigurationManager.AppSettings["UsingGrammar"]))
                    throw new Exception();

                var path = Path.Combine(PATH_GRAMMARS, $"{cultureCfg}.grxml");

                //check - is there available grammar from App.config
                if (!File.Exists(Path.Combine(PATH_GRAMMARS, $"{cultureCfg}.grxml")))
                    throw new Exception("Detector.InitializeStaticFields: File with grammar is not available for current settings");

                //if CurrentCultureInfo is not initialized
                if (ReferenceEquals(CurrentCultureInfo, null))
                    //try to initialize CurrentCultureInfo from App.Config
                    CurrentCultureInfo = new CultureInfo(cultureCfg);

                //if PathToGrammar is not initialized
                if (ReferenceEquals(PathToGrammar, null))
                    //try to initialize PathToGrammr from App.Config
                    PathToGrammar = Path.Combine(PATH_COMPILE_GRAMMARS, ConfigurationManager.AppSettings["UsingGrammar"]);

                //PathToGrammar and CurrentCultureInfo were initialized
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Detector.InitializeStaticFields: {e.Message}");
                Log.Error(e);
            }

            return false;
        }

        /// <summary>
        /// Компилирует указаную граматтику с App.config 
        /// </summary>
        private void CompileGrammar()
        {
            foreach (var file in CreateDirectoryIfNotExists(PATH_GRAMMARS).GetFiles())
            {
                var outputPath = $@"{PATH_COMPILE_GRAMMARS}\{Path.GetFileNameWithoutExtension(file.Name)}.cfg";
                var inputPath = $@"{PATH_GRAMMARS}\{file.Name}";

                using (var fileStream = new FileStream(outputPath, FileMode.Create))
                {
                    SrgsGrammarCompiler.Compile(inputPath, fileStream);
                }
            }
        }

        #endregion Initialize

        #region Actions

        ///// <summary>
        ///// Load all compiled grammars in app context 
        ///// </summary>
        //public List<string> ReLoadCompiledGrammars()
        //{
        //    foreach (var item in diGrammars.GetFiles())
        //    {
        //        if (item.Name.Contains(".cfg"))
        //            CompiledGrammars.Add(new Grammar(item.FullName, "Команды") { Name = item.Name });
        //    }
        //    return CompiledGrammars.Select(x => x.Name).ToList();
        //}

        ///// <summary>
        ///// Load all grammars in app context 
        ///// </summary>
        //public List<string> ReLoadGrammars()
        //{
        //    foreach (var item in diPreCompileGrammars.GetFiles())
        //    {
        //        if (item.Name.Contains(".grxml"))
        //            Grammars.Add(new Grammar(item.FullName, "Команды") { Name = item.Name });
        //    }
        //    return Grammars.Select(x => x.Name).ToList();
        //}

        ///// <summary>
        ///// Load all compiled grammars in app context 
        ///// </summary>
        //public List<string> ReLoadCompiledGrammars()
        //{
        //    var compiledGrammars = new List<string>();

        //    foreach (var item in diGrammars.GetFiles())
        //    {
        //        if (item.Name.Contains(".cfg"))
        //            compiledGrammars.Add(item.Name);
        //    }
        //    return compiledGrammars;
        //}

        ///// <summary>
        ///// Load all grammars in app context 
        ///// </summary>
        //public List<string> ReLoadGrammars()
        //{
        //    var grammars = new List<string>();

        //    foreach (var item in diPreCompileGrammars.GetFiles())
        //    {
        //        if (item.Name.Contains(".grxml"))
        //            grammars.Add(item.Name);
        //    }
        //    return grammars;
        //}

        /// <summary>
        /// Get all available grammar names
        /// </summary>
        /// <param name="isCompiled">Needed grammar type</param>
        /// <returns>all available grammar files name</returns>
        public List<string> ReloadGrammars(bool isCompiled = false)
        {
            var grammarsList = new List<string>(); //get list to store grammar files

            //initialize base on the isCompiled parameter
            var filesInGrammarFolder = CreateDirectoryIfNotExists(isCompiled ? PATH_COMPILE_GRAMMARS : PATH_GRAMMARS).GetFiles(); // isCompiled ? diGrammars.GetFiles() : diPreCompileGrammars.GetFiles(); //file in grammar folder
            var grammarFileExtension = isCompiled ? ".cfg" : ".grxml"; //get grammar extension

            //iterate through all files in folder
            foreach (var file in filesInGrammarFolder)
            {
                if (file.Name.EndsWith(grammarFileExtension)) //if current file has specified extension
                    grammarsList.Add(file.Name); //add grammar to list
            }

            return grammarsList; //return grammar list
        }

        /// <summary>
        /// Устанавливает грамматику для контекста приложения, что использует приложение
        /// </summary>
        /// <param name="gramarToLoad">gramar to load</param>
        /// <param name="sre">SpeechRecognitionEngine to apply grammar</param>
        private void SetUsingGrammar(SpeechRecognitionEngine sre)
        {
            try
            {
                sre.LoadGrammar(new Grammar(PathToGrammar)); //try to load grammar in sre variable
            }
            catch
            {
                var exceptionMessageToDisplay =
                    $"Detector.SetUsingGrammar: can't load grammar with current path - ({PathToGrammar})";

                Console.WriteLine(exceptionMessageToDisplay);
                Log.Error(exceptionMessageToDisplay);
            }

            //if (File.Exists(PathToGrammar)) //if grammar exists
            //else
        }

        /// <summary>
        /// Check is there is installed recognizers on PC (Microsoft.Speech)
        /// </summary>
        /// <returns>If there are at least 2 (invariant is not count as a recognizer) istalled recognizer return true - else return false</returns>
        public static bool IsHasInstalledRecognizers()
        {
            try
            {
                var isHasMSSpeechSR = SpeechRecognitionEngine.InstalledRecognizers().Count > 1; //is more than one SR is installed

                //if only one MSSpeech RS is installed
                if (SpeechRecognitionEngine.InstalledRecognizers().Count == 1)
                {
                    //check is the only one installed RS is Invariant
                    var isInvariant = SpeechRecognitionEngine.InstalledRecognizers()[0] //get first in list
                                                             .Culture //get culture of installed recognizer
                                                             .Equals(CultureInfo.InvariantCulture); //check is Culture is Invariant

                    isHasMSSpeechSR = !isInvariant; //if invariant was found return false; otherwise return true;
                }

                return isHasMSSpeechSR; //check is amount of installed MSSpeech SR greater than 1
            }
            catch (Exception e)
            {
                Console.WriteLine($"Detector.IsHasInstalledRecognizers: {e.Message}");
                Log.Error(e);
            }

            return false;
        }

        /// <summary>
        /// Check is grammar for current culture available
        /// </summary>
        /// <returns>True is grammar available; False if grammar is not available</returns>
        public static bool IsGrammarForCurrentCultureIsAvailable()
        {
            try
            {
                InitializeStaticFields(); //try to initialize CurrentCultureInfo and PathToGrammar

                return File.Exists(PathToGrammar); //check is grammar available
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Log.Error(e);
            }

            return false;
        }

        /// <summary>
        /// Convert some region into ru
        /// </summary>
        /// <param name="firstTwoLetters">letters to change</param>
        /// <returns></returns>
        public static bool GetChangedCultureRegion(ref string firstTwoLetters)
        {
            var isCultureChanged = false; //indicate was culture changed

            //check is there is really two letters in passed parameter
            if (firstTwoLetters.Length == 2)
            {
                var culturesToChange = new string[] { "ua" }; //list of culture that has to be swaped
                var swapCulture = "ru"; //on what to swap

                //iterate throught cultureToChange
                foreach (var culture in culturesToChange)
                {
                    //if one of collections culture is equal to passed parameter
                    if (culture == firstTwoLetters)
                    {
                        isCultureChanged = true; //indicate that culture is changed
                        firstTwoLetters = swapCulture; //swap culture
                        break;
                    }
                }
            }

            //return was culture changed or not
            return isCultureChanged;
        }

        /// <summary>
        /// Checks is can perform Recognition with current set culture
        /// </summary>
        /// <returns>True if SR of current culture installed; False if there is no SR of current culture installed</returns>
        public static bool IsCanPerformRecognitionWithCurrentCulture()
        {
            try
            {
                var initializeResult = InitializeStaticFields(); //try to initialize CurrentCultureInfo and PathToGrammar

                if (IsHasInstalledRecognizers() && initializeResult) //if there is installed MSSPeech SR on PC and class' static fields were initialized
                {
                    var cultureRegion = CurrentCultureInfo.TwoLetterISOLanguageName; //get first two letters of current culture

                    if (cultureRegion != CultureInfo.InvariantCulture.TwoLetterISOLanguageName) //if current region is not invariant
                    {
                        var isCultureRegionChanged = GetChangedCultureRegion(ref cultureRegion); //change culture region if needed

                        var isRecognizerAvailable = SpeechRecognitionEngine.InstalledRecognizers() //get installed MSSpeech recognizers
                                                                           .AsParallel() //get parallel collection
                                                                           .Any(x => x.Culture.TwoLetterISOLanguageName.Contains(cultureRegion)); //check is at least one of installed recognizers is for current culture

                        //if MSSpeech recognizer available and culture region was changed
                        if (isRecognizerAvailable && isCultureRegionChanged)
                        {
                            CurrentCultureInfo = new CultureInfo("ru-RU"); //change current culture info
                        }

                        return isRecognizerAvailable;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Log.Error(e);
            }

            return false;
        }

        ///// <summary>
        ///// Reset values in Auto and NotAuto properties
        ///// </summary>
        //public void ResetRecognitionResultState()
        //{
        //    Auto = 0;
        //    NotAuto = 0;
        //}

        /// <summary>
        /// Запускает процесс распознавания 
        /// </summary>
        /// <param name="_pathFile">Путь к аудио файлу для расспознавания</param>
        /// <param name="confidence">Confidence in result</param>
        /// <param name="pathToAudio">Path to audio that need to recognize</param>
        /// <param name="writeLog">Is need to write logs (auto/human) increment</param>
        /// <returns>Is audio that was compared was found in grammar</returns>
        private bool StartDetector(string pathToAudio, bool writeLog, out float confidence)
        {
            var isDetected = false; //indicate if msspeech recognize phrase

            try
            {
                using (var sre = new SpeechRecognitionEngine(CurrentCultureInfo)) //get SpeechRecognition with set culture info
                {
                    using (Stream fileStream = new FileStream(pathToAudio, FileMode.Open)) //open file to recognize
                    {
                        var confidenceValue = .0f; //contain confidence value

                        sre.SpeechRecognized += (sender, e) =>
                        {
                            confidenceValue = e.Result.Confidence; //get result confidence percentage
                            isDetected = true; //indicate that auto detected

                            var recognizedText = e.Result.Text;
                        };  //if speech recognized with values in grammar

                        SetUsingGrammar(sre); //set grammar

                        sre.SetInputToWaveStream(fileStream); //set input to recognize

                        //sre.SimulateRecognize("повидомлэння видправленно");
                        sre.Recognize(); //launch phrases recognition

                        confidence = confidenceValue; //assign confidence value after performing Recognize
                    }
                }
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine(e);
                Log.Error(e);
                confidence = .0f;
            }

            return isDetected; //return result
        }

        private bool StartSpeechDetector(string pathToAudio, bool writeLog, out float confidence)
        {
            var isDetected = false; //indicate if msspeech recognize phrase

            using (var sre = new SpeechRecognitionEngine(CurrentCultureInfo)) //get SpeechRecognition with set culture info
            {
                using (Stream fileStream = new FileStream(pathToAudio, FileMode.Open)) //open file to recognize
                {
                    SetUsingGrammar(sre); //set grammar

                    sre.SpeechDetected += (sender, e) =>
                    {
                        isDetected = true;
                    };

                    sre.SetInputToWaveStream(fileStream); //set input to recognize

                    sre.Recognize(); //launch phrases recognition
                }
            }

            confidence = isDetected ? 1f : 0f;

            return isDetected;
        }

        private bool PerformDetection(string path, bool writeLog, float percentMatch, out float confidence, SpeechRecognition speechRecognitionMethod)
        {
            var result = false;
            confidence = 0;

            try
            {
                var pathToNormilizedAudio = ImproveAudioFile(path); //create and get path to normilized audio

                if (File.Exists(pathToNormilizedAudio))
                {
                    switch (speechRecognitionMethod)
                    {
                        case SpeechRecognition.Grammars:
                            result = StartDetector(pathToNormilizedAudio, writeLog, out confidence); //perform auto detection
                            break;

                        case SpeechRecognition.Speech:
                            result = StartSpeechDetector(pathToNormilizedAudio, writeLog, out confidence); //perform auto detection
                            break;
                    }

                    //return confidence?? ?? ??
                    //if recognize engine found phrases in audio
                    //if (result)
                    //{
                    //    //confidence = ((float)Math.Round(confidence, 4)) * 100; //round result

                    //    //check confidence level with needed percent match
                    //    if (percentMatch > confidence)
                    //    {
                    //        //if it less it's probably human
                    //        result = false;
                    //    }
                    //}

                    ////if need to write log
                    //if (writeLog)
                    //{
                    //    if (result)
                    //    {
                    //        //found machine
                    //        Auto++;
                    //    }
                    //    else
                    //    {
                    //        //found human
                    //        NotAuto++; 
                    //        CopyAudio(_path, Path.GetFileName(_path));
                    //    }
                    //}

                    //if normilzed audio file still exists
                    if (File.Exists(pathToNormilizedAudio))
                        File.Delete(pathToNormilizedAudio); //remove it
                }
            }
            catch (Exception ex)
            { }

            return result;
        }

        /// <summary>
        /// Запускает детектор расспознавания, улучшает аудио для расспознавания
        /// </summary>
        /// <param name="path">Путь к файлу для расспознавания</param>
        /// <param name="writeLog">Параметр для запсиси лога</param>
        /// <returns></returns>
        public bool PerformDetectionOfSpeech(string path, bool writeLog, float percentMatch, out float confidence)
        {
            return PerformDetection(path, writeLog, percentMatch, out confidence, SpeechRecognition.Speech);
        }

        /// <summary>
        /// Запускает детектор расспознавания, улучшает аудио для расспознавания
        /// </summary>
        /// <param name="path">Путь к файлу для расспознавания</param>
        /// <param name="writeLog">Параметр для запсиси лога</param>
        /// <returns></returns>
        public bool PerformDetectionWithGrammars(string path, bool writeLog, float percentMatch, out float confidence)
        {
            return PerformDetection(path, writeLog, percentMatch, out confidence, SpeechRecognition.Grammars);
        }

        /// <summary>
        /// Check is there only silence or ambient noise
        /// </summary>
        /// <param name="pathToAudio">path to audio to check</param>
        /// <returns>true if there is only silence or ambient noise in the file; false - otherwise</returns>
        public bool IsSilence(string pathToAudio)
        {
            try
            {
                using (var sre = new SpeechRecognitionEngine(CurrentCultureInfo)) //get SpeechRecognition with set culture info
                {
                    using (Stream fileStream = new FileStream(pathToAudio, FileMode.Open)) //open file to recognize
                    {
                        //var statesCount = 0; //states count in audio file
                        //var silenceCount = 0; //silence states count in audio file

                        ////subscribe to change state event
                        //sre.AudioStateChanged += (sender, e) =>
                        //{
                        //    //if state is not speech
                        //    if (e.AudioState != AudioState.Speech)
                        //        silenceCount++; //count as silence

                        //    statesCount++; //indicate that there was state
                        //};

                        var isSilence = true;

                        SetUsingGrammar(sre); //set grammar

                        sre.SpeechDetected += (sender, e) =>
                        {
                            isSilence = false;
                        };

                        sre.SetInputToWaveStream(fileStream); //set input to recognize

                        sre.Recognize(); //launch phrases recognition

                        return isSilence; //indicate is all state was silence
                    }
                }
            }
            catch (Exception ex) //unexpected error occured
            {

            }

            return false; //error occured
        }


        ///// <summary>
        ///// Копирует Аудио в указаную папку
        ///// </summary>
        ///// <param name="FilePath">Путь к файлу</param>
        ///// <param name="FileName">Имя файла</param>
        //private void CopyAudio(string FilePath, string FileName)
        //{
        //    try
        //    {
        //        File.Copy(FilePath, $@"{PATH_INVALID_AUDIO}\{FileName}", true);
        //    }
        //    catch (Exception)
        //    {
        //        Log.Info("Unvailable copy file");
        //    }
        //}

        /// <summary>
        /// Creates directory if it's not exists
        /// </summary>
        /// <param name="path">path to the needed directory</param>
        /// <returns>DirectoryInfo with path</returns>
        private DirectoryInfo CreateDirectoryIfNotExists(string path)
        {
            //if needed directory is not exists
            if (!Directory.Exists(path))
            {
                //create directory
                Directory.CreateDirectory(path);
            }

            return new DirectoryInfo(path);
        }

        /// <summary>
        /// Улучшает аудио файл
        /// </summary>
        /// <param name="filePath">Путь к аудио файлу</param>
        /// <returns></returns>
        private string ImproveAudioFile(string filePath)
        {
            //int bytesPerMillisecond;
            //int blockAlign;
            //int streamLength;

            using (var reader = new WaveFileReader(filePath))
            {
                if (reader.Length < 1024)
                    return filePath;

                //var bytesPerMillisecond = reader.WaveFormat.AverageBytesPerSecond / 1000;
                //blockAlign = reader.BlockAlign;
                //streamLength = (int)reader.Length;

                //var audioAnalizer = new AudioAnalizer(reader.WaveFormat.AverageBytesPerSecond / 1000);
                //AudioСutter audioCutter = new AudioСutter();
                filePath = new AudioAnalizer(reader.WaveFormat.AverageBytesPerSecond / 1000).NormilizeAudio(filePath);

                //var data = audioAnalizer.getData(filePath);
                //filePath = audioCutter.TrimWavFile(filePath, audioAnalizer.detectSilence(data, blockAlign, streamLength), new TimeSpan(0, 0, 0));
            }

            return filePath;
        }

        ///// <summary>
        ///// Копируте все не автоотчики в папку Invalid
        ///// </summary>
        //public static void CopyInvalidAudio()
        //{
        //    Directory.Delete(PATH_INVALID_AUDIO, true); //delete all audios in invalid folder

        //    Thread.Sleep(100); //wait 100 ms to be sure that directory is empty

        //    Directory.CreateDirectory(PATH_INVALID_AUDIO); //create new invalid folder for invalid audio

        //    //copy all invalid audio in a new invalid folder
        //    foreach (var item in InvalidAudios)
        //    {
        //        File.Copy(item.FilePath, $@"{PATH_INVALID_AUDIO}\{item.FileName}", true);
        //    }
        //}

        ////test method???
        //public void syntezSpeech(string phraze)
        //{
        //    SpeechSynthesizer speechSynthesizer = new SpeechSynthesizer();
        //    speechSynthesizer.SetOutputToDefaultAudioDevice();

        //    var voices = speechSynthesizer.GetInstalledVoices();
        //    speechSynthesizer.Speak("Приветик");
        //}


        #endregion Actions
    }
}
