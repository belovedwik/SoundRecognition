using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DeltaM.DeltaTell.Core.Entities.Base;
using DeltaM.DeltaTell.Core.Entities.Voip;
using DeltaM.DeltaTell.Core.Enums;
using DeltaM.DeltaTell.Core.Helpers;
using DeltaM.DeltaTell.Core.Infrastructure;
using DeltaM.DeltaTell.Core.Transport;
using DeltaM.DeltaTell.Core.Voip;
using Newtonsoft.Json;

namespace DeltaM.DeltaTell.Amd
{
    public class AmdDectionService : IAmdDetectionService
    {
        private readonly VoipTelephony _telephony;
        private readonly ISftpClient _sftpClient;
        private readonly INameHelper _nameHelper;

        private AudioComparer _comparer;
        private string _helloText => SettingsProvider.Current.Settings["HelloText"];
        private string _language => SettingsProvider.Current.Settings["IvmLanguage"];

        private string _amdRecordsDir => SettingsProvider.Current.Settings["AmdRecordsDirectory"];

        private string _amdRecordsFormat => SettingsProvider.Current.Settings["AmdRecordsFormat"];

        private bool? _log;
        private AutodetectSettings _autodetectCfg;

        private bool Log
        {

            get
            {
                //if (_log != null) return 
                //        _log.Value;
                var log = false;
                bool.TryParse(SettingsProvider.Current.Settings["AmdLog"], out log);
                _log = log;
                return _log.Value;
            }
        }

        public AmdDectionService(VoipTelephony telephony, INameHelper nameHelper, ISftpClient stfp)
        {
            _telephony = telephony;
            _sftpClient = stfp;
            _nameHelper = nameHelper;
        }


        public void Init()
        {
             AmdDetector();
        }

        public string LoadAutodetectFiles(AudioComparer comparer)
        {
            comparer.ClearBase();
            foreach (string baseFile in Directory.GetFiles(comparer.BaseAudioDir, _amdRecordsFormat))
            {
                comparer.AddToBase(baseFile);
            }
            var path = comparer.BaseAudioDir + "/invalid";
            if (Directory.Exists(path))
            {
                foreach (string baseFile in Directory.GetFiles(path, _amdRecordsFormat))
                {
                    comparer.AddToBase(baseFile, AutoAnswerType.AutoAnswerInvalid);
                }
            }
            return $"{comparer.EnvelopeBaseList.Count(b => b.Key.AnswerType == AutoAnswerType.AutoAnswer)} records, AutoAnswerInvalid {comparer.EnvelopeBaseList.Count(b => b.Key.AnswerType == AutoAnswerType.AutoAnswerInvalid)} records";

        }

        private bool AmdDetector()
        {
            var initOk = false; //indicates that comparer and comboboxes were initialized without errors

            try
            {
                InitializeComboboxes(); //initialize combobox with saved values

                InitializeComparer(); //initialize comparer and display log results

                initOk = true;
            }
            catch (Exception e)
            {
                AnyLog.Log.Error("AudioComparerError: " + e.Message);
            }

            return initOk;
        }

