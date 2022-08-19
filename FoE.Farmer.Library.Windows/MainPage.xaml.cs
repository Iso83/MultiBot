//#define UI_BROWSER

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;
using CefSharp;
using FoE.Farmer.Library.Windows.Events;
#if UI_BROWSER
using CefSharp.Wpf;
#else
using CefSharp.OffScreen;
#endif
using FoE.Farmer.Library.Windows.Helpers;
using MiCHALosoft;
using Newtonsoft.Json.Linq;

namespace FoE.Farmer.Library.Windows
{
    /// <summary>
    /// Interakční logika pro MainPage.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        public static Config Config = new Config("FoE.Config.xml");
        private static ChromiumWebBrowser Browser;
        private static bool CookieLoaded = false;
        private static Dictionary<string, Payload> payloads = new Dictionary<string, Payload>();
        private static bool ConfigLoaded = false;
        private static bool AutoStart = false;
        private static bool IsScriptLoaded = false;
        private static bool FrameLoaded = false;
        private static LogMessageType ShowLogMessageType = LogMessageType.AllWithoutRequest;
        private static bool IsReloginRunning = false;
        private static readonly string TempFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiCHALosoft", "FoEBot");
        private bool IsOtherBrowserInit = false;

        public MainPage(Window w)
        {
            InitSettingBrowser();
            InitializeComponent();
            Directory.CreateDirectory(TempFolder);

            RequestObject.DataRecived += (o, args) =>
            {
                if (args.IsError == RecivedDataType.Error)
                {
                    Manager.Log(args.Data, LogMessageType.Error);
                }
                else
                {
                    payloads[args.Id].TaskSource.SetResult(JArray.Parse(args.Data));
                    ForgeOfEmpires.Manager.ParseStringData(args.Data);
                }
            };

            LoadConfig();

            LoadViewPlayerResourcesHtml();

            InitBrowser();

            w.Closed += (sender, args) =>
            {
                Manager.SaveCache();
                SaveConfig();
                Config.Save();
                Cef.Shutdown();
                Environment.Exit(0);
                if (File.Exists(Path.Combine(TempFolder, "OtherResources.html"))) File.Delete(Path.Combine(TempFolder, "OtherResources.html"));
            };

            var random = new Random();
            Requests.PayloadSendRequest += async (requests, args) =>
            {
                var delay = random.Next(50, 400);
                Manager.Log($"Request delay: {delay}ms", LogMessageType.Request);
                await Task.Delay(delay);
                SendRequest(args.Payload);
            };

            Manager.LogMessageSend += (manager, args) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if ((ShowLogMessageType & args.Type) == args.Type)
                    {
                        LogBox.AppendText(args.Message + "\n");
                        LogBox.ScrollToEnd();
                    }
                });
                //var tr = new TextRange(LogBox.Document.ContentEnd, LogBox.Document.ContentEnd) {Text = args.Message};
                //tr.ApplyPropertyValue(TextElement.­ForegroundProperty, Brushes.Red);
            };

            Manager.Log("Logging...");

            if (UserName.Text.Length == 0 || Password.Password.Length == 0)
            {
                Manager.Log("Login failed. User name or password is empty. Fill in login data and click on Relogin");
            }

            ForgeOfEmpires.Manager.LogoutEvent += (manager, args) =>
            {
                if (IsReloginRunning) return;
                IsReloginRunning = true;
                Relogin();
            };

            ForgeOfEmpires.Manager.ResourcesUpdate += (manager, args) =>
            {
                try
                {
                    foreach (var value in args.Values)
                    {
                        switch (value.Item1)
                        {
                            //case "tavern_silver": TavernSilver.Dispatcher.Invoke(() => TavernSilver.Text = value.Item2.ToString(CultureInfo.CurrentUICulture)); break;
                            //case "premium": DiamondBox.Dispatcher.Invoke(() => DiamondBox.Text = value.Item2.ToString(CultureInfo.CurrentUICulture)); break;
                            //case "money": MoneyBox.Dispatcher.Invoke(() => MoneyBox.Text = value.Item2.ToString(CultureInfo.CurrentUICulture)); break;
                            //case "supplies": SupplyBox.Dispatcher.Invoke(() => SupplyBox.Text = value.Item2.ToString(CultureInfo.CurrentUICulture)); break;
                            //case "strategy_points": ForgePointsBox.Dispatcher.Invoke(() => ForgePointsBox.Text = value.Item2.ToString(CultureInfo.CurrentUICulture)); break;
                            //case "medals": Medals.Dispatcher.Invoke(() => Medals.Text = value.Item2.ToString(CultureInfo.CurrentUICulture)); break;
                            default:
                                viewPlayerResources.ExecuteScriptAsync("updateItem", value.Item1, value.Item2);
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Manager.Log("Resource update Exception: " + e.ToString(), LogMessageType.Exception);
                }
            };

        }

        private void InitSettingBrowser()
        {

#if UI_BROWSER
            Cef.EnableHighDPISupport();
#endif

            var settings = new CefSettings();
            settings.RemoteDebuggingPort = 8088;
            settings.WindowlessRenderingEnabled = true;
            settings.CachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiCHALosoft\\FoFBot\\Cache");//@"C:\Temp\Cache";
            settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/57.0.3071.115 Safari/537.36";

            settings.IgnoreCertificateErrors = true;

            if (!Cef.IsInitialized)
                if (!Cef.Initialize(settings))
                    throw new Exception();
        }

        private void Init_ViewPlayerRes()
        {
            if (IsOtherBrowserInit) return;
            IsOtherBrowserInit = true;
            viewPlayerResources.WebBrowser = viewPlayerResources;
            viewPlayerResources.Address = new Uri(Path.Combine(TempFolder, "OtherResources.html")).AbsoluteUri;
            
            viewPlayerResources.Width = double.NaN;
            viewPlayerResources.Height = double.NaN;

            ForegroundGrid.Children.Remove(viewPlayerResources);
            ResourceInfoGrid.Children.Add(viewPlayerResources);
        }

        private void InitBrowser()
        {
#if UI_BROWSER
            Browser = new ChromiumWebBrowser();
            Browser.WebBrowser = Browser;
            Browser.Address = new Uri($"https://{Requests.Domain}/").AbsoluteUri;
#else
            Browser = new ChromiumWebBrowser(new Uri($"https://{Requests.Domain}/").AbsoluteUri, null, null, false);
#endif

            Browser.JavascriptObjectRepository.Settings.LegacyBindingEnabled = true;
            Browser.JavascriptObjectRepository.Register(AP._f("responseManager"), new RequestObject(), isAsync: true, options: BindingOptions.DefaultBinder);

#if UI_BROWSER
            Browser.BrowserSettings.DefaultEncoding = "UTF-8";
            //Browser.BrowserSettings.FileAccessFromFileUrls = CefState.Enabled;
            Browser.BrowserSettings.WindowlessFrameRate = 30;
#else
            Browser.CreateBrowserAsync();
#endif

            Browser.RequestHandler = new RequestHandler();
            
            Browser.FrameLoadEnd += (sender, args) =>
            {
                Dispatcher.Invoke(() =>
                {
                    FrameLoaded = true;

                    if (UserName.Text.Length == 0 || Password.Password.Length == 0) return;
                    LoadScripts();
                });
            };

#if UI_BROWSER
            BrowserGrid.Children.Add(Browser);
#else
            BrowserTabItem.Visibility = Visibility.Collapsed;
#endif
        }

        private static void LoadScripts()
        {
            IsScriptLoaded = true;

            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "FoE.Farmer.Library.Windows.External.Inject.js";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream))
            {
                var result = new StringBuilder(reader.ReadToEnd());
                var wn = Requests.WorldName == null ? "null" : $"'{Requests.WorldName}'";

                result.Insert(0, $"var FoELoginUserName = '{Requests.UserName}'; var FoELoginPassword = '{Requests.Password}'; var FoEWordName = {wn};");
                result.Replace("FoELoginUserName", AP._f("FoELoginUserName"));
                result.Replace("FoELoginPassword", AP._f("FoELoginPassword"));
                result.Replace("FoEWordName", AP._f("FoEWordName"));
                result.Replace("FoEInit", AP._f("FoEInit"));
                result.Replace("FoELogin", AP._f("FoELogin"));
                result.Replace("FoFTimer", AP._f("FoFTimer"));
                result.Replace("FoEPlay", AP._f("FoEPlay"));

                Browser.ExecuteScriptAsync(result.ToString());
            }
        }

        private void LoadViewPlayerResourcesHtml()
        {
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "FoE.Farmer.Library.Windows.External.OtherResources.html";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream))
            {
                var result = reader.ReadToEnd();
                File.WriteAllText(Path.Combine(TempFolder, "OtherResources.html"), result);
            }
        }

        public void Relogin()
        {
            Dispatcher.Invoke(() =>
            {
                CookieLoaded = false;
                Browser.Load(new Uri($"https://{Requests.Domain}/").AbsoluteUri);
            });
        }

        public static async Task ShowCookiesAsync()
        {
            if (CookieLoaded) return;
            CookieLoaded = true;

            var cookieManager = Cef.GetGlobalCookieManager();

            var tCookies = cookieManager.VisitAllCookiesAsync();
            await tCookies.ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion)
                {
                    var cookies = t.Result;

                    foreach (var cookie in cookies)
                    {
                        switch (cookie.Name)
                        {
                            case "metricsUvId": Requests.MetricsUvId = cookie.Value; break;
                            case "sid": Requests.SID = cookie.Value; break;
                            case "_ga": Requests._GA = cookie.Value; break;
                            case "_gid": Requests._GID = cookie.Value; break;
                            case "startup_microtime": Requests.StartupMicrotime = cookie.Value; break;
                            case "ig_conv_last_site": Requests.IgLastSite = cookie.Value; break;
                        }
                    }
                }
                else
                {
                    //"No Cookies found"
                }
            });


            LoadRequestString();

            // after relogin must start timer
            if (IsReloginRunning)
            {
                Manager.Log("Timer start");
                ForgeOfEmpires.Manager.Start();
            }

            if (AutoStart && !IsReloginRunning)
            {
                await Task.Delay(2000).ContinueWith((t) =>
                {
                    SendRequest(Payloads.StartupService.GetData());
                    Manager.Log("Success login. (StartupService)");
                }, TaskContinuationOptions.ExecuteSynchronously);
            }
            else Manager.Log("Success login");

            IsReloginRunning = false;
        }

        private static void LoadRequestString()
        {
            var FncSend = "function "+ AP._f("sendRequest") + "(data, signature){return new Promise((r, c) => {$.ajax({type: 'POST',url: '/game/json?h=" + Requests.UserKey + "', data: data, contentType: 'application/json', dataType: 'json', beforeSend: (xhr) => {" +
                          "xhr.setRequestHeader('Signature', signature);" +
                          $"xhr.setRequestHeader('Client-Identification', '{Requests.TemplateRequestHeader["Client-Identification"]}');" +
                          //$"xhr.setRequestHeader('X-Requested-With', '{Requests.TemplateRequestHeader["X-Requested-With"]}');" +
                          "},success: function(result) {r(result);},error: function(result) { c(result);}});});}";

            Browser.ExecuteScriptAsync(FncSend);
            ForgeOfEmpires.Manager.IsInitialized = true;
        }

        public static async void SendRequest(Payload payload)
        {
            Manager.Log("Request sent: " + payload.ToString(), LogMessageType.Request);
            var data = "[" + payload + "]";
            var signature = Requests.BuildSignature(data);
            var script = $"(async () => {AP._f("responseManager")}.setData(JSON.stringify(await {AP._f("sendRequest")}('{data}', '{signature}')), '{signature}') )();";

            payloads.Add(signature, payload);
            await Browser.EvaluateScriptAsync(script);
        }

        private void UserInfoChange_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (UserName.Text.Trim().Length > 0)
            {
                Requests.UserName = UserName.Text.Trim();
                Config.SetValue("UserName", Requests.UserName);
            }

            if (WorldName.Text != null)
            {
                Requests.WorldName = WorldName.Text.Trim();
                Config.SetValue("WorldName", Requests.WorldName);
            }

            if (Domain.Text.Trim().Length > 0)
            {
                Requests.Domain = Domain.Text.Trim();
                Config.SetValue("Domain", Requests.Domain);
            }
        }

        private void StartStopBtn_Click(object sender, RoutedEventArgs e)
        {
            Init_ViewPlayerRes();

            if (ForgeOfEmpires.Manager.IsStarted)
            {
                ForgeOfEmpires.Manager.Stop();
                (sender as Button).Content = "Start";
            }
            else
            {
                if (ForgeOfEmpires.Manager.IsStartupServicesLoad) 
                    ForgeOfEmpires.Manager.Start();
                else
                {
                    if (!CookieLoaded)
                    {
                        MessageBox.Show("Wait for login");
                        return;
                    }
                    if (!IsScriptLoaded)
                    {
                        MessageBox.Show("Script not injected. Maybe login data not filled in");
                        return;
                    }
                    SendRequest(Payloads.StartupService.GetData());
                }
                (sender as Button).Content = "Stop";
            }
        }

        private void DevToolsBnt_OnClick(object sender, RoutedEventArgs e)
        {
            if (Browser.IsBrowserInitialized) Browser.ShowDevTools();
        }
        private void DevToolBnt_ViewPlayerRes_OnClick(object sender, RoutedEventArgs e) 
        {       
            if (viewPlayerResources.IsBrowserInitialized) viewPlayerResources.ShowDevTools();        
        }

        private void ReloadBnt_OnClick(object sender, RoutedEventArgs e)
        {
            AutoStart = ForgeOfEmpires.Manager.IsStarted;

            CookieLoaded = false;
            Browser.Load(new Uri($"https://{Requests.Domain}/").AbsoluteUri);
        }

        private void Password_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (Password.Password.Length > 0)
            {
                Requests.Password = Password.Password;
                var pwd = StringCipher.Encrypt(Password.Password, "FoEMultiBot");
                Config.SetValue("Password", pwd);
            }

            if (!IsScriptLoaded && Password.Password.Length > 0 && UserName.Text.Trim().Length > 0 && FrameLoaded)
                LoadScripts();
        }

        private void UpdateUserInterval()
        {
            if (!ConfigLoaded) return;

            var goodsTimer = ConfigGrid.Children.OfType<RadioButton>().Where(item => item.GroupName == "GoodsTimer").FirstOrDefault(r => r.IsChecked.Value)?.Tag.ToString();
            var suppliesTimer = ConfigGrid.Children.OfType<RadioButton>().Where(item => item.GroupName == "SuppliesTimer").FirstOrDefault(r => r.IsChecked.Value)?.Tag.ToString();
            var residentalTimer = ConfigGrid.Children.OfType<RadioButton>().Where(item => item.GroupName == "ResidentalTimer").FirstOrDefault(r => r.IsChecked.Value)?.Tag.ToString();

            if (!Enum.TryParse(goodsTimer, false, out TimeIntervalGoods goodsEnum))
                goodsEnum = TimeIntervalGoods.EightHours;

            if (!Enum.TryParse(suppliesTimer, false, out TimeIntervalSupplies suppliesEnum))
                suppliesEnum = TimeIntervalSupplies.EightHours;

            if (!Enum.TryParse(residentalTimer, false, out TimeIntervalSupplies residentalEnum))
                residentalEnum = TimeIntervalSupplies.EightHours;

            ForgeOfEmpires.Manager.UserIntervalGoods = goodsEnum;
            ForgeOfEmpires.Manager.UserIntervalSupplies = suppliesEnum;
            ForgeOfEmpires.Manager.UserIntervalResidental = residentalEnum;
        }

        public void LoadConfig()
        {
            var pwd = Config.GetValue("Password");
            if (!string.IsNullOrEmpty(pwd))
            {
                pwd = StringCipher.Decrypt(pwd, "FoEMultiBot");
                Requests.Password = pwd;
                Password.Password = pwd;
            }
            pwd = Config.GetValue("UserName");
            if (!string.IsNullOrEmpty(pwd))
            {
                Requests.UserName = pwd;
                UserName.Text = pwd;
            }

            pwd = Config.GetValue("WorldNameFOE"); // TODO: Config.GetValue['WorldName'] is empty on load ... lib_MiCHALosoft.Config 
            if (!string.IsNullOrEmpty(pwd))
            {
                Requests.WorldName = pwd;
                WorldName.Text = pwd;
            }

            pwd = Config.GetValue("Domain");
            if (!string.IsNullOrEmpty(pwd))
            {
                Requests.Domain = pwd;
                Domain.Text = pwd;
            }


            var val = Config.GetValue("GoodsTimer");
            if (!string.IsNullOrEmpty(val))
            {
                var goodsTimer = ConfigGrid.Children.OfType<RadioButton>().Where(item => item.GroupName == "GoodsTimer").FirstOrDefault(r => r.Tag.ToString() == val);
                if (goodsTimer != null) goodsTimer.IsChecked = true;
            }

            val = Config.GetValue("SuppliesTimer");
            if (!string.IsNullOrEmpty(val))
            {
                var goodsTimer = ConfigGrid.Children.OfType<RadioButton>().Where(item => item.GroupName == "SuppliesTimer")
                    .FirstOrDefault(r => r.Tag.ToString() == val);
                if (goodsTimer != null) goodsTimer.IsChecked = true;
            }

            val = Config.GetValue("ResidentalTimer");
            if (!string.IsNullOrEmpty(val))
            {
                var goodsTimer = ConfigGrid.Children.OfType<RadioButton>().Where(item => item.GroupName == "ResidentalTimer")
                    .FirstOrDefault(r => r.Tag.ToString() == val);
                if (goodsTimer != null) goodsTimer.IsChecked = true;
            }

            val = Config.GetValue("TavernMinOccupation");
            if (!string.IsNullOrEmpty(val))
            {
                TavernMinOccupation.Text = val;
                var num = val == "100" ? 1M : decimal.Parse("0." + val, CultureInfo.InvariantCulture);
                Services.TavernService.MinTaverOccupation = num;
            }

            AutoLoginCheck.IsChecked = Config.GetValueAsBool("AutoLoginAfterStart");
            ConfigLoaded = true;
            UpdateUserInterval();
        }

        public void SaveConfig()
        {
            if (UserName.Text.Trim().Length > 0)
            {
                Requests.UserName = UserName.Text.Trim();
                Config.SetValue("UserName", Requests.UserName);
            }
            if (WorldName.Text != null)
            {
                Requests.WorldName = WorldName.Text.Trim();
                Config.SetValue("WorldNameFOE", Requests.WorldName);
            }
            if (Password.Password.Length > 0)
            {
                var pwd = StringCipher.Encrypt(Password.Password, "FoEMultiBot");
                Requests.Password = Password.Password;
                Config.SetValue("Password", pwd);
            }

            Requests.Domain = !string.IsNullOrWhiteSpace(Domain.Text) ? Domain.Text.Trim() : "";
            Config.SetValue("Domain", Requests.Domain);

            var goodsTimer = ConfigGrid.Children.OfType<RadioButton>().Where(item => item.GroupName == "GoodsTimer").FirstOrDefault(r => r.IsChecked.Value)?.Tag.ToString();
            var suppliesTimer = ConfigGrid.Children.OfType<RadioButton>().Where(item => item.GroupName == "SuppliesTimer").FirstOrDefault(r => r.IsChecked.Value)?.Tag.ToString();
            var residentalTimer = ConfigGrid.Children.OfType<RadioButton>().Where(item => item.GroupName == "ResidentalTimer").FirstOrDefault(r => r.IsChecked.Value)?.Tag.ToString();

            if (goodsTimer != null)
            {
                Config.SetValue("GoodsTimer", goodsTimer);
            }
            if (suppliesTimer != null)
            {
                Config.SetValue("SuppliesTimer", suppliesTimer);
            }
            if (residentalTimer != null)
            {
                Config.SetValue("ResidentalTimer", residentalTimer);
            }
            Config.SetValue("TavernMinOccupation", TavernMinOccupation.Text.Trim());

            Config.SetValue("AutoLoginAfterStart", AutoLoginCheck.IsChecked.Value.ToString());
        }

        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            UpdateUserInterval();
        }

        private string TavernMinOcLastValidText = "75";
        private void TavernMinOccupation_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var text = TavernMinOccupation.Text.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;
            if (Regex.IsMatch(text, "^([0-9]|[1-9][0-9]|100)$"))
            {
                
                TavernMinOcLastValidText = text;
                var num = text == "100" ? 1M : decimal.Parse("0." + text, CultureInfo.InvariantCulture);
                Services.TavernService.MinTaverOccupation = num;
            }
            else
            {
                TavernMinOccupation.Text = TavernMinOcLastValidText;
                TavernMinOccupation.SelectAll();
            }
        }
    }
}
