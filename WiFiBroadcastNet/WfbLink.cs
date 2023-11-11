using Bld.WlanUtils;
using Microsoft.Extensions.Logging;
using WiFiBroadcastNet.Crypto;
using WiFiBroadcastNet.Devices;
using WiFiBroadcastNet.RadioStreams;

namespace WiFiBroadcastNet;

public class WfbLink
{
    private readonly bool _useGndIdentifier = true; // Air/ground selector
    private readonly ILogger<WfbLink> _logger;
    private readonly List<DeviceHandler> _deviceHandlers;

    private readonly Dictionary<int, IRadioStream> _radioStreams = new();
    private readonly Decryptor _decryptor;

    public WfbLink(
        IDevicesProvider devicesProvider,
        List<UserStream> streamAccessor,
        ILogger<WfbLink> logger)
    {
        _logger = logger;

        KeyPairTxRx keypair;
        //if (m_options.secure_keypair.has_value())
        //{
        //    keypair = m_options.secure_keypair.value();
        //}
        //else
        {
            keypair = CryptoHelpers.generate_keypair_from_bind_phrase();
        }

        _decryptor = new Decryptor(logger, keypair.get_rx_key(!true));

        var sessionKeyStream = new SessionKeysRadioStream(_decryptor, _logger);
        _radioStreams.Add(sessionKeyStream.Id, sessionKeyStream);

        foreach (var userStream in streamAccessor)
        {
            IRadioStream stream;
            if (userStream.IsFecEnabled)
            {
                stream = new FecStream(userStream.StreamId, userStream.StreamAccessor, _logger);
            }
            else
            {
                stream = new NoFecStream(userStream.StreamId, userStream.StreamAccessor);
            }

            _radioStreams.Add(stream.Id, stream);
        }

        // next session key in delta ms if packets are being fed
        //m_session_key_next_announce_ts = std::chrono::steady_clock::now();
        // Per libsodium documentation, the first nonce should be chosen randomly
        // This selects a random nonce in 32-bit range - we therefore have still 32-bit increasing indexes left, which means tx can run indefinitely
        //m_nonce = randombytes_random();

        _deviceHandlers = devicesProvider
            .GetDevices()
            .Select(device => new DeviceHandler(device, ProcessRxFrame))
            .ToList();
    }

    public void Start()
    {
        foreach (var handler in _deviceHandlers)
        {
            handler.Start();
        }
    }

    public void SetChannel(WlanChannel wlanChannel)
    {
        foreach (var deviceHandler in _deviceHandlers)
        {
            deviceHandler.SetChannel(wlanChannel);
        }
    }

    private readonly HashSet<(byte, bool)> _knownIndexes = new();

    private void ProcessRxFrame(RxFrame frame)
    {
        if (!frame.IsValidWfbFrame())
        {
            return;
        }

        // _logger.LogTrace("Processing valid WFB frame {DataLength}", frame.Data.Length);

        var unique_air_gnd_id = frame.GetValidAirGndId();
        var unique_tx_id = _useGndIdentifier ? LinkConstants.OPENHD_IEEE80211_HEADER_UNIQUE_ID_GND : LinkConstants.OPENHD_IEEE80211_HEADER_UNIQUE_ID_AIR;
        var unique_rx_id = _useGndIdentifier ? LinkConstants.OPENHD_IEEE80211_HEADER_UNIQUE_ID_AIR : LinkConstants.OPENHD_IEEE80211_HEADER_UNIQUE_ID_GND;

        if (unique_air_gnd_id != unique_rx_id)
        {
            _logger.LogWarning("WTF");
            return;
        }

        var radioPort = frame.get_valid_radio_port();

        if (!_knownIndexes.Contains((radioPort.MultiplexIndex, radioPort.Encrypted)))
        {
            _logger.LogWarning($"Val:{radioPort.MultiplexIndex}({radioPort.MultiplexIndex:b8}) Enc:{radioPort.Encrypted} Raw:{frame.MacSrcRadioPort[0]:b8}");
            _knownIndexes.Add((radioPort.MultiplexIndex, radioPort.Encrypted));
        }

        if (_radioStreams.TryGetValue(radioPort.MultiplexIndex, out var stream))
        {
            if (radioPort.MultiplexIndex == 10 && radioPort.Encrypted)
            {

            }

            Memory<byte> decryptedPayload;
            if (radioPort.Encrypted)
            {
                var nonce = frame.GetNonce();
                (var success, decryptedPayload) = _decryptor.AuthenticateAndDecrypt(nonce, frame.Payload);
                if (!success)
                {
                    return;
                }

                //_logger.LogWarning("DECODE SUCCESS");
            }
            else
            {
                // TODO: This is not work for key exchange. Need to put exchange to different place
                var nonce = frame.GetNonce();
                (var success, decryptedPayload) = _decryptor.Authenticate(nonce, frame.Payload);
                if (!success)
                {
                    return;
                }

                //_logger.LogWarning("Auth SUCCESS");
            }

            stream.ProcessFrame(decryptedPayload);
        }
    }
}