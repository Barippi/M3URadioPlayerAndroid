using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Net;
using Android.OS;
using Android.Provider;
using Android.Views;
using AndroidX.Core.View;
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

        // ダークテーマに合わせてステータスバー・ナビゲーションバーの背景色とアイコン色を統一
        var darkColor = Color.ParseColor("#1E1E1E");
        Window?.SetStatusBarColor(darkColor);
        Window?.SetNavigationBarColor(darkColor);
        Window?.DecorView.SetBackgroundColor(darkColor);

        if (Window != null)
        {
            var insetsController = WindowCompat.GetInsetsController(Window, Window.DecorView);
            if (insetsController != null)
            {
                // 背景が暗いため、ステータスバーおよびナビゲーションバーの文字・アイコンを白にする
                insetsController.AppearanceLightStatusBars = false;
                insetsController.AppearanceLightNavigationBars = false;
            }
        }

        // Android 14/15/17 (Target SDK 35+) の Edge-to-Edge による
        // ステータスバー（上部）とナビゲーションバー（下部）の被りを Android ネイティブレベルで確実に押し返す
        var contentView = FindViewById(global::Android.Resource.Id.Content);
        if (contentView != null)
        {
            contentView.SetBackgroundColor(darkColor);
            ViewCompat.SetOnApplyWindowInsetsListener(contentView, new SystemBarInsetsListener());
        }

        CheckAndRequestStoragePermission();
    }

    protected override void OnResume()
    {
        base.OnResume();

        if (MainWindow.Instance != null)
        {
            MainWindow.Instance.TriggerScanPlaylists();
        }
    }

    private void CheckAndRequestStoragePermission()
    {
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

    private class SystemBarInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat OnApplyWindowInsets(View v, WindowInsetsCompat insets)
        {
            var systemBars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars());
            v.SetPadding(systemBars.Left, systemBars.Top, systemBars.Right, systemBars.Bottom);
            return insets;
        }
    }
}
