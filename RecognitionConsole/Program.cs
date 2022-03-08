using System;

namespace SoundRecognitionConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            var percentMatch = .8f; //needed match percent 
            var recognitionMethod = EnumRecognitionMethod.MSSpeech; //choosen algorithm

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
    }
}
