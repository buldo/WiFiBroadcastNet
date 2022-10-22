// See https://aka.ms/new-console-template for more information

using WiFiBroadcastNet;
using WiFiBroadcastNet.Rx;
using WiFiBroadcastNet.Tx;

Console.WriteLine("Starting");

var factory = new RadioDeviceFactory();
var rxDevice = factory.CreateDeviceByName("");
rxDevice.PrepareOs();
rxDevice.SetFrequency(Frequencies.F5805);
var receiver = new Receiver(new[] { rxDevice });
receiver.Start();

var txDevice = factory.CreateDeviceByName("");
txDevice.PrepareOs();
txDevice.SetFrequency(Frequencies.F5805);
var transmitter = new Transmitter(txDevice);
transmitter.Start();

Console.WriteLine("End");