        /// <summary>
        /// Initialize Before/After comboboxes with saved values
        /// </summary>
        private void InitializeComboboxes()
        {
            _autodetectCfg = new AutodetectSettings();

            try
            {
                var settings = SettingsProvider.Current.Settings["AMDSettings"]; //get settings from Database
                var amdSettings = JsonConvert.DeserializeObject<AmdSettings>(settings); //deserialize received data from Database

                //if there is a value in deserialized value
                if (!ReferenceEquals(amdSettings, null))
                {
                    _autodetectCfg.AfterConnection.Enabled = amdSettings.AfterChecking; //is perform check before pickup dial
                    _autodetectCfg.BeforeConnection.Enabled = amdSettings.BeforeChecking; //is perform check after pickup dial

                    _autodetectCfg.BeforeConnection.Metod = (AutoDetectMethod)Enum.Parse(typeof(AutoDetectMethod), amdSettings.SelectedBeforeCheckingMethod); //method to perfrom auto detect (before pickup dial)
                    _autodetectCfg.AfterConnection.Metod = (AutoDetectMethod)Enum.Parse(typeof(AutoDetectMethod), amdSettings.SelectedAfterCheckingMethod); //method ti perform auto detect (after pickup dial)

                    _autodetectCfg.AfterConnection.IsNeedTrim = amdSettings.IsAfterTrimming; //indicate it is should perform trim when try to detect auto (before pickup dial)
                    _autodetectCfg.BeforeConnection.IsNeedTrim = amdSettings.IsBeforeTrimming; //indicate it is should perform trim when try to detect auto (after pickup dial)

                    _autodetectCfg.AfterConnection.NeedTrimValue = amdSettings.AfterTrimValue; //length of needed audio file length (before pickup dial)
                    _autodetectCfg.BeforeConnection.NeedTrimValue = amdSettings.BeforeTrimValue; //length of needed audio file length (after pickup dial)

                    _autodetectCfg.AfterConnection.SilenceDetectMethod = (SilenceDetectMethod)Enum.Parse(typeof(SilenceDetectMethod), amdSettings.CheckSilenceMethod); //indicate is should perform silence recognition (after pickup dial)

                    //----------------------OLD remove after time 22/07/19
                    //_autodetectCfg.AfterConnection.Enabled = bool.Parse(SettingsProvider.Current.Settings["AfterCheckingEnabled"]);
                    //_autodetectCfg.BeforeConnection.Enabled = bool.Parse(SettingsProvider.Current.Settings["BeforeCheckingEnabled"]);

                    //_autodetectCfg.BeforeConnection.Metod = (AutoDetectMethod)Enum.Parse(typeof(AutoDetectMethod), SettingsProvider.Current.Settings["BeforeCheckingMethod"]);
                    //_autodetectCfg.AfterConnection.Metod = (AutoDetectMethod)Enum.Parse(typeof(AutoDetectMethod), SettingsProvider.Current.Settings["AfterCheckingMethod"]);
                }
            }
            catch (Exception ex) //couldn't initialize settings
            {
                AnyLog.Log.Error($"AmdDectionService.InitializeComboboxes> Couldn't initialize AMD settings.{Environment.NewLine}{ex}"); //write error about that we can't initialize settings
            }
        }

        /// <summary>
        /// Initialize auto comparer
        /// </summary>
        private void InitializeComparer()
        {
            _comparer = new AudioComparer(0.80f)
            {
                IsShowConsoleDebug = true
            }; // 80% совпадения входящего файла с базовым == АВТООТВЕТЧИК
            
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _comparer.BaseAudioDir = Path.Combine(baseDir, _amdRecordsDir);

            var tmpDir = Path.Combine(_comparer.BaseAudioDir, "tmp");

            AnyLog.Log.Info($"Path to. BaseDir> {baseDir}; BaseAudioDir> {_comparer.BaseAudioDir}; TmpDir> {tmpDir}");

            if (!Directory.Exists(tmpDir))
                Directory.CreateDirectory(tmpDir);

            DisplayLogResults();

        }

        /// <summary>
        /// displays: MSSpeech is available; Grammars for MSSpeech is available
        /// </summary>
        private void DisplayLogResults()
        {
            try
            {
                //seperator
                AnyLog.Log.Info($"{new String('=', 16)} AMD module {new String('=', 16)}");

                //display is comparer can perform MSSpeech recognition
                AnyLog.Log.Info($"{new String('=', 16)} MSSpeech recognition is available: {_comparer.IsCanUseMSSpeech} {new String('=', 16)}");

                //if comparer can perform MSSpeech recognition
                if (_comparer.IsCanUseMSSpeech)
                    //display - there is available grammar for recognition
                    AnyLog.Log.Info($"{new String('=', 16)} Grammars from App.config is available: {_comparer.IsCanUseCurrentGrammar} {new String('=', 16)}");

                var res = LoadAutodetectFiles(_comparer);
                AnyLog.Log.Info($"AMDDetector: {res}");
            }
            catch (COMException) { }
        }

