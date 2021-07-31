﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Util;
using Xamarin.Forms;

[assembly: Xamarin.Forms.Dependency(typeof(LightScout.Droid.PeripheralManagement))]
namespace LightScout.Droid
{
    public enum BluetoothLogType
    {
        Successful,
        Unsuccessful,
        Aborted,
        ElevatedEvent
    }
    public class BluetoothDataLog
    {
        public DateTime timestamp { get; set; }
        public BluetoothLogType bluetoothLogType { get; set; }
        public string extraData { get; set; }
    }
    public class QueueItemOut
    {
        public string communicationId;
        public string messageLeft;
        public string fullMessage;
        public DateTime startedAt;
    }
    public class QueueItemIn
    {
        public string communicationId;
        public string latestHeader;
        public int numMessages;
        public string latestData;
        public bool isEnded;
        public string deviceId;
        public string protocolIn;
        public string protocolOut;
    }
    public class NotifyingDevice
    {
        public BluetoothDevice Device;
        public BluetoothGattCharacteristic Characteristic;
    }
    
    public sealed class ServerManagement
    {
        public static StorageManager storageManager = StorageManager.Instance;
        private static readonly object l1 = new object();
        public static ServerManagement instance = null;
        public static ServerManagement Instance
        {
            get
            {
                lock (l1)
                {
                    if (instance == null)
                    {
                        instance = new ServerManagement();
                    }
                    return instance;
                }

            }
        }

