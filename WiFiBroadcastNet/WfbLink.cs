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

namespace WiFiBroadcastNet;

public class WfbLink
{
    private static readonly byte STREAM_INDEX_SESSION_KEY_PACKETS = 127;

    private readonly bool _useGndIdentifier = false; // Air/ground selector
    private readonly ILogger<WfbLink> _logger;
    private readonly List<DeviceHandler> _deviceHandlers;
    private readonly Dictionary<int, RadioStream> _radioStreams = new()
    {
        {128, new RadioStream(128, new NullFec(), new NullCrypto()) }
    };
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

    private void ProcessRxFrame(RxFrame frame)
    {
        if (!frame.IsValidWfbFrame())
        {
            return;
        }

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

        if (radio_port.MultiplexIndex == STREAM_INDEX_SESSION_KEY_PACKETS)
        {
            ProcessSessionKeyFrame(radio_port, frame);
        }
        else
        {

        }
    }

    private void ProcessSessionKeyFrame(RadioPort radio_port, RxFrame frame)
    {
        // encryption bit must always be set to off on session key packets, since encryption serves no purpose here
        if (radio_port.Encrypted)
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

        var decrypt_res = m_decryptor.onNewPacketSessionKeyData(sessionKeyPacket.sessionKeyNonce, sessionKeyPacket.sessionKeyData);
        //if (decrypt_res == DecryptorResult.SESSION_VALID_NEW || decrypt_res == DecryptorResult.SESSION_VALID_NOT_NEW)
        //{
        //    if (wlan_idx == 0)
        //    { // Pollution is calculated only on card0
        //        m_pollution_openhd_rx_packets++;
        //    }
        //    m_likely_wrong_encryption_valid_session_keys++;
        //}
        //else
        //{
        //    m_likely_wrong_encryption_invalid_session_keys++;
        //}

        // A lot of invalid session keys and no valid session keys hint at a bind phrase mismatch
        //var elapsed_likely_wrong_key = std::chrono::steady_clock::now() - m_likely_wrong_encryption_last_check;
        //if (elapsed_likely_wrong_key > std::chrono::seconds(5))
        //{
        //    // No valid session key(s) and at least one invalid session key
        //    if (m_likely_wrong_encryption_valid_session_keys == 0 && m_likely_wrong_encryption_invalid_session_keys >= 1)
        //    {
        //        m_rx_stats.likely_mismatching_encryption_key = true;
        //    }
        //    else
        //    {
        //        m_rx_stats.likely_mismatching_encryption_key = false;
        //    }
        //    m_likely_wrong_encryption_last_check = std::chrono::steady_clock::now();
        //    m_likely_wrong_encryption_valid_session_keys = 0;
        //    m_likely_wrong_encryption_invalid_session_keys = 0;
        //}

        if (decrypt_res == DecryptorResult.SESSION_VALID_NEW)
        {
            _logger.LogDebug("Initializing new session.");
            foreach (var (key, radioStream) in _radioStreams)
            {
                radioStream.cb_session();
            }
        }
    }
}