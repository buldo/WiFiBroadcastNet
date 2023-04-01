using Grpc.Core;

namespace PerformanceTester.Tx;

public class TxService : TxServer.TxServerBase
{
     public async override Task<StartTxReply> StartTransmit(StartTxRequest request, ServerCallContext context)
     {
         return new StartTxReply();
     }
}