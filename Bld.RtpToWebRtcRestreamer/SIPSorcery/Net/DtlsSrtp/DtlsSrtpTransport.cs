//-----------------------------------------------------------------------------
// Filename: DtlsSrtpTransport.cs
//
// Description: This class represents the DTLS SRTP transport connection to use
// as Client or Server.
//
// Author(s):
// Rafael Soares (raf.csoares@kyubinteractive.com)
//
// History:
// 01 Jul 2020	Rafael Soares   Created.
// 02 Jul 2020  Aaron Clauson   Switched underlying transport from socket to
//                              piped memory stream.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Net.Sockets;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp.Transform;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Tls;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp;

internal class DtlsSrtpTransport : DatagramTransport, IDisposable
{
    private const int DefaultRetransmissionWaitMillis = 100;
    private const int DefaultMtu = 1500;
    private const int MinIpOverhead = 20;
    private const int MaxIpOverhead = MinIpOverhead + 64;
    private const int UdpOverhead = 8;
    private const int DefaultTimeoutMilliseconds = 20000;
    private const int DtlsRetransmissionCode = -1;
    private const int DtlsReceiveErrorCode = -2;

    private static readonly ILogger Logger = Log.Logger;

    private static readonly Random Random = new();

    private SrtpTransformer _srtpEncoder;
    private SrtcpTransformer _srtcpDecoder;
    private readonly IDtlsSrtpPeer _connection;
    private readonly Func<ReadOnlyMemory<byte>, Task> _sendHandler;

    /// <summary>The collection of chunks to be written.</summary>
    private readonly BlockingCollection<byte[]> _chunks = new(new ConcurrentQueue<byte[]>());

    /// <summary>
    /// Sets the period in milliseconds that the handshake attempt will timeout
    /// after.
    /// </summary>
    private readonly int _timeoutMilliseconds = DefaultTimeoutMilliseconds;

    /// <summary>
    /// Sets the period in milliseconds that receive will wait before try retransmission
    /// </summary>
    private readonly int _retransmissionMilliseconds = DefaultRetransmissionWaitMillis;

    /// <summary>
    /// Parameters:
    ///  - alert level,
    ///  - alert type,
    ///  - alert description.
    /// </summary>
    public event Action<AlertLevelsEnum, AlertTypesEnum, string> OnAlert;

    private DateTime _startTime = DateTime.MinValue;
    private bool _isClosed;

    // Network properties
    private int _waitMillis = DefaultRetransmissionWaitMillis;
    private readonly int _receiveLimit;
    private readonly int _sendLimit;

    private volatile bool _handshakeComplete;
    private volatile bool _handshaking;

    public DtlsSrtpTransport(
        IDtlsSrtpPeer connection,
        Func<ReadOnlyMemory<byte>, Task> sendHandler)
    {
        // Network properties
        _receiveLimit = Math.Max(0, DefaultMtu - MinIpOverhead - UdpOverhead);
        _sendLimit = Math.Max(0, DefaultMtu - MaxIpOverhead - UdpOverhead);
        _connection = connection;
        _sendHandler = sendHandler;

        connection.OnAlert += (level, type, description) => OnAlert?.Invoke(level, type, description);
    }

    public bool IsHandshakeComplete()
    {
        return _handshakeComplete;
    }

    public bool DoHandshake(out string handshakeError)
    {
        if (_connection.IsClient)
        {
            return DoHandshakeAsClient(out handshakeError);
        }

        return DoHandshakeAsServer(out handshakeError);
    }

    private bool DoHandshakeAsClient(out string handshakeError)
    {
        handshakeError = null;

        Logger.LogDebug("DTLS commencing handshake as client.");

        if (!_handshaking && !_handshakeComplete)
        {
            _waitMillis = _retransmissionMilliseconds;
            _startTime = DateTime.Now;
            _handshaking = true;
            var clientProtocol = new DtlsClientProtocol();
            try
            {
                var client = (DtlsSrtpClient)_connection;
                // Perform the handshake in a non-blocking fashion
                clientProtocol.Connect(client, this);

                // Prepare the shared key to be used in RTP streaming
                //client.PrepareSrtpSharedSecret();
                // Generate encoders for DTLS traffic
                if (client.SrtpPolicy != null)
                {
                    _srtpEncoder = GenerateRtpEncoder();
                    _srtcpDecoder = GenerateRtcpDecoder();
                }
                // Declare handshake as complete
                _handshakeComplete = true;
                _handshaking = false;
                // Warn listeners handshake completed
                //UnityEngine.Debug.Log("DTLS Handshake Completed");

                return true;
            }
            catch (Exception excp)
            {
                if (excp.InnerException is TimeoutException)
                {
                    Logger.LogWarning(excp, "DTLS handshake as client timed out waiting for handshake to complete.");
                    handshakeError = "timeout";
                }
                else
                {
                    handshakeError = "unknown";
                    if (excp is TlsFatalAlert alert)
                    {
                        handshakeError = alert.Message;
                    }

                    Logger.LogWarning(excp, $"DTLS handshake as client failed. {excp.Message}");
                }

                // Declare handshake as failed
                _handshakeComplete = false;
                _handshaking = false;
                // Warn listeners handshake completed
                //UnityEngine.Debug.Log("DTLS Handshake failed\n" + e);
            }
        }
        return false;
    }

