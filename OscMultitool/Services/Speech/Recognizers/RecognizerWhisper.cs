﻿using Hoscy.Services.Speech.Utilities.Whisper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Whisper;

namespace Hoscy.Services.Speech.Recognizers
{
    internal class RecognizerWhisper : RecognizerBase
    {
        new internal static RecognizerPerms Perms => new()
        {
            Description = "Local AI, quality / RAM usage varies, startup may take a while",
            UsesMicrophone = true,
            Type = RecognizerType.Whisper
        };

        internal override bool IsListening => _muteTimes.Count == 0 || _muteTimes[^1].End < DateTime.Now;

        private CaptureThread? _cptThread;
        private readonly List<TimeInterval> _muteTimes = new()
        {
            new(DateTime.MinValue, DateTime.MaxValue) //Default value - indefinite mute
        };

        #region Starting / Stopping
        protected override bool StartInternal()
        {
            try
            {
                Logger.Info("Attempting to load whisper model");
                var model = Library.loadModel(Config.Speech.WhisperModels[Config.Speech.WhisperModelCurrent]);

                var captureDevice = GetCaptureDevice();
                if (captureDevice == null)
                    return false;

                var ctx = model.createContext();
                ApplyParameters(ref ctx.parameters);

                Logger.Info("Starting whisper thread, this might take a while");
                CaptureThread thread = new(ctx, captureDevice);
                thread.StartException?.Throw();
                thread.SpeechRecognized += OnSpeechRecognized;
                _cptThread = thread;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return false;
            }
            return true;
        }

        protected override void StopInternal()
        {
            Textbox.EnableTyping(false);
            _cptThread?.Stop();
            _cptThread = null;
        }

        protected override bool SetListeningInternal(bool enabled)
        {
            if (IsListening == enabled)
                return false;

            if (enabled)
            {
                //Set mute records last time to not be indefinite
                if (_muteTimes.Count > 0)
                    _muteTimes[^1] = new(_muteTimes[^1].Start, DateTime.Now);
            }
            else
                //Add new indefinite mute record
                _muteTimes.Add(new(DateTime.Now, DateTime.MaxValue));

            return true;
        }
        #endregion

        #region Transcription
        private void OnSpeechRecognized(object? sender, sSegment[] segments)
        {
            if (_cptThread == null || segments.Length == 0) return;

            //Ensure segments are ordered correctly
            var sortedSegments = segments.OrderBy(x => x.time.begin);
            var strings = new List<string>();

            foreach (var segment in sortedSegments)
            {
                if (string.IsNullOrWhiteSpace(segment.text) || IsSpokenWhileMuted(_cptThread.StartTime, segment))
                    continue;

                var trimmedText = segment.text.TrimStart(' ', '-').TrimEnd(' ');

                var action = TryGetAction(trimmedText);
                if (action == null)
                {
                    strings.Add(trimmedText);
                    continue;
                }

                //Adding action
                foreach (var filter in Config.Speech.WhisperNoiseWhitelist) //todo: [WHISPER] display with filtered actions too
                {
                    if (filter.Matches(action))
                    {
                        strings.Add($"*{action}*");
                        break;
                    }
                }
                //Does nothing if action has no match, if more has to be added, code above needs to be adjusted
            }

            var denoised = Denoise(string.Join(' ', strings));
            if (!string.IsNullOrWhiteSpace(denoised))
                ProcessMessage(denoised);
        }

        /// <summary>
        /// Determines if a segment has been spoken while muted, also clears all unneeded values
        /// </summary>
        /// <param name="startTime">CaptureThread start time</param>
        /// <param name="segment">Segment to check</param>
        /// <returns>Was spoken while muted?</returns>
        private bool IsSpokenWhileMuted(DateTime startTime, sSegment segment)
        {
            var start = startTime + segment.time.begin;
            var end = startTime + segment.time.end;

            //Remove all unneeded values
            _muteTimes.RemoveAll(x => x.End <= start);

            if (_muteTimes.Any())
            {
                var first = _muteTimes.First();
                if (end > first.Start || start < first.End)
                    return true;
            }
            return false;
        }

        private static readonly Regex _actionDetector = new(@"^[\[\(\*](.+)[\*\)\]]$");
        /// <summary>
        /// Tries to detect if a text is an "action"
        /// </summary>
        /// <param name="text">Text to check</param>
        /// <returns>Formatted action / null</returns>
        private static string? TryGetAction(string text)
        {
            var match = _actionDetector.Match(text);
            if (!match.Success)
                return null;

            var actionText = match.Groups[1].Value;
            return actionText.ToLower();
        }
        #endregion

        #region Setup
        private static iAudioCapture? GetCaptureDevice()
        {
            Logger.Info("Attempting to grab capture device for whisper");
            var medf = Library.initMediaFoundation();
            if (medf == null)
            {
                Logger.Error("No media foundation could be found");
                return null;
            }

            var devices = medf.listCaptureDevices();
            if (devices == null)
            {
                Logger.Error("No audio devices could be found");
                return null;
            }

            CaptureDeviceId? deviceId = null;
            foreach (var device in devices)
            {
                if (device.displayName.StartsWith(Config.Speech.MicId))
                {
                    deviceId = device;
                    continue;
                }
            }

            if (deviceId == null)
            {
                Logger.Error("No matching audio device could be found");
                return null;
            }

            sCaptureParams cp = new()
            {
                dropStartSilence = 0.25f,
                minDuration = 1,
                maxDuration = Config.Speech.WhisperRecMaxDuration,
                pauseDuration = Config.Speech.WhisperRecPauseDuration
            };

            return medf.openCaptureDevice(deviceId.Value,cp);
        }

        private static void ApplyParameters(ref Parameters p)
        {
            //Threads
            var maxThreads = Environment.ProcessorCount;
            var cfgThreads = Config.Speech.WhisperThreads;
            p.cpuThreads = cfgThreads > maxThreads || cfgThreads == 0 ? maxThreads : cfgThreads;

            //Normal Flags
            p.setFlag(eFullParamsFlags.SingleSegment, Config.Speech.WhisperSingleSegment);
            p.setFlag(eFullParamsFlags.Translate, Config.Speech.WhisperToEnglish);
            p.setFlag(eFullParamsFlags.SpeedupAudio, Config.Speech.WhisperSpeedup);

            //Number Flags
            if (Config.Speech.WhisperMaxContext >= 0)
                p.n_max_text_ctx = Config.Speech.WhisperMaxContext;
            p.setFlag(eFullParamsFlags.TokenTimestamps, Config.Speech.WhisperMaxSegLen > 0);
            p.max_len = Config.Speech.WhisperMaxSegLen;

            p.language = Config.Speech.WhisperLanguage;
            
            //Hardcoded
            p.thold_pt = 0.01f;
            p.duration_ms = 0;
            p.offset_ms = 0;
            p.setFlag(eFullParamsFlags.PrintRealtime, false);
            p.setFlag(eFullParamsFlags.PrintTimestamps, false);
        }
        #endregion
    }
}