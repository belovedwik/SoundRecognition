using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Speech.Recognition;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace ConsoleDetectMachine
{
    public class Program
    {
        #region delegates

        public delegate Tuple<int, int> dPerformComparing(string[] localFiles, string autoAnswerDir, string humanAnswerDir, AutoDetectMethod detectMethod); //delegate to call comparing method type (linear or parallel)

        public delegate bool BoolDelegate(AutoDetectMethod detectMethod, string fileName, out AudioComparerResult res, bool writeLog = false); //delegate to call comparing method

        #endregion

        #region enums

        public enum Algorithms { Normal, Hard, Partial, Partial75, MSSpeech };
        
        public enum AlgorithmsType { Linear, Parallel, ParallelPartioner, Tasks };

        #endregion

        #region fields

        //folders name
        private const string DIR_AUDIO_DETECT = "AudioDetect"; //audio detect folder name
        private const string DIR_TMP = "tmp"; //audio tmp folder name
        private const string DIR_AUTO = "auto"; //audio auto folder name
        private const string DIR_HUMAN = "human"; //audio human folder name

        private static AudioComparer m_AudioComparer; //instance of the class to compare audio

        private static bool m_IsTrimNeeded = false; //trim input audio
        private static int m_TrimValue = 7; //need record 6 seconds

        private static SilenceDetectMethod m_SilenceDetectMethod = SilenceDetectMethod.MSSpeech; //indicate is detector will try to detect silence in audio

        #endregion

        #region methods

        #region directory methods

        /// <summary>
        /// Form and return base audio dir
        /// </summary>
        /// <returns>Return base audio dir</returns>
        private static string GetAudioDetectDir()
        {
            var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); //get project directory
            return Path.Combine(baseDir, DIR_AUDIO_DETECT); //add AudioDetect folder to path
        }

        /// <summary>
        /// Create AudioDetect directory if it's not exist in the given path
        /// </summary>
        /// <param name="path">Path to the needed directory</param>
        /// <returns>path to the needed directory</returns>
        private static string DirectoryExists(string path)
        {
            if (!Directory.Exists(path)) //if there is not such directory
                Directory.CreateDirectory(path); //create directory

            return path;
        }

        /// <summary>
        /// Add audio files to audioComparer
        /// </summary>
        /// <param name="audioComparer">Where to add audio</param>
        /// <param name="directoryWithFiles">directories with audio files</param>
        /// <param name="autoAnswerType">Type of AutoAnswer</param>
        private static void AddAudioFilesToBase(AudioComparer audioComparer, string[] directoryWithFiles,
                                                    AutoAnswerType autoAnswerType = AutoAnswerType.AutoAnswer)
        {
            foreach (string BaseFile in directoryWithFiles)
            {
                audioComparer.AddToBase(BaseFile, autoAnswerType);
            }
        }

        #endregion

        #region comparing methods

        /// <summary>
        /// Return method to perform comparing
        /// </summary>
        /// <param name="algorithmType">Algorithm type to return</param>
        /// <returns>Comparing method</returns>
        private static dPerformComparing GetComparingMethod(AlgorithmsType algorithmType)
        {
            dPerformComparing returnMethod = null;

            switch (algorithmType)
            {
                case AlgorithmsType.Parallel:
                    returnMethod = ParallelComparing; //assign parallel comparing method
                    break;

                case AlgorithmsType.Linear:
                    returnMethod = LinearComparing; //assign linear comparing method
                    break;

                case AlgorithmsType.ParallelPartioner:
                    returnMethod = ParallelPartionerComparing; //assign task comparing method
                    break;

                case AlgorithmsType.Tasks:
                    returnMethod = TaskComparing;
                    break;
            }

            return returnMethod;
        }

      
        /// <summary>
        /// Perform linear search with simple loop
        /// </summary>
        /// <param name="localFiles">Files in base directory</param>
        /// <param name="autoAnswerDir">Directory with "auto" answers</param>
        /// <param name="humanAnswerDir">Directory with "human" answers</param>
        /// <param name="boolDelegate">Method that will perform comparing</param>
        /// <returns>Count of Auto and Humans comparing results</returns>
        private static Tuple<int, int> LinearComparing(string[] localFiles, string autoAnswerDir, string humanAnswerDir, AutoDetectMethod detectMethod)
        {
            var autoCount = 0; //auto answers count
            var humanCount = 0; //human answers cont

            foreach (var audioFilePath in localFiles)
            {
                try
                {
                    var isHumanAudio = m_AudioComparer.PerformRecognition(detectMethod, audioFilePath, out AudioComparerResult res, m_IsTrimNeeded, m_TrimValue, m_SilenceDetectMethod, true); //performe compare (return true if it's auto and false if it's human)

                    var compareResult = res != null ? res.ToString() : string.Empty; //get result text if exist

                    if (isHumanAudio) autoCount++;
                    else humanCount++;

                    var pathToCopy = Path.Combine((isHumanAudio ? autoAnswerDir : humanAnswerDir), Path.GetFileName(audioFilePath));

                    if (!File.Exists(pathToCopy))
                        File.Copy(audioFilePath, pathToCopy);

                    DisplayElementInfoWithColor(isHumanAudio, audioFilePath, compareResult);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    //continue;
                }
            }

            return new Tuple<int, int>(autoCount, humanCount);
        }

        /// <summary>
        /// Perform Parallel search with Parallel.ForEach
        /// </summary>
        /// <param name="localFiles">Files in base directory</param>
        /// <param name="autoAnswerDir">Directory with "auto" answers</param>
        /// <param name="humanAnswerDir">Directory with "human" answers</param>
        /// <param name="boolDelegate">Method that will perform comparing</param>
        /// <returns>Count of Auto and Humans comparing results</returns>
        private static Tuple<int, int> ParallelComparing(string[] localFiles, string autoAnswerDir, string humanAnswerDir, AutoDetectMethod detectMethod)
        {
            var autoCount = 0; //auto answers count
            var humanCount = 0; //human answers cont

            Parallel.ForEach(localFiles, (element) =>
            {
                //lock (_audioComparer.sync)
                try
                {
                    var isHumanAudio = m_AudioComparer.PerformRecognition(detectMethod, element, out AudioComparerResult res, m_IsTrimNeeded, m_TrimValue, m_SilenceDetectMethod, true); //performe compare (return true if it's auto and false if it's human)

                    var compareResult = res != null ? res.ToString() : string.Empty; //get result text if exist

                    if (isHumanAudio) autoCount++;
                    else humanCount++;

                    var pathToCopy = Path.Combine((isHumanAudio ? autoAnswerDir : humanAnswerDir), Path.GetFileName(element));

                    if (!File.Exists(pathToCopy))
                        File.Copy(element, pathToCopy);


                    DisplayElementInfoWithColor(isHumanAudio, element, compareResult);
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Checked element is> {element}");
                    Console.ResetColor();

                    Console.WriteLine($"\n{e.Message}\n\n\n");
                    //continue;
                }

            });

            return new Tuple<int, int>(autoCount, humanCount);
        }

   

        /// <summary>
        /// Perform Parallel search with Tasks
        /// </summary>
        /// <param name="localFiles">Files in base directory</param>
        /// <param name="autoAnswerDir">Directory with "auto" answers</param>
        /// <param name="humanAnswerDir">Directory with "human" answers</param>
        /// <param name="boolDelegate">Method that will perform comparing</param>
        /// <returns>Count of Auto and Humans comparing results</returns>
        private static Tuple<int, int> TaskComparing(string[] localFiles, string autoAnswerDir, string humanAnswerDir, AutoDetectMethod detectMethod)
        {
            var autoCount = 0; //auto answers count
            var humanCount = 0; //human answers cont

            var tasks = new List<Task>(); //will contains all running tasks

            foreach (var file in localFiles)
            {
                tasks.Add(Task.Factory.StartNew(
                    () =>
                    {
                        try
                        {
                            var isHumanAudio = m_AudioComparer.PerformRecognition(detectMethod, file, out AudioComparerResult res, m_IsTrimNeeded, m_TrimValue, m_SilenceDetectMethod, true); //performe compare (return true if it's auto and false if it's human)

                            var compareResult = res != null ? res.ToString() : string.Empty; //get result text if exist

                            if (isHumanAudio) autoCount++;
                            else humanCount++;

                            var pathToCopy = Path.Combine((isHumanAudio ? autoAnswerDir : humanAnswerDir), Path.GetFileName(file));

                            if (!File.Exists(pathToCopy))
                                File.Copy(file, pathToCopy);


                            DisplayElementInfoWithColor(isHumanAudio, file, compareResult);
                        }
                        catch (Exception e)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Checked element is> {file}");
                            Console.ResetColor();

                            Console.WriteLine($"\n{e.Message}\n\n\n");
                            //continue;
                        }
                    }));
            }

            //sync threads

            Task.WaitAll( tasks.Where(x => x != null).ToArray() );

            return new Tuple<int, int>(autoCount, humanCount);

        }

        /// <summary>
        /// Generic method to perform comparing
        /// </summary>
        /// <param name="tmpDir">Dir with audio files to compare</param>
        /// <param name="autoAnswerDir">Directory with "auto" answers</param>
        /// <param name="humanAnswerDir">Directory with "human" answers</param>
        /// <param name="boolDelegate">Method that will perform comparing</param>
        /// <param name="comparing">Method to perform comparing</param>
        private static void PerformComparing(string tmpDir, string autoAnswerDir, string humanAnswerDir,
                                               AutoDetectMethod detectMethod, dPerformComparing comparing)
        {
            DisplaySeperator(); //seperate

            var localFiles = Directory.GetFiles(tmpDir, "*.wav"); //get all wav files in tmp directory

            var sw = Stopwatch.StartNew(); //for time calculation

            var result = comparing?.Invoke(localFiles, autoAnswerDir, humanAnswerDir, detectMethod); //perform comparing method

            sw.Stop(); //stop timer

            DisplaySeperator();

            DisplayResultInfo(localFiles.Length, sw.ElapsedMilliseconds, result?.Item2 ?? 0, result?.Item1 ?? 0); //display total comparing result

            DisplayWithColor("\n\nPress ", "Enter", " to clear results", color: ConsoleColor.DarkYellow); //display how to clear results

            DeleteAllGeneratedAudio();

            Console.ReadLine();
        }

        private static void DeleteAllGeneratedAudio()
        {
            var pathToAudio = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Audio"));

            foreach (var audio in pathToAudio.GetFiles())
            {
                audio.Delete();
            }

            //Console.WriteLine($"{nameof(pathToAudio)}>{pathToAudio}");

            //\Audio
        }

        #endregion Comparing methods

        #region console handlers

        #region console display

        /// <summary>
        /// Display basic console info
        /// </summary>
        /// <param name="percentMatch">Need match percent</param>
        /// <param name="algorithm">Choosen algorithm</param>
        /// <param name="algorithmsType">Choosen algorithm type</param>
        /// <param name="recordsCount">How many audio files in base directory</param>
        /// <param name="recordAutoAnswerInvalid">How many invalid audio files</param>
        private static void DisplayBasicConsoleInfo(float percentMatch, AutoDetectMethod algorithm, AlgorithmsType algorithmsType, int recordsCount, int recordAutoAnswerInvalid, AudioComparer audioComparer)
        {
            Console.ResetColor();

            //Console.WriteLine($"Audiodetector, percent={percentMatch:P}, algorithm: {algorithm}, algorithm type: {parallelMessage}");
            DisplayWithColor("Audiodetector, percent: ", String.Format($"{percentMatch:P}"));
            DisplayWithColor(", algorithm: ", String.Format($"{algorithm}"));
            DisplayWithColor(", algorithm type: ", String.Format($"{algorithmsType}"));
            DisplayWithColor(", detecting silence: ", String.Format($"{m_SilenceDetectMethod}"), isNeedNewLine: true);
            DisplayWithColor("is trim needed: ", String.Format($"{m_IsTrimNeeded}"));
            DisplayWithColor(", trim value: ", String.Format($"{m_TrimValue}"), isNeedNewLine: true);

            //Console.WriteLine($"AutoAnswer: {recordsCount} records, AutoAnswerInvalid {recordAutoAnswerInvalid} records");
            DisplayWithColor("AutoAnswer: ", String.Format($"{recordsCount}"));
            DisplayWithColor(" records, AutoAnswerInvalid ", String.Format($"{recordAutoAnswerInvalid}"), " records", isNeedNewLine: true);

            DisplaySeperator(); //seperate

            //if choosen algorithm is MSSpeech
            if (algorithm == AutoDetectMethod.MSSpeech)
                DisplayInstalledMSSpeech(audioComparer); //display stats

            DisplayWithColor("To setup Comparing algorithm: Set Percent '", "p=(0-100)", "'", color: ConsoleColor.DarkYellow, isNeedNewLine: true);
            DisplayWithColor("Set Algoritm '", "a=(1-5)", "'", color: ConsoleColor.DarkYellow, isNeedNewLine: true);
            DisplayWithColor("Detecting Silence '", "s=(0 - None; 1 - Default; 2 - MSSpeech)", "'", color: ConsoleColor.DarkYellow, isNeedNewLine: true);
            DisplayWithColor("Set Algorithm type '", String.Format($"t=(1-{Enum.GetNames(typeof(AlgorithmsType)).Length})"), "'", color: ConsoleColor.DarkYellow, isNeedNewLine: true);
            DisplayWithColor("Is Trim need '", "c=(> 0 - detecting; <= 0 - is not detecting)", "'", color: ConsoleColor.DarkYellow, isNeedNewLine: true);
            DisplayWithColor("Trim value '", "l=(> 0)", "'", color: ConsoleColor.DarkYellow);
            DisplayWithColor(" To close console '", "q", "'", color: ConsoleColor.DarkYellow, isNeedNewLine: true);

            DisplayWithColor("To perform comparing press ", "ENTER", color: ConsoleColor.DarkYellow, isNeedNewLine: true);

            Console.Write("\nYour choise> ");
            Console.ForegroundColor = ConsoleColor.Yellow; //apply color to console
        }

        /// <summary>
        /// Display compare results
        /// </summary>
        /// <param name="filesCount">How many files was compared</param>
        /// <param name="searchTime">How much did it took</param>
        /// <param name="humanCount">How much compared files was "humans"</param>
        /// <param name="autoCount">How much compared files was "auto"</param>
        private static void DisplayResultInfo(int filesCount, long searchTime, int humanCount, int autoCount)
        {
            DisplayWithColor("Took time> ", searchTime.ToString(), " ms", isNeedNewLine: true);

            Console.WriteLine("\nSearch results:");

            DisplayWithColor("Files processed> ", filesCount.ToString(), isNeedNewLine: true);
            DisplayWithColor("Human> ", humanCount.ToString());
            DisplayWithColor(" AutoAnswer> ", autoCount.ToString(), isNeedNewLine: true);
        }

        /// <summary>
        /// Display message with color
        /// </summary>
        /// <param name="startTextWithoutColor">Message before color</param>
        /// <param name="message">Message on which color will be applied</param>
        /// <param name="endMessageWithoutColor">Message after colored message</param>
        /// <param name="color">Collor to apply</param>
        /// <param name="isNeedNewLine">Indicates is need to add new line after endMessageWithoutColor</param>
        private static void DisplayWithColor(string startTextWithoutColor, string message,
            string endMessageWithoutColor = "", ConsoleColor color = ConsoleColor.Green, bool isNeedNewLine = false)
        {
            Console.Write(startTextWithoutColor); //display text without collor applying at start

            Console.ForegroundColor = color; //apply color to console
            Console.Write(message); //write text that need to be colored
            Console.ResetColor(); //reset console color

            Console.Write(endMessageWithoutColor); //display text without collor applying at end

            //if need move to the next line
            if (isNeedNewLine)
                Console.WriteLine();
        }

        /// <summary>
        /// Display seperator
        /// </summary>
        private static void DisplaySeperator()
        {
            Console.WriteLine(new string('-', 110));
        }

        /// <summary>
        /// Display Installed MSSpeech recognizers
        /// </summary>
        private static void DisplayInstalledMSSpeech(AudioComparer audioComparer)
        {
            if (audioComparer.IsCanUseMSSpeech) //check is there is installed MSSpeech recognition "libraries" on PC 
            {
                Console.WriteLine("MSSpeech stats:");
                //display count 
                DisplayWithColor("Installed MSSpeech recognizers count> ", SpeechRecognitionEngine.InstalledRecognizers().Count.ToString(), isNeedNewLine: true);
                //display first two letter of installed MSSpeech recognizers
                DisplayWithColor("Installed MSSpeech recognizers culture (first two letters)> ",
                        String.Join(", ", SpeechRecognitionEngine.InstalledRecognizers().Select(x => x.Culture.TwoLetterISOLanguageName)),
                        isNeedNewLine: true);
            }
            else //there is no instaled MSSpeech recognition "libraries" on PC
            {
                //display error message
                Console.WriteLine("There are no installed MSSpeech recognizers!");
            }

            DisplaySeperator(); //seperate
        }

        /// <summary>
        /// Display found element with color
        /// </summary>
        /// <param name="isHumanAudio">Indicates is "human" results or not</param>
        /// <param name="audioFilePath">Path to the compared file</param>
        /// <param name="compareResult">Compare results</param>
        private static void DisplayElementInfoWithColor(bool isHumanAudio, string audioFilePath, string compareResult)
        {
            Console.ForegroundColor = isHumanAudio ? ConsoleColor.DarkGreen : ConsoleColor.DarkGray; //change color base on the result (auto or human)
            Console.WriteLine($"{Path.GetFileName(audioFilePath)} : {isHumanAudio} {compareResult}\n");
            Console.ResetColor();
        }

        #endregion console display

        #region console input

        /// <summary>
        /// Get algorithm from user input
        /// </summary>
        /// <param name="input">User input</param>
        /// <returns>Algorithm</returns>
        private static AutoDetectMethod GetAlgorithmFromUserInput(int input)
        {
            return (AutoDetectMethod) input;
        }

        /// <summary>
        /// Get algorithm type base on user input
        /// </summary>
        /// <param name="input">User input</param>
        /// <returns>Algorithm type from user input</returns>
        private static AlgorithmsType GetAlgorithmsTypeFromUserInput(int input)
        {
            var algorithmType = AlgorithmsType.Linear; //default algorithm is linear

            //base on user input
            switch (input)
            {
                case 1:
                    algorithmType = AlgorithmsType.Linear;
                    break;

                case 2:
                    algorithmType = AlgorithmsType.Parallel; //parallel
                    break;

                case 3:
                    algorithmType = AlgorithmsType.ParallelPartioner;
                    break;

                case 4:
                    algorithmType = AlgorithmsType.Tasks;
                    break;
            }

            return algorithmType;
        }

        /// <summary>
        /// Get percent match base on user input
        /// </summary>
        /// <param name="input">User input</param>
        /// <returns>Percent Match</returns>
        private static float GetPercentMatchFromUserInput(float input)
        {
            //if input less or equal than zero return default value .8f; if input greater than zero calculate PercentMatch
            return input > 0 ? input / 100 : 0.80f;
        }

        #endregion console input

        #endregion console handlers

        /// <summary>
        /// Enter point
        /// </summary>
        /// <param name="args">Arguments from console</param>
        static void Main(string[] args)
        { 
            var percentMatch = .8f; //needed match percent 
            var detectMethod = AutoDetectMethod.MSSpeech; //choosen algorithm

            var algorithmType = AlgorithmsType.Linear; //determine algorith type
            var isExitPressed = false; //is user want to exit

            var userInput = string.Empty; //command from user
                 
            do
            {
                Console.Clear(); //clear console screen

                m_AudioComparer = new AudioComparer(percentMatch, 20, LogTarget.Console)
                                        { IsShowConsoleDebug = false }; //initialize comparer

                m_AudioComparer.BaseAudioDir = GetAudioDetectDir(); //set base audiodir
                
                DirectoryExists(m_AudioComparer.BaseAudioDir); //check is audio dir exist and create if it doesn't

                AddAudioFilesToBase(m_AudioComparer, Directory.GetFiles(m_AudioComparer.BaseAudioDir, "*.wav")); //add all wav files from base audio dir
                AddAudioFilesToBase(m_AudioComparer, Directory.GetFiles(m_AudioComparer.BaseAudioDir + "/invalid", "*.wav"), AutoAnswerType.AutoAnswerInvalid);

                var tmpDir = DirectoryExists(Path.Combine(m_AudioComparer.BaseAudioDir, DIR_TMP)); //check is tmp dir exist and create if it doesn't
                var autoAnswerDir = DirectoryExists(Path.Combine(m_AudioComparer.BaseAudioDir, DIR_AUTO)); //check is auto dir exist and create if it doesn't
                var humanAnswerDir = DirectoryExists(Path.Combine(m_AudioComparer.BaseAudioDir, DIR_HUMAN));   //check is human dir exist and create if it doesn't          

                DisplayBasicConsoleInfo(percentMatch, detectMethod, algorithmType, m_AudioComparer.EnvelopeBaseList.Count(b => b.Key.AnswerType == AutoAnswerType.AutoAnswer),
                    m_AudioComparer.EnvelopeBaseList.Count(b => b.Key.AnswerType == AutoAnswerType.AutoAnswerInvalid), m_AudioComparer); //show information on console

                userInput = Console.ReadLine(); //get user input

                //if user input starts with "p=" or with "a=" or "t=" or "q" go to setCommand
                if ((userInput.StartsWith("p=") || userInput.StartsWith("a=")
                                                || userInput.StartsWith("t=")) 
                                                || userInput.StartsWith("s=")
                                                || userInput.StartsWith("c=")
                                                || userInput.StartsWith("l=")
                                                && userInput.Length > 2) //there is something more except command
                {
                    var command = userInput.Substring(0, 1); //get command letter

                    //detect command
                    switch (command)
                    {
                        case "p": //percent match
                            float.TryParse(userInput.Substring(2), out float newPercentMatch); //try to parse percent string to float
                            percentMatch = GetPercentMatchFromUserInput(newPercentMatch); //get formated percent match base on parse result

                            break;

                        case "a": //algorithm
                            int.TryParse(userInput.Substring(2, 1), out int newAlgorithm); //try to parse algorithm number string to int
                            detectMethod = GetAlgorithmFromUserInput(newAlgorithm); //get algorithm base on parse result

                            break;

                        case "t": //algorithm type
                            int.TryParse(userInput.Substring(2, 1), out int newAlorithmType); //try to parse algorithm type number to int
                            algorithmType = GetAlgorithmsTypeFromUserInput(newAlorithmType); //get algorithm type base on parse result

                            break;

                        case "s":
                            int.TryParse(userInput.Substring(2, 1), out int isDetectSilenece); //try to get is detecting silence or not
                            m_SilenceDetectMethod = (SilenceDetectMethod)isDetectSilenece; //assign is detect silence
                            break;

                        case "c":
                            int.TryParse(userInput.Substring(2, 1), out int isTrimValue); //try to get is detecting silence or not
                            m_IsTrimNeeded = isTrimValue > 0; //assign is detect silence
                            break;

                        case "l":
                            int.TryParse(userInput.Substring(2, userInput.Length - 2), out int trimLength); //try to get is detecting silence or not

                            if (trimLength > 0)
                                m_TrimValue = trimLength; //assign is detect silence
                            break;
                    }
                }
                else if (userInput.StartsWith("q")) //exit from console
                {
                    isExitPressed = true;
                }
                else //perform comparing with indicated values
                {
                    Console.ResetColor(); //reset input color

                    var comparing = GetComparingMethod(algorithmType); //contains method to perform comparing
                    //perform comparing
                    PerformComparing(tmpDir, autoAnswerDir, humanAnswerDir, detectMethod, comparing);
                }

            } while (!isExitPressed); //perform app while q is not pressed
        }

        #endregion methods
    }

}
