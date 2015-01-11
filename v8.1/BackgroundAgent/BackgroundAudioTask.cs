using System;
using System.Diagnostics;
using System.Threading;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Playback;

namespace BackgroundAgent
{
    public sealed class BackgroundAudioTask : IBackgroundTask
    {
        private bool _backgroundtaskrunning;
        private BackgroundTaskDeferral _deferral;
        private SystemMediaTransportControls _systemmediatransportcontrol;
        private ForegroundAppStatus _foregroundAppState = ForegroundAppStatus.Unknown;
        private readonly AutoResetEvent _backgroundTaskStarted = new AutoResetEvent(false);

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _systemmediatransportcontrol = SystemMediaTransportControls.GetForCurrentView();
            _systemmediatransportcontrol.ButtonPressed += SystemmediatransportcontrolOnButtonPressed;
            _systemmediatransportcontrol.PropertyChanged += SystemmediatransportcontrolOnPropertyChanged;
            _systemmediatransportcontrol.IsEnabled = true;
            _systemmediatransportcontrol.IsPauseEnabled = true;
            _systemmediatransportcontrol.IsPlayEnabled = true;
            _systemmediatransportcontrol.IsNextEnabled = false;
            _systemmediatransportcontrol.IsPreviousEnabled = false;


            taskInstance.Canceled += taskInstance_Canceled;
            taskInstance.Task.Completed += Task_Completed;

            var value = ApplicationSettingsHelper.ReadResetSettingsValue(Constants.AppState);
            if (value == null)
                _foregroundAppState = ForegroundAppStatus.Unknown;
            else
                _foregroundAppState = (ForegroundAppStatus) Enum.Parse(typeof (ForegroundAppStatus), value.ToString());

            BackgroundMediaPlayer.Current.CurrentStateChanged += Current_CurrentStateChanged;

            BackgroundMediaPlayer.MessageReceivedFromForeground += BackgroundMediaPlayer_MessageReceivedFromForeground;

            if (_foregroundAppState != ForegroundAppStatus.Suspended)
            {
                var message = new ValueSet();
                message.Add(Constants.BackgroundTaskStarted, "");
                BackgroundMediaPlayer.SendMessageToForeground(message);
            }

            _backgroundTaskStarted.Set();
            BackgroundMediaPlayer.Current.SetUriSource(new Uri(Constants.StreamUri));
            _backgroundtaskrunning = true;

            ApplicationSettingsHelper.SaveSettingsValue(Constants.BackgroundTaskState, Constants.BackgroundTaskRunning);
            _deferral = taskInstance.GetDeferral();
        }

        private void BackgroundMediaPlayer_MessageReceivedFromForeground(object sender,
            MediaPlayerDataReceivedEventArgs e)
        {
            foreach (var key in e.Data.Keys)
            {
                switch (key.ToLower())
                {
                    case Constants.AppSuspended:
                        Debug.WriteLine("App suspending");
                            // App is suspended, you can save your task state at this point
                        _foregroundAppState = ForegroundAppStatus.Suspended;
                        break;
                    case Constants.AppResumed:
                        Debug.WriteLine("App resuming"); // App is resumed, now subscribe to message channel
                        _foregroundAppState = ForegroundAppStatus.Active;
                        break;
                    case Constants.StartPlayback: //Foreground App process has signalled that it is ready for playback
                        Debug.WriteLine("Starting Playback");
                        StartPlayback();
                        break;
                }
            }
        }

        private void Current_CurrentStateChanged(MediaPlayer sender, object args)
        {
            if (sender.CurrentState == MediaPlayerState.Playing)
            {
                _systemmediatransportcontrol.PlaybackStatus = MediaPlaybackStatus.Playing;
            }
            else if (sender.CurrentState == MediaPlayerState.Paused)
            {
                _systemmediatransportcontrol.PlaybackStatus = MediaPlaybackStatus.Paused;
            }
        }

        private void SystemmediatransportcontrolOnPropertyChanged(SystemMediaTransportControls sender,
            SystemMediaTransportControlsPropertyChangedEventArgs args)
        {
        }

        private void SystemmediatransportcontrolOnButtonPressed(SystemMediaTransportControls sender,
            SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    Debug.WriteLine("UVC play button pressed");
                    // If music is in paused state, for a period of more than 5 minutes, 
                    //app will get task cancellation and it cannot run code. 
                    //However, user can still play music by pressing play via UVC unless a new app comes in clears UVC.
                    //When this happens, the task gets re-initialized and that is asynchronous and hence the wait
                    if (!_backgroundtaskrunning)
                    {
                        var result = _backgroundTaskStarted.WaitOne(2000);
                        if (!result)
                            throw new Exception("Background Task didnt initialize in time");
                    }
                    StartPlayback();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    Debug.WriteLine("UVC pause button pressed");
                    try
                    {
                        BackgroundMediaPlayer.Current.Pause();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                    }
                    break;
            }
        }

        private void StartPlayback()
        {
            _systemmediatransportcontrol.PlaybackStatus = MediaPlaybackStatus.Playing;
            _systemmediatransportcontrol.DisplayUpdater.Type = MediaPlaybackType.Music;
            _systemmediatransportcontrol.DisplayUpdater.MusicProperties.Title = "AAM Aadmi Radio";
            _systemmediatransportcontrol.DisplayUpdater.Update();
            BackgroundMediaPlayer.Current.Play();
        }

        private void Task_Completed(BackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs args)
        {
            Debug.WriteLine("Task Completed");
            _deferral.Complete();
        }

        private void taskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            ApplicationSettingsHelper.SaveSettingsValue(Constants.BackgroundTaskState, Constants.BackgroundTaskCancelled);
            ApplicationSettingsHelper.SaveSettingsValue(Constants.AppState,
                Enum.GetName(typeof (ForegroundAppStatus), _foregroundAppState));
            _backgroundtaskrunning = false;

            _systemmediatransportcontrol.ButtonPressed -= SystemmediatransportcontrolOnButtonPressed;
            _systemmediatransportcontrol.PropertyChanged -= SystemmediatransportcontrolOnPropertyChanged;

            BackgroundMediaPlayer.Shutdown();
            Debug.WriteLine("Task Cancelled");
            _deferral.Complete();
        }
    }

    internal enum ForegroundAppStatus
    {
        Active,
        Suspended,
        Unknown
    }
}