        public async Task Detect(Dial dial)
        {
            try
            {
                bool hasAutoAnswer = false;
                var channelName = dial.Channel?.Name;

                if (_autodetectCfg != null && !string.IsNullOrEmpty(channelName))
                {
                    var befCfg = _autodetectCfg.BeforeConnection;
                    if (befCfg?.Enabled == true)
                        hasAutoAnswer = await IsAutoAnswer(channelName, dial, befCfg.Metod,
                            befCfg.IsNeedTrim ?? false, befCfg.NeedTrimValue,
                            SilenceDetectMethod.None, beforeConnect: true);

                    var afterCfg = _autodetectCfg.AfterConnection;
                    if (!hasAutoAnswer && dial.Status != EnumStatusOfDial.Finished && afterCfg?.Enabled == true)
                        hasAutoAnswer = await IsAutoAnswer(channelName, dial, _autodetectCfg.AfterConnection.Metod,
                            afterCfg.IsNeedTrim ?? false, afterCfg.NeedTrimValue,
                            afterCfg.SilenceDetectMethod, beforeConnect: false);

                    if (hasAutoAnswer)
                    {
                        _telephony.Drop(channelName);
                        if (dial.FinishReason == EnumFinishReasonOfDial.DestanationHangUp)
                            AnyLog.Log.Info("DestanationHangUp: No such channel " + channelName + ", number " + dial?.DestChannel?.Phone?.Number);
                        else
                            AnyLog.Log.Info("AutoMachine detect " + channelName + ", number " + dial.DestChannel?.Phone?.Number);
                    }
                }
            }
            catch { }
        }

        public void ApplySettings(AmdSettings settings)
        {
            _autodetectCfg = new AutodetectSettings()
            {
                BeforeConnection = new AutodetectConnection()
                {
                    Enabled = settings.BeforeChecking,
                    Metod = (AutoDetectMethod) Enum.Parse(
                        typeof(AutoDetectMethod), settings.SelectedBeforeCheckingMethod),
                    IsNeedTrim = settings.IsBeforeTrimming,
                    NeedTrimValue = settings.BeforeTrimValue
                },
                AfterConnection = new AutodetectConnection()
                {
                    Enabled = settings.AfterChecking,
                    Metod = (AutoDetectMethod) Enum.Parse(
                        typeof(AutoDetectMethod), settings.SelectedAfterCheckingMethod),
                    IsNeedTrim = settings.IsAfterTrimming,
                    NeedTrimValue = settings.AfterTrimValue,
                    SilenceDetectMethod = (SilenceDetectMethod)Enum.Parse(typeof(SilenceDetectMethod), settings.CheckSilenceMethod)
                }

            };
                       
            SettingsProvider.Current.Settings["AMDSettings"] = JsonConvert.SerializeObject(settings);


            //SettingsProvider.Current.Settings["BeforeCheckingEnabled"] = settings.BeforeChecking.ToString();
            //SettingsProvider.Current.Settings["AfterCheckingEnabled"] = settings.AfterChecking.ToString();
            //SettingsProvider.Current.Settings["BeforeCheckingMethod"] = settings.SelectedBeforeCheckingMethod;
            //SettingsProvider.Current.Settings["AfterCheckingMethod"] = settings.SelectedAfterCheckingMethod;
        }

        public AmdSettings GetCurrentSettings()
        {
            return new AmdSettings()
            {
                AfterChecking = _autodetectCfg.AfterConnection.Enabled ?? false,
                BeforeChecking = _autodetectCfg.BeforeConnection.Enabled ?? false,
                SelectedBeforeCheckingMethod = _autodetectCfg.BeforeConnection.Metod.ToString(),
                SelectedAfterCheckingMethod = _autodetectCfg.AfterConnection.Metod.ToString(),
                IsAfterTrimming = _autodetectCfg.AfterConnection.IsNeedTrim ?? false,
                IsBeforeTrimming = _autodetectCfg.BeforeConnection.IsNeedTrim ?? false,
                AfterTrimValue = _autodetectCfg.AfterConnection.NeedTrimValue,
                BeforeTrimValue = _autodetectCfg.BeforeConnection.NeedTrimValue,
                CheckSilenceMethod = _autodetectCfg.AfterConnection.SilenceDetectMethod.ToString(),

                AvailableAmdMethods = GetAvailableMethods(), //initialize combobox list 
                AvailableSilenceMethods = GetAvailableSilenceMethods() //initialize silence list
            };
        }

