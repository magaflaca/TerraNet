using System.Net;
using System.Net.Sockets;
using Terraria;
using Terraria.Localization;
using Terraria.Net;
using Terraria.Net.Sockets;
using TShockAPI;

namespace TerraNet.ServerAgent;

public sealed class RobustSocket : ISocket
{
    private const int SilenceLimitMs = 150_000;
    private const int StalledSendMs = 60_000;
    private const int CheckIntervalMs = 1_000;
    private const int FreeSlotWaitMs = 3_000;
    private const int Backlog = 512;

    private readonly TcpClient _connection;
    private readonly Lock _closing = new();
    private RemoteAddress? _remoteAddress;
    private volatile bool _closed;
    private long _lastReceive = Environment.TickCount64;
    private long _lastCheck;
    private bool _alive = true;
    private int _pendingSends;
    private long _pendingSince;

    private TcpListener? _listener;
    private Thread? _acceptThread;

    public RobustSocket() => _connection = new TcpClient { NoDelay = true };

    private RobustSocket(TcpClient client)
    {
        _connection = client;
        _connection.NoDelay = true;
        var endpoint = (IPEndPoint)client.Client.RemoteEndPoint!;
        _remoteAddress = new TcpAddress(endpoint.Address, endpoint.Port);
        EnableKeepAlive(client.Client);
    }

    public static long ClosedZombies;
    public static long RejectedFull;

    void ISocket.Close()
    {
        lock (_closing)
        {
            if (_closed)
                return;
            _closed = true;
        }
        _remoteAddress = null;
        try
        {
            _connection.Close();
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
        }
    }

    bool ISocket.IsConnected()
    {
        if (_closed || _connection.Client is null || !_connection.Connected)
            return false;
        var now = Environment.TickCount64;
        if (now - _lastCheck < CheckIntervalMs)
            return _alive;
        _lastCheck = now;
        if (DeathReason(now) is not { } reason)
            return _alive = true;
        Interlocked.Increment(ref ClosedZombies);
        TShock.Log.Info($"[TerraNet] Releasing connection {_remoteAddress}: {reason}.");
        ((ISocket)this).Close();
        return _alive = false;
    }

    private string? DeathReason(long now)
    {
        try
        {
            var socket = _connection.Client;
            if (socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0)
                return "closed by client";
            if (socket.Poll(0, SelectMode.SelectError))
                return "socket error";
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            return "invalid socket";
        }
        if (Volatile.Read(ref _pendingSends) > 0 && now - Volatile.Read(ref _pendingSince) > StalledSendMs)
            return "send stalled";
        if (now - Volatile.Read(ref _lastReceive) > SilenceLimitMs)
            return "silent too long";
        return null;
    }

    void ISocket.Connect(RemoteAddress address)
    {
        var tcp = (TcpAddress)address;
        _connection.Connect(tcp.Address, tcp.Port);
        _remoteAddress = address;
    }

    void ISocket.AsyncSend(byte[] data, int offset, int size, SocketSendCallback callback, object state)
    {
        if (_closed)
            return;
        var buffer = LegacyNetBufferPool.RequestBuffer(data, offset, size);
        if (Interlocked.Increment(ref _pendingSends) == 1)
            Volatile.Write(ref _pendingSince, Environment.TickCount64);
        try
        {
            _connection.GetStream().BeginWrite(buffer, 0, size, OnSent, (callback, state, buffer));
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or InvalidOperationException)
        {
            Interlocked.Decrement(ref _pendingSends);
            LegacyNetBufferPool.ReturnBuffer(buffer);
            ((ISocket)this).Close();
        }
    }

    private void OnSent(IAsyncResult result)
    {
        var (callback, state, buffer) = ((SocketSendCallback, object, byte[]))result.AsyncState!;
        LegacyNetBufferPool.ReturnBuffer(buffer);
        try
        {
            _connection.GetStream().EndWrite(result);
            if (Interlocked.Decrement(ref _pendingSends) > 0)
                Volatile.Write(ref _pendingSince, Environment.TickCount64);
            callback(state);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or InvalidOperationException)
        {
            Interlocked.Decrement(ref _pendingSends);
            ((ISocket)this).Close();
        }
    }

    void ISocket.AsyncReceive(byte[] data, int offset, int size, SocketReceiveCallback callback, object state)
    {
        try
        {
            _connection.GetStream().BeginRead(data, offset, size, OnReceived, (callback, state));
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or InvalidOperationException)
        {
            ((ISocket)this).Close();
            callback(state, 0);
        }
    }

    private void OnReceived(IAsyncResult result)
    {
        var (callback, state) = ((SocketReceiveCallback, object))result.AsyncState!;
        int read;
        try
        {
            read = _connection.GetStream().EndRead(result);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or InvalidOperationException)
        {
            ((ISocket)this).Close();
            read = 0;
        }
        if (read > 0)
            Volatile.Write(ref _lastReceive, Environment.TickCount64);
        try
        {
            callback(state, read);
        }
        catch (Exception ex)
        {
            TShock.Log.Error(ex.ToString());
        }
    }

