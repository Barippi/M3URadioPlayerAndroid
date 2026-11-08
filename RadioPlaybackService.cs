using System;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace M3URadioPlayer.Android;

[Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaPlayback)]
public class RadioPlaybackService : Service
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

    public override void OnCreate()
    {
        base.OnCreate();
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
        }
        else if (action == ActionUpdateTitle)
        {
            _trackTitle = intent.GetStringExtra(ExtraTitle) ?? "";
            var notificationManager = (NotificationManager)GetSystemService(NotificationService)!;
            notificationManager.Notify(NotificationId, BuildNotification(_stationName, _trackTitle));
        }
        else if (action == ActionStop)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
            {
                StopForeground(StopForegroundFlags.Remove);
            }
            else
            {
                StopForeground(true);
            }
            StopSelf();
        }

        return StartCommandResult.Sticky;
    }

    private Notification BuildNotification(string station, string title)
    {
        var intent = new Intent(this, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        var pendingIntent = PendingIntent.GetActivity(
            this, 
            0, 
            intent, 
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var builder = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle(station)
            .SetContentText(string.IsNullOrEmpty(title) ? "再生中" : title)
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
            .SetOngoing(true)
            .SetPriority(NotificationCompat.PriorityLow)
            .SetContentIntent(pendingIntent);

        return builder.Build();
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
}
