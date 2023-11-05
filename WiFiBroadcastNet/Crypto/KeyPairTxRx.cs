namespace WiFiBroadcastNet.Crypto;

/// <summary>
/// A wb keypair are 2 keys, one for transmitting, one for receiving
/// (Since both ground and air unit talk bidirectional)
/// We use a different key for the down-link / uplink, respective
/// </summary>
internal class KeyPairTxRx
{
    public Key key_1;
    public Key key_2;
    Key get_tx_key(bool is_air)
    {
        return is_air ? key_1 : key_2;
    }
    public Key get_rx_key(bool is_air)
    {
        return is_air ? key_2 : key_1;
    }
};