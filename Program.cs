using System;
using System.Threading;
using Avalonia;
using EndfieldCharge.Services;

namespace EndfieldCharge;

class Program
{
    private const string SingleInstanceMutexName = @"Local\EndfieldCharge_SingleInstance_7C1D";

    [STAThread]
    public static void Main(string[] args)
    {
        // 只允许一个实例常驻；已运行时静默退出
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);

        if (!createdNew)
        {
            // 已有实例：仅 --show 时请求它唤醒岛，随后本进程直接退出（不初始化 Avalonia，避免闪窗）
            if (HasArg(args, "--show"))
                HudIpc.TrySend(HudIpc.ShowCommand);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        GC.KeepAlive(mutex);
    }

    private static bool HasArg(string[] args, string name)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>Avalonia 配置入口，设计器也会用到，勿删。</summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
