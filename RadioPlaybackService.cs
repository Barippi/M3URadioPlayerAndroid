using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using Android.Telephony;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Avalonia.Threading;

namespace M3URadioPlayer.Android;

[Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaPlayback)]
public class RadioPlaybackService : Service, AudioManager.IOnAudioFocusChangeListener
{
    public const string ChannelId = "radio_playback_channel";
    public const int NotificationId = 1001;

    public const string ActionStart = "ACTION_START";
    public const string ActionStop = "ACTION_STOP";
    public const string ActionUpdateTitle = "ACTION_UPDATE_TITLE";
    public const string ExtraStation = "EXTRA_STATION";
    public const string ExtraTitle = "EXTRA_TITLE";

    private string _stationName = "M3U Radio Player";
    private string _trackTitle = "再生中";

    // イヤホン切断検知レシーバー
    private BecomingNoisyReceiver? _noisyReceiver;
    private bool _isReceiverRegistered = false;

    // オーディオフォーカス管理
    private AudioManager? _audioManager;
    private AudioFocusRequestClass? _focusRequest;
    private bool _resumeOnFocusGain = false;

    // 電話着信状態監視 (TelephonyManager)
    private TelephonyManager? _telephonyManager;
    private CallStateCallbackApi31? _callStateCallbackApi31;
    private LegacyPhoneStateListener? _legacyPhoneStateListener;
    private bool _pausedByCallState = false;

