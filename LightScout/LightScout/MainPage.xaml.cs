﻿using LightScout.Models;
using Newtonsoft.Json;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace LightScout
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        private bool MenuAnimationActive = false;
        private bool MenuOpen = false;
        private bool NotificationActive = false;
        private int NextMatchIndex = -1;
        private Button currentlySelectedComm;
        private static bool[] ControlPanel = new bool[2];
        private static IBluetoothLE ble = CrossBluetoothLE.Current;
        private static IAdapter adapter = CrossBluetoothLE.Current.Adapter;
        private static IDevice deviceIWant;
        private static ObservableCollection<IDevice> Devices = new ObservableCollection<IDevice>();
        private static bool Balanced;
        private List<TeamMatch> listofmatches = new List<TeamMatch>();
        private List<TeamMatchViewItem> listofviewmatches = new List<TeamMatchViewItem>();
        private static bool TimerAlreadyCreated = false;
        private static int timesalive = 0;
        private string currentCodeString = "";
        private CodeReason currentCodeReason;
        private static SubmitVIABluetooth submitVIABluetoothInstance = new SubmitVIABluetooth();
        private List<string> tabletlist = new List<string>();

        public enum CodeReason
        {
            DeleteMatch,
            EditMatch,
            CreateMatch,
            EditConfig
        }

        public MainPage()
        {
            
            InitializeComponent();
            ControlPanel[0] = false;
            ControlPanel[1] = false;
            adapter.DeviceDiscovered += async (s, a) =>
            {

                if (a.Device.Name != null)
                {
                    Devices.Add(a.Device);
                }

            };
            adapter.DeviceConnected += async (s, a) =>
            {
                Console.WriteLine("Connected to: " + a.Device.Name.ToString());
                //status.Text = "Connected to: " + a.Device.Name.ToString();
                deviceIWant = a.Device;
            };
            adapter.DeviceConnectionLost += (s, a) =>
            {
                Console.WriteLine("Lost connection to: " + a.Device.Name.ToString());
                //status.Text = "Disconnected from: " + a.Device.Name.ToString();
                Devices.Clear();
            };
            adapter.DeviceDisconnected += (s, a) =>
            {
                Console.WriteLine("Lost connection to: " + a.Device.Name.ToString());
                //status.Text = "Disconnected from: " + a.Device.Name.ToString();
                Devices.Clear();
            };

            if (!TimerAlreadyCreated)
            {
                Console.WriteLine("Test started :)");
                Device.StartTimer(TimeSpan.FromMinutes(1), () =>
                {
                    timesalive++;
                    Console.WriteLine("This message has appeared " + timesalive.ToString() + " times. Last ping at " + DateTime.Now.ToShortTimeString());
                    TimerAlreadyCreated = true;
                    return true;
                });
            }

            /*Device.StartTimer(TimeSpan.FromMinutes(1), () =>
            {
                if(deviceIWant != null)
                {
                    bluetoothHandler.SubmitBluetooth(adapter, deviceIWant);
                }
                
                return true;
            });*/
            try
            {
                var converter = new ColorTypeConverter();
                var allmatchesraw = DependencyService.Get<DataStore>().LoadData("JacksonEvent2020.txt");
                listofmatches = JsonConvert.DeserializeObject<List<TeamMatch>>(allmatchesraw);
                var upnext = false;
                TeamMatchViewItem selectedItem = new TeamMatchViewItem();
                var upnextselected = false;
                int i = 0;
                foreach (var match in listofmatches.OrderBy(x => x.MatchNumber).ToList())
                {

                    var newmatchviewitem = new TeamMatchViewItem();
                    upnext = false;
                    if (!match.ClientSubmitted)
                    {
                        if (!upnextselected)
                        {
                            upnext = true;
                            upnextselected = true;
                        }
                    }
                    newmatchviewitem.Completed = match.ClientSubmitted;
                    if (match.TabletId != null)
                    {
                        newmatchviewitem.IsRed = match.TabletId.StartsWith("R");
                        newmatchviewitem.IsBlue = match.TabletId.StartsWith("B");
                    }

                    newmatchviewitem.IsUpNext = upnext;
                    newmatchviewitem.TeamName = match.TeamName;
                    if (match.TeamName == null)
                    {
                        newmatchviewitem.TeamName = "FRC Team " + match.TeamNumber.ToString();
                    }
                    newmatchviewitem.TeamNumber = match.TeamNumber;
                    newmatchviewitem.NewPlaceholder = false;
                    newmatchviewitem.ActualMatch = true;
                    newmatchviewitem.MatchNumber = match.MatchNumber;
                    newmatchviewitem.TabletName = match.TabletId;
                    if(match.AlliancePartners != null && match.AlliancePartners.Length >= 2)
                    {
                        newmatchviewitem.AlliancePartner1 = match.AlliancePartners[0];
                        newmatchviewitem.AlliancePartner2 = match.AlliancePartners[1];
                    }
                    
                    if (newmatchviewitem.IsBlue)
                    {
                        newmatchviewitem.TeamColor = (Color)converter.ConvertFromInvariantString("#3475da");
                    }
                    else
                    {
                        newmatchviewitem.TeamColor = (Color)converter.ConvertFromInvariantString("#e53434");
                    }
                    //newmatchviewitem.teamIcon = ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String("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")));
                    newmatchviewitem.teamIcon = ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String("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")));
                    if (upnext)
                    {
                        selectedItem = newmatchviewitem;
                        NextMatchIndex = i;
                    }
                    listofviewmatches.Add(newmatchviewitem);
                    i++;
                }
                var placeholdermatch = new TeamMatchViewItem();
                placeholdermatch.ActualMatch = false;
                placeholdermatch.NewPlaceholder = true;
                listofviewmatches.Add(placeholdermatch);
                //listOfMatches.ItemsSource = listofviewmatches;
                carouseluwu.ItemsSource = listofviewmatches;
                if (selectedItem != null)
                {
                    carouseluwu.CurrentItem = selectedItem;
                }
                else
                {
                    carouseluwu.CurrentItem = listofviewmatches.First();
                }
            }
            catch (Exception ex)
            {

            }


            tabletlist = new string[6] { "R1", "R2", "R3", "B1", "B2", "B3" }.ToList();
            tabletPicker.ItemsSource = tabletlist;
            MatchProgressList.Progress = (float)((float)1 / (float)listofviewmatches.Count);
            SetMenuItems();
        }
        private async void SetMenuItems()
        {
            settingsButton.Opacity = 0;
            showUsb.TranslationX = 0;
            showSettings.TranslationX = 0;
            showUsb.TranslationX = (showMenu.X - showUsb.X);
            showUsb.IsVisible = true;
            showUsb.TranslateTo(showUsb.TranslationX - (showMenu.X - showUsb.X), showUsb.TranslationY, 10, Easing.CubicInOut);
            showAbout.TranslationX = (showMenu.X - showAbout.X);
            showAbout.IsVisible = true;
            showAbout.TranslateTo(showAbout.TranslationX - (showMenu.X - showAbout.X), showAbout.TranslationY, 10, Easing.CubicInOut);
            showSettings.TranslationX = (showMenu.X - showSettings.X);
            showSettings.IsVisible = true;
            await showSettings.TranslateTo(showSettings.TranslationX - (showMenu.X - showSettings.X), showSettings.TranslationY, 10, Easing.CubicInOut);
            await Task.Delay(10);
            showUsb.TranslateTo((showMenu.X - showUsb.X), showUsb.TranslationY, 10);
            showAbout.TranslateTo((showMenu.X - showAbout.X), showAbout.TranslationY, 10);
            await showSettings.TranslateTo((showMenu.X - showSettings.X), showSettings.TranslationY, 10);
            showUsb.IsVisible = false;
            showSettings.IsVisible = false;
            settingsButton.Opacity = 1;
        }

        private void FTCShow_Clicked(object sender, EventArgs e)
        {
            Navigation.PushAsync(new FTCMain());
        }

        private void FRCShow_Clicked(object sender, EventArgs e)
        {
            Navigation.PushAsync(new FRCMain(new TeamMatch() { PowerCellInner = new int[21], PowerCellOuter = new int[21], PowerCellLower = new int[21], PowerCellMissed = new int[21], MatchNumber = 1, TeamNumber = 862 }));


        }
        private async void CreateNewScoutNames(object sender, EventArgs e)
        {
            DependencyService.Get<DataStore>().SaveConfigurationFile("scoutNames", new string[3] { "John Doe", "Imaex Ample", "Guest Scouter" });
            Console.WriteLine(DependencyService.Get<DataStore>().LoadConfigFile());
            await DismissNotification();
            await NewNotification("Scout Names Reset!");
        }
        private async void SendDummyData(object sender, EventArgs e)
        {
            DependencyService.Get<DataStore>().SaveDummyData("JacksonEvent2020.txt");
            await DismissNotification();
            await NewNotification("Data Reset!");
        }
        private void ReloadScreen(object sender, EventArgs e)
        {
            Navigation.PushAsync(new MainPage());
        }
        private void commscheck_Clicked(object sender, EventArgs e)
        {

        }
        private async void CheckBluetooth(object sender, EventArgs e)
        {
            //BindingContext = new BluetoothDeviceViewModel();

            Devices.Clear();

            await adapter.StartScanningForDevicesAsync();

        }

        private async void listofdevices_ItemTapped(object sender, ItemTappedEventArgs e)
        {
            IDevice selectedDevice = e.Item as IDevice;

            if (deviceIWant != null)
            {
                await adapter.DisconnectDeviceAsync(deviceIWant);
                await adapter.ConnectToDeviceAsync(selectedDevice);
                deviceIWant = selectedDevice;
            }
            else
            {
                await adapter.ConnectToDeviceAsync(selectedDevice);
                deviceIWant = selectedDevice;
            }
        }

        private void dcFromBT_Clicked(object sender, EventArgs e)
        {
            adapter.DisconnectDeviceAsync(deviceIWant);
        }

        

        private async void listOfMatches_ItemTapped(object sender, ItemTappedEventArgs e)
        {
            Console.WriteLine(listofmatches[e.ItemIndex].TeamNumber.ToString() + "'s match at match #" + listofmatches[e.ItemIndex].MatchNumber.ToString());
            bool answer = true;
            if (listofmatches[e.ItemIndex].ClientSubmitted)
            {
                answer = await DisplayAlert("Match Completed", "This match has already been completed by someone using this tablet, would you still like to continue?", "Continue", "Cancel");
            }
            if (answer)
            {
                Navigation.PushAsync(new FRCMain(listofmatches[e.ItemIndex]));
            }
            else
            {
                var listobject = sender as ListView;
                listobject.SelectedItem = null;
            }

        }

        private void MenuItem_Clicked(object sender, EventArgs e)
        {
            moreinfoMenu.IsVisible = true;
        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            moreinfoMenu.IsVisible = false;
        }

        private void tabletPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            var list = sender as Picker;
            DependencyService.Get<DataStore>().SaveConfigurationFile("tabletId", tabletlist[list.SelectedIndex]);
        }
        private async void GoToFRCPage(object sender, EventArgs e)
        {
            var currentindex = listofviewmatches.FindIndex(a => a == carouseluwu.CurrentItem);
            Console.WriteLine(listofmatches[currentindex].TeamNumber.ToString() + "'s match at match #" + listofmatches[currentindex].MatchNumber.ToString());
            bool answer = true;
            if (listofmatches[currentindex].ClientSubmitted)
            {
                answer = await DisplayAlert("Match Completed", "This match has already been completed by someone using this tablet, would you still like to continue?", "Continue", "Cancel");
            }
            else if (currentindex != NextMatchIndex)
            {
                if (NextMatchIndex != -1)
                {
                    answer = await DisplayAlert("Match Not Next", "This match is not up next! Would you still like to continue?", "Continue", "Cancel");
                }

            }
            if (answer)
            {
                Navigation.PushAsync(new FRCMain(listofmatches[currentindex]));
            }
            else
            {
                //var listobject = sender as ListView;
                //listobject.SelectedItem = null;
            }
        }


        private void carouseluwu_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
        {
            var currentindex = listofviewmatches.FindIndex(a => a == carouseluwu.CurrentItem);
            if (((TeamMatchViewItem)carouseluwu.CurrentItem).ActualMatch == true)
            {
                MatchProgressList.ProgressTo((double)((float)(currentindex + 1) / (float)(listofviewmatches.Count - 1)), 250, Easing.CubicInOut);
            }
            else
            {
                MatchProgressList.ProgressTo(1, 250, Easing.CubicInOut);
            }

        }

        private async void getDataFromServer_Clicked(object sender, EventArgs e)
        {

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = cancellationTokenSource.Token;
            MessagingCenter.Subscribe<SubmitVIABluetooth, int>(this, "receivedata", async (messagesender, value) => {
                switch (value)
                {
                    case 1:
                        getDataFromServer.Text = "Process in Progress";
                        break;
                    case 2:
                        getDataFromServer.Text = "Process in Progress";
                        break;
                    case 3:
                        ReloadMatches();
                        cancellationTokenSource.Cancel();
                        MessagingCenter.Unsubscribe<SubmitVIABluetooth, int>(this, "receivedata");
                        await DismissNotification();
                        await NewNotification("GET Succeeded!");
                        break;
                    case -1:
                        getDataFromServer.Text = "Process Failed";
                        break;
                }
            });
            await submitVIABluetoothInstance.GetDefaultData(token);
            var i = 0;
            Device.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                i++;
                if (i < 8)
                {
                    return true;
                }
                else
                {
                    cancellationTokenSource.Cancel();
                    return false;
                }
            });
        }

        private async void resetBTLock_Clicked(object sender, EventArgs e)
        {
            //DependencyService.Get<DataStore>().SaveConfigurationFile("bluetoothStage", 0);
            //resetBTLock.Text = "Reset!!";
            MessagingCenter.Subscribe<object, int>(this, "USBResponse", async (mssender, value) =>
            {
                switch (value)
                {
                    case 1:
                        await DismissNotification();
                        await NewNotification("USB: Handshake Started");
                        break;
                    case 2:
                        await DismissNotification();
                        await NewNotification("USB: Response Gotten");
                        break;
                    case 3:
                        await DismissNotification();
                        await NewNotification("USB: Completed");
                        break;
                    case -1:
                        await DismissNotification();
                        await NewNotification("USB: Failed");
                        DisplayAlert("Not Available", "The USB socket is currently in use from a previous request. We closed it for you. Please try again!", "Ok!");
                        MessagingCenter.Unsubscribe<object, int>(this, "USBResponse");
                        break;
                }
            });
            if (Battery.PowerSource == BatteryPowerSource.Usb || Battery.PowerSource == BatteryPowerSource.AC)
            {
                var jsondata = DependencyService.Get<DataStore>().LoadData("JacksonEvent2020.txt");
                DependencyService.Get<USBCommunication>().SendData(jsondata);
            }
            else
            {
                await DismissNotification();
                await NewNotification("USB Not Connected!");
            }


        }
        public static bool CheckForInternetConnection()
        {
            try
            {
                using (var client = new WebClient())
                using (client.OpenRead("http://google.com/generate_204"))
                    return true;
            }
            catch
            {
                return false;
            }
        }
        private async void SendTBAData(object sender, EventArgs e)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = cancellationTokenSource.Token;
            var isconnected = CheckForInternetConnection();
            //DependencyService.Get<DataStore>().SaveConfigurationFile("bluetoothStage", 0);
            //resetBTLock.Text = "Reset!!";
            
            if (isconnected)
            {
                MessagingCenter.Subscribe<SubmitVIABluetooth, int>(this, "tbasenddata", async (mssender, value) =>
                {
                    switch (value)
                    {
                        case 1:
                            await DismissNotification();
                            await NewNotification("TBA: Handshake Started");
                            break;
                        case 2:
                            await DismissNotification();
                            await NewNotification("TBA: Response Gotten");
                            break;
                        case 3:
                            await DismissNotification();
                            await NewNotification("TBA: Completed");
                            break;
                        case -1:
                            await DismissNotification();
                            await NewNotification("TBA: Failed");
                            DisplayAlert("Not Available", "You don't have internet! If you were not told to do this, keep it that way.", "Ok!");
                            MessagingCenter.Unsubscribe<SubmitVIABluetooth, int>(this, "tbasenddata");
                            break;
                    }
                });
                await submitVIABluetoothInstance.SendTBAData(token);
            }
            else
            {
                DisplayAlert("Not Available", "You don't have internet! If you were not told to do this, keep it that way.", "Ok!");
            }
            


        }
        private async Task NewNotification(string NotificationText)
        {
            Device.BeginInvokeOnMainThread(async () =>
            {
                NotificationActive = true;
                NotificationLabel.Text = NotificationText;
                notificationContainer.TranslationY = 150;
                notificationMedium.IsVisible = true;
                await notificationContainer.TranslateTo(notificationContainer.TranslationX, notificationContainer.TranslationY - 150, 500, Easing.CubicInOut);
                await NotificationIcon.RotateTo(25, 100, Easing.CubicIn);
                await NotificationIcon.RotateTo(0, 100, Easing.CubicIn);
                await NotificationIcon.RotateTo(25, 100, Easing.CubicIn);
                await NotificationIcon.RotateTo(0, 100, Easing.CubicIn);
                await NotificationIcon.RotateTo(25, 100, Easing.CubicIn);
                await NotificationIcon.RotateTo(0, 100, Easing.CubicIn);
            });
            
        }
        private async Task DismissNotification()
        {
            Device.BeginInvokeOnMainThread(async () =>
            {
                if (NotificationActive)
                {
                    NotificationActive = false;
                    await notificationContainer.TranslateTo(notificationContainer.TranslationX, notificationContainer.TranslationY + 150, 500, Easing.CubicInOut);

                    notificationMedium.IsVisible = false;
                    notificationContainer.TranslationY = 0;
                }
            });
            


        }
        private async void sendNotification_Clicked(object sender, EventArgs e)
        {
            await DismissNotification();
            await NewNotification("Test Notification");
        }
        private async void dismissNotification_Clicked(object sender, EventArgs e)
        {
            await DismissNotification();

        }

        private async void AddCodeNumber(object sender, EventArgs e)
        {
            var converter = new ColorTypeConverter();
            var codebutton = (Button)sender as Button;
            switch (codebutton.Text)
            {
                case "0":
                    currentCodeString = currentCodeString + "0";
                    break;
                case "1":
                    currentCodeString = currentCodeString + "1";
                    break;
                case "2":
                    currentCodeString = currentCodeString + "2";
                    break;
                case "3":
                    currentCodeString = currentCodeString + "3";
                    break;
                case "4":
                    currentCodeString = currentCodeString + "4";
                    break;
                case "5":
                    currentCodeString = currentCodeString + "5";
                    break;
                case "6":
                    currentCodeString = currentCodeString + "6";
                    break;
                case "7":
                    currentCodeString = currentCodeString + "7";
                    break;
                case "8":
                    currentCodeString = currentCodeString + "8";
                    break;
                case "9":
                    currentCodeString = currentCodeString + "9";
                    break;
                default:
                    currentCodeString = "";
                    codeButtonCancel.FadeTo(0, 150, Easing.SinIn);
                    codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    break;
            }
            if (currentCodeString.Length == 1)
            {
                codeButtonCancel.FadeTo(1, 150, Easing.SinIn);
                codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("#2a7afa");
                codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            }
            else if (currentCodeString.Length == 2)
            {
                codeButtonCancel.FadeTo(1, 150, Easing.SinIn);
                codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("#2a7afa");
                codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("#2a7afa");
                codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            }
            else if (currentCodeString.Length == 3)
            {
                codeButtonCancel.FadeTo(1, 150, Easing.SinIn);
                codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("#2a7afa");
                codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("#2a7afa");
                codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("#2a7afa");
                codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            }
            else if (currentCodeString.Length == 4)
            {
                codeButtonCancel.FadeTo(0, 150, Easing.SinIn);
                codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("#2a7afa");
                codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("#2a7afa");
                codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("#2a7afa");
                codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("#2a7afa");
                if (currentCodeString == JsonConvert.DeserializeObject<LSConfiguration>(DependencyService.Get<DataStore>().LoadConfigFile()).ScoutAuthCode.ToString())
                {
                    codeButton0.IsEnabled = false;
                    codeButton1.IsEnabled = false;
                    codeButton2.IsEnabled = false;
                    codeButton3.IsEnabled = false;
                    codeButton4.IsEnabled = false;
                    codeButton5.IsEnabled = false;
                    codeButton6.IsEnabled = false;
                    codeButton7.IsEnabled = false;
                    codeButton8.IsEnabled = false;
                    codeButton9.IsEnabled = false;
                    codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Green");
                    codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Green");
                    codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Green");
                    codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Green");

                    await Task.Delay(100);
                    codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    await Task.Delay(100);
                    codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Green");
                    codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Green");
                    codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Green");
                    codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Green");
                    await Task.Delay(100);
                    codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    await Task.Delay(100);
                    codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Green");
                    codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Green");
                    codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Green");
                    codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Green");
                    await Task.Delay(100);
                    codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    await Task.Delay(100);
                    codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Green");
                    codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Green");
                    codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Green");
                    codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Green");

                    codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeButton0.IsEnabled = true;
                    codeButton1.IsEnabled = true;
                    codeButton2.IsEnabled = true;
                    codeButton3.IsEnabled = true;
                    codeButton4.IsEnabled = true;
                    codeButton5.IsEnabled = true;
                    codeButton6.IsEnabled = true;
                    codeButton7.IsEnabled = true;
                    codeButton8.IsEnabled = true;
                    codeButton9.IsEnabled = true;
                    switch (currentCodeReason)
                    {
                        case CodeReason.DeleteMatch:
                            var currentindex = listofviewmatches.FindIndex(a => a == carouseluwu.CurrentItem);
                            listofmatches.RemoveAt(currentindex);
                            DependencyService.Get<DataStore>().SaveDefaultData("JacksonEvent2020.txt", listofmatches);
                            await strategyCodePanel.TranslateTo(strategyCodePanel.TranslationX - 600, strategyCodePanel.TranslationY, 500, Easing.CubicInOut);
                            strategyCodeInterface.IsVisible = false;
                            strategyCodePanel.TranslationX = 0;
                            await Task.Delay(2000);
                            ReloadMatches();
                            break;
                        case CodeReason.EditMatch:
                            var currentindexedit = listofviewmatches.FindIndex(a => a == carouseluwu.CurrentItem);
                            var currentmatch = listofmatches.ToArray()[currentindexedit];
                            coreEditMatchNumberLabel.Text = "#" + currentmatch.MatchNumber.ToString();
                            coreEditMatchNumberStepper.Value = currentmatch.MatchNumber;
                            if (currentmatch.TeamName != null)
                            {
                                coreEditTeamName.Text = currentmatch.TeamName;
                            }
                            coreEditTeamNumber.Text = currentmatch.TeamNumber.ToString();
                            editCoreInfoPanel.TranslationX = 600;
                            await Task.Delay(50);
                            await strategyCodePanel.TranslateTo(strategyCodePanel.TranslationX - 600, strategyCodePanel.TranslationY, 500, Easing.CubicInOut);
                            strategyCodeInterface.IsVisible = false;
                            strategyCodePanel.TranslationX = 0;
                            editCoreInfoInterface.IsVisible = true;
                            editCoreInfoPanel.TranslateTo(editCoreInfoPanel.TranslationX - 600, editCoreInfoPanel.TranslationY, 500, Easing.CubicInOut);
                            await Task.Delay(2000);
                            ReloadMatches();
                            break;
                        case CodeReason.CreateMatch:
                            createNewMatchNumberStepper.Value = 1;
                            createNewMatchNumberLabel.Text = "#1";
                            createMatchInfoPanel.TranslationX = 600;
                            await Task.Delay(50);
                            await strategyCodePanel.TranslateTo(strategyCodePanel.TranslationX - 600, strategyCodePanel.TranslationY, 500, Easing.CubicInOut);
                            strategyCodeInterface.IsVisible = false;
                            strategyCodePanel.TranslationX = 0;
                            createNewMatchInterface.IsVisible = true;
                            createMatchInfoPanel.TranslateTo(createMatchInfoPanel.TranslationX - 600, createMatchInfoPanel.TranslationY, 500, Easing.CubicInOut);

                            break;
                        case CodeReason.EditConfig:
                            editConfigFilePanel.TranslationX = 600;
                            await Task.Delay(50);
                            await strategyCodePanel.TranslateTo(strategyCodePanel.TranslationX - 600, strategyCodePanel.TranslationY, 500, Easing.CubicInOut);
                            strategyCodeInterface.IsVisible = false;
                            strategyCodePanel.TranslationX = 0;
                            editConfigFileInterface.IsVisible = true;
                            editConfigFilePanel.TranslateTo(editConfigFilePanel.TranslationX - 600, editConfigFilePanel.TranslationY, 500, Easing.CubicInOut);
                            break;
                    }

                }
                else
                {
                    currentCodeString = "";
                    codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Red");
                    codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Red");
                    codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Red");
                    codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.Red");
                    codeButton0.IsEnabled = false;
                    codeButton1.IsEnabled = false;
                    codeButton2.IsEnabled = false;
                    codeButton3.IsEnabled = false;
                    codeButton4.IsEnabled = false;
                    codeButton5.IsEnabled = false;
                    codeButton6.IsEnabled = false;
                    codeButton7.IsEnabled = false;
                    codeButton8.IsEnabled = false;
                    codeButton9.IsEnabled = false;
                    try
                    {
                        Vibration.Vibrate();
                    }
                    catch (Exception ex)
                    {

                    }

                    await codeProgressContainer.TranslateTo(codeProgressContainer.TranslationX - 10, codeProgressContainer.TranslationY, 75, Easing.SinIn);
                    await codeProgressContainer.TranslateTo(codeProgressContainer.TranslationX + 20, codeProgressContainer.TranslationY, 75, Easing.SinIn);
                    await codeProgressContainer.TranslateTo(codeProgressContainer.TranslationX - 20, codeProgressContainer.TranslationY, 75, Easing.SinIn);
                    await codeProgressContainer.TranslateTo(codeProgressContainer.TranslationX + 20, codeProgressContainer.TranslationY, 75, Easing.SinIn);
                    await codeProgressContainer.TranslateTo(codeProgressContainer.TranslationX - 10, codeProgressContainer.TranslationY, 75, Easing.SinIn);
                    codeButton0.IsEnabled = true;
                    codeButton1.IsEnabled = true;
                    codeButton2.IsEnabled = true;
                    codeButton3.IsEnabled = true;
                    codeButton4.IsEnabled = true;
                    codeButton5.IsEnabled = true;
                    codeButton6.IsEnabled = true;
                    codeButton7.IsEnabled = true;
                    codeButton8.IsEnabled = true;
                    codeButton9.IsEnabled = true;
                    codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                    codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
                }
            }
        }

        private async void SeeSettingsPage(object sender, EventArgs e)
        {
            if (!MenuAnimationActive)
            {
                mainInterface.TranslateTo(mainInterface.TranslationX - 600, mainInterface.TranslationY, 500, Easing.SinIn);
                if (MenuOpen)
                {
                    showUsb.TranslateTo((showMenu.X - showUsb.X), showUsb.TranslationY, 350, Easing.CubicIn);
                    showAbout.TranslateTo((showMenu.X - showAbout.X), showAbout.TranslationY, 350, Easing.CubicIn);
                    showSettings.TranslateTo((showMenu.X - showSettings.X), showSettings.TranslationY, 350, Easing.CubicIn);
                    showMenu.Focus();
                    pureblueOverButton.IsVisible = true;
                    pureblueOverButton.FadeTo(1, 200);
                    await Task.Delay(200);
                    showMenu.RotateTo(0, easing: Easing.CubicInOut);
                    cancelToggleMenu.RotateTo(0, easing: Easing.CubicInOut);
                    await Task.Delay(50);
                    cancelToggleMenu.FadeTo(0, 25, Easing.Linear);
                    await Task.Delay(200);
                    showUsb.IsVisible = false;
                    showAbout.IsVisible = false;
                    showSettings.IsVisible = false;

                    MenuOpen = false;
                }
                MenuAnimationActive = true;
                await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY - 10, 75, Easing.CubicOut);
                await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY + 110, 250, Easing.CubicOut);
                MenuAnimationActive = false;
                settingsButton.IsVisible = false;
                settingsButton.TranslationY = 0;
                await Task.Delay(100);
                settingsInterface.TranslationY = 1200;
                await Task.Delay(100);
                settingsInterface.IsVisible = true;
                settingsInterface.TranslateTo(settingsInterface.TranslationX, settingsInterface.TranslationY - 1200, 500, Easing.SinOut);
            }

        }
        private async void ToggleMenuItems(object sender, EventArgs e)
        {
            if (!MenuAnimationActive)
            {
                if (!MenuOpen)
                {
                    MenuAnimationActive = true;
                    showMenu.RotateTo(90, easing: Easing.CubicInOut);
                    cancelToggleMenu.RotateTo(90, easing: Easing.CubicInOut);
                    await Task.Delay(50);
                    cancelToggleMenu.FadeTo(1, 25, Easing.Linear);
                    await Task.Delay(100);
                    showUsb.TranslationX = 0;
                    showSettings.TranslationX = 0;
                    showUsb.TranslationX = (showMenu.X - showUsb.X);
                    showUsb.IsVisible = true;
                    showUsb.TranslateTo(showUsb.TranslationX - (showMenu.X - showUsb.X), showUsb.TranslationY, 600, Easing.CubicOut);
                    showSettings.TranslationX = (showMenu.X - showSettings.X);
                    showSettings.IsVisible = true;
                    showSettings.TranslateTo(showSettings.TranslationX - (showMenu.X - showSettings.X), showSettings.TranslationY, 600, Easing.CubicOut);
                    showAbout.TranslationX = (showMenu.X - showAbout.X);
                    showAbout.IsVisible = true;
                    await showAbout.TranslateTo(showAbout.TranslationX - (showMenu.X - showAbout.X), showAbout.TranslationY, 600, Easing.CubicOut);
                    MenuOpen = true;
                    MenuAnimationActive = false;
                }
                else
                {
                    MenuAnimationActive = true;
                    showUsb.TranslateTo((showMenu.X - showUsb.X), showUsb.TranslationY, 350, Easing.CubicIn);
                    showSettings.TranslateTo((showMenu.X - showSettings.X), showSettings.TranslationY, 350, Easing.CubicIn);
                    showAbout.TranslateTo((showMenu.X - showAbout.X), showAbout.TranslationY, 350, Easing.CubicIn);
                    await Task.Delay(200);
                    showMenu.RotateTo(0, easing: Easing.CubicInOut);
                    cancelToggleMenu.RotateTo(0, easing: Easing.CubicInOut);
                    await Task.Delay(50);
                    cancelToggleMenu.FadeTo(0, 25, Easing.Linear);
                    await Task.Delay(200);
                    showUsb.IsVisible = false;
                    showSettings.IsVisible = false;
                    showAbout.IsVisible = false;

                    MenuOpen = false;
                    MenuAnimationActive = false;
                }
            }




        }
        private async void CancelSettingsPage(object sender, EventArgs e)
        {
            settingsInterface.TranslateTo(settingsInterface.TranslationX, settingsInterface.TranslationY + 1200, 500, Easing.SinIn);
            await Task.Delay(350);
            settingsInterface.IsVisible = false;
            settingsInterface.TranslationY = 0;
            pureblueOverButton.IsVisible = false;
            pureblueOverButton.Opacity = 0;
            mainInterface.TranslationX = -600;
            mainInterface.TranslateTo(mainInterface.TranslationX + 600, mainInterface.TranslationY, 500, Easing.SinOut);
            settingsButton.TranslationY = 100;
            settingsButton.IsVisible = true;
            MenuAnimationActive = true;
            await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY - 110, 400, Easing.CubicIn);
            await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY + 10, 100, Easing.CubicIn);
            MenuAnimationActive = false;


        }
        private async void SeeUsbPage(object sender, EventArgs e)
        {
            if (!MenuAnimationActive)
            {
                mainInterface.TranslateTo(mainInterface.TranslationX - 600, mainInterface.TranslationY, 500, Easing.SinIn);
                if (MenuOpen)
                {
                    showUsb.TranslateTo((showMenu.X - showUsb.X), showUsb.TranslationY, 350, Easing.CubicIn);
                    showAbout.TranslateTo((showMenu.X - showAbout.X), showAbout.TranslationY, 350, Easing.CubicIn);
                    showSettings.TranslateTo((showMenu.X - showSettings.X), showSettings.TranslationY, 350, Easing.CubicIn);
                    showMenu.Focus();
                    pureblueOverButton.IsVisible = true;
                    pureblueOverButton.FadeTo(1, 200);
                    await Task.Delay(200);
                    showMenu.RotateTo(0, easing: Easing.CubicInOut);
                    cancelToggleMenu.RotateTo(0, easing: Easing.CubicInOut);
                    await Task.Delay(50);
                    cancelToggleMenu.FadeTo(0, 25, Easing.Linear);
                    await Task.Delay(200);
                    showUsb.IsVisible = false;
                    showAbout.IsVisible = false;
                    showSettings.IsVisible = false;

                    MenuOpen = false;
                }
                MenuAnimationActive = true;
                await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY - 10, 75, Easing.CubicOut);
                await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY + 110, 250, Easing.CubicOut);
                MenuAnimationActive = false;
                settingsButton.IsVisible = false;
                settingsButton.TranslationY = 0;
                await Task.Delay(100);
                usbInterface.TranslationY = 1200;
                await Task.Delay(100);
                usbInterface.IsVisible = true;
                usbInterface.TranslateTo(usbInterface.TranslationX, usbInterface.TranslationY - 1200, 500, Easing.SinOut);
            }

        }
        private async void CancelUsbPage(object sender, EventArgs e)
        {
            usbInterface.TranslateTo(usbInterface.TranslationX, usbInterface.TranslationY + 1200, 500, Easing.SinIn);
            await Task.Delay(350);
            usbInterface.IsVisible = false;
            usbInterface.TranslationY = 0;
            pureblueOverButton.IsVisible = false;
            pureblueOverButton.Opacity = 0;
            mainInterface.TranslationX = -600;
            mainInterface.TranslateTo(mainInterface.TranslationX + 600, mainInterface.TranslationY, 500, Easing.SinOut);
            settingsButton.TranslationY = 100;
            settingsButton.IsVisible = true;
            MenuAnimationActive = true;
            await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY - 110, 400, Easing.CubicIn);
            await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY + 10, 100, Easing.CubicIn);
            MenuAnimationActive = false;

        }
        private async void SeeAboutPage(object sender, EventArgs e)
        {
            if (!MenuAnimationActive)
            {
                mainInterface.TranslateTo(mainInterface.TranslationX - 600, mainInterface.TranslationY, 500, Easing.SinIn);
                if (MenuOpen)
                {
                    showUsb.TranslateTo((showMenu.X - showUsb.X), showUsb.TranslationY, 350, Easing.CubicIn);
                    showAbout.TranslateTo((showMenu.X - showAbout.X), showAbout.TranslationY, 350, Easing.CubicIn);
                    showSettings.TranslateTo((showMenu.X - showSettings.X), showSettings.TranslationY, 350, Easing.CubicIn);
                    showMenu.Focus();
                    pureblueOverButton.IsVisible = true;
                    pureblueOverButton.FadeTo(1, 200);
                    await Task.Delay(200);
                    showMenu.RotateTo(0, easing: Easing.CubicInOut);
                    cancelToggleMenu.RotateTo(0, easing: Easing.CubicInOut);
                    await Task.Delay(50);
                    cancelToggleMenu.FadeTo(0, 25, Easing.Linear);
                    await Task.Delay(200);
                    showUsb.IsVisible = false;
                    showAbout.IsVisible = false;
                    showSettings.IsVisible = false;

                    MenuOpen = false;
                }
                MenuAnimationActive = true;
                await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY - 10, 75, Easing.CubicOut);
                await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY + 110, 250, Easing.CubicOut);
                MenuAnimationActive = false;
                settingsButton.IsVisible = false;
                settingsButton.TranslationY = 0;
                await Task.Delay(100);
                aboutInterface.TranslationY = 1200;
                await Task.Delay(100);
                aboutInterface.IsVisible = true;
                aboutInterface.TranslateTo(aboutInterface.TranslationX, aboutInterface.TranslationY - 1200, 500, Easing.SinOut);
            }

        }
        private async void CancelAboutPage(object sender, EventArgs e)
        {
            aboutInterface.TranslateTo(aboutInterface.TranslationX, aboutInterface.TranslationY + 1200, 500, Easing.SinIn);
            await Task.Delay(350);
            aboutInterface.IsVisible = false;
            aboutInterface.TranslationY = 0;
            pureblueOverButton.IsVisible = false;
            pureblueOverButton.Opacity = 0;
            mainInterface.TranslationX = -600;
            mainInterface.TranslateTo(mainInterface.TranslationX + 600, mainInterface.TranslationY, 500, Easing.SinOut);
            settingsButton.TranslationY = 100;
            settingsButton.IsVisible = true;
            MenuAnimationActive = true;
            await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY - 110, 400, Easing.CubicIn);
            await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY + 10, 100, Easing.CubicIn);
            MenuAnimationActive = false;

        }
        private async void CancelCodePanel(object sender, EventArgs e)
        {
            await strategyCodePanel.TranslateTo(strategyCodePanel.TranslationX + 600, strategyCodePanel.TranslationY, 500, Easing.CubicInOut);
            strategyCodeInterface.IsVisible = false;
            strategyCodePanel.TranslationX = 0;


            currentCodeString = "";
        }
        private async void CancelCoreInfoEdit(object sender, EventArgs e)
        {
            DependencyService.Get<IKeyboardHelper>().HideKeyboard();
            await editCoreInfoPanel.TranslateTo(editCoreInfoPanel.TranslationX + 600, editCoreInfoPanel.TranslationY, 500, Easing.CubicInOut);
            editCoreInfoInterface.IsVisible = false;
            editCoreInfoPanel.TranslationX = 0;

        }
        private async void SaveCoreInfoEdit(object sender, EventArgs e)
        {
            var currentindex = listofviewmatches.FindIndex(a => a == carouseluwu.CurrentItem);
            var currentmatch = listofmatches.ToArray()[currentindex];
            currentmatch.TeamName = coreEditTeamName.Text;
            currentmatch.TeamNumber = int.Parse(coreEditTeamNumber.Text);
            currentmatch.MatchNumber = (int)coreEditMatchNumberStepper.Value;
            listofmatches.ToArray()[currentindex] = currentmatch;
            DependencyService.Get<DataStore>().SaveDefaultData("JacksonEvent2020.txt", listofmatches);
            DependencyService.Get<IKeyboardHelper>().HideKeyboard();
            await editCoreInfoPanel.TranslateTo(editCoreInfoPanel.TranslationX - 600, editCoreInfoPanel.TranslationY, 500, Easing.CubicInOut);
            editCoreInfoInterface.IsVisible = false;
            editCoreInfoPanel.TranslationX = 0;
            await Task.Delay(2000);
            ReloadMatches();
        }
        private async void CancelCreateMatch(object sender, EventArgs e)
        {
            DependencyService.Get<IKeyboardHelper>().HideKeyboard();
            await createMatchInfoPanel.TranslateTo(createMatchInfoPanel.TranslationX + 600, createMatchInfoPanel.TranslationY, 500, Easing.CubicInOut);
            createNewMatchInterface.IsVisible = false;
            createMatchInfoPanel.TranslationX = 0;

        }
        private async void SaveCreateMatch(object sender, EventArgs e)
        {


            var currentmatch = new TeamMatch() { PowerCellInner = new int[20], PowerCellOuter = new int[20], PowerCellLower = new int[20], PowerCellMissed = new int[20], TabletId = JsonConvert.DeserializeObject<LSConfiguration>(DependencyService.Get<DataStore>().LoadConfigFile()).TabletIdentifier };
            currentmatch.TeamName = createNewTeamName.Text;
            currentmatch.TeamNumber = int.Parse(createNewTeamNumber.Text);
            currentmatch.MatchNumber = (int)createNewMatchNumberStepper.Value;
            listofmatches.Add(currentmatch);
            DependencyService.Get<DataStore>().SaveDefaultData("JacksonEvent2020.txt", listofmatches);
            DependencyService.Get<IKeyboardHelper>().HideKeyboard();
            await createMatchInfoPanel.TranslateTo(createMatchInfoPanel.TranslationX - 600, createMatchInfoPanel.TranslationY, 500, Easing.CubicInOut);
            createNewMatchInterface.IsVisible = false;
            createMatchInfoPanel.TranslationX = 0;
            await Task.Delay(2000);
            ReloadMatches();
        }
        private async void EditConfigInfo(object sender, EventArgs e)
        {
            var converter = new ColorTypeConverter();
            codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            strategyCodePanel.TranslationX = 600;
            await Task.Delay(50);
            strategyCodeInterface.IsVisible = true;
            strategyCodePanel.TranslateTo(strategyCodePanel.TranslationX - 600, strategyCodePanel.TranslationY, 500, Easing.CubicInOut);
            currentCodeString = "";
            currentCodeReason = CodeReason.EditConfig;
        }
        private async void CloseConfigInfoEdit(object sender, EventArgs e)
        {
            DependencyService.Get<IKeyboardHelper>().HideKeyboard();
            await editConfigFilePanel.TranslateTo(editConfigFilePanel.TranslationX + 600, editConfigFilePanel.TranslationY, 500, Easing.CubicInOut);
            editConfigFileInterface.IsVisible = false;
            editConfigFilePanel.TranslationX = 0;

        }
        private void coreEditMatchNumberStepper_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            coreEditMatchNumberLabel.Text = "#" + e.NewValue.ToString();
        }
        private void createMatchNumberStepper_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            createNewMatchNumberLabel.Text = "#" + e.NewValue.ToString();
        }
        private async void CreateNewMatch(object sender, EventArgs e)
        {
            var converter = new ColorTypeConverter();
            codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            strategyCodePanel.TranslationX = 600;
            await Task.Delay(50);
            strategyCodeInterface.IsVisible = true;
            strategyCodePanel.TranslateTo(strategyCodePanel.TranslationX - 600, strategyCodePanel.TranslationY, 500, Easing.CubicInOut);
            currentCodeString = "";
            currentCodeReason = CodeReason.CreateMatch;
        }
        private async void DeleteEntry(object sender, EventArgs e)
        {
            var converter = new ColorTypeConverter();
            codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            strategyCodePanel.TranslationX = 600;
            await Task.Delay(50);
            strategyCodeInterface.IsVisible = true;
            strategyCodePanel.TranslateTo(strategyCodePanel.TranslationX - 600, strategyCodePanel.TranslationY, 500, Easing.CubicInOut);
            currentCodeString = "";
            currentCodeReason = CodeReason.DeleteMatch;
        }
        private async void EditEntry(object sender, EventArgs e)
        {
            var converter = new ColorTypeConverter();
            codeProgress1.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            codeProgress2.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            codeProgress3.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            codeProgress4.BackgroundColor = (Color)converter.ConvertFromInvariantString("Color.LightGray");
            strategyCodePanel.TranslationX = 600;
            await Task.Delay(50);
            strategyCodeInterface.IsVisible = true;
            strategyCodePanel.TranslateTo(strategyCodePanel.TranslationX - 600, strategyCodePanel.TranslationY, 500, Easing.CubicInOut);
            currentCodeString = "";
            currentCodeReason = CodeReason.EditMatch;
        }
        private async void SelectGenComm(object sender, EventArgs e)
        {
            var converter = new ColorTypeConverter();
            Button selected = (Button)sender as Button;
            if(selected == usbGeneral)
            {
                usbSelected.FadeTo(1, 100, Easing.CubicInOut);
                bluetoothSelected.FadeTo(0, 100, Easing.CubicInOut);
                commOptions.FadeTo(1, 100, Easing.CubicInOut);
            }
            else
            {
                usbSelected.FadeTo(0, 100, Easing.CubicInOut);
                bluetoothSelected.FadeTo(1, 100, Easing.CubicInOut);
                commOptions.FadeTo(1, 100, Easing.CubicInOut);
            }
        }
        private async void SelectCommOption(object sender, EventArgs e)
        {
            var converter = new ColorTypeConverter();
            Button selected = (Button)sender as Button;
            if (selected == comm_checkTBA)
            {
                if(currentlySelectedComm != selected)
                {
                    if (currentlySelectedComm != null)
                    {
                        var checkmarkbutton = this.FindByName<Label>(currentlySelectedComm.ClassId + "_check");
                        currentlySelectedComm.TranslateTo(currentlySelectedComm.TranslationX - 40, currentlySelectedComm.TranslationY, easing: Easing.CubicInOut);
                        comm_checkTBA_check.Opacity = 1;
                        await comm_checkTBA.TranslateTo(comm_checkTBA.TranslationX + 40, comm_checkTBA.TranslationY, easing: Easing.CubicInOut);
                        currentlySelectedComm.TranslationX = 0;
                        checkmarkbutton.Opacity = 0;
                        checkmarkbutton.TranslationX = 0;
                        comm_checkTBA.TranslationX = 40;
                    }
                    else
                    {
                        comm_checkTBA_check.Opacity = 1;
                        await comm_checkTBA.TranslateTo(comm_checkTBA.TranslationX + 40, comm_checkTBA.TranslationY, easing: Easing.CubicInOut);
                        comm_checkTBA.TranslationX = 40;
                    }
                    currentlySelectedComm = comm_checkTBA;
                }
                else
                {
                    await currentlySelectedComm.TranslateTo(currentlySelectedComm.TranslationX +10, currentlySelectedComm.TranslationY,125, easing: Easing.CubicInOut);
                    await currentlySelectedComm.TranslateTo(currentlySelectedComm.TranslationX -10, currentlySelectedComm.TranslationY,125, easing: Easing.CubicInOut);
                    comm_checkTBA.TranslationX = 40;
                }
                
                

            }
            else if (selected == comm_getMatchData)
            {
                if (currentlySelectedComm != selected)
                {
                    if (currentlySelectedComm != null)
                    {
                        var checkmarkbutton = this.FindByName<Label>(currentlySelectedComm.ClassId + "_check");
                        currentlySelectedComm.TranslateTo(currentlySelectedComm.TranslationX - 40, currentlySelectedComm.TranslationY, easing: Easing.CubicInOut);
                        comm_getMatchData_check.Opacity = 1;
                        await comm_getMatchData.TranslateTo(comm_getMatchData.TranslationX + 40, comm_getMatchData.TranslationY, easing: Easing.CubicInOut);
                        currentlySelectedComm.TranslationX = 0;
                        checkmarkbutton.Opacity = 0;
                        checkmarkbutton.TranslationX = 0;
                        comm_getMatchData.TranslationX = 40;
                    }
                    else
                    {
                        comm_getMatchData_check.Opacity = 1;
                        await comm_getMatchData.TranslateTo(comm_getMatchData.TranslationX + 40, comm_getMatchData.TranslationY, easing: Easing.CubicInOut);
                        comm_getMatchData.TranslationX = 40;
                    }
                    currentlySelectedComm = comm_getMatchData;
                }
                else
                {
                    await currentlySelectedComm.TranslateTo(currentlySelectedComm.TranslationX + 10, currentlySelectedComm.TranslationY, 125, easing: Easing.CubicInOut);
                    await currentlySelectedComm.TranslateTo(currentlySelectedComm.TranslationX - 10, currentlySelectedComm.TranslationY, 125, easing: Easing.CubicInOut);
                    comm_getMatchData.TranslationX = 40;
                }
                
            }
            else if(selected == comm_preloadTBA)
            {
                if (currentlySelectedComm != selected)
                {
                    if (currentlySelectedComm != null)
                    {
                        var checkmarkbutton = this.FindByName<Label>(currentlySelectedComm.ClassId + "_check");
                        currentlySelectedComm.TranslateTo(currentlySelectedComm.TranslationX - 40, currentlySelectedComm.TranslationY, easing: Easing.CubicInOut);
                        comm_preloadTBA_check.Opacity = 1;
                        await comm_preloadTBA.TranslateTo(comm_preloadTBA.TranslationX + 40, comm_preloadTBA.TranslationY, easing: Easing.CubicInOut);
                        currentlySelectedComm.TranslationX = 0;
                        comm_preloadTBA.TranslationX = 40;
                        checkmarkbutton.Opacity = 0;
                        checkmarkbutton.TranslationX = 0;
                    }
                    else
                    {
                        comm_preloadTBA_check.Opacity = 1;
                        await comm_preloadTBA.TranslateTo(comm_preloadTBA.TranslationX + 40, comm_preloadTBA.TranslationY, easing: Easing.CubicInOut);
                        comm_preloadTBA.TranslationX = 40;
                    }
                    currentlySelectedComm = comm_preloadTBA;
                }
                else
                {
                    await currentlySelectedComm.TranslateTo(currentlySelectedComm.TranslationX + 10, currentlySelectedComm.TranslationY, 125, easing: Easing.CubicInOut);
                    await currentlySelectedComm.TranslateTo(currentlySelectedComm.TranslationX - 10, currentlySelectedComm.TranslationY, 125, easing: Easing.CubicInOut);
                    comm_preloadTBA.TranslationX = 40;
                }
                
            }
            else if(selected == comm_sendCurrent)
            {
                if (currentlySelectedComm != selected)
                {
                    if (currentlySelectedComm != null)
                    {
                        var checkmarkbutton = this.FindByName<Label>(currentlySelectedComm.ClassId + "_check");
                        currentlySelectedComm.TranslateTo(currentlySelectedComm.TranslationX - 40, currentlySelectedComm.TranslationY, easing: Easing.CubicInOut);
                        comm_sendCurrent_check.Opacity = 1;
                        await comm_sendCurrent.TranslateTo(comm_sendCurrent.TranslationX + 40, comm_sendCurrent.TranslationY, easing: Easing.CubicInOut);
                        currentlySelectedComm.TranslationX = 0;
                        checkmarkbutton.Opacity = 0;
                        checkmarkbutton.TranslationX = 0;
                        comm_sendCurrent.TranslationX = 40;
                    }
                    else
                    {
                        comm_sendCurrent.Opacity = 1;
                        await comm_sendCurrent.TranslateTo(comm_sendCurrent.TranslationX + 40, comm_sendCurrent.TranslationY, easing: Easing.CubicInOut);
                        comm_sendCurrent.TranslationX = 40;
                    }
                    currentlySelectedComm = comm_sendCurrent;
                }
                else
                {
                    await currentlySelectedComm.TranslateTo(currentlySelectedComm.TranslationX + 10, currentlySelectedComm.TranslationY, 125, easing: Easing.CubicInOut);
                    await currentlySelectedComm.TranslateTo(currentlySelectedComm.TranslationX - 10, currentlySelectedComm.TranslationY, 125, easing: Easing.CubicInOut);
                    comm_sendCurrent.TranslationX = 40;
                }
                
            }
        }
        private void ReloadMatches()
        {
            try
            {
                var converter = new ColorTypeConverter();
                listofviewmatches = new List<TeamMatchViewItem>();
                var allmatchesraw = DependencyService.Get<DataStore>().LoadData("JacksonEvent2020.txt");
                listofmatches = JsonConvert.DeserializeObject<List<TeamMatch>>(allmatchesraw);
                var upnext = false;
                TeamMatchViewItem selectedItem = null;
                var upnextselected = false;
                int i2 = 0;
                foreach (var match in listofmatches)
                {

                    var newmatchviewitem = new TeamMatchViewItem();
                    upnext = false;
                    if (!match.ClientSubmitted)
                    {
                        if (!upnextselected)
                        {
                            upnext = true;
                            upnextselected = true;
                        }
                    }
                    newmatchviewitem.Completed = match.ClientSubmitted;
                    if (match.TabletId != null)
                    {
                        newmatchviewitem.IsRed = match.TabletId.StartsWith("R");
                        newmatchviewitem.IsBlue = match.TabletId.StartsWith("B");
                    }

                    newmatchviewitem.IsUpNext = upnext;
                    newmatchviewitem.TeamName = match.TeamName;
                    if (match.TeamName == null)
                    {
                        newmatchviewitem.TeamName = "FRC Team " + match.TeamNumber.ToString();
                    }
                    newmatchviewitem.TeamNumber = match.TeamNumber;
                    newmatchviewitem.NewPlaceholder = false;
                    newmatchviewitem.ActualMatch = true;
                    newmatchviewitem.MatchNumber = match.MatchNumber;
                    newmatchviewitem.TabletName = match.TabletId;
                    newmatchviewitem.AlliancePartner1 = match.AlliancePartners[0];
                    newmatchviewitem.AlliancePartner2 = match.AlliancePartners[1];
                    if (newmatchviewitem.IsBlue)
                    {
                        newmatchviewitem.TeamColor = (Color)converter.ConvertFromInvariantString("#3475da");
                    }
                    else
                    {
                        newmatchviewitem.TeamColor = (Color)converter.ConvertFromInvariantString("#e53434");
                    }

                    //newmatchviewitem.teamIcon = ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String("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")));
                    newmatchviewitem.teamIcon = ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String("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")));
                    if (upnext)
                    {
                        selectedItem = newmatchviewitem;
                        NextMatchIndex = i2;
                    }
                    listofviewmatches.Add(newmatchviewitem);
                    i2++;
                }
                if (selectedItem != null)
                {
                    carouseluwu.CurrentItem = selectedItem;
                }
                else
                {
                    carouseluwu.CurrentItem = listofviewmatches.First();
                }
            }
            catch (Exception ex)
            {

            }
            var placeholdermatch = new TeamMatchViewItem();
            placeholdermatch.ActualMatch = false;
            placeholdermatch.NewPlaceholder = true;
            listofviewmatches.Add(placeholdermatch);
            //listOfMatches.ItemsSource = listofviewmatches;
            carouseluwu.ItemsSource = listofviewmatches;
            MatchProgressList.Progress = (float)((float)1 / (float)(listofviewmatches.Count - 1));
        }

        private void OverlayEntryUnfocused(object sender, FocusEventArgs e)
        {
            DependencyService.Get<IKeyboardHelper>().HideKeyboard();
        }

        private void ProtocolV3(object sender, EventArgs e)
        {
            var argumentsToUse = new SubmitVIABluetooth.BLEMessageArguments() { messageType = 20, messageData = "I WANT IT NOWWWWW", expectation = SubmitVIABluetooth.ResponseExpectation.Expected };

            MessagingCenter.Subscribe<SubmitVIABluetooth, BluetoothControllerData>(this, "bluetoothController", async (mssender, value) =>
            {
                switch (value.status)
                {
                    case BluetoothControllerDataStatus.Initialize:
                        await DismissNotification();
                        await NewNotification("Bluetooth Handshake Started; Method Called;");
                        break;
                    case BluetoothControllerDataStatus.Connected:
                        await DismissNotification();
                        await NewNotification("Bluetooth Handshake Successful; Sending Data");
                        break;
                    case BluetoothControllerDataStatus.DataSent:
                        await DismissNotification();
                        await NewNotification("Bluetooth Data Sent Successfully; Waiting for Data");
                        if(argumentsToUse.expectation == SubmitVIABluetooth.ResponseExpectation.NoResponse)
                        {
                            MessagingCenter.Unsubscribe<SubmitVIABluetooth, int>(this, "bluetoothController");
                        }
                        break;
                    case BluetoothControllerDataStatus.DataGet:
                        await DismissNotification();
                        await NewNotification("Bluetooth Data Got Successfully!");
                        var listofmatches = JsonConvert.DeserializeObject<List<TeamMatch>>(value.data);
                        DependencyService.Get<DataStore>().SaveDefaultData("JacksonEvent2020.txt", listofmatches);
                        MessagingCenter.Unsubscribe<SubmitVIABluetooth, int>(this, "bluetoothController");
                        break;
                    case BluetoothControllerDataStatus.Abort:
                        await DismissNotification();
                        await NewNotification("Bluetooth Communication Failure");
                        MessagingCenter.Unsubscribe<SubmitVIABluetooth, int>(this, "bluetoothController");
                        break;
                }
            });


            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = cancellationTokenSource.Token;
            submitVIABluetoothInstance.ConnectToDevice(argumentsToUse, token);
        }
        private async void MenuPanSettings(object sender, PanUpdatedEventArgs e)
        {
            if (Device.RuntimePlatform == Device.iOS)
            {
                if (e.StatusType == GestureStatus.Canceled || e.StatusType == GestureStatus.Completed)
                {
                    if (settingsInterface.TranslationY > 250)
                    {
                        settingsInterface.TranslateTo(settingsInterface.TranslationX, settingsInterface.TranslationY + 1200, easing: Easing.SinIn);
                        
                        await Task.Delay(350);
                        settingsInterface.IsVisible = false;
                        settingsInterface.TranslationY = 0;
                        pureblueOverButton.IsVisible = false;
                        pureblueOverButton.Opacity = 0;
                        mainInterface.TranslationX = -600;
                        mainInterface.TranslateTo(mainInterface.TranslationX + 600, mainInterface.TranslationY, 500, Easing.SinOut);
                        settingsButton.TranslationY = 100;
                        settingsButton.IsVisible = true;
                        MenuAnimationActive = true;
                        await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY - 110, 400, Easing.CubicIn);
                        await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY + 10, 100, Easing.CubicIn);
                        MenuAnimationActive = false;
                    }
                    else
                    {
                        settingsInterface.TranslateTo(0, 0, easing: Easing.CubicInOut);
                        settingsInterface.FadeTo(1, 250, easing: Easing.CubicInOut);
                    }
                }
                else
                {
                    if (e.TotalY < 0)
                    {
                        settingsInterface.TranslateTo(0, 0, easing: Easing.CubicInOut);
                        settingsInterface.Opacity = 1;
                    }
                    else
                    {
                        settingsInterface.TranslationY = e.TotalY;
                        
                    }

                }
            }
            else
            {
                if (e.StatusType == GestureStatus.Canceled || e.StatusType == GestureStatus.Completed)
                {
                    if (e.TotalY + settingsInterface.TranslationY > 250)
                    {
                        settingsInterface.TranslateTo(settingsInterface.TranslationX, settingsInterface.TranslationY + 1200, easing: Easing.SinIn);
                        
                        await Task.Delay(350);
                        settingsInterface.IsVisible = false;
                        settingsInterface.TranslationY = 0;
                        pureblueOverButton.IsVisible = false;
                        pureblueOverButton.Opacity = 0;
                        mainInterface.TranslationX = -600;
                        mainInterface.TranslateTo(mainInterface.TranslationX + 600, mainInterface.TranslationY, 500, Easing.SinOut);
                        settingsButton.TranslationY = 100;
                        settingsButton.IsVisible = true;
                        MenuAnimationActive = true;
                        await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY - 110, 400, Easing.CubicIn);
                        await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY + 10, 100, Easing.CubicIn);
                        MenuAnimationActive = false;
                    }
                    else
                    {
                        settingsInterface.TranslateTo(0, 0, easing: Easing.CubicInOut);
                        settingsInterface.FadeTo(1, 250, easing: Easing.CubicInOut);
                    }
                }
                else
                {
                    if (e.TotalY < 0)
                    {
                        settingsInterface.TranslateTo(0, 0, easing: Easing.CubicInOut);
                        settingsInterface.Opacity = 1;
                    }
                    else
                    {
                        settingsInterface.TranslationY += e.TotalY;
                        
                    }

                }

            }
        }
        private async void MenuPanUSB(object sender, PanUpdatedEventArgs e)
        {
            if (Device.RuntimePlatform == Device.iOS)
            {
                if (e.StatusType == GestureStatus.Canceled || e.StatusType == GestureStatus.Completed)
                {
                    if (usbInterface.TranslationY > 250)
                    {
                        usbInterface.TranslateTo(usbInterface.TranslationX, usbInterface.TranslationY + 1200, easing: Easing.SinIn);
                        
                        await Task.Delay(350);
                        usbInterface.IsVisible = false;
                        usbInterface.TranslationY = 0;
                        pureblueOverButton.IsVisible = false;
                        pureblueOverButton.Opacity = 0;
                        mainInterface.TranslationX = -600;
                        mainInterface.TranslateTo(mainInterface.TranslationX + 600, mainInterface.TranslationY, 500, Easing.SinOut);
                        settingsButton.TranslationY = 100;
                        settingsButton.IsVisible = true;
                        MenuAnimationActive = true;
                        await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY - 110, 400, Easing.CubicIn);
                        await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY + 10, 100, Easing.CubicIn);
                        MenuAnimationActive = false;
                    }
                    else
                    {
                        usbInterface.TranslateTo(0, 0, easing: Easing.CubicInOut);
                        usbInterface.FadeTo(1, 250, easing: Easing.CubicInOut);
                    }
                }
                else
                {
                    if(e.TotalY < 0)
                    {
                        usbInterface.TranslateTo(0, 0, easing: Easing.CubicInOut);
                        
                    }
                    else
                    {
                        usbInterface.TranslationY = e.TotalY;
                        
                    }
                    
                }
            }
            else
            {
                if (e.StatusType == GestureStatus.Canceled || e.StatusType == GestureStatus.Completed)
                {
                    if (e.TotalY + usbInterface.TranslationY > 250)
                    {
                        usbInterface.TranslateTo(usbInterface.TranslationX, usbInterface.TranslationY + 1200, easing: Easing.SinIn);
                        
                        await Task.Delay(350);
                        usbInterface.IsVisible = false;
                        usbInterface.TranslationY = 0;
                        pureblueOverButton.IsVisible = false;
                        pureblueOverButton.Opacity = 0;
                        mainInterface.TranslationX = -600;
                        mainInterface.TranslateTo(mainInterface.TranslationX + 600, mainInterface.TranslationY, 500, Easing.SinOut);
                        settingsButton.TranslationY = 100;
                        settingsButton.IsVisible = true;
                        MenuAnimationActive = true;
                        await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY - 110, 400, Easing.CubicIn);
                        await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY + 10, 100, Easing.CubicIn);
                        MenuAnimationActive = false;
                    }
                    else
                    {
                        usbInterface.TranslateTo(0, 0, easing: Easing.CubicInOut);
                        usbInterface.FadeTo(1, 250, easing: Easing.CubicInOut);
                    }
                }
                else
                {
                    if (e.TotalY < 0)
                    {
                        usbInterface.TranslateTo(0, 0, easing: Easing.CubicInOut);
                     
                    }
                    else
                    {
                        usbInterface.TranslationY += e.TotalY;
                       
                    }

                }
                
            }
        }
        private async void MenuPanAbout(object sender, PanUpdatedEventArgs e)
        {
            if (Device.RuntimePlatform == Device.iOS)
            {
                if (e.StatusType == GestureStatus.Canceled || e.StatusType == GestureStatus.Completed)
                {
                    if (aboutInterface.TranslationY > 250)
                    {
                        aboutInterface.TranslateTo(aboutInterface.TranslationX, aboutInterface.TranslationY + 1200, easing: Easing.SinIn);
                        await Task.Delay(350);
                        aboutInterface.IsVisible = false;
                        aboutInterface.TranslationY = 0;
                        pureblueOverButton.IsVisible = false;
                        pureblueOverButton.Opacity = 0;
                        mainInterface.TranslationX = -600;
                        mainInterface.TranslateTo(mainInterface.TranslationX + 600, mainInterface.TranslationY, 500, Easing.SinOut);
                        settingsButton.TranslationY = 100;
                        settingsButton.IsVisible = true;
                        MenuAnimationActive = true;
                        await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY - 110, 400, Easing.CubicIn);
                        await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY + 10, 100, Easing.CubicIn);
                        MenuAnimationActive = false;
                    }
                    else
                    {
                        aboutInterface.TranslateTo(0, 0, easing: Easing.CubicInOut);
                        aboutInterface.FadeTo(1, 250, easing: Easing.CubicInOut);
                    }
                }
                else
                {
                    if (e.TotalY < 0)
                    {
                        aboutInterface.TranslateTo(0, 0, easing: Easing.CubicInOut);
                    }
                    else
                    {
                        aboutInterface.TranslationY = e.TotalY;
                    }

                }
            }
            else
            {
                if (e.StatusType == GestureStatus.Canceled || e.StatusType == GestureStatus.Completed)
                {
                    if (e.TotalY + aboutInterface.TranslationY > 250)
                    {
                        aboutInterface.TranslateTo(aboutInterface.TranslationX, aboutInterface.TranslationY + 1200, easing: Easing.SinIn);
                        await Task.Delay(350);
                        aboutInterface.IsVisible = false;
                        aboutInterface.TranslationY = 0;
                        pureblueOverButton.IsVisible = false;
                        pureblueOverButton.Opacity = 0;
                        mainInterface.TranslationX = -600;
                        mainInterface.TranslateTo(mainInterface.TranslationX + 600, mainInterface.TranslationY, 500, Easing.SinOut);
                        settingsButton.TranslationY = 100;
                        settingsButton.IsVisible = true;
                        MenuAnimationActive = true;
                        await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY - 110, 400, Easing.CubicIn);
                        await settingsButton.TranslateTo(settingsButton.TranslationX, settingsButton.TranslationY + 10, 100, Easing.CubicIn);
                        MenuAnimationActive = false;
                    }
                    else
                    {
                        aboutInterface.TranslateTo(0, 0, easing: Easing.CubicInOut);
                        aboutInterface.FadeTo(1, 250, easing: Easing.CubicInOut);
                    }
                }
                else
                {
                    if (e.TotalY < 0)
                    {
                        aboutInterface.TranslateTo(0, 0, easing: Easing.CubicInOut);
                    }
                    else
                    {
                        aboutInterface.TranslationY += e.TotalY;
                    }

                }

            }
        }

        private void Button_Clicked_1(object sender, EventArgs e)
        {
            Navigation.PushAsync(new Database());
        }

    }
    [ContentProperty("Content")]
    public class MultiLineButton : ContentView
    {
        public event EventHandler Clicked;

        protected Grid ContentGrid;
        protected ContentView ContentContainer;
        protected Label TextContainer;

        public String Text
        {
            get
            {
                return (String)GetValue(TextProperty);
            }
            set
            {
                SetValue(TextProperty, value);
                OnPropertyChanged();
                RaiseTextChanged();
            }
        }

        public new View Content
        {
            get { return ContentContainer.Content; }
            set
            {
                if (ContentGrid.Children.Contains(value))
                    return;

                ContentContainer.Content = value;
            }
        }

        public static BindableProperty TextProperty = BindableProperty.Create(
            propertyName: "Text",
            returnType: typeof(String),
            declaringType: typeof(MultiLineButton),
            defaultValue: null,
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: TextValueChanged);

        private static void TextValueChanged(BindableObject bindable, object oldValue, object newValue)
        {
            ((MultiLineButton)bindable).TextContainer.Text = (String)newValue;
        }

        public event EventHandler TextChanged;
        private void RaiseTextChanged()
        {
            if (TextChanged != null)
                TextChanged(this, EventArgs.Empty);
        }

        public MultiLineButton()
        {
            ContentGrid = new Grid
            {
                VerticalOptions = LayoutOptions.FillAndExpand,
                HorizontalOptions = LayoutOptions.FillAndExpand
            };

            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            ContentContainer = new ContentView
            {
                VerticalOptions = LayoutOptions.FillAndExpand,
                HorizontalOptions = LayoutOptions.FillAndExpand,
            };

            TextContainer = new Label
            {
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.FillAndExpand,
            };
            ContentContainer.Content = TextContainer;

            ContentGrid.Children.Add(ContentContainer);

            var button = new Button
            {
                VerticalOptions = LayoutOptions.FillAndExpand,
                HorizontalOptions = LayoutOptions.FillAndExpand,
                BackgroundColor = Color.FromHex("#01000000")
            };

            button.Clicked += (sender, e) => OnClicked();

            ContentGrid.Children.Add(button);

            base.Content = ContentGrid;

        }

        public void OnClicked()
        {
            if (Clicked != null)
                Clicked(this, new EventArgs());
        }

    }
}
