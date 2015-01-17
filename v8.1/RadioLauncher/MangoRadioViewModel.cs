using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Playback;
using Windows.UI.Core;
using App1.Annotations;
using BackgroundAgent;

namespace RadioLauncher
{
    public class MangoRadioViewModel : INotifyPropertyChanged
    {
        private bool _isPlaying;
        private ICommand _playCommand;
        private bool _isMyBackgroundTaskRunning;
        private readonly AutoResetEvent _serverInitialized = new AutoResetEvent(false);

        public MangoRadioViewModel(CoreDispatcher dispatcher_)
        {
            Dispatcher = dispatcher_;
        }

        public CoreDispatcher Dispatcher { get; set; }

        public bool IsPlaying
        {
            get { return _isPlaying; }
            set
            {
                _isPlaying = value;
                OnPropertyChanged();
            }
        }

        private bool IsMyBackgroundTaskRunning
        {
            get
            {
                if (_isMyBackgroundTaskRunning)
                    return true;

                var value = ApplicationSettingsHelper.ReadResetSettingsValue(Constants.BackgroundTaskState);
                if (value == null)
                {
                    return false;
                }
                _isMyBackgroundTaskRunning = ((String) value).Equals(Constants.BackgroundTaskRunning);
                return _isMyBackgroundTaskRunning;
            }
        }

        public ICommand PlayCommand
        {
            get
            {
                return _playCommand ??
                       (_playCommand =
                           new DelegateCommand(obj =>
                           {
                               IsPlaying = !IsPlaying;
                               PlayOrPause();
                           },
                               obj => true
                               ));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Suspend()
        {
            RemoveMediaPlayerEventHandlers();
        }

        public void Resume()
        {
            // Verify if the task was running before
            if (IsMyBackgroundTaskRunning)
            {
                //if yes, reconnect to media play handlers
                AddMediaPlayerEventHandlers();

                //send message to background task that app is resumed, so it can start sending notifications
                var messageDictionary = new ValueSet {{Constants.AppResumed, DateTime.Now.ToString()}};
                BackgroundMediaPlayer.SendMessageToBackground(messageDictionary);

                if (BackgroundMediaPlayer.Current.CurrentState == MediaPlayerState.Playing)
                {
                    IsPlaying = true;
                }
                else
                {
                    IsPlaying = false;
                }
            }
            else
            {
                IsPlaying = true;
            }
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private void PlayOrPause()
        {
            Debug.WriteLine("Play button pressed from App");
            if (IsMyBackgroundTaskRunning)
            {
                if (MediaPlayerState.Playing == BackgroundMediaPlayer.Current.CurrentState)
                {
                    BackgroundMediaPlayer.Current.Pause();
                }
                else if (MediaPlayerState.Paused == BackgroundMediaPlayer.Current.CurrentState)
                {
                    BackgroundMediaPlayer.Current.Play();
                }
                else if (MediaPlayerState.Closed == BackgroundMediaPlayer.Current.CurrentState)
                {
                    StartBackgroundAudioTask();
                }
            }
            else
            {
                StartBackgroundAudioTask();
            }
        }

        private void AddMediaPlayerEventHandlers()
        {
            BackgroundMediaPlayer.Current.CurrentStateChanged += MediaPlayer_CurrentStateChanged;
            BackgroundMediaPlayer.MessageReceivedFromBackground += BackgroundMediaPlayer_MessageReceivedFromBackground;
        }

        private void RemoveMediaPlayerEventHandlers()
        {
            BackgroundMediaPlayer.Current.CurrentStateChanged -= MediaPlayer_CurrentStateChanged;
            BackgroundMediaPlayer.MessageReceivedFromBackground -= BackgroundMediaPlayer_MessageReceivedFromBackground;
        }

        private async void MediaPlayer_CurrentStateChanged(MediaPlayer sender, object args)
        {
            switch (sender.CurrentState)
            {
                case MediaPlayerState.Playing:
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { IsPlaying = true; }
                        );

                    break;
                case MediaPlayerState.Paused:
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { IsPlaying = false; }
                        );

                    break;
            }
        }

        private async void BackgroundMediaPlayer_MessageReceivedFromBackground(object sender,
            MediaPlayerDataReceivedEventArgs e)
        {
            foreach (var key in e.Data.Keys)
            {
                switch (key)
                {
                    case Constants.BackgroundTaskStarted:
                        //Wait for Background Task to be initialized before starting playback
                        Debug.WriteLine("Background Task started");
                        _serverInitialized.Set();
                        break;
                }
            }
        }

        private void StartBackgroundAudioTask()
        {
            AddMediaPlayerEventHandlers();
            var backgroundtaskinitializationresult = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var result = _serverInitialized.WaitOne(5000);
                //Send message to initiate playback
                if (result)
                {
                    var message = new ValueSet();
                    message.Add(Constants.StartPlayback, "0");
                    BackgroundMediaPlayer.SendMessageToBackground(message);
                }
                else
                {
                    throw new Exception("Background Audio Task didn't start in expected time");
                }
            }
                );
            backgroundtaskinitializationresult.Completed = BackgroundTaskInitializationCompleted;
        }

        private void BackgroundTaskInitializationCompleted(IAsyncAction action, AsyncStatus status)
        {
            if (status == AsyncStatus.Completed)
            {
                Debug.WriteLine("Background Audio Task initialized");
            }
            else if (status == AsyncStatus.Error)
            {
                Debug.WriteLine("Background Audio Task could not initialized due to an error ::" + action.ErrorCode);
            }
        }
    }
}