using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EndfieldCharge.Services;

/// <summary>
/// HUD 进程间通信：命名管道，单命令 / 单连接 / UTF-8 行协议。
/// 客户端用于「第二个实例请求唤醒岛」；服务端由宿主启动后常驻（仅当前用户可连接）。
/// </summary>
public static class HudIpc
{
    /// <summary>管道名（同机、当前用户会话内唯一，与单实例互斥/AppData 路径无关）。</summary>
    public const string PipeName = "EndfieldIsland.Hud.Ipc";

    /// <summary>唤醒命令：让当前实例把岛切到等待态显示。</summary>
    public const string ShowCommand = "show";

    private const int DefaultConnectTimeoutMs = 800;
    private const int ServerReadTimeoutMs = 2000;

    /// <summary>
    /// 客户端：向已运行实例发送一条命令。
    /// 任何失败（无实例 / 超时 / 管道已关闭 / 权限不足）都静默返回 false。
    /// </summary>
    public static bool TrySend(string command, int timeoutMs = DefaultConnectTimeoutMs)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);

            client.Connect(Math.Max(1, timeoutMs));

            using var writer = new StreamWriter(
                client,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 128,
                leaveOpen: false)
            {
                AutoFlush = true,
            };
            writer.WriteLine(command);
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException or InvalidOperationException or ObjectDisposedException)
        {
            // 尽力唤醒：失败不影响第二个实例静默退出
            return false;
        }
    }

    /// <summary>
    /// 服务端：后台循环接受连接（一次一个），每条连接读一行 UTF-8 命令后回调。
    /// Dispose 后停止接受新连接；回调在后台线程触发，订阅方需自行切回 UI 线程。
    /// </summary>
    public static IDisposable StartServer(Action<string> onCommand)
    {
        var server = new HudPipeServer(onCommand);
        server.Start();
        return server;
    }

    private sealed class HudPipeServer : IDisposable
    {
        private readonly Action<string> _onCommand;
        private readonly CancellationTokenSource _cts = new();
        private int _disposed;

        public HudPipeServer(Action<string> onCommand) => _onCommand = onCommand;

        public void Start() => _ = Task.Run(() => RunAsync(_cts.Token));

        private async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // CurrentUserOnly：只有同一用户会话内的进程能连接
                    await using var pipe = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        maxNumberOfServerInstances: 1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                    await pipe.WaitForConnectionAsync(token);

                    using var reader = new StreamReader(
                        pipe,
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                        detectEncodingFromByteOrderMarks: false,
                        bufferSize: 256,
                        leaveOpen: true);

                    // 单连接读超时：客户端连上却不发命令时，不能让接受循环永久卡住
                    using var readCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    readCts.CancelAfter(ServerReadTimeoutMs);
                    string? line = await reader.ReadLineAsync(readCts.Token);

                    if (!string.IsNullOrWhiteSpace(line))
                        Dispatch(line.Trim());
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested)
                        break;

                    // 单连接读超时：丢弃这条连接，继续接受下一个
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or UnauthorizedAccessException)
                {
                    if (token.IsCancellationRequested)
                        break;

                    Logger.Warn($"HudIpc: 管道连接异常（已忽略）—— {ex.Message}");
                }
            }
        }

        /// <summary>分发命令；回调异常只记录，不允许终结接受循环。</summary>
        private void Dispatch(string command)
        {
            try
            {
                _onCommand(command);
            }
            catch (Exception ex)
            {
                Logger.Warn($"HudIpc: 命令处理异常（{command}）—— {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
