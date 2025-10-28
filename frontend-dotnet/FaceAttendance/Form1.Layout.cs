using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FaceAttendance
{
    public partial class Form1
    {
       private void InitializeModernUI()
        {
            Text = "Face Attendance System";
            Size = new Size(1450, 850);
            MinimumSize = new Size(1200, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = _darkBg;
            Font = new Font("Segoe UI", 9.75F);

            Controls.Clear();

            CreateHeader();
            CreateSidebar();
            CreateContentContainer();

            _checkInTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _checkInTimer.Tick += async (s, e) => await AutoCheckInAsync();

            SwitchContent(MenuView.Register);

            Load += Form1_Load;
            FormClosing += Form1_FormClosing;
        }

        #region Header & Sidebar

        private void CreateHeader()
        {
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 75,
                BackColor = _cardBg,
                Padding = new Padding(35, 0, 35, 0)
            };
            _headerPanel.Paint += (s, e) => DrawHeaderGradient(e.Graphics, _headerPanel.ClientRectangle);
            Controls.Add(_headerPanel);

            var iconLabel = new Label
            {
                Text = "🎭",
                Font = new Font("Segoe UI Emoji", 24, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(35, 20),
                ForeColor = _primaryColor
            };
            _headerPanel.Controls.Add(iconLabel);

            _lblTitle = new Label
            {
                Text = "Face Attendance System",
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = _lightText,
                AutoSize = true,
                Location = new Point(85, 19)
            };
            _headerPanel.Controls.Add(_lblTitle);

            var subtitleLabel = new Label
            {
                Text = "Hệ thống điểm danh nhận diện khuôn mặt",
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = _mutedText,
                AutoSize = true,
                Location = new Point(87, 50)
            };
            _headerPanel.Controls.Add(subtitleLabel);
        }

        private void DrawHeaderGradient(Graphics g, Rectangle rect)
        {
            using var brush = new LinearGradientBrush(rect, _cardBg, Color.FromArgb(45, 55, 72), LinearGradientMode.Horizontal);
            g.FillRectangle(brush, rect);
        }

        private void CreateSidebar()
        {
            _sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 290,
                BackColor = _sidebarBg
            };
            _sidebar.Paint += (s, e) => DrawSidebarShadow(e.Graphics, _sidebar.ClientRectangle);
            Controls.Add(_sidebar);

            var logoPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 10,
                BackColor = Color.Transparent,
                Padding = new Padding(25, 20, 25, 20)
            };
            _sidebar.Controls.Add(logoPanel);

            var menuLabel = new Label
            {
                Text = "MENU CHÍNH",
                Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold),
                ForeColor = _mutedText,
                AutoSize = true,
                Location = new Point(25, 5),
                Padding = new Padding(0, 10, 0, 10)
            };

            _menuItemsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoScroll = true,
                WrapContents = false,
                Padding = new Padding(20, 10, 0, 20),
                BackColor = Color.Transparent
            };
            _sidebar.Controls.Add(_menuItemsPanel);
            _menuItemsPanel.Controls.Add(menuLabel);

            _activeIndicator = new Panel
            {
                Width = 5,
                Height = 56,
                BackColor = _primaryColor,
                Left = 0,
                Visible = false
            };
            _sidebar.Controls.Add(_activeIndicator);

            _btnMenuRegister = CreateSidebarButton("Đăng ký khuôn mặt", "👤");
            _btnMenuRegister.Click += (s, e) => SwitchContent(MenuView.Register);
            _menuItemsPanel.Controls.Add(_btnMenuRegister);

            _btnMenuCheckIn = CreateSidebarButton("Chấm công realtime", "⏰");
            _btnMenuCheckIn.Click += (s, e) => SwitchContent(MenuView.CheckIn);
            _menuItemsPanel.Controls.Add(_btnMenuCheckIn);

            _btnMenuReport = CreateSidebarButton("Báo cáo chấm công", "📊");
            _btnMenuReport.Click += (s, e) => SwitchContent(MenuView.Report);
            _menuItemsPanel.Controls.Add(_btnMenuReport);
        }

        private void DrawSidebarShadow(Graphics g, Rectangle rect)
        {
            using var shadowBrush = new LinearGradientBrush(
                new Rectangle(rect.Width - 15, 0, 15, rect.Height),
                Color.FromArgb(30, 0, 0, 0),
                Color.FromArgb(0, 0, 0, 0),
                LinearGradientMode.Horizontal);
            g.FillRectangle(shadowBrush, rect.Width - 15, 0, 15, rect.Height);
        }

        private Button CreateSidebarButton(string text, string icon)
        {
            var btn = new Button
            {
                Width = 250,
                Height = 56,
                Margin = new Padding(0, 0, 0, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = _mutedText,
                Font = new Font("Segoe UI Semibold", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(50, 0, 15, 0),
                Text = text,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 50, 70);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(50, 60, 80);

            btn.Paint += (s, e) =>
            {
                var iconRect = new Rectangle(18, (btn.Height - 24) / 2, 24, 24);
                using var iconFont = new Font("Segoe UI Emoji", 16, FontStyle.Regular);
                var iconColor = btn.ForeColor == _lightText ? _primaryColor : _mutedText;
                using var iconBrush = new SolidBrush(iconColor);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(icon, iconFont, iconBrush, iconRect, sf);
            };

            return btn;
        }

        #endregion

      #region Main content switching
      private void CreateContentContainer()
        {
            _contentContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _darkBg,
                Padding = new Padding(25, 25, 25, 25)
            };
            Controls.Add(_contentContainer);
            _contentContainer.BringToFront();
        }

        private void SwitchContent(MenuView view)
        {
            if (_currentView == view && _currentContent != null)
                return;

            bool leavingRegister = _currentView == MenuView.Register;
            bool leavingCheckIn = _currentView == MenuView.CheckIn;

            if (leavingRegister && view != MenuView.Register)
            {
                StopManualCheckInMode();
            }

            if (leavingCheckIn)
            {
                StopRealtimeCheckInMode();
            }

            Control newContent = view switch
            {
                MenuView.Register => BuildRegisterView(),
                MenuView.CheckIn => BuildCheckInView(),
                MenuView.Report => BuildReportView(),
                _ => BuildRegisterView()
            };

            if (_currentContent != null)
            {
                _contentContainer.Controls.Remove(_currentContent);
            }

            _currentContent = newContent;
            _currentContent.Dock = DockStyle.Fill;
            _contentContainer.Controls.Add(_currentContent);
            _currentContent.BringToFront();

            _currentView = view;

            MoveActiveIndicator(view switch
            {
                MenuView.Register => _btnMenuRegister,
                MenuView.CheckIn => _btnMenuCheckIn,
                MenuView.Report => _btnMenuReport,
                _ => _btnMenuRegister
            });

            if (view == MenuView.Register)
            {
                if (_videoSource == null || !_videoSource.IsRunning)
                    BtnStart_Click(null, EventArgs.Empty);
            }
            else if (view == MenuView.CheckIn)
            {
                EnsureCameraRunning();
                StartRealtimeCheckInMode();
            }
        }

        private void MoveActiveIndicator(Button? targetButton)
{
    if (targetButton == null || _menuItemsPanel == null || _sidebar == null)
        return;

    foreach (Control ctrl in _menuItemsPanel.Controls)
    {
        if (ctrl is Button btn)
        {
            btn.ForeColor = _mutedText;
            btn.BackColor = Color.Transparent;
        }
    }

    targetButton.ForeColor = _lightText;
    targetButton.BackColor = Color.FromArgb(40, 50, 70);

            if (_activeIndicator != null)
            {
                if (!_activeIndicator.Visible)
                    _activeIndicator.Visible = true;

                _activeIndicator.Height = targetButton.Height;

                var parent = targetButton.Parent;
                if (parent != null)
                {
                    var targetScreenPoint = parent.PointToScreen(targetButton.Location);
                    var sidebarScreenPoint = _sidebar.PointToScreen(Point.Empty);
                    int relativeY = targetScreenPoint.Y - sidebarScreenPoint.Y;

                    _activeIndicator.Top = relativeY;
                    _activeIndicator.Left = 0;
                    _activeIndicator.BringToFront();
                }
            }
        }


        #endregion

        #region Register view

        private Panel BuildRegisterView()
        {
            if (_registerView != null)
                return _registerView;

            var root = new Panel { Dock = DockStyle.Fill, BackColor = _darkBg };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = _darkBg,
                ColumnStyles = { new ColumnStyle(SizeType.Absolute, 380F), new ColumnStyle(SizeType.Percent, 100F) },
                RowStyles = { new RowStyle(SizeType.Percent, 100F) }
            };
            root.Controls.Add(layout);

            var leftPanel = CreateRoundedCard(new Padding(30));
            layout.Controls.Add(leftPanel, 0, 0);
            BuildRegisterLeftPanel(leftPanel);

            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _darkBg,
                Padding = new Padding(20, 0, 0, 0)
            };
            layout.Controls.Add(rightPanel, 1, 0);
            BuildRegisterRightPanel(rightPanel);

            _registerView = root;
            UpdateCaptureState();
            return _registerView;
        }

        private Panel CreateRoundedCard(Padding padding)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _cardBg,
                Padding = padding
            };
            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = GetRoundedRect(panel.ClientRectangle, 16);
                using var brush = new SolidBrush(_cardBg);
                e.Graphics.FillPath(brush, path);
                using var pen = new Pen(_borderColor, 1);
                e.Graphics.DrawPath(pen, path);
            };
            return panel;
        }

        private void BuildRegisterLeftPanel(Panel parent)
        {
            parent.Controls.Clear();

            var titleLabel = new Label
            {
                Text = "📝 Thông tin nhân viên",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = _lightText,
                AutoSize = true,
                Location = new Point(0, 0),
                Margin = new Padding(0, 0, 0, 20)
            };
            parent.Controls.Add(titleLabel);

            int yPos = 45;

            _inputPanel = new Panel
            {
                Location = new Point(0, yPos),
                Size = new Size(parent.Width - 10, 210),
                BackColor = Color.FromArgb(45, 55, 72)
            };
            _inputPanel.Paint += (s, e) => DrawRoundedPanel(e.Graphics, _inputPanel.ClientRectangle, 12);
            parent.Controls.Add(_inputPanel);

            var lblMaNV = new Label
            {
                Text = "Mã nhân viên",
                Location = new Point(20, 20),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10),
                ForeColor = _lightText
            };
            _inputPanel.Controls.Add(lblMaNV);

            _txtMaNV = CreateStyledTextBox(20, 45, 280);
            _inputPanel.Controls.Add(_txtMaNV);

            var lblTenNV = new Label
            {
                Text = "Tên nhân viên",
                Location = new Point(20, 95),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10),
                ForeColor = _lightText
            };
            _inputPanel.Controls.Add(lblTenNV);

            _txtTenNV = CreateStyledTextBox(20, 120, 280);
            _inputPanel.Controls.Add(_txtTenNV);

            var instructionLabel = new Label
            {
                Text = "Nhập thông tin và chụp 5 ảnh theo hướng dẫn",
                Location = new Point(20, 170),
                Size = new Size(280, 30),
                Font = new Font("Segoe UI", 9),
                ForeColor = _mutedText
            };
            _inputPanel.Controls.Add(instructionLabel);

            yPos = _inputPanel.Bottom + 25;

            _lblCameraStatus = CreateStatusLabel(0, yPos, "📷 Camera: Chưa kết nối");
            parent.Controls.Add(_lblCameraStatus);
            yPos += 35;

            _lblInstruction = new Label
            {
                Location = new Point(0, yPos),
                Size = new Size(parent.Width - 20, 60),
                Text = "Nhấn 'Bật Camera' để bắt đầu",
                Font = new Font("Segoe UI Semibold", 11),
                ForeColor = _warningColor
            };
            parent.Controls.Add(_lblInstruction);
            yPos += 65;

            _lblCapturedCount = CreateStatusLabel(0, yPos, "📸 Đã chụp: 0/5 ảnh");
            parent.Controls.Add(_lblCapturedCount);
            yPos += 45;

            _controlPanel = new Panel
            {
                Location = new Point(0, yPos),
                Size = new Size(parent.Width - 10, 260),
                BackColor = Color.Transparent
            };
            parent.Controls.Add(_controlPanel);

            BuildRegisterButtons();
        }

        private TextBox CreateStyledTextBox(int x, int y, int width)
        {
            return new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 35),
                Font = new Font("Segoe UI", 11),
                BackColor = _darkBg,
                ForeColor = _lightText,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private Label CreateStatusLabel(int x, int y, string text)
        {
            return new Label
            {
                Location = new Point(x, y),
                AutoSize = true,
                Text = text,
                Font = new Font("Segoe UI", 10),
                ForeColor = _mutedText
            };
        }

        private void BuildRegisterButtons()
        {
            _btnStart = CreateModernButton("Bật Camera", 0, _primaryColor, 320, 52);
            _btnStart.Click += BtnStart_Click;

            _btnCapture = CreateModernButton("Chụp Ảnh", 60, _successColor, 320, 52);
            _btnCapture.Click += BtnCapture_Click;
            _btnCapture.Enabled = false;

            _btnRegister = CreateModernButton("Đăng Ký", 120, _secondaryColor, 320, 52);
            _btnRegister.Click += BtnRegister_Click;
            _btnRegister.Enabled = false;
            _controlPanel.Controls.AddRange(new Control[]
            {
                _btnStart,
                _btnCapture,
                _btnRegister,
                _btnManualCheckIn
            });
        }

        private Button CreateModernButton(string text, int yPos, Color bgColor, int width = 320, int height = 48)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(0, yPos),
                Size = new Size(width, height),
                BackColor = bgColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = bgColor == _primaryColor ? _primaryHover : ControlPaint.Light(bgColor, 0.1f);
            btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(bgColor, 0.1f);

            btn.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = GetRoundedRect(btn.ClientRectangle, 8);
                using var brush = new SolidBrush(btn.BackColor);
                e.Graphics.FillPath(brush, path);

                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                using var textBrush = new SolidBrush(btn.ForeColor);
                e.Graphics.DrawString(btn.Text, btn.Font, textBrush, btn.ClientRectangle, sf);
            };

            return btn;
        }

        private void BuildRegisterRightPanel(Panel container)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = _darkBg
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 65F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 230F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            container.Controls.Add(layout);

            _previewWrapper = CreateRoundedCard(new Padding(25));
            layout.Controls.Add(_previewWrapper, 0, 0);

            _previewBox = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black,
                Dock = DockStyle.None,
                Size = new Size(960, 540)
            };
            _previewWrapper.Controls.Add(_previewBox);
            _previewWrapper.Resize += (s, e) => AdjustPreview(_previewWrapper, _previewBox);

            var thumbOuterPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _darkBg,
                Padding = new Padding(0, 15, 0, 0)
            };
            layout.Controls.Add(thumbOuterPanel, 0, 1);

            var thumbCard = CreateRoundedCard(new Padding(20));
            thumbCard.Dock = DockStyle.Fill;
            thumbOuterPanel.Controls.Add(thumbCard);

            _thumbPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = false,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _thumbPanel.Resize += ThumbPanel_Resize;
            thumbCard.Controls.Add(_thumbPanel);

            CreateThumbnails();

            var statusPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 15, 0, 0)
            };
            layout.Controls.Add(statusPanel, 0, 2);

            var statusCard = CreateRoundedCard(new Padding(20, 15, 20, 15));
            statusCard.Dock = DockStyle.Fill;
            statusPanel.Controls.Add(statusCard);

            _lblStatus = new Label
            {
                Text = "💡 Sẵn sàng",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 11, FontStyle.Bold),
                ForeColor = _lightText,
                TextAlign = ContentAlignment.MiddleLeft
            };
            statusCard.Controls.Add(_lblStatus);
        }

        #endregion

        #region Check-in view

     private Panel BuildCheckInView()
        {
            if (_checkInView != null)
                return _checkInView;

            var root = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _darkBg,
                Padding = new Padding(0)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = _darkBg,
                ColumnStyles = { new ColumnStyle(SizeType.Absolute, 350F), new ColumnStyle(SizeType.Percent, 100F) }
            };
            root.Controls.Add(layout);

            var leftPanel = CreateRoundedCard(new Padding(30));
            layout.Controls.Add(leftPanel, 0, 0);

            var infoLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent
            };
            leftPanel.Controls.Add(infoLayout);

            var iconLabel = new Label
            {
                Text = "⏰",
                Font = new Font("Segoe UI Emoji", 36, FontStyle.Regular),
                ForeColor = _primaryColor,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 15)
            };
            infoLayout.Controls.Add(iconLabel);

            var lblTitle = new Label
            {
                Text = "Chấm công realtime",
                AutoSize = true,
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = _lightText,
                Margin = new Padding(0, 0, 0, 10)
            };
            infoLayout.Controls.Add(lblTitle);

            var lblDesc = new Label
            {
                Text = "Hệ thống tự động phát hiện người đứng trước camera, kiểm tra liveness và gửi kết quả chấm công.",
                Width = 280,
                AutoSize = false,
                Height = 70,
                Font = new Font("Segoe UI", 10.5F),
                ForeColor = _mutedText,
                Margin = new Padding(0, 0, 0, 25)
            };
            infoLayout.Controls.Add(lblDesc);

            var autoModeLabel = new Label
            {
                Text = "🟢 Chế độ tự động đang bật",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
                ForeColor = _successColor,
                Margin = new Padding(0, 0, 0, 20)
            };
            infoLayout.Controls.Add(autoModeLabel);

            var instructionCard = new Panel
            {
                Width = 280,
                Height = 85,
                BackColor = Color.FromArgb(45, 55, 72),
                Margin = new Padding(0, 0, 0, 10)
            };
            instructionCard.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = GetRoundedRect(instructionCard.ClientRectangle, 12);
                using var brush = new SolidBrush(Color.FromArgb(45, 55, 72));
                e.Graphics.FillPath(brush, path);
            };
            infoLayout.Controls.Add(instructionCard);

            _lblCheckInInstruction = new Label
            {
                Text = _currentInstructionText,
                Location = new Point(15, 15),
                MaximumSize = new Size(250, 0),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10.5F),
                ForeColor = _mutedText
            };
            instructionCard.Controls.Add(_lblCheckInInstruction);

            var statusCard = new Panel
            {
                Width = 280,
                Height = 85,
                BackColor = Color.FromArgb(45, 55, 72),
                Margin = new Padding(0)
            };
            statusCard.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = GetRoundedRect(statusCard.ClientRectangle, 12);
                using var brush = new SolidBrush(Color.FromArgb(45, 55, 72));
                e.Graphics.FillPath(brush, path);
            };
            infoLayout.Controls.Add(statusCard);

            _lblCheckInStatus = new Label
            {
                Text = "💡 Trạng thái: Chưa bắt đầu",
                Location = new Point(15, 15),
                MaximumSize = new Size(250, 0),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10.5F),
                ForeColor = _mutedText
            };
            statusCard.Controls.Add(_lblCheckInStatus);

            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _darkBg,
                Padding = new Padding(20, 0, 0, 0)
            };
            layout.Controls.Add(rightPanel, 1, 0);

            _checkInPreviewWrapper = CreateRoundedCard(new Padding(25));
            rightPanel.Controls.Add(_checkInPreviewWrapper);

            _checkInPreviewBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            _checkInPreviewWrapper.Controls.Add(_checkInPreviewBox);
            _checkInPreviewWrapper.Resize += (s, e) => AdjustPreview(_checkInPreviewWrapper, _checkInPreviewBox);

            _checkInView = root;
            return _checkInView;
        }

            private Panel BuildReportView()
    {
        if (_reportView != null)
            return _reportView;

        var root = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _darkBg,
            Padding = new Padding(0)
        };

        var mainCard = CreateRoundedCard(new Padding(30));
        mainCard.Dock = DockStyle.Fill;
        root.Controls.Add(mainCard);

        var innerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            BackColor = Color.Transparent
        };
        innerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
        innerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 75F));
        innerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        innerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        mainCard.Controls.Add(innerLayout);

        var headerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        innerLayout.Controls.Add(headerPanel, 0, 0);

        var lblTitle = new Label
        {
            Text = "Báo cáo chấm công",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = _lightText,
            AutoSize = true,
            Location = new Point(50, 15)
        };
        headerPanel.Controls.Add(lblTitle);

    var filterPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(45, 55, 72),
                    Padding = new Padding(25, 15, 25, 15)
                };
                filterPanel.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = GetRoundedRect(filterPanel.ClientRectangle, 12);
                    using var brush = new SolidBrush(Color.FromArgb(45, 55, 72));
                    e.Graphics.FillPath(brush, path);
                };
                innerLayout.Controls.Add(filterPanel, 0, 1);

                var iconFrom = new Label
                {
                    Text = "📅",
                    Font = new Font("Segoe UI Emoji", 14),
                    Location = new Point(0, 15),
                    Size = new Size(30, 35),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                filterPanel.Controls.Add(iconFrom);

                var fromLabel = new Label
                {
                    Text = "Từ ngày:",
                    Location = new Point(35, 17),
                    Width = 75,
                    ForeColor = _lightText,
                    Font = new Font("Segoe UI Semibold", 10.5F),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                filterPanel.Controls.Add(fromLabel);

                _dtpReportFrom = new DateTimePicker
                {
                    Location = new Point(115, 12),
                    Width = 190,
                    Height = 38,
                    Value = DateTime.Today.AddDays(-7),
                    Format = DateTimePickerFormat.Custom,
                    CustomFormat = "dd/MM/yyyy",
                    Font = new Font("Segoe UI", 11),
                    CalendarMonthBackground = _darkBg,
                    CalendarForeColor = _lightText
                };
                filterPanel.Controls.Add(_dtpReportFrom);

                var iconTo = new Label
                {
                    Text = "📅",
                    Font = new Font("Segoe UI Emoji", 14),
                    Location = new Point(325, 15),
                    Size = new Size(30, 35),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                filterPanel.Controls.Add(iconTo);

                var toLabel = new Label
                {
                    Text = "Đến ngày:",
                    Location = new Point(360, 17),
                    Width = 85,
                    ForeColor = _lightText,
                    Font = new Font("Segoe UI Semibold", 10.5F),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                filterPanel.Controls.Add(toLabel);

                _dtpReportTo = new DateTimePicker
                {
                    Location = new Point(450, 12),
                    Width = 190,
                    Height = 38,
                    Value = DateTime.Today,
                    Format = DateTimePickerFormat.Custom,
                    CustomFormat = "dd/MM/yyyy",
                    Font = new Font("Segoe UI", 11),
                    CalendarMonthBackground = _darkBg,
                    CalendarForeColor = _lightText
                };
                filterPanel.Controls.Add(_dtpReportTo);

                _btnReportReload = CreateModernButton("Làm mới", 0, _primaryColor, 155, 38);
                _btnReportReload.Location = new Point(665, 12);
                _btnReportReload.Click += async (s, e) => await LoadReportAsync();
                filterPanel.Controls.Add(_btnReportReload);

        var gridContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(0)
        };
        gridContainer.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = GetRoundedRect(gridContainer.ClientRectangle, 10);
            e.Graphics.SetClip(path);
        };
        innerLayout.Controls.Add(gridContainer, 0, 2);

        _gridReport = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            EditMode = DataGridViewEditMode.EditProgrammatically,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 45,
            RowTemplate = { Height = 40 },
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = _primaryColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
                Padding = new Padding(10, 0, 0, 0),
                Alignment = DataGridViewContentAlignment.MiddleLeft
            },
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 41, 59),
                SelectionBackColor = Color.FromArgb(224, 231, 255),
                SelectionForeColor = Color.FromArgb(30, 41, 59),
                Font = new Font("Segoe UI", 10),
                Padding = new Padding(10, 5, 5, 5)
            },
            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(248, 250, 252)
            },
            GridColor = Color.FromArgb(226, 232, 240),
            RowHeadersVisible = false
        };
        gridContainer.Controls.Add(_gridReport);
        _gridReport.Columns.AddRange(new[]
        {
            new DataGridViewTextBoxColumn { DataPropertyName = nameof(ReportRow.MaNV), HeaderText = "Mã nhân viên" },
            new DataGridViewTextBoxColumn { DataPropertyName = nameof(ReportRow.TenNV), HeaderText = "Tên nhân viên" },
            new DataGridViewTextBoxColumn { DataPropertyName = nameof(ReportRow.Ngay), HeaderText = "Ngày làm việc" },
            new DataGridViewTextBoxColumn { DataPropertyName = nameof(ReportRow.GioVao), HeaderText = "Giờ vào" },
            new DataGridViewTextBoxColumn { DataPropertyName = nameof(ReportRow.GioRa), HeaderText = "Giờ ra" }
        });

        _lblReportStatus = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = _mutedText,
            Font = new Font("Segoe UI", 10),
            Text = "💡 Chọn ngày rồi nhấn \"Làm mới\" để xem báo cáo."
        };
        innerLayout.Controls.Add(_lblReportStatus, 0, 3);

        _reportView = root;
        _ = LoadReportAsync();

        return _reportView;
    }

        #endregion

        #region Report view
        // (Không đổi so với phiên bản trước)
        // ... phần BuildReportView và các helper cho report vẫn giữ nguyên ...
        #endregion

        #region Helpers (preview/thumbnail/draw)

        private void AdjustPreview(Panel wrapper, PictureBox pic)
        {
            if (wrapper == null || pic == null) return;

            var availableWidth = wrapper.ClientSize.Width - wrapper.Padding.Horizontal;
            var availableHeight = wrapper.ClientSize.Height - wrapper.Padding.Vertical;
            if (availableWidth <= 0 || availableHeight <= 0) return;

            int targetWidth = availableWidth;
            int targetHeight = (int)(targetWidth / PreviewAspectRatio);

            if (targetHeight > availableHeight)
            {
                targetHeight = availableHeight;
                targetWidth = (int)(targetHeight * PreviewAspectRatio);
            }

            pic.Size = new Size(targetWidth, targetHeight);

            var baseOffsetX = (availableWidth - targetWidth) / 2;
            var shiftX = Math.Min(PreviewHorizontalShift, baseOffsetX);

            var offsetX = wrapper.Padding.Left + baseOffsetX + shiftX;
            var offsetY = wrapper.Padding.Top + (availableHeight - targetHeight) / 2;

            pic.Location = new Point(offsetX, offsetY);
        }

        private void CreateThumbnails()
        {
            _captureBoxes = new PictureBox[5];
            int totalThumbs = 5;
            int spacing = 12;

            _thumbPanel.SuspendLayout();
            _thumbPanel.Controls.Clear();

            int availableWidth = _thumbPanel.Width - _thumbPanel.Padding.Horizontal;
            int totalSpacing = spacing * (totalThumbs - 1);
            int thumbWidth = (availableWidth - totalSpacing) / totalThumbs;
            int thumbHeight = 180;

            if (thumbWidth < 140)
            {
                thumbWidth = 140;
                _thumbPanel.AutoScroll = true;
            }
            else
            {
                _thumbPanel.AutoScroll = false;
            }

            for (int i = 0; i < totalThumbs; i++)
            {
                var container = new Panel
                {
                    Width = thumbWidth,
                    Height = thumbHeight,
                    Margin = new Padding(i == 0 ? 0 : spacing / 2, 0, i == totalThumbs - 1 ? 0 : spacing / 2, 0),
                    BackColor = Color.FromArgb(45, 55, 72)
                };
                container.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = GetRoundedRect(container.ClientRectangle, 10);
                    using var brush = new SolidBrush(Color.FromArgb(45, 55, 72));
                    e.Graphics.FillPath(brush, path);
                };

                var thumb = new PictureBox
                {
                    Location = new Point(10, 10),
                    Size = new Size(thumbWidth - 20, thumbHeight - 55),
                    BorderStyle = BorderStyle.None,
                    BackColor = _darkBg,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Cursor = Cursors.Hand,
                    Tag = i
                };
                thumb.Click += Thumbnail_Click;
                thumb.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = GetRoundedRect(thumb.ClientRectangle, 8);
                    e.Graphics.SetClip(path);
                };

                var lblStep = new Label
                {
                    Location = new Point(0, thumbHeight - 40),
                    Size = new Size(thumbWidth, 35),
                    Text = $"{i + 1}. {_captureSteps[i]}",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold),
                    ForeColor = _mutedText,
                    BackColor = Color.Transparent
                };

                container.Controls.Add(thumb);
                container.Controls.Add(lblStep);
                _thumbPanel.Controls.Add(container);
                _captureBoxes[i] = thumb;
            }

            _thumbPanel.ResumeLayout();
            _thumbPanel.PerformLayout();
        }

        private void ThumbPanel_Resize(object? sender, EventArgs e)
        {
            if (_thumbPanel == null || _thumbPanel.Controls.Count == 0) return;

            int totalThumbs = 5;
            int spacing = 12;
            int availableWidth = _thumbPanel.Width - _thumbPanel.Padding.Horizontal;
            int totalSpacing = spacing * (totalThumbs - 1);
            int thumbWidth = (availableWidth - totalSpacing) / totalThumbs;
            int thumbHeight = 180;

            if (thumbWidth < 140)
            {
                thumbWidth = 140;
                _thumbPanel.AutoScroll = true;
            }
            else
            {
                _thumbPanel.AutoScroll = false;
            }

            for (int i = 0; i < _thumbPanel.Controls.Count; i++)
            {
                if (_thumbPanel.Controls[i] is Panel container)
                {
                    container.Width = thumbWidth;
                    container.Height = thumbHeight;
                    container.Margin = new Padding(i == 0 ? 0 : spacing / 2, 0, i == totalThumbs - 1 ? 0 : spacing / 2, 0);

                    foreach (Control ctrl in container.Controls)
                    {
                        if (ctrl is PictureBox thumb)
                        {
                            thumb.Size = new Size(thumbWidth - 20, thumbHeight - 55);
                        }
                        else if (ctrl is Label lbl)
                        {
                            lbl.Location = new Point(0, thumbHeight - 40);
                            lbl.Size = new Size(thumbWidth, 35);
                        }
                    }
                }
            }
        }

        private void DrawRoundedPanel(Graphics g, Rectangle rect, int radius)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = GetRoundedRect(rect, radius);
            using var brush = new SolidBrush(_inputPanel.BackColor);
            g.FillPath(brush, path);
        }

        private GraphicsPath GetRoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        #endregion
    }
}