    bool ISocket.IsDataAvailable()
    {
        if (_closed)
            return false;
        try
        {
            return _connection.GetStream().DataAvailable || _connection.Client.Poll(0, SelectMode.SelectRead);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or InvalidOperationException)
        {
            return false;
        }
    }

    RemoteAddress ISocket.GetRemoteAddress() => _remoteAddress!;

    bool ISocket.StartListening(SocketConnectionAccepted callback)
    {
        if (_acceptThread is { IsAlive: true })
            return true;
        var address = Program.LaunchParameters.TryGetValue("-ip", out var value) && IPAddress.TryParse(value, out var parsed) ? parsed : IPAddress.Any;
        try
        {
            _listener = Listen(new IPEndPoint(address, Netplay.ListenPort));
        }
        catch (SocketException ex)
        {
            TShock.Log.ConsoleError($"[TerraNet] Cannot listen on port {Netplay.ListenPort}: {ex.Message}");
            return false;
        }
        _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "TerraNet listener" };
        _acceptThread.Start();
        return true;
    }

    void ISocket.StopListening()
    {
    }

    private static TcpListener Listen(IPEndPoint endpoint)
    {
        var listener = new TcpListener(endpoint);
        listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        listener.Start(Backlog);
        return listener;
    }

    private void AcceptLoop()
    {
        while (!Netplay.Disconnect && _listener is { } listener)
        {
            TcpClient client;
            try
            {
                client = listener.AcceptTcpClient();
            }
            catch (Exception ex) when (!Netplay.Disconnect && ex is SocketException or ObjectDisposedException or InvalidOperationException)
            {
                Reopen(listener);
                continue;
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException or InvalidOperationException)
            {
                break;
            }
            Admit(client);
        }
        try
        {
            _listener?.Stop();
        }
        catch (SocketException)
        {
        }
    }

    private void Reopen(TcpListener broken)
    {
        TShock.Log.ConsoleError("[TerraNet] Listener failed, reopening the port.");
        var endpoint = broken.LocalEndpoint as IPEndPoint ?? new IPEndPoint(IPAddress.Any, Netplay.ListenPort);
        try
        {
            broken.Stop();
        }
        catch (SocketException)
        {
        }
        Thread.Sleep(CheckIntervalMs);
        try
        {
            _listener = Listen(endpoint);
        }
        catch (SocketException ex)
        {
            TShock.Log.ConsoleError($"[TerraNet] Reopen failed: {ex.Message}");
        }
    }

    private static void Admit(TcpClient client)
    {
        try
        {
            var socket = new RobustSocket(client);
            var slot = FreeSlot();
            if (slot < 0)
            {
                Interlocked.Increment(ref RejectedFull);
                var reason = TShock.Config.Settings.ServerFullReason;
                ThreadPool.QueueUserWorkItem(_ => Reject(client, reason));
                return;
            }
            Console.WriteLine(Language.GetTextValue("Net.ClientConnecting", socket._remoteAddress));
            Netplay.Clients[slot].Reset();
            Netplay.Clients[slot].Socket = socket;
        }
        catch (Exception ex)
        {
            TShock.Log.Error($"[TerraNet] Accept failed: {ex}");
            client.Close();
        }
    }

    private static int FreeSlot()
    {
        var deadline = Environment.TickCount64 + FreeSlotWaitMs;
        while (true)
        {
            var pending = false;
            for (var i = 0; i < Main.maxNetPlayers; i++)
            {
                var client = Netplay.Clients[i];
                if (!client.IsActive && !client.PendingTermination && (client.Socket is null || !client.Socket.IsConnected()))
                    return i;
                if (client.PendingTermination || !client.IsConnected())
                    pending = true;
            }
            if (!pending || Environment.TickCount64 > deadline)
                return -1;
            Thread.Sleep(20);
        }
    }

    private static void Reject(TcpClient client, string reason)
    {
        try
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write((short)0);
                writer.Write((byte)2);
                NetworkText.FromLiteral(reason).Serialize(writer);
            }
            var bytes = stream.ToArray();
            BitConverter.GetBytes((short)bytes.Length).CopyTo(bytes, 0);
            client.Client.SendTimeout = 2_000;
            client.GetStream().Write(bytes, 0, bytes.Length);
            client.Client.Shutdown(SocketShutdown.Send);
            Drain(client.Client);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
        }
        finally
        {
            client.Close();
        }
    }

    private static void Drain(Socket socket)
    {
        socket.ReceiveTimeout = 1_000;
        var scratch = new byte[512];
        var deadline = Environment.TickCount64 + 2_000;
        while (Environment.TickCount64 < deadline && socket.Receive(scratch) > 0)
        {
        }
    }

    private static void EnableKeepAlive(Socket socket)
    {
        try
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 30);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
        }
        catch (Exception ex) when (ex is SocketException or PlatformNotSupportedException)
        {
        }
    }
}
