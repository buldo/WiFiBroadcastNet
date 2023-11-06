using Microsoft.Extensions.Logging;
using WiFiBroadcastNet.Crypto;
using WiFiBroadcastNet.Fec;

namespace WiFiBroadcastNet.RadioStreams;

internal class SessionKeysRadioStream : RadioStream
{
    private readonly Decryptor _decryptor;
    private readonly ILogger _logger;
    private static readonly byte STREAM_INDEX_SESSION_KEY_PACKETS = 127;


    public SessionKeysRadioStream(
        Decryptor decryptor,
        ILogger logger)
        : base(STREAM_INDEX_SESSION_KEY_PACKETS, new NullFec(), new NullCrypto())
    {
        _decryptor = decryptor;
        _logger = logger;
    }

    public override void ProcessFrame(RadioPort radioPort, RxFrame frame)
    {
        // _logger.LogDebug("Processing session key frame");

        if (radioPort.Encrypted)
        {
            _logger.LogWarning("Cannot be session key packet - encryption flag set to true");
            return;
        }

        var sessionKeyPacket = new SessionKeyPacket(frame);
        if (!sessionKeyPacket.IsValid)
        {
            _logger.LogWarning("Cannot be session key packet - size mismatch {ActualLen}", frame.Payload.Length);
            return;
        }

        var decrypt_res = _decryptor.onNewPacketSessionKeyData(sessionKeyPacket.sessionKeyNonce, sessionKeyPacket.sessionKeyData);

        if (decrypt_res == DecryptorResult.SESSION_VALID_NEW)
        {
            _logger.LogDebug("Initializing new session.");
            //foreach (var (key, radioStream) in _radioStreams)
            //{
            //    radioStream.cb_session();
            //}
        }
    }
}

