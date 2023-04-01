// See https://aka.ms/new-console-template for more information

using PerformanceTester.Tx;
using PerformanceTester.Rx;

var isTx = true;
var isRx = true;
Console.WriteLine("Hello, World!");

var tasks = new List<Task>();

var txServer = new TxHost();
if (isTx)
{
    var txTask = txServer.StartAsync();
    tasks.Add(txTask);
}

var rxServer = new RxHost();
if (isRx)
{
    var rxTask = rxServer.StartAsync();
    tasks.Add(rxTask);
}

await Task.WhenAll(tasks.ToArray());