﻿using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using LibVLCSharp.Shared;

namespace libVLCPlayer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        bool _mediaReady = false;
        LibVLC _libVLC;
        MediaPlayer _mediaPlayer;
        Media _media;
        string _filePath;
        bool _isStream;
        float vidPosition;

        public MainWindow(string[] args)
        {
            InitializeComponent();
            videoView.Loaded += VideoView_Loaded;
            _filePath = args[0];
            _isStream = false;
            ListenToParent();
        }

        //todo errorhandling
        async void VideoView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {

                LibVLCSharp.Shared.Core.Initialize();

                //flags: 
                //"--no-disable-screensaver" : enable monitor sleep.
                //ref: https://wiki.videolan.org/VLC_command-line_help
                _libVLC = new LibVLC("--no-disable-screensaver");
                _mediaPlayer = new MediaPlayer(_libVLC)
                {
                    AspectRatio = "Fill",
                    EnableHardwareDecoding = true
                };
                _mediaPlayer.EndReached += _mediaPlayer_EndReached;
                videoView.MediaPlayer = _mediaPlayer;

                if (_isStream)
                {
                    //ref: https://code.videolan.org/videolan/LibVLCSharp/-/issues/156#note_35657
                    _media = new Media(_libVLC, _filePath, FromType.FromLocation);
                    await _media.Parse(MediaParseOptions.ParseNetwork);
                    _mediaPlayer.Play(_media.SubItems.First());
                }
                else
                {
                    _media = new Media(_libVLC, _filePath, FromType.FromPath);
                    _mediaPlayer.Play(_media);
                }
                _mediaReady = true;
            }
            catch 
            {
                //todo: send error to lively parent program.
            }
        }

        private void _mediaPlayer_EndReached(object sender, EventArgs e)
        {
            if (_isStream)
            {
                ThreadPool.QueueUserWorkItem(_ => _mediaPlayer.Play(_media.SubItems.First()));
            }
            else
            {
                ThreadPool.QueueUserWorkItem(_ => _mediaPlayer.Play(_media));
            }
        }

        public void PausePlayer()
        {
            if (_mediaPlayer.IsPlaying && _mediaReady)
            {
                vidPosition = _mediaPlayer.Position;
                _mediaPlayer.Stop();
                //_mediaPlayer.Pause();
                //ThreadPool.QueueUserWorkItem(_ => _mediaPlayer.Pause());
            }
        }

        public void PlayMedia()
        {
            if (_mediaReady && !_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Play();
                _mediaPlayer.Position = vidPosition;
                //ThreadPool.QueueUserWorkItem(_ => _mediaPlayer.Play());
            }
        }

        public void StopPlayer()
        {
            if (_mediaReady)
            {
                _mediaPlayer.Stop();
                //ThreadPool.QueueUserWorkItem(_ => _mediaPlayer.Stop());
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _mediaReady = false;
                _mediaPlayer.EndReached -= _mediaPlayer_EndReached;
                _mediaPlayer.Dispose();
                _libVLC.Dispose();
                _media.Dispose();
            }
            catch { }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            var styleNewWindowExtended =
                           (Int64)WS_EX_NOACTIVATE |
                           (Int64)WS_EX_TOOLWINDOW;

            // update window styles
            SetWindowLongPtr(new HandleRef(null, handle), (-20), (IntPtr)styleNewWindowExtended);
            this.ShowInTaskbar = false;
            this.ShowInTaskbar = true;

            //passing handle to lively.
            Console.WriteLine("HWND" + handle);
        }

        /// <summary>
        /// std I/O redirect, used to communicate with lively. 
        /// </summary>
        public async void ListenToParent()
        {
            try
            {
                await Task.Run(async () =>
                {
                    // Loop runs only once per line received
                    while (true) 
                    {
                        string text = await Console.In.ReadLineAsync();
                        if (String.Equals(text, "lively:vid-pause", StringComparison.OrdinalIgnoreCase))
                        {
                            PausePlayer();
                        }
                        else if (String.Equals(text, "lively:vid-play", StringComparison.OrdinalIgnoreCase))
                        {
                            PlayMedia();
                        }
                        else if (String.Equals(text, "lively:terminate", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }
                    }
                });
                Application.Current.Shutdown();
            }
            catch { }
        }

        #region pinvoke

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        public static extern int SetWindowLong32(HandleRef hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        public static extern IntPtr SetWindowLongPtr64(HandleRef hWnd, int nIndex, IntPtr dwNewLong);
        private const uint WS_EX_NOACTIVATE = 0x08000000;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        // This helper static method is required because the 32-bit version of user32.dll does not contain this API
        // (on any versions of Windows), so linking the method will fail at run-time. The bridge dispatches the request
        // to the correct function (GetWindowLong in 32-bit mode and GetWindowLongPtr in 64-bit mode)
        public static IntPtr SetWindowLongPtr(HandleRef hWnd, int nIndex, IntPtr dwNewLong)
        {

            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

        }

        #endregion //pinvoke
    }
}