        /// <summary>
        /// Create list of available methods to recognize auto
        /// </summary>
        /// <returns>List of available methods to recognize auto</returns>
        public string[] GetAvailableMethods()
        {
            var listOfAvailableMethods = new List<string>(); //list that will contains algorithm to determine auto

            //iterate through AutoDetectMethod enum
            foreach (var item in Enum.GetValues(typeof(AutoDetectMethod)).Cast<AutoDetectMethod>())
            {
                //if current item is AutoDetectMethod.MSSpeech and MSSpeech can't be used on this PC
                if (item == AutoDetectMethod.MSSpeech && (!_comparer?.IsCanUseMSSpeech ?? false))
                    continue; //move to next method

                //add method's name to list
                listOfAvailableMethods.Add(item.ToString());
            }

            return listOfAvailableMethods.ToArray();
        }

        public string[] GetAvailableSilenceMethods()
        {
            var listOfAvailableMethods = new List<string>(); //list that will contains algorithm to determine auto

            //iterate through AutoDetectMethod enum
            foreach (var item in Enum.GetValues(typeof(SilenceDetectMethod)).Cast<SilenceDetectMethod>())
            {
                //if current item is AutoDetectMethod.MSSpeech and MSSpeech can't be used on this PC
                if (item == SilenceDetectMethod.MSSpeech && (!_comparer?.IsCanUseMSSpeech ?? false))
                    continue; //move to next method

                //add method's name to list
                listOfAvailableMethods.Add(item.ToString());
            }

            return listOfAvailableMethods.ToArray();
        }

        private async Task<bool> IsAutoAnswer(string parkedChannel, Dial dial, AutoDetectMethod method, bool isNeedTrim, int NeedTrimValue, 
            SilenceDetectMethod silenceDetectMethod = SilenceDetectMethod.None, 
            bool beforeConnect = false)
        {

            try
            {
                if (dial.IsAutoMachine)
                    return true;

                int maxExecutionTime = 5000;
                if (beforeConnect)
                    maxExecutionTime = 2000;

                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMilliseconds(maxExecutionTime));

                var autoDetectTask = Detect(parkedChannel, dial, cts.Token, method, isNeedTrim, NeedTrimValue, silenceDetectMethod, beforeConnect: beforeConnect);
                var resultTask = await Task.WhenAny(new Task[] { autoDetectTask, Task.Delay(maxExecutionTime) });

                var isAutoMachine = false;
                if (resultTask == autoDetectTask)
                    isAutoMachine = autoDetectTask.Result;

                if (!autoDetectTask.IsCompleted)
                    AnyLog.Log.Info($"AmdDetector was not completed {parkedChannel}, {dial?.DestChannel?.Phone?.Number}");

                AnyLog.Log.Info($"AmdDetector: {parkedChannel}, number {dial?.DestChannel?.Phone?.Number}, dial:{dial.Id}, isAutoMachine:{isAutoMachine}");

                return isAutoMachine;
            }
            catch { }

            return false;
        }


