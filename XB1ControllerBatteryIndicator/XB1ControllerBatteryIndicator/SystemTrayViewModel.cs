using System.Linq;
using System.Threading;
using SharpDX.XInput;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using System.Collections.Generic;
using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using XB1ControllerBatteryIndicator.ShellHelpers;
using MS.WindowsAPICodePack.Internal;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using Microsoft.Win32;
using System.Windows.Controls;

namespace XB1ControllerBatteryIndicator
{
    public class SystemTrayViewModel : Caliburn.Micro.Screen
    {
        private string _activeIcon;
        private Controller _controller;
        private string _tooltipText;
        private const string APP_ID = "NiyaShy.XB1ControllerBatteryIndicator";
        private bool[] toast_shown = new bool[5];
        private Dictionary<string, int> numdict = new Dictionary<string, int>();

        public SystemTrayViewModel()
        {
            ActiveIcon = "Resources/battery_disconnected.ico";
            numdict["One"] = 1;
            numdict["Two"] = 2;
            numdict["Three"] = 3;
            numdict["Four"] = 4;
            TryCreateShortcut();
            Thread th = new Thread(RefreshControllerState);
            th.IsBackground = true;
            th.Start();
        }

        public string ActiveIcon
        {
            get { return _activeIcon; }
            set
            {
                _activeIcon = value;
                NotifyOfPropertyChange();
            }
        }

        public string TooltipText
        {
            get { return _tooltipText; }
            set
            {
                _tooltipText = value;
                NotifyOfPropertyChange();
            }
        }

        private void RefreshControllerState()
        {
            while (true)
            {
                //Initialize controllers
                var controllers = new[]
                {
                    new Controller(UserIndex.One), new Controller(UserIndex.Two), new Controller(UserIndex.Three),
                    new Controller(UserIndex.Four)
                };
                //Check if at least one is present
                _controller = controllers.FirstOrDefault(selectControler => selectControler.IsConnected);

                if (_controller != null)
                {
                    //cycle through all recognized controllers
                    foreach (var CurrentController in controllers)
                    {
                        if (CurrentController.IsConnected)
                        {
                            var batteryInfo = CurrentController.GetBatteryInformation(BatteryDeviceType.Gamepad);
                            //wired
                            if (batteryInfo.BatteryType == BatteryType.Wired)
                            {
                                TooltipText = $"Controller {CurrentController.UserIndex} - {batteryInfo.BatteryType}";
                                ActiveIcon = $"Resources/battery_wired_{CurrentController.UserIndex.ToString().ToLower()}.ico";
                            }
                            //"disconnected", a controller that was detected but hasn't sent battery data yet has this state
                            else if (batteryInfo.BatteryType == BatteryType.Disconnected)
                            {
                                TooltipText = $"Controller {CurrentController.UserIndex} - Found but no data - Please press a Button";
                                ActiveIcon = $"Resources/battery_disconnected_{CurrentController.UserIndex.ToString().ToLower()}.ico";
                            }
                            //this state should never happen
                            else if (batteryInfo.BatteryType == BatteryType.Unknown)
                            {
                                TooltipText = $"Controller {CurrentController.UserIndex} - {batteryInfo.BatteryType}";
                                ActiveIcon = $"Resources/battery_disconnected_{CurrentController.UserIndex.ToString().ToLower()}.ico";
                            }
                            //a battery level was detected
                            else
                            {
                                TooltipText = $"Controller {CurrentController.UserIndex} - Battery level: {batteryInfo.BatteryLevel}";
                                ActiveIcon = $"Resources/battery_{batteryInfo.BatteryLevel.ToString().ToLower()}_{CurrentController.UserIndex.ToString().ToLower()}.ico";
                                //when "empty" state is detected...
                                if (batteryInfo.BatteryLevel == BatteryLevel.Empty)
                                {
                                    //check if toast (notification) for current controller was already triggered
                                    if (toast_shown[numdict[$"{CurrentController.UserIndex}"]] == false)
                                    {
                                        //if not, trigger it
                                        toast_shown[numdict[$"{CurrentController.UserIndex}"]] = true;
                                        ShowToast($"{ CurrentController.UserIndex}");
                                    }
                                }

                                else
                                {
                                    //battery back to a good level, return toast check to false
                                    toast_shown[numdict[$"{CurrentController.UserIndex}"]] = false;
                                }
                            }
                            Thread.Sleep(5000);
                        }
                    }
                }
                else
                {
                    TooltipText = $"No controller detected";
                    ActiveIcon = $"Resources/battery_unknown.ico";
                }
                Thread.Sleep(1000);
            }
        }

