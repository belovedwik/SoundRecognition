using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NewAMDLibrary.Test
{
    [TestClass]
    public class AmdDetectorTests
    {
        private float m_Delta = 1e-2f; //to compare float values
        private bool m_IsDetectSilence = false; //indicate should AMD perform silence detection
        private bool m_IsTrimNeeded = true; //indicate that trim is needed
        private int m_TrimValue = 6; //needed audio length

        private static AudioComparer m_Detector; //detector to perform Auto detection

        [ClassInitialize]
        public static void InitializeTestClass(TestContext testContext)
        {
            m_Detector = new AudioComparer();  //Detector.CreateDetector();
        }

        /// <summary>
        /// Check is MSSpeech recognizer is available on PC
        /// </summary>
        [TestMethod]
        public void MSSpeechAvailable()
        {
            var result = Detector.IsHasInstalledRecognizers();

            Assert.IsTrue(result);
        }

        /// <summary>
        /// Check is Grammar for UA culture availabe
        /// </summary>
        [TestMethod]
        public void GrammarForCurrentAppConfigAvailable()
        {
            var region = new CultureInfo("ua-UA");
            Detector.CurrentCultureInfo = region;

            var result = Detector.IsGrammarForCurrentCultureIsAvailable();

            Assert.IsTrue(result);
        }

        /// <summary>
        /// Check is MSSpeech can perform recognize with RU culture
        /// </summary>
        [TestMethod]
        public void MSSpeechAvailableForRuCulture()
        {
            var region = new CultureInfo("ru-RU");

            Detector.CurrentCultureInfo = region;

            var result = Detector.IsCanPerformRecognitionWithCurrentCulture();

            Assert.IsTrue(result);
        }

        /// <summary>
        /// Check is MSSpeech can perform recognize with UA culture
        /// </summary>
        [TestMethod]
        public void MSSpeechAvailableForUaCulture()
        {
            var region = new CultureInfo("ua-UA");

            Detector.CurrentCultureInfo = region;

            var result = Detector.IsCanPerformRecognitionWithCurrentCulture();

            Assert.IsTrue(result);
        }

        /// <summary>
        /// Check is MSSpeech can perform recognize with kz-KZ culture
        /// </summary>
        [TestMethod]
        public void MSSpeechNotAvailableForKzCulture()
        {
            var region = new CultureInfo("kz-KZ");

            Detector.CurrentCultureInfo = region;

            var result = Detector.IsCanPerformRecognitionWithCurrentCulture();

            Assert.IsFalse(result);
        }

        /// <summary>
        /// Check is MSSpeech can perform recognize with Be culture
        /// </summary>
        [TestMethod]
        public void MSSpeechNotAvailableForBeCulture()
        {
            var region = new CultureInfo("be");

            Detector.CurrentCultureInfo = region;

            var result = Detector.IsCanPerformRecognitionWithCurrentCulture();

            Assert.IsFalse(result);
        }

        /// <summary>
        /// Check is MSSpeech can't perform recognize with Iv culture
        /// </summary>
        [TestMethod]
        public void MSSpeechNotAvailableForIvCulture()
        {
            var region = CultureInfo.InvariantCulture;

            Detector.CurrentCultureInfo = region;

            var result = Detector.IsCanPerformRecognitionWithCurrentCulture();

            Assert.IsFalse(result);
        }

        /// <summary>
        /// Check is can create new detector instance
        /// </summary>
        [TestMethod]
        public void SuccessfulCreateNewDetectInstance()
        {
            Detector.CurrentCultureInfo = new CultureInfo(ConfigurationManager.AppSettings["UsingCulture"]);
            var newDetectorInstance = Detector.CreateDetector();

            Assert.IsNotNull(newDetectorInstance);
        }

        /// <summary>
        /// CreateDetector tries to get new culture info if current is reference to 0
        /// </summary>
        [TestMethod]
        public void CreateDetectorWithNullCI()
        {
            Detector.CurrentCultureInfo = null;
            var result = Detector.CreateDetector();

            Assert.IsNotNull(result);
        }

        /// <summary>
        /// Check is reloadgrammars find all available grammars in Compile grammars folder
        /// </summary>
        [TestMethod]
        public void GetNotEmptyGrammarsList()
        {
            Detector.CurrentCultureInfo = new CultureInfo(ConfigurationManager.AppSettings["UsingCulture"]);
            var detector = Detector.CreateDetector();

            var result = detector.ReloadGrammars(false).Count;
            var expected = 4; //change if different amount of grammars available in folder

            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// Check is reloadgrammars can get all compiled grammars in Grammars folder
        /// </summary>
        [TestMethod]
        public void GetNotEmptyCompiledGrammarsList()
        {
            Detector.CurrentCultureInfo = new CultureInfo(ConfigurationManager.AppSettings["UsingCulture"]);
            var detector = Detector.CreateDetector();

            var result = detector.ReloadGrammars(true).Count;
            var expected = 4; //change if different amount of grammars available in folder

            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// Check is recognizer can recognize auto
        /// </summary>
        [TestMethod]
        public void RecognizeAutoAudioSuccessful()
        {
            Detector.CurrentCultureInfo = new CultureInfo(ConfigurationManager.AppSettings["UsingCulture"]);
            var autoFileName = "Auto.wav"; //auto audio file name to test

            //"Audio" - folder with audio to test
            var pathToAutoAudio = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Audio", autoFileName);

            var result = m_Detector.PerformRecognition(AutoDetectMethod.MSSpeech, pathToAutoAudio, 
                out _, m_IsTrimNeeded, m_TrimValue, m_IsDetectSilence); //try to recognize auto in auto record

            RemoveNormalizedAudio(autoFileName); //remove normalized audio

            Assert.IsTrue(result); //MSSPeech detetector should recognize auto record as auto
        }

        /// <summary>
        /// Check is recognizer can recognize human
        /// </summary>
        [TestMethod]
        public void RecognizeHumanAudioSuccessful()
        {
            Detector.CurrentCultureInfo = new CultureInfo(ConfigurationManager.AppSettings["UsingCulture"]);
            var humanFileName = "Human.wav"; //human audio file name to test

            //"Audio" - folder with audio to test
            var pathToHumanAudio = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Audio", humanFileName);

            var result = m_Detector.PerformRecognition(AutoDetectMethod.MSSpeech, pathToHumanAudio, 
                out _, m_IsTrimNeeded, m_TrimValue, m_IsDetectSilence); //try to recognize auto in human record

            RemoveNormalizedAudio(humanFileName); //remove normalized audio

            Assert.IsFalse(result); //MSSPeech detetector shouldn't recognize human record as auto
        }

        /// <summary>
        /// Check is start return confidence level if recognize auto
        /// </summary>
        [TestMethod]
        public void ConfidenceIsNotEmptyWhenRecognizeAuto()
        {
            Detector.CurrentCultureInfo = new CultureInfo(ConfigurationManager.AppSettings["UsingCulture"]);
            var detector = Detector.CreateDetector();

            var autoFileName = "Auto.wav"; //auto audio file name to test

            //"Audio" - folder with audio to test
            var pathToAutoAudio = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Audio", autoFileName);

            detector.Start(pathToAutoAudio, false, 0, out float result); //perform MSSpeech recognition
            var expected = .93f; //expected result

            RemoveNormalizedAudio(autoFileName); //remove normalized audio

            Assert.AreEqual(expected, result, m_Delta); //recognized auto successfuly
        }

        /// <summary>
        /// Check is recognizer can recognize auto
        /// </summary>
        [TestMethod]
        public void RecognizeAllAutoInFolder()
        {
            Detector.CurrentCultureInfo = new CultureInfo(ConfigurationManager.AppSettings["UsingCulture"]);

            var autoFileName = Directory.GetFiles(@"Audio\Auto"); //auto audio file name to test
            var results = new Dictionary<string, bool>(); //file path and isAuto result

            foreach (var file in autoFileName)
            {
                var isAutoMS = m_Detector.PerformRecognition(AutoDetectMethod.MSSpeech, file, out _, m_IsTrimNeeded, m_TrimValue, true);
                var fileName = Path.GetFileName(file); //file name to add in result list

                RemoveNormalizedAudio(fileName); //remove normalized audio

                results.Add(fileName, isAutoMS);
            }

            var autoRecordsCount = results.Where(x => x.Value)
                                     .Count(); //count of auto records

            var humanRecordsCount = results.Where(x => !x.Value)
                                      .Count(); //count of human records

            var namesOfHumanRecords = results.Where(x => !x.Value)
                                             .Select(x => x.Key)
                                             .ToList();

            Assert.IsTrue(humanRecordsCount == 0); //equal amount of recognized audio
        }

        private void RemoveNormalizedAudio(string audioFileName)
        {
            var pathToAudioFileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Audio", $"Normilized{audioFileName}");

            //if needed file exists
            if (File.Exists(pathToAudioFileName))
                File.Delete(pathToAudioFileName); //remove it
        }
    }
}