using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using WebSocket.Portable;
using WebSocket.Portable.Interfaces;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Websocket.Portable.WSA
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private string _serverUrl = "ws://echo.websocket.org";
        private string _message = "Hello World";


        WebSocketClient Client;

        public MainPage()
        {
            this.InitializeComponent();
            Init();
        }

        async void Init()
        {
            try
            {
                Client = new WebSocketClient();
                Client.MessageReceived += OnMessage;

                await Client.OpenAsync("ws://echo.websocket.org");

                await Client.SendAsync("Hello");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

        }

        private void OnMessage(IWebSocketMessage obj)
        {

        }
    }
}
