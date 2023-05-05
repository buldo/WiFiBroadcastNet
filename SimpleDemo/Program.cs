// See https://aka.ms/new-console-template for more information

using WiFiBroadcastNet;
using WiFiBroadcastNet.Rx;
using WiFiBroadcastNet.Tx;

Console.WriteLine("Starting");

var factory = new RadioDeviceFactory();
var rxDevice = factory.CreateDeviceByName("wlx502b73201c53");
rxDevice.PrepareOs();
rxDevice.SetFrequency(Frequencies.F5805);
var receiver = new Receiver(new[] { rxDevice });
receiver.Start();

var txDevice = factory.CreateDeviceByName("wlx502b736002d8");
txDevice.PrepareOs();
txDevice.SetFrequency(Frequencies.F5805);
var transmitter = new Transmitter(txDevice);
transmitter.Start();

var testData = new byte[1024];
Random.Shared.NextBytes(testData);

var readTask = ReadTask(receiver, testData);
var writeTask = WriteTask(transmitter, testData);

var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
cts.Token.WaitHandle.WaitOne();
Console.WriteLine("End");

async Task ReadTask(Receiver receiver, byte[] bytes)
{
    Console.WriteLine($"Received {bytes.Length} bytes");
}

async Task WriteTask(Transmitter transmitter, byte[] bytes)
{
    transmitter.Send(bytes);
}