        //try to create a start menu shortcut (required for sending toasts)
        private bool TryCreateShortcut()
        {
            String shortcutPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Microsoft\\Windows\\Start Menu\\Programs\\XB1ControllerBatteryIndicator.lnk";
            if (!File.Exists(shortcutPath))
            {
                InstallShortcut(shortcutPath);
                return true;
            }
            return false;
        }
        //create the shortcut
        private void InstallShortcut(String shortcutPath)
        {
            // Find the path to the current executable 
            String exePath = Process.GetCurrentProcess().MainModule.FileName;
            IShellLinkW newShortcut = (IShellLinkW)new CShellLink();

            // Create a shortcut to the exe 
            ShellHelpers.ErrorHelper.VerifySucceeded(newShortcut.SetPath(exePath));
            ShellHelpers.ErrorHelper.VerifySucceeded(newShortcut.SetArguments(""));

            // Open the shortcut property store, set the AppUserModelId property 
            IPropertyStore newShortcutProperties = (IPropertyStore)newShortcut;

            using (PropVariant appId = new PropVariant(APP_ID))
            {
                ShellHelpers.ErrorHelper.VerifySucceeded(newShortcutProperties.SetValue(SystemProperties.System.AppUserModel.ID, appId));
                ShellHelpers.ErrorHelper.VerifySucceeded(newShortcutProperties.Commit());
            }

            // Commit the shortcut to disk 
            IPersistFile newShortcutSave = (IPersistFile)newShortcut;

            ShellHelpers.ErrorHelper.VerifySucceeded(newShortcutSave.Save(shortcutPath, true));
        }
        //send a toast
        private void ShowToast(string ControllerIndex)
        {
            //content of the toast
            string ToastTitle = $"Controller {ControllerIndex} low battery warning";
            string ToastText = $"Battery of controller {ControllerIndex} is (almost) empty.";
            string ToastText2 = $"(Click on the Button to stop the reappearing of this warning.)";
            int controllerId = numdict[$"{ControllerIndex}"];
            string argsDismiss = $"dismissed";
            string argsLaunch = $"{controllerId}";
            //how the content gets arranged
            string toastVisual =
                $@"<visual>
                        <binding template='ToastGeneric'>
                            <text>{ToastTitle}</text>
                            <text>{ToastText}</text>
                            <text>{ToastText2}</text>
                        </binding>
                    </visual>";
            //Button on the toast
            string toastActions =
                $@"<actions>
                        <action content='Shut up!' arguments='{argsDismiss}'/>
                   </actions>";
            //combine content and button
            string toastXmlString =
                $@"<toast scenario='reminder' launch='{argsLaunch}'>
                        {toastVisual}
                        {toastActions}
                   </toast>";

            XmlDocument toastXml = new XmlDocument();
            toastXml.LoadXml(toastXmlString);
            //create the toast
            var toast = new ToastNotification(toastXml);
            toast.Activated += ToastActivated;
            toast.Dismissed += ToastDismissed;
            //..and send it
            ToastNotificationManager.CreateToastNotifier(APP_ID).Show(toast);

        }
        //react to click on toast or button
        private void ToastActivated(ToastNotification sender, object e)
        {
            var toastArgs = e as ToastActivatedEventArgs;
            int controllerId = 0;
            //if the return value contains a controller ID
            if (Int32.TryParse(toastArgs.Arguments, out controllerId))
            {
                //reset the toast warning (it will trigger again if battery level is still empty)
                toast_shown[controllerId] = false;
            }
            //otherwise, do nothing
        }
        private void ToastDismissed(ToastNotification sender, object e)
        {
            //do nothing
        }

        public void ExitApplication()
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
}
