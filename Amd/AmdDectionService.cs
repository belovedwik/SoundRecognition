using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DeltaM.DeltaTell.Core.Entities.Base;
using DeltaM.DeltaTell.Core.Entities.Voip;
using DeltaM.DeltaTell.Core.Enums;
using DeltaM.DeltaTell.Core.Helpers;
using DeltaM.DeltaTell.Core.Infrastructure;
using DeltaM.DeltaTell.Core.Transport;
using DeltaM.DeltaTell.Core.Voip;


namespace DeltaM.DeltaTell.Amd
{
    public  class AmdDectionService:IAmdDetectionService
    {
        private readonly VoipTelephony _telephony;
        private readonly ISftpClient _sftpClient;
        private readonly INameHelper _nameHelper;

        private AudioComparer _comparer;
        private string _helloText => SettingsProvider.Current.Settings["HelloText"];
        private string _language => SettingsProvider.Current.Settings["IvmLanguage"];

        private string _amdRecordsDir=> SettingsProvider.Current.Settings["AmdRecordsDirectory"];

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


        public async void Init()
        {
            await AmdDetector();
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

        private async Task<bool> AmdDetector()
        {
            var initOk = false;

            try
            {
                _autodetectCfg=new AutodetectSettings();

                _autodetectCfg.AfterConnection.Enabled = bool.Parse(SettingsProvider.Current.Settings["AfterCheckingEnabled"]);
                _autodetectCfg.BeforeConnection.Enabled = bool.Parse(SettingsProvider.Current.Settings["BeforeCheckingEnabled"]);
                _autodetectCfg.BeforeConnection.Metod = (AutodetectConnection.AutoDetectMetod) Enum.Parse(typeof(AutodetectConnection.AutoDetectMetod), SettingsProvider.Current.Settings["BeforeCheckingMethod"]);
                _autodetectCfg.AfterConnection.Metod = (AutodetectConnection.AutoDetectMetod) Enum.Parse(typeof(AutodetectConnection.AutoDetectMetod),SettingsProvider.Current.Settings["AfterCheckingMethod"]);

                _comparer = new AudioComparer(0.80f)
                {
                    showConsoleDebug = true
                }; // 80% совпадения входящего файла с базовым = АВТООТВЕТЧИК
                var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);


                if (baseDir != null)
                    _comparer.BaseAudioDir = Path.Combine(baseDir, _amdRecordsDir);
                var tmpDir = Path.Combine(_comparer.BaseAudioDir, "tmp");

                if (!Directory.Exists(tmpDir))
                    Directory.CreateDirectory(tmpDir);

                var res = LoadAutodetectFiles(_comparer);
                AnyLog.Log.Info($"AMDDetector: {res}");


                initOk = true;
            }
            catch (Exception e)
            {
                AnyLog.Log.Error("AudioComparerError: " + e.Message);
            }

            return initOk;
        }


        public async Task Detect(Dial dial)
        {
            bool hasAutoAnswer = false;
            var channelName = dial.Channel?.Name;

            if (_autodetectCfg != null && !string.IsNullOrEmpty(channelName))
            {
               
                if (_autodetectCfg.BeforeConnection?.Enabled.HasValue == true && _autodetectCfg.BeforeConnection?.Enabled.Value == true)
                    hasAutoAnswer = await IsAutoAnswer(channelName, dial, _autodetectCfg.BeforeConnection.Metod, true);
                if (!hasAutoAnswer && dial.Status != EnumStatusOfDial.Finished &&
                    _autodetectCfg.AfterConnection?.Enabled.HasValue == true && _autodetectCfg.AfterConnection?.Enabled.Value == true)
                    hasAutoAnswer = await IsAutoAnswer(channelName, dial, _autodetectCfg.AfterConnection.Metod);
                
                if (hasAutoAnswer)
                {
                    if (dial.FinishReason == EnumFinishReasonOfDial.DestanationHangUp)
                    {
                        _telephony.Drop(channelName);
                        AnyLog.Log.Info("DestanationHangUp: No such channel " + channelName + ", number " + dial?.DestChannel?.Phone?.Number);
                    }
                    else
                    {
                        _telephony.Drop(channelName);
                        AnyLog.Log.Info("AutoMachine detect " + channelName + ", number " + dial.DestChannel?.Phone?.Number);
                    }
                }
            }

        }

