// UI/Form1.cs
using System;
using System.Windows.Forms;

namespace FaceAttendance
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            InitializeModernUI();
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
            InitializeCameraList();
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            StopRealtimeCheckInMode();
            StopManualCheckInMode();
            StopCamera();
            ClearCapturedImages();

            lock (_frameLock)
            {
                _currentFrame?.Dispose();
                _currentFrame = null;
            }
        }
    }
}