        public static BluetoothGattServer Server;
        public static List<QueueItemOut> QueueOut = new List<QueueItemOut>();
        public static List<QueueItemIn> QueueIn = new List<QueueItemIn>();
        public static int CurrentRequestId;
        public static NotifyingDevice CurrentNotificationTo;
    }
    public class GattServerCallback : BluetoothGattServerCallback
    {
        private ServerManagement ServerManagement = new ServerManagement();
        public override void OnConnectionStateChange(BluetoothDevice device, [GeneratedEnum] ProfileState status, [GeneratedEnum] ProfileState newState)
        {
            base.OnConnectionStateChange(device, status, newState);
        }
        public override void OnCharacteristicReadRequest(BluetoothDevice device, int requestId, int offset, BluetoothGattCharacteristic characteristic)
        {
            Console.WriteLine("Thingy read");
            // need to somehow pass server here
            ServerManagement.Server.SendResponse(device, requestId, GattStatus.Success, Encoding.ASCII.GetBytes("Hello, nothing has been set!").Length, Encoding.ASCII.GetBytes("Hello, nothing has been set!"));

            base.OnCharacteristicReadRequest(device, requestId, offset, characteristic);
        }
        public override void OnCharacteristicWriteRequest(BluetoothDevice device, int requestId, BluetoothGattCharacteristic characteristic, bool preparedWrite, bool responseNeeded, int offset, byte[] value)
        {
            Console.WriteLine("Detecting if proper message");
            try
            {
                ServerManagement.CurrentRequestId = requestId;
                var header = BitConverter.ToString(value.Take(19).ToArray()).Replace("-", string.Empty);
                var communicationId = header.Substring(26, 8);
                var deviceId = header.Substring(4, 12);
                var protocolIn = header.Substring(16, 4);
                var protocolOut = header.Substring(20, 4);
                var messageNumber = int.Parse(header.Substring(26, 4));
                var isEnded = header.Substring(24, 1) == "A" ? false : true;
                var expectingResponse = header.Substring(25, 1) == "1" ? true : false;
                var data = Encoding.ASCII.GetString(value.Skip(19).ToArray());
                Console.WriteLine("Data: " + data);
                // 2 things are essentially writing at once, and I don't know why. It disrupts the flow
                // assume for now that it just wants to send a test value
                if (ServerManagement.QueueIn.Exists(item => item.communicationId == communicationId))
                {
                    // use existing device
                    ServerManagement.QueueIn.Single(item => item.communicationId == communicationId).latestHeader = header;
                    ServerManagement.QueueIn.Single(item => item.communicationId == communicationId).latestData = (ServerManagement.QueueIn.Single(item => item.communicationId == communicationId).latestData == null ? "" : ServerManagement.QueueIn.Single(item => item.communicationId == communicationId).latestData) + data;
                    ServerManagement.QueueIn.Single(item => item.communicationId == communicationId).isEnded = isEnded;
                    ServerManagement.QueueIn.Single(item => item.communicationId == communicationId).numMessages += 1;
                }
                else
                {
                    ServerManagement.QueueIn.Add(new QueueItemIn() { communicationId = communicationId, deviceId = deviceId, protocolIn = protocolIn, protocolOut = protocolOut, latestData = data, latestHeader = header, isEnded = isEnded, numMessages = 1 });
                    
                }
                if (ServerManagement.CurrentNotificationTo != null && device.Address == ServerManagement.CurrentNotificationTo.Device.Address)
                {
                    CheckIfFinished(ServerManagement.QueueIn.Single(item => item.communicationId == communicationId));
                }
                else
                {
                    // cannot be notified of changes, so I must send back an error!
                    ServerManagement.Server.SendResponse(device, requestId, GattStatus.RequestNotSupported, Encoding.ASCII.GetBytes("Comm ID updated!").ToArray().Length, Encoding.ASCII.GetBytes("Comm ID updated!").ToArray());
                }
                //check if device is currently subscribed





                ServerManagement.Server.SendResponse(device, requestId, GattStatus.Success, Encoding.ASCII.GetBytes("Comm ID updated!").ToArray().Length, Encoding.ASCII.GetBytes("Comm ID updated!").ToArray());
            }
            catch(Exception e)
            {
                ServerManagement.Server.SendResponse(device, requestId, GattStatus.Failure, Encoding.ASCII.GetBytes("Comm ID updated!").ToArray().Length, Encoding.ASCII.GetBytes("Comm ID updated!").ToArray());
                Console.WriteLine("Detected BAD Write Request!");
                Console.WriteLine(e);
            }
            
            
            //characteristic.SetValue(value);
            base.OnCharacteristicWriteRequest(device, requestId, characteristic, preparedWrite, responseNeeded, offset, value);
        }
        public async void UpdateNotification(string data, string protocolIn, string protocolOut)
        {
            var communicationId = GenerateRandomHexString();
            var encodedMessage = Encoding.ASCII.GetBytes(data);
            var numMessages = (int)Math.Ceiling((float)Encoding.ASCII.GetBytes(data).Length / (float)459);
            for(int m = 0; m < numMessages; m++)
            {
                var headerString = (862).ToString("0000") + "1234567890ab" + protocolIn + protocolOut + (m + 1 == numMessages ? "e" : "a")+ "0" + (m + 1).ToString("0000") + communicationId;
                var finalByteArray = StringToByteArray(headerString).Concat(encodedMessage.Skip(m * 459).ToArray()).ToArray();
                ServerManagement.CurrentNotificationTo.Characteristic.SetValue(finalByteArray);
                ServerManagement.Server.NotifyCharacteristicChanged(ServerManagement.CurrentNotificationTo.Device, ServerManagement.CurrentNotificationTo.Characteristic, false);
                
                Console.WriteLine(m.ToString() + " message notified");
                await Task.Delay(1000);
            }
        }
        public override void OnDescriptorWriteRequest(BluetoothDevice device, int requestId, BluetoothGattDescriptor descriptor, bool preparedWrite, bool responseNeeded, int offset, byte[] value)
        {
            Console.WriteLine("Notify yay!");
            ServerManagement.CurrentNotificationTo = new NotifyingDevice() { Characteristic = descriptor.Characteristic, Device = device };
            ServerManagement.Server.SendResponse(device, requestId, GattStatus.Success, offset, value);
            base.OnDescriptorWriteRequest(device, requestId, descriptor, preparedWrite, responseNeeded, offset, value);
        }
        public override void OnDescriptorReadRequest(BluetoothDevice device, int requestId, int offset, BluetoothGattDescriptor descriptor)
        {
            var alreadyExistingUUID = UUID.FromString("0000A404-0000-1000-8000-00805F9B34FB");
            if (descriptor.Uuid.ToString() == alreadyExistingUUID.ToString())
            {
                // getting tablet unique ID
                ServerManagement.Server.SendResponse(device, requestId, GattStatus.Success, Encoding.ASCII.GetBytes("1234567890ab").Length, Encoding.ASCII.GetBytes("1234567890ab"));
            }
            else
            {
                ServerManagement.Server.SendResponse(device, requestId, GattStatus.Success, Encoding.ASCII.GetBytes("uwu").Length, Encoding.ASCII.GetBytes("uwu"));
                
            }
            //ServerManagement.CurrentNotificationTo = new NotifyingDevice() { Characteristic = descriptor.Characteristic, Device = device };
            base.OnDescriptorReadRequest(device, requestId, offset, descriptor);
        }
        /*public override void OnNotificationSent(BluetoothDevice device, [GeneratedEnum] GattStatus status)
        {
            ServerManagement.Server.SendResponse(device, ServerManagement.CurrentRequestId, GattStatus.Success, Encoding.ASCII.GetBytes("AAA").ToArray().Length, Encoding.ASCII.GetBytes("AAA").ToArray());
            base.OnNotificationSent(device, status);
        }*/
        public void CheckIfFinished(QueueItemIn item)
        {
            if (item.isEnded)
            {
                // first do what was told to do
                switch (item.protocolIn)
                {
                    case "0000":
                        Console.WriteLine("(0000) Doing nothing");
                        break;
                    case "0998":
                        Console.WriteLine("(0998) Ping from the central!");
                        break;
                    case "0999":
                        Console.WriteLine("(0999) Data from central: " + item.protocolIn);
                        break;
                    case "A101":
                        Console.WriteLine("(a101) This should lock tablet");
                        // lock tablet here
                        break;
                    case "A102":
                        Console.WriteLine("(a101) This should unlock tablet");
                        // unlock tablet here
                        break;
                    case "A111":
                        Console.WriteLine("(a111) Sending dialog box");
                        MessagingCenter.Send("MasterPage", "DialogBox", item.latestData);
                        break;
                }
                // next send back what told to
                switch (item.protocolOut)
                {
                    case "0000":
                        Console.WriteLine("(0000 >) Doing nothing");
                        break;
                    case "0001":
                        // allow queue to do something here
                        break;
                    case "0998":
                        UpdateNotification("Pong!", item.protocolIn, item.protocolOut);
                        break;
                    case "0999":
                        UpdateNotification("I'm a data packet!", item.protocolIn, item.protocolOut);
                        break;
                }
            }
            

        }
        private static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        private static string GenerateRandomHexString()
        {
            var characters = "0 1 2 3 4 5 6 7 8 9 a b c d e f";
            var randomGen = new System.Random();
            var finalString = "";
            for (var i = 0; i < 8; i++)
            {
                finalString = finalString + characters.Split(' ')[randomGen.Next(0, 16)];
            }
            return finalString;
        }
    }
    public class AdvertiserCallback : AdvertiseCallback
    {
        public async override void OnStartSuccess(AdvertiseSettings settingsInEffect)
        {
            Console.WriteLine(settingsInEffect.IsConnectable);
            AddToBluetoothLog("", BluetoothLogType.Successful);
            base.OnStartSuccess(settingsInEffect);
        }
        public async override void OnStartFailure([GeneratedEnum] AdvertiseFailure errorCode)
        {
            Console.WriteLine(errorCode.ToString());
            AddToBluetoothLog("", BluetoothLogType.Unsuccessful);
            base.OnStartFailure(errorCode);
        }
        public async void AddToBluetoothLog(string extraData, BluetoothLogType bluetoothLogType)
        {
            var existingData = await ServerManagement.storageManager.GetData("bluetooth_log");
            if (existingData == "")
            {
                var emptyList = new List<BluetoothDataLog>() { new BluetoothDataLog() { timestamp = DateTime.Now, bluetoothLogType = bluetoothLogType, extraData = extraData } };
                existingData = Newtonsoft.Json.JsonConvert.SerializeObject(emptyList);
            }
            else
            {
                var existingList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<BluetoothDataLog>>(existingData);
                var newItem = new BluetoothDataLog() { extraData = extraData, bluetoothLogType = bluetoothLogType, timestamp = DateTime.Now };
                existingList.Add(newItem);
                existingData = Newtonsoft.Json.JsonConvert.SerializeObject(existingList);
            }
            Console.WriteLine(await ServerManagement.storageManager.SetData("bluetooth_log", existingData));
        }
    }


