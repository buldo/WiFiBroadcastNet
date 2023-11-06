using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ASodium;

using Bld.WlanUtils;
using Microsoft.Extensions.Logging;
using WiFiBroadcastNet.Crypto;
using WiFiBroadcastNet.Devices;
using WiFiBroadcastNet.Fec;
using WiFiBroadcastNet.RadioStreams;

namespace WiFiBroadcastNet;

public class WfbLink
{
    private static readonly byte STREAM_INDEX_SESSION_KEY_PACKETS = 127;

    private readonly bool _useGndIdentifier = true; // Air/ground selector
    private readonly ILogger<WfbLink> _logger;
    private readonly List<DeviceHandler> _deviceHandlers;

    private readonly Dictionary<int, RadioStream> _radioStreams = new();
    private readonly Decryptor m_decryptor;

    public WfbLink(
        IDevicesProvider devicesProvider,
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

        m_decryptor = new Decryptor(logger, keypair.get_rx_key(!true));

        var sessionKeyStream = new SessionKeysRadioStream(m_decryptor, _logger);
        _radioStreams.Add(sessionKeyStream.Id, sessionKeyStream);

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

    private readonly HashSet<byte> _knownIndexes = new HashSet<byte>();

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

        var radio_port = frame.get_valid_radio_port();
        var nonce = frame.GetNonce();

        if (!_knownIndexes.Contains(radio_port.MultiplexIndex))
        {
            _logger.LogWarning($"Val:{radio_port.MultiplexIndex}({radio_port.MultiplexIndex:b8}) Enc:{radio_port.Encrypted} Raw:{frame.MacSrcRadioPort[0]:b8}");
            _knownIndexes.Add(radio_port.MultiplexIndex);
        }

        if (_radioStreams.TryGetValue(radio_port.MultiplexIndex, out var stream))
        {
            stream.ProcessFrame(radio_port, frame);
        }
    }
}