using WiFiBroadcastNet.Crypto;
using WiFiBroadcastNet.Fec;

namespace WiFiBroadcastNet.RadioStreams;

internal abstract class RadioStream
{
    private readonly IFecProcessor _fecProcessor;
    private readonly ICryptoBlock _cryptoBlock;

    public RadioStream(
        int id,
        IFecProcessor fecProcessor,
        ICryptoBlock cryptoBlock)
    {
        Id = id;
        _fecProcessor = fecProcessor;
        _cryptoBlock = cryptoBlock;
    }

    public int Id { get; }

    public void cb_session()
    {

    }

    public abstract void ProcessFrame(RadioPort radioPort, RxFrame frame);
}