    class PeripheralManagement : BLEPeripheral
    {
        private ServerManagement serverManagement = new ServerManagement();
        private AdvertiseCallback callback;
        public void StartAdvertising(string serviceUUID, string serviceName)
        {
            AdvertiseSettings settings = new AdvertiseSettings.Builder().SetConnectable(true).Build();

            BluetoothAdapter.DefaultAdapter.SetName("Glare");

            ParcelUuid parcelUuid = new ParcelUuid(UUID.FromString("00000862-0000-1000-8000-00805f9b34fb"));
            AdvertiseData data = new AdvertiseData.Builder().AddServiceUuid(parcelUuid).SetIncludeDeviceName(true).Build();

            this.callback = new AdvertiserCallback();
            BluetoothAdapter.DefaultAdapter.BluetoothLeAdvertiser.StartAdvertising(settings, data, callback);

            BluetoothGattCharacteristic chara = new BluetoothGattCharacteristic(
                UUID.FromString("00000001-0000-1000-8000-00805f9b34fb"),
                GattProperty.Read | GattProperty.Write | GattProperty.Notify,
                GattPermission.Read | GattPermission.Write
            );
            BluetoothGattDescriptor uniqueIdDesc = new BluetoothGattDescriptor(UUID.FromString("0000a404-0000-1000-8000-00805F9B34FB"), GattDescriptorPermission.Read);
            BluetoothGattDescriptor notifdesc = new BluetoothGattDescriptor(UUID.FromString("00002902-0000-1000-8000-00805F9B34FB"), GattDescriptorPermission.Write | GattDescriptorPermission.Read);
            chara.AddDescriptor(uniqueIdDesc);
            chara.AddDescriptor(notifdesc);
            BluetoothGattService service = new BluetoothGattService(
                UUID.FromString("00000862-0000-1000-8000-00805f9b34fb"),
                GattServiceType.Primary
            );
            service.AddCharacteristic(chara);

            BluetoothManager manager = (BluetoothManager)Android.App.Application.Context.GetSystemService(Context.BluetoothService);
            BluetoothGattServer server = manager.OpenGattServer(Android.App.Application.Context, new GattServerCallback());
            ServerManagement.Server = server;
            server.AddService(service);
        }
    }
}