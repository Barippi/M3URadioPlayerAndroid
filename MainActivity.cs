using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.OS;
using Android.Provider;
using Avalonia;
using Avalonia.Android;

namespace M3URadioPlayer.Android;

[Activity(
    Label = "M3U Radio Player",
    Theme = "@style/Theme.AppCompat.NoActionBar",
    Icon = "@mipmap/ic_launcher",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    public static MainActivity? Instance { get; private set; }

    public MainActivity()
    {
        Instance = this;
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        CheckAndRequestStoragePermission();
    }

    protected override void OnResume()
    {
        base.OnResume();

        // 設定画面から戻った際などにプレイリスト一覧を自動再スキャン
        if (MainWindow.Instance != null)
        {
            MainWindow.Instance.TriggerScanPlaylists();
        }
    }

    private void CheckAndRequestStoragePermission()
    {
        // Android 11 (API 30) 以降の場合
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            if (!global::Android.OS.Environment.IsExternalStorageManager)
            {
                try
                {
                    var uri = Uri.Parse($"package:{PackageName}");
                    var intent = new Intent(Settings.ActionManageAppAllFilesAccessPermission, uri);
                    StartActivity(intent);
                }
                catch
                {
                    var fallbackIntent = new Intent(Settings.ActionManageAllFilesAccessPermission);
                    StartActivity(fallbackIntent);
                }
            }
        }
        else if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            if (CheckSelfPermission(global::Android.Manifest.Permission.ReadExternalStorage) != Permission.Granted)
            {
                RequestPermissions(new[] { global::Android.Manifest.Permission.ReadExternalStorage }, 1001);
            }
        }
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder);
    }
}