        private async Task<bool> Detect(string channelName, Dial dial, CancellationToken ct, AutoDetectMethod method, 
            bool isNeedTrim, int needTrimValue, SilenceDetectMethod silenceDetectMethod, bool beforeConnect = false)
        {
            bool result = false;
            string fileName = string.Empty;
            var strMetod = method + (beforeConnect ? "_before" : "_after");
            var localFile = string.Empty;
         //   var wf = Tell.GetWorkflow();

            try
            {
                var name = channelName.Substring(channelName.LastIndexOf('/') + 1) + strMetod;
                fileName = $"{name}-in.wav";

                if (!beforeConnect)
                {
                    //Log.Info($"Start MoveToAsyncAgi {channelName}");
                   // await Asterisk.MoveToAsyncAgi(channelName);
                    //Log.Info($"End MoveToAsyncAgi {channelName}");

                    await Task.Delay(100, ct);
                    //Thread.Sleep(100);

                    ct.ThrowIfCancellationRequested(); // прерываем выполнение Таска, если он долго выполнялся

                    var monStart = await _telephony.MonitorStart(channelName, $"/tmp/{name}");

                    //  var monStart = await Asterisk.Ami.Monitor(channelName, $"/tmp/{name}", "wav", "false");
                    AnyLog.Log.Info($"Start monitor {channelName} result:" + monStart);

                    _telephony.PlaybackText(channelName, _helloText, _language);
                    AnyLog.Log.Info($"AMD SayText ok {channelName}");

                    await Task.Delay(3000, ct);
                    //Thread.Sleep(3000);
                }
                
                var monStop = await _telephony.MonitorStop(channelName);
                AnyLog.Log.Info($"Stop monitor {channelName} result:" + monStop);

                if (monStop == "No such channel")
                {
                    AnyLog.Log.Info($"Autodetect exit and drop: " + monStop);
                    dial.FinishReason = EnumFinishReasonOfDial.DestanationHangUp;
                    return true;
                }

                await Task.Delay(300, ct); // ждем пока будет записана музыка
                //Thread.Sleep(100);

                ct.ThrowIfCancellationRequested(); // снова прерываем выполнение Таска, если он долго выполнялся

                //  var _sftp = new AsteriskSshClient(Tell.audioComparer.BaseAudioDir);
                var localFileName = Path.Combine(_comparer.BaseAudioDir, "tmp", fileName);
                localFile = _sftpClient.SaveToLocalFile(fileName, localFileName);

                if (string.IsNullOrEmpty(localFile))
                {
                    AnyLog.Log.Info($"Autodetect: Cant get file {fileName} {strMetod}, exit");
                    return false;
                }

                ct.ThrowIfCancellationRequested(); // снова прерываем выполнение Таска, если он долго выполнялся

                AudioComparerResult res = new AudioComparerResult(fileName, 0, 0f, AutoAnswerType.Silence);
                //lock (_comparer.sync)
                {

                    result = _comparer.PerformRecognition(method, localFile, out res, isNeedTrim: isNeedTrim, needTrimValue: needTrimValue, silenceDetectMethod);
                    //switch (method)
                    //{
                    //    case AutoDetectMethod.Hard:
                    //        result = _comparer.HasEquivalentHard(localFile, out res);
                    //        break;

                    //    case AutoDetectMethod.Partial:
                    //        result = _comparer.HasEquivalentPartial(localFile, out res);
                    //        break;

                    //    case AutoDetectMethod.Partial75:
                    //        result = _comparer.HasEquivalentPartial75(localFile, out res);
                    //        break;

                    //    case AutoDetectMethod.MSSpeech:
                    //        result = _comparer.HasEquivalentMSSpeech(localFile, out res, false);
                    //        break;

                    //    default:
                    //        result = _comparer.HasEquivalent(localFile, out res);
                    //        break;
                    //}
                }

                if (result)
                {
                    dial.FinishReason = res.aaType == AutoAnswerType.AutoAnswerInvalid ? EnumFinishReasonOfDial.WrongNumber : EnumFinishReasonOfDial.AutoAnswerMachine;
                    dial.IsAutoMachine = true;
                    AnyLog.Log.Info($"Autodetect: AutoMachine_{dial?.DestChannel?.Phone?.Number + fileName} {strMetod}, {res}");
                }
                else
                    AnyLog.Log.Info($"Autodetect: Human_{dial?.DestChannel?.Phone?.Number + fileName} {strMetod}");

                //File.Move(localFile, Path.Combine(DirName, (result ? "AutoMachine_" : "Human_") + dial.DestIdNum + fileName));
                // AsyncAgi.ProcessCommand(channelName, $"EXEC System 'rm /tmp/{fileName}'");
            }
            catch (OperationCanceledException)
            {
                AnyLog.Log.Info(
                    $"AmdDetectorErr: Cancelled, executed task too long. File:{fileName} {strMetod}, Channel:{channelName}");
            }
            catch (Exception e)
            {
                AnyLog.Log.Info($"AmdDetectorErr: {e.Message}; fileName:{fileName}; Channel:{channelName} {strMetod}");
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrEmpty(localFile))
                    {
                        if (Log && File.Exists(localFile))
                        {
                            var dirName = Path.GetDirectoryName(localFile);
                            File.Move(localFile, Path.Combine(dirName, (result ? "AutoMachine_" : "Human_") + dial?.DestChannel?.Phone?.Number + fileName));
                        }
                        else
                        {
                            var dirName = Path.GetDirectoryName(localFile);
                            File.Move(localFile, Path.Combine(dirName, (result ? "AutoMachine_" : "Human_") + dial?.DestChannel?.Phone?.Number + fileName));
                            //if (File.Exists(localFile))
                            //    File.Delete(localFile);
                        }
                    }
                }
                catch { }
            
            }
            return result;
        }
    }
}