        public void ApplySettings(AmdSettings settings)
        {
            _autodetectCfg = new AutodetectSettings()
            {
                BeforeConnection = new AutodetectConnection()
                {
                    Enabled = settings.BeforeChecking,
                    Metod = (AutodetectConnection.AutoDetectMetod) Enum.Parse(
                        typeof(AutodetectConnection.AutoDetectMetod), settings.SelectedBeforeCheckingMethod)
                },
                AfterConnection = new AutodetectConnection()
                {
                    Enabled = settings.AfterChecking,
                    Metod = (AutodetectConnection.AutoDetectMetod) Enum.Parse(
                        typeof(AutodetectConnection.AutoDetectMetod), settings.SelectedAfterCheckingMethod)
                }

            };
            SettingsProvider.Current.Settings["BeforeCheckingEnabled"] = settings.BeforeChecking.ToString();
            SettingsProvider.Current.Settings["AfterCheckingEnabled"] = settings.AfterChecking.ToString();
            SettingsProvider.Current.Settings["BeforeCheckingMethod"] = settings.SelectedBeforeCheckingMethod;
            SettingsProvider.Current.Settings["AfterCheckingMethod"] = settings.SelectedAfterCheckingMethod;
        }

        public AmdSettings GetCurrentSettigns()
        {
            return new AmdSettings()
            {
                AfterChecking = _autodetectCfg.AfterConnection.Enabled??false,
                BeforeChecking = _autodetectCfg.BeforeConnection.Enabled??false,
                SelectedBeforeCheckingMethod = _autodetectCfg.BeforeConnection.Metod.ToString(),
                SelectedAfterCheckingMethod = _autodetectCfg.AfterConnection.Metod.ToString(),
                AvailableAmdMethods = Enum.GetNames(typeof(AutodetectConnection.AutoDetectMetod))
            };
        }

        private async Task<bool> IsAutoAnswer(string parkedChannel, Dial dial, AutodetectConnection.AutoDetectMetod method, bool beforeConnect = false)
        {

            if (dial.IsAutoMachine)
                return true;

            int maxExecutionTime = 5000;
            if (beforeConnect)
                maxExecutionTime = 2000;

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(maxExecutionTime));

            var autoDetectTask = Detect(parkedChannel, dial, cts.Token, method, beforeConnect);
            var resultTask = await Task.WhenAny(new Task[] { autoDetectTask, Task.Delay(maxExecutionTime) });

            var isAutoMachine = false;
            if (resultTask == autoDetectTask)
                isAutoMachine = autoDetectTask.Result;

            if (!autoDetectTask.IsCompleted)
                AnyLog.Log.Info($"AmdDetector was not completed {parkedChannel}, {dial?.DestChannel?.Phone?.Number}");

            AnyLog.Log.Info($"AmdDetector: {parkedChannel}, number {dial?.DestChannel?.Phone?.Number}, dial:{dial.Id}, isAutoMachine:{isAutoMachine}");
            
            return isAutoMachine;
        }


        private async Task<bool> Detect(string channelName, Dial dial, CancellationToken ct, AutodetectConnection.AutoDetectMetod method, bool beforeConnect = false)
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

                AnyLog.Log.Info($"Stop monitor start ..{channelName}");

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

                AudioComparerResult res;
                //lock (_comparer.sync)
                {
                    switch (method)
                    {
                        case AutodetectConnection.AutoDetectMetod.Hard:
                            result = _comparer.HasEquivalentHard(localFile, out res);
                            break;
                        case AutodetectConnection.AutoDetectMetod.Partial:
                            result = _comparer.HasEquivalentPartial(localFile, out res);
                            break;
                        case AutodetectConnection.AutoDetectMetod.Partial75:
                            result = _comparer.HasEquivalentPartial75(localFile, out res);
                            break;
                        default:
                            result = _comparer.HasEquivalent(localFile, out res);
                            break;
                    }
                }

                if (result)
                {
                    dial.FinishReason = res.aaType == AutoAnswerType.AutoAnswer ? EnumFinishReasonOfDial.AutoAnswerMachine : EnumFinishReasonOfDial.WrongNumber;
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
                if (!string.IsNullOrEmpty(localFile))
                {
                    if (Log)
                    {
                        var dirName = Path.GetDirectoryName(localFile);
                        File.Move(localFile, Path.Combine(dirName, (result ? "AutoMachine_" : "Human_") + dial?.DestChannel?.Phone?.Number + fileName));
                    }
                    else
                    {
                        if (File.Exists(localFile))
                            File.Delete(localFile);
                    }
                }
            
            }
            return result;
        }
    }
}