    private bool DoHandshakeAsServer(out string handshakeError)
    {
        handshakeError = null;

        Logger.LogDebug("DTLS commencing handshake as server.");

        if (!_handshaking && !_handshakeComplete)
        {
            _waitMillis = _retransmissionMilliseconds;
            _startTime = DateTime.Now;
            _handshaking = true;
            var serverProtocol = new DtlsServerProtocol
            {
                VerifyRequests = false
            };
            try
            {
                var server = (DtlsSrtpServer)_connection;

                // Perform the handshake in a non-blocking fashion
                serverProtocol.Accept(server, this);
                // Prepare the shared key to be used in RTP streaming
                //server.PrepareSrtpSharedSecret();
                // Generate encoders for DTLS traffic
                if (server.SrtpPolicy != null)
                {
                    _srtpEncoder = GenerateRtpEncoder();
                    _srtcpDecoder = GenerateRtcpDecoder();
                }
                // Declare handshake as complete
                _handshakeComplete = true;
                _handshaking = false;
                // Warn listeners handshake completed
                //UnityEngine.Debug.Log("DTLS Handshake Completed");
                return true;
            }
            catch (Exception excp)
            {
                if (excp.InnerException is TimeoutException)
                {
                    Logger.LogWarning(excp, "DTLS handshake as server timed out waiting for handshake to complete.");
                    handshakeError = "timeout";
                }
                else
                {
                    handshakeError = "unknown";
                    if (excp is TlsFatalAlert alert)
                    {
                        handshakeError = alert.Message;
                    }

                    Logger.LogWarning(excp, $"DTLS handshake as server failed. {excp.Message}");
                }

                // Declare handshake as failed
                _handshakeComplete = false;
                _handshaking = false;
                // Warn listeners handshake completed
                //UnityEngine.Debug.Log("DTLS Handshake failed\n"+ e);
            }
        }
        return false;
    }

    public Certificate RemoteCertificate => _connection.RemoteCertificate;

    private SrtpTransformer GenerateRtpEncoder()
    {
        return GenerateRtpTransformer(_connection.IsClient);
    }

    private SrtcpTransformer GenerateRtcpDecoder()
    {
        //Generate the reverse result of "GenerateRctpEncoder"
        return GenerateRtcpTransformer(!_connection.IsClient);
    }

    private SrtpTransformer GenerateRtpTransformer(bool isClient)
    {
        SrtpTransformEngine engine;
        if (!isClient)
        {
            engine = new SrtpTransformEngine(_connection.SrtpMasterServerKey, _connection.SrtpMasterServerSalt, _connection.SrtpPolicy, _connection.SrtcpPolicy);
        }
        else
        {
            engine = new SrtpTransformEngine(_connection.SrtpMasterClientKey, _connection.SrtpMasterClientSalt, _connection.SrtpPolicy, _connection.SrtcpPolicy);
        }

        return engine.GetRTPTransformer();
    }


    private SrtcpTransformer GenerateRtcpTransformer(bool isClient)
    {
        SrtpTransformEngine engine;
        if (!isClient)
        {
            engine = new SrtpTransformEngine(_connection.SrtpMasterServerKey, _connection.SrtpMasterServerSalt, _connection.SrtpPolicy, _connection.SrtcpPolicy);
        }
        else
        {
            engine = new SrtpTransformEngine(_connection.SrtpMasterClientKey, _connection.SrtpMasterClientSalt, _connection.SrtpPolicy, _connection.SrtcpPolicy);
        }

        return engine.GetRtcpTransformer();
    }

    public ReadOnlyMemory<byte> ProtectRTP(long ssrc, byte[] toEncryptBuffer, int length)
    {
        ReadOnlyMemory<byte> result;
        lock (_srtpEncoder)
        {
            result = _srtpEncoder.Transform(ssrc, toEncryptBuffer, length);
        }

        return result;
    }

    private byte[] UnprotectRtcp(byte[] packet, int length)
    {
        lock (_srtcpDecoder)
        {
            return _srtcpDecoder.ReverseTransform(packet, length);
        }
    }

