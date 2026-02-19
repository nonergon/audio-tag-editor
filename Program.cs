using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Reflection; // Нужно для работы с ресурсами
using TagLib;

namespace SimpleTagEditor
{
    public class LogForm : Form
    {
        public RichTextBox LogBox;
        public LogForm()
        {
            this.Text = "System Debug Output";
            this.Size = new Size(500, 400);
            this.BackColor = Color.FromArgb(20, 20, 20);
            this.StartPosition = FormStartPosition.CenterScreen;
            LogBox = new RichTextBox { 
                Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 20, 20), 
                ForeColor = Color.LimeGreen, ReadOnly = true, BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9) 
            };
            this.Controls.Add(LogBox);
            
            // Загрузка иконки из ресурсов
            LoadIconFromResource(this);

            this.FormClosing += (s, e) => { e.Cancel = true; this.Hide(); };
        }

        private void LoadIconFromResource(Form form)
        {
            try {
                // Пытаемся найти иконку в ресурсах сборки
                var assembly = Assembly.GetExecutingAssembly();
                // Ищем ресурс, который заканчивается на icon.ico
                string resourceName = Array.Find(assembly.GetManifestResourceNames(), name => name.EndsWith("icon.ico"));
                if (!string.IsNullOrEmpty(resourceName))
                {
                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null) form.Icon = new Icon(stream);
                    }
                }
            } catch { /* Игнорируем, если иконка не зашита */ }
        }

        public void AddLog(string msg) {
            if (LogBox.InvokeRequired) LogBox.Invoke(new Action(() => AddLog(msg)));
            else LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
        }
    }

    public class MainForm : Form
    {
        private readonly Color ClrBack = Color.FromArgb(15, 15, 15);
        private readonly Color ClrPanel = Color.FromArgb(28, 28, 28);
        private readonly Color ClrInput = Color.FromArgb(40, 40, 40);
        private readonly Color ClrText = Color.FromArgb(200, 200, 200);
        private readonly Color ClrAccent = Color.FromArgb(0, 150, 250); 
        private readonly Color ClrDim = Color.FromArgb(120, 120, 120); 

        private Panel dropZone = null!;
        private PictureBox picCover = null!;
        private Button btnSave = null!, btnClear = null!, btnExtractCover = null!, btnDebug = null!;
        private Label lblStatus = null!;
        private LogForm debugWindow = new LogForm();
        
        private System.Windows.Forms.TextBox txtTitle = null!, txtArtist = null!, txtAlbum = null!, txtYear = null!, 
            txtGenre = null!, txtTrack = null!, txtDisc = null!, txtBpm = null!, txtComposer = null!, txtComment = null!;

        private string? currentSourcePath;
        private byte[]? pendingCoverData;

        public MainForm()
        {
            SetupWindow();
            InitializeUI();
            debugWindow.AddLog("Application Initialized.");
        }

        private void SetupWindow()
        {
            this.Text = "  Audio Tag Editor v. 1.0.0";
            this.Size = new Size(1200, 680);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = ClrBack;
            this.ForeColor = ClrText;
            this.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            this.StartPosition = FormStartPosition.CenterScreen;

            // ГАРАНТИРОВАННАЯ ЗАГРУЗКА ИКОНКИ ИЗ РЕСУРСОВ
            try {
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = Array.Find(assembly.GetManifestResourceNames(), name => name.EndsWith("icon.ico"));
                if (!string.IsNullOrEmpty(resourceName))
                {
                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null) this.Icon = new Icon(stream);
                    }
                }
            } catch (Exception ex) {
                debugWindow.AddLog("Resource Icon error: " + ex.Message);
            }
        }

        private void InitializeUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(20) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));

            dropZone = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = ClrPanel, AllowDrop = true, Cursor = Cursors.Hand };
            var lblDrop = new Label { 
                Text = "DRAG AUDIO FILE HERE", 
                TextAlign = ContentAlignment.MiddleCenter, 
                Dock = DockStyle.Fill, 
                Font = new Font("Segoe UI", 12, FontStyle.Bold), 
                ForeColor = ClrDim, 
                Enabled = true      
            };
            
            lblDrop.Click += (s, e) => SelectAudioFile();
            dropZone.Controls.Add(lblDrop);
            dropZone.Click += (s, e) => SelectAudioFile();
            dropZone.DragEnter += (s, e) => { if (e.Data!.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; dropZone.BackColor = ClrInput; };
            dropZone.DragLeave += (s, e) => dropZone.BackColor = ClrPanel;
            dropZone.DragDrop += (s, e) => { 
                dropZone.BackColor = ClrPanel;
                var files = (string[]?)e.Data?.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0) LoadAudioFile(files[0]);
            };

            var contentLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300F));
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var coverLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            picCover = new PictureBox { Size = new Size(280, 280), BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom, BackColor = ClrPanel, Margin = new Padding(0,25,0,10), Cursor = Cursors.Hand };
            picCover.Click += (s, e) => SelectCoverImage();

            btnExtractCover = CreateButton("EXTRACT COVER", Color.FromArgb(50, 50, 50), false);
            btnExtractCover.Width = 280;
            btnExtractCover.Enabled = false;
            btnExtractCover.Click += (s, e) => ExtractCover();
            coverLayout.Controls.AddRange(new Control[] { picCover, btnExtractCover });

            var fieldsGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 10, Padding = new Padding(20, 10, 0, 0) };
            fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
            fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
            fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

            txtTitle = AddField(fieldsGrid, "Title", 0, 0, 3);
            txtArtist = AddField(fieldsGrid, "Artist", 1, 0, 3);
            txtAlbum = AddField(fieldsGrid, "Album", 2, 0, 3);
            txtComposer = AddField(fieldsGrid, "Composer", 3, 0, 3);
            txtGenre = AddField(fieldsGrid, "Genre", 4, 0, 3);
            txtYear = AddField(fieldsGrid, "Year", 5, 0);
            txtBpm = AddField(fieldsGrid, "BPM", 5, 2);
            txtTrack = AddField(fieldsGrid, "Track #", 6, 0);
            txtDisc = AddField(fieldsGrid, "Disc #", 6, 2);
            txtComment = AddField(fieldsGrid, "Note", 7, 0, 3);

            contentLayout.Controls.Add(coverLayout, 0, 0);
            contentLayout.Controls.Add(fieldsGrid, 1, 0);

            var bottomLayout = new Panel { Dock = DockStyle.Fill };
            btnSave = CreateButton("SAVE AS", ClrAccent, true);
            btnSave.Location = new Point(940, 15);
            btnSave.Size = new Size(180, 40);
            btnSave.Enabled = false;

            btnClear = CreateButton("CLEAR ALL", ClrAccent, false);
            btnClear.Location = new Point(740, 15);
            btnClear.Size = new Size(180, 40);

            btnDebug = CreateButton("DEBUG", Color.FromArgb(40, 40, 40), false);
            btnDebug.Location = new Point(620, 15);
            btnDebug.Size = new Size(100, 40);
            btnDebug.Click += (s, e) => debugWindow.Show();

            lblStatus = new Label { Text = "Ready", AutoSize = false, Width = 400, Height = 40, Location = new Point(0, 15), TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(100, 100, 100) };

            btnSave.Click += OnSaveClick;
            btnClear.Click += (s, e) => ClearAll();
            
            bottomLayout.Controls.AddRange(new Control[] { btnSave, btnClear, btnDebug, lblStatus });

            mainLayout.Controls.Add(dropZone, 0, 0);
            mainLayout.Controls.Add(contentLayout, 0, 1);
            mainLayout.Controls.Add(bottomLayout, 0, 2);
            this.Controls.Add(mainLayout);
        }

        private void SelectAudioFile() {
            using var ofd = new OpenFileDialog { Filter = "Audio Files|*.mp3;*.flac;*.wav;*.m4a;*.ogg" };
            if (ofd.ShowDialog() == DialogResult.OK) LoadAudioFile(ofd.FileName);
        }

        private void SelectCoverImage() {
            using var ofd = new OpenFileDialog { Filter = "Images|*.jpg;*.png;*.jpeg" };
            if (ofd.ShowDialog() == DialogResult.OK) {
                try {
                    pendingCoverData = System.IO.File.ReadAllBytes(ofd.FileName);
                    using var ms = new MemoryStream(pendingCoverData);
                    picCover.Image = Image.FromStream(ms);
                    debugWindow.AddLog("New cover image selected.");
                } catch { debugWindow.AddLog("Failed to load image."); }
            }
        }

        private void ClearAll() {
            debugWindow.AddLog("Form cleared.");
            txtTitle.Clear(); txtArtist.Clear(); txtAlbum.Clear(); txtYear.Clear();
            txtGenre.Clear(); txtTrack.Clear(); txtDisc.Clear(); txtBpm.Clear();
            txtComposer.Clear(); txtComment.Clear();
            picCover.Image = null;
            pendingCoverData = null;
            btnExtractCover.Enabled = false;
        }

        private System.Windows.Forms.TextBox AddField(TableLayoutPanel panel, string labelText, int row, int col, int colSpan = 1)
        {
            panel.Controls.Add(new Label { Text = labelText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = Color.Gray }, col, row);
            var tb = new System.Windows.Forms.TextBox { Dock = DockStyle.Fill, BackColor = ClrInput, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 5, 0, 5), Font = new Font("Segoe UI", 10) };
            panel.Controls.Add(tb, col + 1, row);
            if (colSpan > 1) panel.SetColumnSpan(tb, colSpan);
            return tb;
        }

        private Button CreateButton(string text, Color color, bool bold)
        {
            var btn = new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = color, ForeColor = Color.White, Font = new Font("Segoe UI", 9, bold ? FontStyle.Bold : FontStyle.Regular), Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => btn.BackColor = ControlPaint.Light(color);
            btn.MouseLeave += (s, e) => btn.BackColor = color;
            return btn;
        }

        private void LoadAudioFile(string path)
        {
            try {
                debugWindow.AddLog($"Loading: {Path.GetFileName(path)}");
                currentSourcePath = path;
                using var tfile = TagLib.File.Create(path);
                txtTitle.Text = tfile.Tag.Title ?? "";
                txtArtist.Text = string.Join("; ", tfile.Tag.Performers);
                txtAlbum.Text = tfile.Tag.Album ?? "";
                txtYear.Text = tfile.Tag.Year != 0 ? tfile.Tag.Year.ToString() : "";
                txtGenre.Text = string.Join("; ", tfile.Tag.Genres);
                txtTrack.Text = tfile.Tag.Track != 0 ? tfile.Tag.Track.ToString() : "";
                txtDisc.Text = tfile.Tag.Disc != 0 ? tfile.Tag.Disc.ToString() : "";
                txtBpm.Text = tfile.Tag.BeatsPerMinute != 0 ? tfile.Tag.BeatsPerMinute.ToString() : "";
                txtComposer.Text = string.Join("; ", tfile.Tag.Composers);
                txtComment.Text = tfile.Tag.Comment ?? "";

                if (tfile.Tag.Pictures.Length > 0) {
                    pendingCoverData = tfile.Tag.Pictures[0].Data.Data;
                    using var ms = new MemoryStream(pendingCoverData);
                    picCover.Image = Image.FromStream(ms);
                    btnExtractCover.Enabled = true;
                } else {
                    picCover.Image = null;
                    btnExtractCover.Enabled = false;
                }
                btnSave.Enabled = true;
                lblStatus.Text = "LOADED: " + Path.GetFileName(path);
            } catch (Exception ex) { debugWindow.AddLog("ERROR: " + ex.Message); }
        }

        private void ExtractCover() {
            if (pendingCoverData == null) return;
            using var sfd = new SaveFileDialog { Filter = "JPEG|*.jpg", FileName = "cover" };
            if (sfd.ShowDialog() == DialogResult.OK) {
                System.IO.File.WriteAllBytes(sfd.FileName, pendingCoverData);
                debugWindow.AddLog("Cover extracted.");
            }
        }

        private void OnSaveClick(object? sender, EventArgs e)
        {
            if (currentSourcePath == null) return;
            using var sfd = new SaveFileDialog { Filter = "Audio|*" + Path.GetExtension(currentSourcePath), FileName = Path.GetFileName(currentSourcePath) };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try {
                    System.IO.File.Copy(currentSourcePath, sfd.FileName, true);
                    using var tfile = TagLib.File.Create(sfd.FileName);
                    tfile.Tag.Title = txtTitle.Text;
                    tfile.Tag.Performers = txtArtist.Text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    tfile.Tag.Album = txtAlbum.Text;
                    tfile.Tag.Year = uint.TryParse(txtYear.Text, out var y) ? y : 0;
                    tfile.Tag.Genres = txtGenre.Text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    tfile.Tag.Track = uint.TryParse(txtTrack.Text, out var t) ? t : 0;
                    tfile.Tag.Disc = uint.TryParse(txtDisc.Text, out var d) ? d : 0;
                    tfile.Tag.BeatsPerMinute = uint.TryParse(txtBpm.Text, out var b) ? b : 0;
                    tfile.Tag.Composers = txtComposer.Text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    tfile.Tag.Comment = txtComment.Text;

                    if (pendingCoverData != null)
                        tfile.Tag.Pictures = new TagLib.IPicture[] { new TagLib.Picture(new TagLib.ByteVector(pendingCoverData)) };
                    
                    tfile.Save();
                    debugWindow.AddLog("File saved.");
                    MessageBox.Show("Success!");
                } catch (Exception ex) { debugWindow.AddLog("SAVE ERROR: " + ex.Message); }
            }
        }

        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}