// UI/Form1.Field.cs
using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;

namespace FaceAttendance
{
    public partial class Form1
    {
        private readonly object _frameLock = new();
        private readonly string[] _captureSteps =
        {
            "Nhìn thẳng",
            "Nghiêng trái",
            "Nghiêng phải",
            "Ngẩng đầu",
            "Cúi đầu"
        };

        private readonly Bitmap?[] _captures = new Bitmap?[5];
        private PictureBox[] _captureBoxes = Array.Empty<PictureBox>();

        private FilterInfoCollection? _videoDevices;
        private VideoCaptureDevice? _videoSource;
        private Bitmap? _currentFrame;

        private Panel _headerPanel = null!;
        private Panel _sidebar = null!;
        private FlowLayoutPanel _menuItemsPanel = null!;
        private Button _btnMenuRegister = null!;
        private Button _btnMenuCheckIn = null!;
        private Button _btnMenuReport = null!;
        private Panel _activeIndicator = null!;
        private Panel _contentContainer = null!;
        private Control? _currentContent;
        private Panel? _registerView;
        private Panel? _checkInView;
        private Panel? _reportView;
        private MenuView _currentView = MenuView.Register;

        private Panel _inputPanel = null!;
        private Panel _controlPanel = null!;
        private Panel _previewWrapper = null!;
        private FlowLayoutPanel _thumbPanel = null!;
        private PictureBox _previewBox = null!;

        private Label _lblTitle = null!;
        private Label _lblCameraStatus = null!;
        private Label _lblInstruction = null!;
        private Label _lblCapturedCount = null!;
        private Label _lblStatus = null!;
        private TextBox _txtMaNV = null!;
        private TextBox _txtTenNV = null!;
        private Button _btnStart = null!;
        private Button _btnCapture = null!;
        private Button _btnRegister = null!;
        private Button _btnManualCheckIn = null!;

        private DateTimePicker _dtpReportFrom = null!;
        private DateTimePicker _dtpReportTo = null!;
        private Button _btnReportReload = null!;
        private DataGridView _gridReport = null!;
        private Label _lblReportStatus = null!;

        private System.Windows.Forms.Timer _checkInTimer = null!;
        private bool _isProcessingCheckIn = false;
        private bool _isCheckInMode = false;
        private bool _isCheckInViewMode = false;

        private Panel _checkInPreviewWrapper = null!;
        private PictureBox _checkInPreviewBox = null!;
        private Label _lblCheckInInstruction = null!;
        private Label _lblCheckInStatus = null!;

        private string _currentInstructionText = "🔄 Hệ thống sẵn sàng.";

        private readonly LivenessController _liveness = new();
        private readonly HttpClient _httpClient = new();
        private const string PoseEndpoint = "http://127.0.0.1:5000/pose";
        private const string CheckInEndpoint = "http://127.0.0.1:5000/checkin";
        private const double MotionThreshold = 3.0;

        private DateTime _lastFaceDetected = DateTime.MinValue;
        private DateTime _cooldownUntil = DateTime.MinValue;

        private readonly object _motionLock = new();
        private byte[]? _prevMotionFrame;
        private DateTime _lastMotionTime = DateTime.MinValue;
        private readonly TimeSpan _motionPersistence = TimeSpan.FromSeconds(5);
        private const double MotionDiffThreshold = 12.0;

        private readonly Color _primaryColor = Color.FromArgb(79, 70, 229);
        private readonly Color _primaryHover = Color.FromArgb(67, 56, 202);
        private readonly Color _secondaryColor = Color.FromArgb(168, 85, 247);
        private readonly Color _successColor = Color.FromArgb(16, 185, 129);
        private readonly Color _warningColor = Color.FromArgb(245, 158, 11);
        private readonly Color _dangerColor = Color.FromArgb(239, 68, 68);
        private readonly Color _darkBg = Color.FromArgb(15, 23, 42);
        private readonly Color _cardBg = Color.FromArgb(30, 41, 59);
        private readonly Color _sidebarBg = Color.FromArgb(20, 27, 45);
        private readonly Color _lightText = Color.FromArgb(248, 250, 252);
        private readonly Color _mutedText = Color.FromArgb(148, 163, 184);
        private readonly Color _borderColor = Color.FromArgb(51, 65, 85);

        private const float PreviewAspectRatio = 16f / 9f;
        private const int PreviewHorizontalShift = 25;

        private enum MenuView
        {
            Register,
            CheckIn,
            Report
        }
    }
}