    public int UnprotectRtcp(byte[] payload, int length, out int outLength)
    {
        var result = UnprotectRtcp(payload, length);
        if (result == null)
        {
            outLength = 0;
            return -1;
        }

        Buffer.BlockCopy(result, 0, payload, 0, result.Length);
        outLength = result.Length;

        return 0; //No Errors
    }

    /// <summary>
    /// Returns the number of milliseconds remaining until a timeout occurs.
    /// </summary>
    private int GetMillisecondsRemaining()
    {
        return _timeoutMilliseconds - (int)(DateTime.Now - _startTime).TotalMilliseconds;
    }

    public int GetReceiveLimit()
    {
        return _receiveLimit;
    }

    public int GetSendLimit()
    {
        return _sendLimit;
    }

    public void WriteToRecvStream(byte[] buf)
    {
        if (!_isClosed)
        {
            _chunks.Add(buf);
        }
    }

    private int Read(Span<byte> buffer, int timeout)
    {
        try
        {
            if(_isClosed)
            {
                throw new SocketException((int)SocketError.NotConnected);
                //return DTLS_RECEIVE_ERROR_CODE;
            }

            if (_chunks.TryTake(out var item, timeout))
            {
                item.AsSpan().CopyTo(buffer);
                return item.Length;
            }
        }
        catch (ObjectDisposedException) { }
        catch (ArgumentNullException) { }

        return DtlsRetransmissionCode;
    }

    public int Receive(byte[] buf, int off, int len, int waitMillis)
    {
        return Receive(buf.AsSpan(off, len), waitMillis);
    }

    public int Receive(Span<byte> buffer, int waitMillis)
    {
        if (!_handshakeComplete)
        {
            // The timeout for the handshake applies from when it started rather than
            // for each individual receive..
            var millisecondsRemaining = GetMillisecondsRemaining();

            //Handle DTLS 1.3 Retransmission time (100 to 6000 ms)
            //https://tools.ietf.org/id/draft-ietf-tls-dtls13-31.html#rfc.section.5.7
            //As HandshakeReliable class contains too long hardcoded initial waitMillis (1000 ms) we must control this internally
            //PS: Random extra delta time guarantee that work in local networks.
            waitMillis = _waitMillis + Random.Next(5, 25);

            if (millisecondsRemaining <= 0)
            {
                Logger.LogWarning($"DTLS transport timed out after {_timeoutMilliseconds}ms waiting for handshake from remote {(_connection.IsClient ? "server" : "client")}.");
                throw new TimeoutException();
            }

            if (!_isClosed)
            {
                waitMillis = Math.Min(waitMillis, millisecondsRemaining);
                var receiveLen = Read(buffer, waitMillis);

                //Handle DTLS 1.3 Retransmission time (100 to 6000 ms)
                //https://tools.ietf.org/id/draft-ietf-tls-dtls13-31.html#rfc.section.5.7
                if (receiveLen == DtlsRetransmissionCode)
                {
                    _waitMillis = BackOff(_waitMillis);
                }
                else
                {
                    _waitMillis = _retransmissionMilliseconds;
                }

                return receiveLen;
            }

            throw new SocketException((int)SocketError.NotConnected);
            //return DTLS_RECEIVE_ERROR_CODE;
        }

        if (!_isClosed)
        {
            return Read(buffer, waitMillis);
        }

        //throw new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.NotConnected);
        return DtlsReceiveErrorCode;
    }

    public void Send(byte[] buf, int off, int len)
    {
        if (SynchronizationContext.Current == null && TaskScheduler.Current == TaskScheduler.Default)
        {
            _sendHandler(buf.AsMemory(off, len)).GetAwaiter().GetResult();
        }
        else
        {
            Task.Run(() => _sendHandler(buf.AsMemory(off, len))).GetAwaiter().GetResult();
        }
    }

    public void Send(ReadOnlySpan<byte> buffer)
    {
        var bufArray = buffer.ToArray();
        if (SynchronizationContext.Current == null && TaskScheduler.Current == TaskScheduler.Default)
        {
            _sendHandler(bufArray).GetAwaiter().GetResult();
        }
        else
        {
            Task.Run(() => _sendHandler(bufArray)).GetAwaiter().GetResult();
        }
    }

    public void Close()
    {
        _isClosed = true;
        _startTime = DateTime.MinValue;
        _chunks?.Dispose();
    }

    /// <summary>
    /// Close the transport if the instance is out of scope.
    /// </summary>
    public void Dispose()
    {
        Close();
    }

    /// <summary>
    /// Handle retransmission time based in DTLS 1.3
    /// </summary>
    /// <param name="currentWaitMillis"></param>
    /// <returns></returns>
    private int BackOff(int currentWaitMillis)
    {
        return Math.Min(currentWaitMillis * 2, 6000);
    }
}