    public override void OnCreate()
    {
        base.OnCreate();
        _audioManager = (AudioManager?)GetSystemService(AudioService);
        _telephonyManager = (TelephonyManager?)GetSystemService(TelephonyService);
        CreateNotificationChannel();

        var notification = BuildNotification(_stationName, _trackTitle);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            StartForeground(NotificationId, notification, global::Android.Content.PM.ForegroundService.TypeMediaPlayback);
        }
        else
        {
            StartForeground(NotificationId, notification);
        }
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent == null) return StartCommandResult.Sticky;

        string? action = intent.Action;

        if (action == ActionStart)
        {
            _stationName = intent.GetStringExtra(ExtraStation) ?? "M3U Radio Player";
            _trackTitle = intent.GetStringExtra(ExtraTitle) ?? "再生中";

            var notificationManager = (NotificationManager)GetSystemService(NotificationService)!;
            notificationManager.Notify(NotificationId, BuildNotification(_stationName, _trackTitle));

            RegisterNoisyReceiver();
            RequestAudioFocus();
            RegisterTelephonyListener();
        }
        else if (action == ActionUpdateTitle)
        {
            _trackTitle = intent.GetStringExtra(ExtraTitle) ?? "";
            var notificationManager = (NotificationManager)GetSystemService(NotificationService)!;
            notificationManager.Notify(NotificationId, BuildNotification(_stationName, _trackTitle));
        }
        else if (action == ActionStop)
        {
            UnregisterTelephonyListener();
            AbandonAudioFocus();
            UnregisterNoisyReceiver();

            Dispatcher.UIThread.Post(() =>
            {
                if (MainWindow.Instance != null && MainWindow.Instance.IsPlaying)
                {
                    MainWindow.Instance.StopPlayback();
                }
            });

            if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
            {
                StopForeground(StopForegroundFlags.Remove);
            }
            else
            {
#pragma warning disable CA1422
                StopForeground(true);
#pragma warning restore CA1422
            }
            StopSelf();
        }

        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        UnregisterTelephonyListener();
        AbandonAudioFocus();
        UnregisterNoisyReceiver();
        base.OnDestroy();
    }

    #region TelephonyManager Call State Handling (マナーモード着信でも確実に一時停止)

    private void RegisterTelephonyListener()
    {
        if (_telephonyManager == null) return;

        if (ContextCompat.CheckSelfPermission(this, global::Android.Manifest.Permission.ReadPhoneState) != Permission.Granted)
        {
            return;
        }

        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.S) // API 31+ (Android 12+)
            {
                _callStateCallbackApi31 = new CallStateCallbackApi31(this);
                _telephonyManager.RegisterTelephonyCallback(new HandlerExecutor(new Handler(Looper.MainLooper)), _callStateCallbackApi31);
            }
            else
            {
#pragma warning disable CA1422
                _legacyPhoneStateListener = new LegacyPhoneStateListener(this);
                _telephonyManager.Listen(_legacyPhoneStateListener, PhoneStateListenerFlags.CallState);
#pragma warning restore CA1422
            }
        }
        catch { }
    }

    private void UnregisterTelephonyListener()
    {
        if (_telephonyManager == null) return;

        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.S && _callStateCallbackApi31 != null)
            {
                _telephonyManager.UnregisterTelephonyCallback(_callStateCallbackApi31);
                _callStateCallbackApi31 = null;
            }
            else if (_legacyPhoneStateListener != null)
            {
#pragma warning disable CA1422
                _telephonyManager.Listen(_legacyPhoneStateListener, PhoneStateListenerFlags.None);
#pragma warning restore CA1422
                _legacyPhoneStateListener = null;
            }
        }
        catch { }

        _pausedByCallState = false;
    }

    private void HandleCallStateChanged(int state)
    {
        // CallState.Ringing (1) = 着信音・マナーモード問わず着信中
        // CallState.Offhook (2) = 通話中
        // CallState.Idle (0)    = 終話・着信拒否・待機状態
        if (state == (int)CallState.Ringing || state == (int)CallState.Offhook)
        {
            if (!_pausedByCallState)
            {
                _pausedByCallState = true;
                Dispatcher.UIThread.Post(() =>
                {
                    if (MainWindow.Instance != null && MainWindow.Instance.IsPlaying)
                    {
                        MainWindow.Instance.PauseForFocusLoss();
                    }
                });
            }
        }
        else if (state == (int)CallState.Idle)
        {
            if (_pausedByCallState)
            {
                _pausedByCallState = false;
                Dispatcher.UIThread.Post(() =>
                {
                    MainWindow.Instance?.ResumeFromFocusGain();
                });
            }
        }
    }

    // Android 12+ (API 31+) 用コールバック
    private class CallStateCallbackApi31 : TelephonyCallback, TelephonyCallback.ICallStateListener
    {
        private readonly RadioPlaybackService _service;
        public CallStateCallbackApi31(RadioPlaybackService service) => _service = service;

        public void OnCallStateChanged(int state)
        {
            _service.HandleCallStateChanged(state);
        }
    }

    // Android 9〜11 用リスナー
    private class LegacyPhoneStateListener : PhoneStateListener
    {
        private readonly RadioPlaybackService _service;
        public LegacyPhoneStateListener(RadioPlaybackService service) => _service = service;

        public override void OnCallStateChanged(CallState state, string? incomingNumber)
        {
            _service.HandleCallStateChanged((int)state);
        }
    }

    // MainLooper 用の IExecutor 実装
    private class HandlerExecutor : Java.Lang.Object, Java.Util.Concurrent.IExecutor
    {
        private readonly Handler _handler;
        public HandlerExecutor(Handler handler) => _handler = handler;
        public void Execute(Java.Lang.IRunnable? command)
        {
            if (command != null) _handler.Post(command);
        }
    }

    #endregion

    #region AudioFocus Handling

    private void RequestAudioFocus()
    {
        if (_audioManager == null) return;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var playbackAttributes = new AudioAttributes.Builder()
                ?.SetUsage(AudioUsageKind.Media)
                ?.SetContentType(AudioContentType.Music)
                ?.Build();

            var handler = new Handler(Looper.MainLooper);

            _focusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain)
                ?.SetAudioAttributes(playbackAttributes!)
                ?.SetAcceptsDelayedFocusGain(false)
                ?.SetWillPauseWhenDucked(true)
                ?.SetOnAudioFocusChangeListener(this, handler)
                ?.Build();

            if (_focusRequest != null)
            {
                _audioManager.RequestAudioFocus(_focusRequest);
            }
        }
        else
        {
#pragma warning disable CA1422
            _audioManager.RequestAudioFocus(this, Stream.Music, AudioFocus.Gain);
#pragma warning restore CA1422
        }
    }

    private void AbandonAudioFocus()
    {
        if (_audioManager == null) return;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O && _focusRequest != null)
        {
            _audioManager.AbandonAudioFocusRequest(_focusRequest);
            _focusRequest = null;
        }
        else
        {
#pragma warning disable CA1422
            _audioManager.AbandonAudioFocus(this);
#pragma warning restore CA1422
        }
    }

    public void OnAudioFocusChange(AudioFocus focusChange)
    {
        switch (focusChange)
        {
            case AudioFocus.Loss:
                _resumeOnFocusGain = false;
                Dispatcher.UIThread.Post(() =>
                {
                    if (MainWindow.Instance != null && MainWindow.Instance.IsPlaying)
                    {
                        MainWindow.Instance.StopPlayback();
                    }
                });
                break;

            case AudioFocus.LossTransient:
            case AudioFocus.LossTransientCanDuck:
                _resumeOnFocusGain = true;
                Dispatcher.UIThread.Post(() =>
                {
                    if (MainWindow.Instance != null && MainWindow.Instance.IsPlaying)
                    {
                        MainWindow.Instance.PauseForFocusLoss();
                    }
                });
                break;

            case AudioFocus.Gain:
                if (_resumeOnFocusGain)
                {
                    _resumeOnFocusGain = false;
                    Dispatcher.UIThread.Post(() => MainWindow.Instance?.ResumeFromFocusGain());
                }
                break;
        }
    }

    #endregion

    private void RegisterNoisyReceiver()
    {
        if (!_isReceiverRegistered)
        {
            _noisyReceiver = new BecomingNoisyReceiver();
            var filter = new IntentFilter(AudioManager.ActionAudioBecomingNoisy);
            RegisterReceiver(_noisyReceiver, filter);
            _isReceiverRegistered = true;
        }
    }

    private void UnregisterNoisyReceiver()
    {
        if (_isReceiverRegistered && _noisyReceiver != null)
        {
            try { UnregisterReceiver(_noisyReceiver); } catch { }
            _noisyReceiver = null;
            _isReceiverRegistered = false;
        }
    }

    private Notification BuildNotification(string station, string title)
    {
        var openIntent = new Intent(this, typeof(MainActivity));
        openIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        var openPendingIntent = PendingIntent.GetActivity(
            this, 0, openIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var stopIntent = new Intent(this, typeof(RadioPlaybackService));
        stopIntent.SetAction(ActionStop);
        var stopPendingIntent = PendingIntent.GetService(
            this, 1, stopIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        return new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle(station)
            .SetContentText(string.IsNullOrEmpty(title) ? "再生中" : title)
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
            .SetOngoing(true)
            .SetPriority(NotificationCompat.PriorityLow)
            .SetContentIntent(openPendingIntent)
            .AddAction(global::Android.Resource.Drawable.IcMediaPause, "停止", stopPendingIntent)
            .Build();
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "ラジオ再生通知", NotificationImportance.Low)
            {
                Description = "ラジオ再生中のバックグラウンド維持用通知"
            };
            var notificationManager = (NotificationManager)GetSystemService(NotificationService)!;
            notificationManager.CreateNotificationChannel(channel);
        }
    }

    public override IBinder? OnBind(Intent? intent) => null;

    private class BecomingNoisyReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action == AudioManager.ActionAudioBecomingNoisy)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (MainWindow.Instance != null && MainWindow.Instance.IsPlaying)
                    {
                        MainWindow.Instance.StopPlayback();
                    }
                });
            }
        }
    }
}
