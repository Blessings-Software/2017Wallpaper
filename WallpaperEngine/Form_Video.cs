using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace YoutubeWallpaper
{
    public partial class Form_Video : Form
    {
        public Form_Video(int ownerScreenIndex = 0)
        {

            InitializeComponent();

            // 문서(동영상)로드가 완료되면 플래시의 핸들을 찾도록 함.
            //webBrowser_page.DocumentCompleted += ((_s, _e) => UpdatePlayerHandle());

            // 파일 다운로드 창이 뜨지 않도록 함.
            //


            OwnerScreenIndex = ownerScreenIndex;


            PinToBackground();
        }
        #region public_variables
        //#############################################################################################
        public static string video_path = "";
        //private int m_Volume = 100;
        private static bool m_Repeat = false;
        public bool Repeat
        {
            get
            {
                return m_Repeat;
            }
            set
            {
                if (m_Repeat)
                {
                    m_Repeat = false;
                }
                else
                {
                    m_Repeat = true;
                }
            }
        }
        private bool m_isFixed = false;
        public bool IsFixed
        { get { return m_isFixed; } }
        /*
        public string Uri
        {
            get { return webBrowser_page.Url.ToString(); }
            set
            {
                webBrowser_page.Navigate(value);
            }
        }
        */
        private int m_latestVolume = 100;
        public int Volume
        {
            get
            {
                //WinApi.waveOutGetVolume(IntPtr.Zero, out uint temp);
                return axWindowsMediaPlayer1.settings.volume;
            }
            set
            {
                m_latestVolume = value;

                //uint vol = (uint)((double)0xFFFF * value / 100) & 0xFFFF;
                axWindowsMediaPlayer1.settings.volume = value;
            }
        }

        public string Caption
        {
            set
            {
                axWindowsMediaPlayer1.closedCaption.SAMIFileName = value;
            }
        }

        public void play()
        {
            axWindowsMediaPlayer1.Ctlcontrols.play();
        }

        public void pause()
        {
            axWindowsMediaPlayer1.Ctlcontrols.pause();
        }

        private IntPtr m_playerHandle = IntPtr.Zero;
        private IntPtr PlayerHandle
        {
            get
            {
                if (m_playerHandle != IntPtr.Zero)
                    return m_playerHandle;

                return UpdatePlayerHandle();
            }
        }

        private int m_ownerScreenIndex = 0;
        public int OwnerScreenIndex
        {
            get { return m_ownerScreenIndex; }
            set
            {
                if (value < 0)
                    value = 0;
                else if (value >= Screen.AllScreens.Length)
                    value = 0;

                if (m_ownerScreenIndex != value)
                {
                    m_ownerScreenIndex = value;

                    PinToBackground();
                }
            }
        }
        public WinApi.MONITORINFO OwnerScreen
        {
            get
            {
                if (OwnerScreenIndex < ScreenUtility.Screens.Length)
                    return ScreenUtility.Screens[OwnerScreenIndex];
                return new WinApi.MONITORINFO()
                {
                    rcMonitor = Screen.PrimaryScreen.Bounds,
                    rcWork = Screen.PrimaryScreen.WorkingArea,
                };
            }
        }

        public bool AutoMute
        { get; set; } = false;

        public bool AutoTogglePlay
        { get; set; } = true;

        private Task m_checkParent = null;
        private bool m_onRunning = false;
        private EventWaitHandle m_waitHandle = null;

        private readonly object m_lockFlag = new object();
        private bool m_needUpdate = false;
    #endregion
        //private bool m_wasOverlayed = false;

        //#############################################################################################

        protected IntPtr UpdatePlayerHandle()
        {
            IntPtr flash = IntPtr.Zero;
            flash = WinApi.FindWindowEx(this.webBrowser_page.Handle, IntPtr.Zero, "Shell Embedding", IntPtr.Zero);
            flash = WinApi.FindWindowEx(flash, IntPtr.Zero, "Shell DocObject View", IntPtr.Zero);
            flash = WinApi.FindWindowEx(flash, IntPtr.Zero, "Internet Explorer_Server", IntPtr.Zero);
            flash = WinApi.FindWindowEx(flash, IntPtr.Zero, "MacromediaFlashPlayerActiveX", IntPtr.Zero);

            m_playerHandle = flash;


            return flash;
        }

        public void TogglePlay()
        {
            //PerformClickWallpaper(this.Width / 2, this.Height / 2);
        }

        protected bool PinToBackground()
        {
            m_isFixed = BehindDesktopIcon.FixBehindDesktopIcon(this.Handle);

            if (m_isFixed)
            {
                ScreenUtility.FillScreen(this, OwnerScreen);
            }


            return m_isFixed;
        }

        protected void CheckParent(object thisHandle)
        {
            IntPtr me = (IntPtr)thisHandle;


            while (m_onRunning)
            {
                bool isChildOfProgman = false;


                var progman = WinApi.FindWindow("Progman", null);

                WinApi.EnumChildWindows(progman, new WinApi.EnumWindowsProc((handle, lparam) =>
                {
                    if (handle == me)
                    {
                        isChildOfProgman = true;
                        return false;
                    }

                    return true;
                }), IntPtr.Zero);


                if (isChildOfProgman == false)
                {
                    lock (m_lockFlag)
                    {
                        m_needUpdate = true;
                    }
                }


                m_waitHandle.WaitOne(2000);
            }
        }

        //#############################################################################################

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            lock (m_lockFlag)
            {
                m_needUpdate = true;
            }
        }

        private void Form_Video_Load(object sender, EventArgs e)
        {
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;


            // 생성자에서 배경에 고정되었을테니 DPI의 영향에서 벗어난다.
            // 이때 모니터 정보들을 다시 구하면 DPI의 영향을 받지 않는 해상도가 나온다.
            ScreenUtility.Initialize();


            // 그렇게 구해진 올바른 해상도로 다시 배경에 고정한다.
            if (PinToBackground())
            {
                m_waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
                m_onRunning = true;
                m_checkParent = Task.Factory.StartNew(CheckParent, this.Handle);


                this.timer_check.Start();
            }
            else
            {
                this.Close();
            }
            axWindowsMediaPlayer1.URL = video_path;
            axWindowsMediaPlayer1.Ctlcontrols.play();
        }

        private void Form_Video_FormClosing(object sender, FormClosingEventArgs e)
        {
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;


            this.timer_check.Stop();


            if (m_checkParent != null)
            {
                m_onRunning = false;
                m_waitHandle.Set();
                m_checkParent.Wait(TimeSpan.FromSeconds(10.0));
                m_checkParent = null;

                m_waitHandle.Dispose();
            }
        }

        private void timer_check_Tick(object sender, EventArgs e)
        {
            bool needUpdate = false;
            lock (m_lockFlag)
            {
                needUpdate = m_needUpdate;
                m_needUpdate = false;
            }

            if (needUpdate)
            {
                PinToBackground();
            }
        }

        private void axWindowsMediaPlayer1_Enter(object sender, EventArgs e)
        {

        }

        private void axWindowsMediaPlayer1_PlayStateChange(object sender, AxWMPLib._WMPOCXEvents_PlayStateChangeEvent e)
        {
            if (axWindowsMediaPlayer1.playState == WMPLib.WMPPlayState.wmppsMediaEnded)
            {
               // if (axWindowsMediaPlayer1.Ctlcontrols.currentPosition == axWindowsMediaPlayer1.currentMedia.duration)
               // {
                    MessageBox.Show("Play Ended");
               // }
            }

        }
    }
}
