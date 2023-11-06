using Microsoft.Extensions.Logging;
using WiFiBroadcastNet.Crypto;

namespace WiFiBroadcastNet.RadioStreams;

internal class SessionKeysRadioStream : IRadioStream
{
    private readonly Decryptor _decryptor;
    private readonly ILogger _logger;
    private static readonly byte STREAM_INDEX_SESSION_KEY_PACKETS = 127;


    public SessionKeysRadioStream(
        Decryptor decryptor,
        ILogger logger)
    {
        _decryptor = decryptor;
        _logger = logger;
    }

    public int Id => STREAM_INDEX_SESSION_KEY_PACKETS;

    public void ProcessFrame(Memory<byte> decryptedPayload)
    {
        // _logger.LogDebug("Processing session key frame");

        //if (radioPort.Encrypted)
        //{
        //    _logger.LogWarning("Cannot be session key packet - encryption flag set to true");
        //    return;
        //}

        var sessionKeyPacket = new SessionKeyPacket(decryptedPayload);
        if (!sessionKeyPacket.IsValid)
        {
            _logger.LogWarning("Cannot be session key packet - size mismatch {ActualLen}", decryptedPayload.Length);
            return;
        }

        var decrypt_res = _decryptor.onNewPacketSessionKeyData(sessionKeyPacket.SessionKeyNonce, sessionKeyPacket.SessionKeyData);

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

