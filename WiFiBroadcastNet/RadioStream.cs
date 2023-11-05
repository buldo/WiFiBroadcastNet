using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WiFiBroadcastNet.Crypto;
using WiFiBroadcastNet.Fec;

namespace WiFiBroadcastNet;

internal class RadioStream
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
}
