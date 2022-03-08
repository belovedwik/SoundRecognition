using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Speech.Recognition;



namespace ConsoleDetectMachine
{
    public class ConsoleTests
    {

        static bool completed;

        static void MainTest1(string[] args)
        {

            // Create an in-process speech recognizer for the en-US locale.  
            using (
                SpeechRecognitionEngine recognizer =
                    new SpeechRecognitionEngine(
                        new System.Globalization.CultureInfo("en-US")))
            {

                recognizer.LoadGrammar(new DictationGrammar());

/*                var PathToGrammar = AppDomain.CurrentDomain.BaseDirectory + @"Grammars\Grammars Compiled\ua-Ua2.cfg"; //path to compiled grammars
                recognizer.LoadGrammar(new Grammar(PathToGrammar));
                */
                // Add a handler for the speech recognized event.  
                recognizer.SpeechRecognized +=
                    new EventHandler<SpeechRecognizedEventArgs>(recognizer_SpeechRecognized);

                recognizer.SetInputToWaveFile(AppDomain.CurrentDomain.BaseDirectory + @"\AudioDetect\tmp\Human_siphub-00039288Partial75_after-in.wav");

                // Start asynchronous, continuous speech recognition.  
                recognizer.RecognizeAsync(RecognizeMode.Multiple);

                // Keep the console window open.  
                while (true)
                {
                    Console.ReadLine();
                }
            }
        }

        // Handle the SpeechRecognized event.  
        static void recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            Console.WriteLine("Recognized text: " + e.Result.Text);
        }


        void MainTest(string[] args)
        {
            Stopwatch sw = new Stopwatch();
            int WorkMs = 0;

            Console.WriteLine("**********  Single  *********");

            // файлы, которые сравниваются с базовыми
            var files_compare = Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../audio_testfiles"));

            // базовые ответы
            var files_base = Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../audio_base"));

            foreach (var compareFile in files_compare)
            {
                WorkMs += ExecCompare(compareFile, files_base);
            }

            Console.WriteLine("Обработка {0} файлов заняла {1}ms", files_compare.Count(), WorkMs);
            Console.WriteLine("\n\n\n");

            Console.WriteLine("**********  Paralel  *********");
            Console.WriteLine("");

            WorkMs = 0;
            sw.Start();

            // while (true)
            {
                Parallel.ForEach(files_compare, (currentFile) =>
                {
                    WorkMs += ExecCompare(currentFile, files_base);
                });
                Thread.Sleep(10);
            }

            sw.Stop();

            Console.WriteLine("Paralle {1}ms, общее время выполнения {0}ms", WorkMs, sw.ElapsedMilliseconds);


            Console.WriteLine("\n\n\n");
            Console.WriteLine("**********   Memory  *********");
            Console.WriteLine("");

            AudioComparer comparer = new AudioComparer();
            WorkMs = 0;

            // заполняем память файлами для сравнения
            foreach (var file_base in files_base)
            {
                comparer.AddToBase(file_base);
            }

            sw.Start();

            foreach (var compareFile in files_compare)
            {
                int execTime = 0; // comparer.CompareInMem(compareFile);
                WorkMs += execTime;
                Console.WriteLine("{0,40} : {1} ms", Path.GetFileName(compareFile), execTime);
            }

            /*
            Parallel.ForEach(files_compare, (currentFile) =>
            {
                Stopwatch pws = new Stopwatch();
                pws.Start();

                comparer.CompareInMem(currentFile);

                pws.Stop();
                WorkMs += (int)pws.ElapsedMilliseconds;
               Console.WriteLine("{0,40} : {1} ms", Path.GetFileName(currentFile), pws.ElapsedMilliseconds);
            });
            */
            sw.Stop();
            Console.WriteLine("Memory, общее время выполнения {0}ms", WorkMs);


            Console.ReadKey();
        }


        static int ExecCompare(string currentFile, string[] base_files)
        {
            /*
            Stopwatch pws = new Stopwatch();
            pws.Start();
            AudioComparer comparer = new AudioComparer(currentFile);

            foreach (var file_base in base_files)
            {
                comparer.Compare(file_base);
                //  Console.WriteLine("Executed... {0}", sw.ElapsedMilliseconds);
            }
            pws.Stop();
            Console.WriteLine("{0,40} : {1} ms", Path.GetFileName(currentFile), pws.ElapsedMilliseconds);

            return (int)pws.ElapsedMilliseconds;
            */
            return 0;
        }
    }
}




