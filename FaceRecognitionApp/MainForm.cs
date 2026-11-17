using OpenCvSharp;
using FaceRecognitionApp.Models;
using FaceRecognitionApp.Services;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace FaceRecognitionApp;

public sealed class MainForm : Form
{
    private readonly SampleDatasetService _datasetService;
    private readonly FaceVisionService _visionService;
    private readonly System.Windows.Forms.Timer _cameraTimer;

    private VideoCapture? _videoCapture;
    private Mat? _currentRawFrame;
    private IReadOnlyList<DetectedFaceInfo> _lastDetections = [];

    private readonly PictureBox _picViewport;
    private readonly Label _lblSystemModeBadge;
    private readonly Button _btnCameraStart;
    private readonly Button _btnCameraStop;
    private readonly Button _btnSelectImage;
    private readonly ComboBox _cmbSampleSelector;
    private readonly Button _btnSaveCroppedFaces;

    // Sağ Panel Kontrolleri
    private readonly Label _lblPrimaryPrediction;
    private readonly Label _lblPrimaryConfidence;
    private readonly ListBox _lstDetectedFaces;
    private readonly ListBox _lstDatasetClasses;
    private readonly TextBox _txtNewLabel;
    private readonly Button _btnAddTrainingSample;
    private readonly Label _lblFooterStatus;

    public MainForm()
    {
        _datasetService = new SampleDatasetService();
        _visionService = new FaceVisionService(_datasetService);

        Text = "YMH219 — OpenCV ile Yüz Algılama ve Tanıma (Biometric Vision Studio)";
        ClientSize = new Size(1010, 675);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(9, 20, 26);
        ForeColor = Color.FromArgb(226, 232, 240);

        // 1. Üst Başlık Barı (Teal Biometric Header)
        Panel headerPanel = new()
        {
            Dock = DockStyle.Top,
            Height = 74,
            BackColor = Color.FromArgb(13, 43, 48)
        };
        Controls.Add(headerPanel);

        Panel tealTopStripe = new()
        {
            Dock = DockStyle.Top,
            Height = 4,
            BackColor = Color.FromArgb(20, 184, 166)
        };
        headerPanel.Controls.Add(tealTopStripe);

        Label lblTitle = new()
        {
            Text = "👁️  OPENCV YÜZ ALGILAMA VE TANIMA SİSTEMİ",
            Font = new Font("Segoe UI", 14.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(94, 234, 212),
            Location = new Point(20, 12),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblTitle);

        Label lblSubtitle = new()
        {
            Text = "OpenCvSharp4 · Kamera ve Görüntü İşleme · Çoklu Yüz İşaretleme · Yerel Gönüllü Veri Kümesi & Güven Skoru",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(153, 246, 228),
            Location = new Point(22, 42),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblSubtitle);

        _lblSystemModeBadge = new Label
        {
            Text = "● MOD: GÖRÜNTÜ ANALİZİ AKTİF",
            Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
            ForeColor = Color.FromArgb(4, 47, 46),
            BackColor = Color.FromArgb(94, 234, 212),
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(724, 20),
            Size = new Size(268, 34)
        };
        headerPanel.Controls.Add(_lblSystemModeBadge);

        // 2. Sol/Orta Görüntüleme ve Kontrol Paneli
        Panel viewportContainer = new()
        {
            Location = new Point(16, 88),
            Size = new Size(612, 540),
            BackColor = Color.FromArgb(15, 31, 38)
        };
        Controls.Add(viewportContainer);

        Label lblViewportTitle = new()
        {
            Text = "KAMERA / SEÇİLEN GÖRÜNTÜ EKRANI (PictureBox - OpenCV Çıktısı):",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(45, 212, 191),
            Location = new Point(16, 10),
            AutoSize = true
        };
        viewportContainer.Controls.Add(lblViewportTitle);

        _picViewport = new PictureBox
        {
            Location = new Point(16, 34),
            Size = new Size(580, 412),
            BackColor = Color.FromArgb(8, 15, 20),
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        viewportContainer.Controls.Add(_picViewport);

        // Kamera ve Görüntü Seçim Butonları (Asgari + İsteğe Bağlı Gereksinimler)
        _btnCameraStart = CreateVisionButton("📷 Kamera Başlat", 16, 456, 136, 34, Color.FromArgb(13, 148, 136), Color.White);
        _btnCameraStart.Click += (_, _) => HandleStartCamera();
        viewportContainer.Controls.Add(_btnCameraStart);

        _btnCameraStop = CreateVisionButton("⏹ Kamera Durdur", 158, 456, 136, 34, Color.FromArgb(71, 85, 105), Color.White);
        _btnCameraStop.Enabled = false;
        _btnCameraStop.Click += (_, _) => HandleStopCamera();
        viewportContainer.Controls.Add(_btnCameraStop);

        _btnSelectImage = CreateVisionButton("📂 Görüntü Seç...", 300, 456, 138, 34, Color.FromArgb(14, 116, 144), Color.White);
        _btnSelectImage.Click += (_, _) => HandleSelectImageFromFile();
        viewportContainer.Controls.Add(_btnSelectImage);

        _btnSaveCroppedFaces = CreateVisionButton("💾 Yüzleri Kaydet", 444, 456, 152, 34, Color.FromArgb(217, 119, 6), Color.White);
        _btnSaveCroppedFaces.Click += (_, _) => HandleSaveCroppedFaces();
        viewportContainer.Controls.Add(_btnSaveCroppedFaces);

        Label lblSample = new()
        {
            Text = "Yerel Örnek Küme:",
            Font = new Font("Segoe UI Semibold", 9f),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(16, 502),
            AutoSize = true
        };
        viewportContainer.Controls.Add(lblSample);

        _cmbSampleSelector = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9.2f),
            BackColor = Color.FromArgb(22, 46, 56),
            ForeColor = Color.White,
            Location = new Point(136, 498),
            Size = new Size(320, 26)
        };
        _cmbSampleSelector.Items.AddRange(
        [
            "Örnek 1 — Gönüllü Sınıf #1 (Fatma Portre)",
            "Örnek 2 — Gönüllü Sınıf #2 (Ahmet Portre)",
            "Örnek 3 — Çoklu Yüz Algılama ve Tanıma Testi (2 Kişi)"
        ]);
        _cmbSampleSelector.SelectedIndex = 0;
        _cmbSampleSelector.SelectedIndexChanged += (_, _) => LoadSelectedSampleImage(_cmbSampleSelector.SelectedIndex);
        viewportContainer.Controls.Add(_cmbSampleSelector);

        Button btnReloadSample = CreateVisionButton("🔄 Analiz Et", 464, 496, 132, 30, Color.FromArgb(20, 184, 166), Color.FromArgb(4, 47, 46));
        btnReloadSample.Click += (_, _) => LoadSelectedSampleImage(_cmbSampleSelector.SelectedIndex);
        viewportContainer.Controls.Add(btnReloadSample);

        // 3. Sağ Panel: Tahmin Sonuçları, Güven Skoru ve Eğitim/Veri Ekleme Ekranı
        Panel rightSidebar = new()
        {
            Location = new Point(640, 88),
            Size = new Size(354, 540),
            BackColor = Color.FromArgb(15, 31, 38)
        };
        Controls.Add(rightSidebar);

        Label lblResultHeader = new()
        {
            Text = "🎯 ALGILAMA VE TANIMA SONUCU",
            Font = new Font("Segoe UI", 10.2f, FontStyle.Bold),
            ForeColor = Color.FromArgb(94, 234, 212),
            Location = new Point(16, 12),
            AutoSize = true
        };
        rightSidebar.Controls.Add(lblResultHeader);

        // Birincil Etiket ve Güven Skoru Kartı
        Panel predictionCard = new()
        {
            Location = new Point(16, 38),
            Size = new Size(322, 92),
            BackColor = Color.FromArgb(9, 20, 26)
        };
        rightSidebar.Controls.Add(predictionCard);

        Panel leftBar = new()
        {
            Dock = DockStyle.Left,
            Width = 5,
            BackColor = Color.FromArgb(20, 184, 166)
        };
        predictionCard.Controls.Add(leftBar);

        Label lblPredCaption = new()
        {
            Text = "TAHMİN EDİLEN ETİKET / KİŞİ SINIFI:",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(14, 8),
            AutoSize = true
        };
        predictionCard.Controls.Add(lblPredCaption);

        _lblPrimaryPrediction = new Label
        {
            Text = "Fatma_Ornek1",
            Font = new Font("Segoe UI", 15.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(94, 234, 212),
            Location = new Point(12, 26),
            Size = new Size(298, 32)
        };
        predictionCard.Controls.Add(_lblPrimaryPrediction);

        _lblPrimaryConfidence = new Label
        {
            Text = "Güven Skoru: %96.4  |  Algılanan Yüz: 1",
            Font = new Font("Consolas", 9.8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(253, 224, 71),
            Location = new Point(14, 62),
            AutoSize = true
        };
        predictionCard.Controls.Add(_lblPrimaryConfidence);

        Label lblDetectedListHeader = new()
        {
            Text = "KAREDEKİ TÜM YÜZLER VE KOORDİNATLAR:",
            Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(16, 140),
            AutoSize = true
        };
        rightSidebar.Controls.Add(lblDetectedListHeader);

        _lstDetectedFaces = new ListBox
        {
            Location = new Point(16, 162),
            Size = new Size(322, 118),
            Font = new Font("Consolas", 8.8f),
            BackColor = Color.FromArgb(9, 20, 26),
            ForeColor = Color.FromArgb(167, 243, 208),
            BorderStyle = BorderStyle.FixedSingle,
            HorizontalScrollbar = true
        };
        rightSidebar.Controls.Add(_lstDetectedFaces);

        // Basit Eğitim / Veri Ekleme Bölümü (İsteğe Bağlı Geliştirme)
        Panel trainingPanel = new()
        {
            Location = new Point(16, 292),
            Size = new Size(322, 234),
            BackColor = Color.FromArgb(19, 39, 48)
        };
        rightSidebar.Controls.Add(trainingPanel);

        Label lblTrainTitle = new()
        {
            Text = "🧠 YEREL ÖRNEK KÜME & EĞİTİM EKLEME",
            Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
            ForeColor = Color.FromArgb(253, 224, 71),
            Location = new Point(12, 10),
            AutoSize = true
        };
        trainingPanel.Controls.Add(lblTrainTitle);

        Label lblClassesSub = new()
        {
            Text = "Tanımlı Gönüllü Sınıfları / Etiketler:",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(203, 213, 225),
            Location = new Point(12, 32),
            AutoSize = true
        };
        trainingPanel.Controls.Add(lblClassesSub);

        _lstDatasetClasses = new ListBox
        {
            Location = new Point(12, 52),
            Size = new Size(298, 96),
            Font = new Font("Consolas", 9.2f),
            BackColor = Color.FromArgb(9, 20, 26),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        trainingPanel.Controls.Add(_lstDatasetClasses);

        Label lblNewTag = new()
        {
            Text = "Yeni Sınıf / Kişi Etiketi:",
            Font = new Font("Segoe UI Semibold", 8.8f),
            ForeColor = Color.FromArgb(203, 213, 225),
            Location = new Point(12, 156),
            AutoSize = true
        };
        trainingPanel.Controls.Add(lblNewTag);

        _txtNewLabel = new TextBox
        {
            Location = new Point(12, 176),
            Size = new Size(176, 26),
            Font = new Font("Segoe UI", 9.5f),
            BackColor = Color.FromArgb(9, 20, 26),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Örn: Mehmet_Ornek4"
        };
        trainingPanel.Controls.Add(_txtNewLabel);

        _btnAddTrainingSample = CreateVisionButton("➕ Eğitime Ekle", 196, 174, 114, 28, Color.FromArgb(16, 185, 129), Color.White);
        _btnAddTrainingSample.Click += (_, _) => HandleAddTrainingSample();
        trainingPanel.Controls.Add(_btnAddTrainingSample);

        Label lblSecurityNote = new()
        {
            Text = "Etik Sınır: Yalnızca yerel ve gönüllü örneklerle çalışır.",
            Font = new Font("Segoe UI", 7.8f, FontStyle.Italic),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(12, 208),
            AutoSize = true
        };
        trainingPanel.Controls.Add(lblSecurityNote);

        // 4. Alt Durum Çubuğu
        _lblFooterStatus = new Label
        {
            Text = "Hazır: Yerel örnek kümesi yüklendi. Kamera başlatabilir veya bilgisayardan görüntü seçebilirsiniz.",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(204, 251, 241),
            BackColor = Color.FromArgb(13, 43, 48),
            Dock = DockStyle.Bottom,
            Height = 32,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 0, 0)
        };
        Controls.Add(_lblFooterStatus);

        _cameraTimer = new System.Windows.Forms.Timer { Interval = 45 };
        _cameraTimer.Tick += CameraTimer_Tick;

        RefreshDatasetClassList();
        LoadSelectedSampleImage(0);

        FormClosing += (_, _) =>
        {
            HandleStopCamera();
            _currentRawFrame?.Dispose();
        };
    }

    private static Button CreateVisionButton(string text, int x, int y, int width, int height, Color bg, Color fg)
    {
        Button btn = new()
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold),
            BackColor = bg,
            ForeColor = fg,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(x, y),
            Size = new Size(width, height),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private void RefreshDatasetClassList()
    {
        _lstDatasetClasses.Items.Clear();
        foreach (VolunteerProfile profile in _datasetService.Profiles)
        {
            _lstDatasetClasses.Items.Add(profile.ToString());
        }
    }

    private void LoadSelectedSampleImage(int index)
    {
        HandleStopCamera();

        string fileName = index switch
        {
            1 => "Ornek2_Ahmet_Portre.png",
            2 => "Ornek3_Coklu_Yuz_Testi.png",
            _ => "Ornek1_Fatma_Portre.png"
        };

        string fullPath = Path.Combine(_datasetService.SampleImagesDirectory, fileName);
        ProcessImageFile(fullPath);
    }

    private void ProcessImageFile(string imagePath)
    {
        try
        {
            if (!File.Exists(imagePath))
            {
                MessageBox.Show($"Görüntü dosyası bulunamadı:\n{imagePath}", "Dosya Bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _currentRawFrame?.Dispose();
            _currentRawFrame = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (_currentRawFrame.Empty())
            {
                throw new InvalidOperationException("Seçilen dosya geçerli bir görüntü formatında değil.");
            }

            using Mat annotated = _currentRawFrame.Clone();
            _lastDetections = _visionService.DetectAndAnnotateFaces(annotated);
            UpdateViewportAndResults(annotated, $"Görüntü analiz edildi: '{Path.GetFileName(imagePath)}' ({_lastDetections.Count} yüz tespit edildi).");
        }
        catch (Exception ex)
        {
            _lblFooterStatus.Text = $"Hata: {ex.Message}";
            MessageBox.Show(
                $"Görüntü veya model işlenirken bir hata oluştu:\n\n{ex.Message}",
                "Görüntü / Model Hatası",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void UpdateViewportAndResults(Mat annotatedMat, string statusText)
    {
        byte[] pngBytes = annotatedMat.ToBytes(".png");
        using MemoryStream ms = new(pngBytes);
        Bitmap newBmp = new(ms);
        Image? oldImg = _picViewport.Image;
        _picViewport.Image = newBmp;
        oldImg?.Dispose();

        _lstDetectedFaces.Items.Clear();
        foreach (DetectedFaceInfo face in _lastDetections)
        {
            _lstDetectedFaces.Items.Add(face.ToFormattedSummary());
        }

        if (_lastDetections.Count > 0)
        {
            DetectedFaceInfo primary = _lastDetections[0];
            _lblPrimaryPrediction.Text = _lastDetections.Count == 1
                ? primary.PredictedLabel
                : $"{primary.PredictedLabel} (+{_lastDetections.Count - 1} yüz)";
            _lblPrimaryConfidence.Text = $"Güven Skoru: %{primary.ConfidencePercent:0.0}  |  Algılanan Yüz: {_lastDetections.Count}";
        }
        else
        {
            _lblPrimaryPrediction.Text = "Yüz Algılanmadı";
            _lblPrimaryConfidence.Text = "Güven Skoru: -  |  Algılanan Yüz: 0";
        }

        _lblFooterStatus.Text = statusText;
    }

    private void HandleStartCamera()
    {
        try
        {
            _visionService.EnsureModelReady(simulateMissingModel: false);
            _videoCapture?.Dispose();
            _videoCapture = new VideoCapture(0);

            if (!_videoCapture.IsOpened())
            {
                _videoCapture.Dispose();
                _videoCapture = null;
                _lblSystemModeBadge.Text = "⚠️ KAMERA BULUNAMADI";
                _lblSystemModeBadge.BackColor = Color.FromArgb(254, 202, 202);
                _lblSystemModeBadge.ForeColor = Color.FromArgb(153, 27, 27);
                _lblFooterStatus.Text = "Uyarı: Sistemde aktif bir web kamerası bulunamadı. 'Görüntü Seç' veya 'Yerel Örnek Küme' ile devam edebilirsiniz.";

                MessageBox.Show(
                    "Bilgisayara bağlı veya erişilebilir bir kamera (Webcam) bulunamadı!\n\n" +
                    "Uygulamayı 'Görüntü Seç' butonuyla veya hazır 'Yerel Örnek Küme' görüntüleriyle test etmeye devam edebilirsiniz.",
                    "Kamera Erişim Uyarısı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _btnCameraStart.Enabled = false;
            _btnCameraStop.Enabled = true;
            _lblSystemModeBadge.Text = "● KAMERA CANLI AKIŞ AKTİF";
            _lblSystemModeBadge.BackColor = Color.FromArgb(52, 211, 153);
            _lblSystemModeBadge.ForeColor = Color.FromArgb(6, 78, 59);
            _cameraTimer.Start();
            _lblFooterStatus.Text = "Kamera başlatıldı: Canlı video kareleri üzerinde gerçek zamanlı yüz tanıma yapılıyor.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Kamera başlatılırken hata oluştu:\n{ex.Message}",
                "Kamera Hatası",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void HandleStopCamera()
    {
        _cameraTimer.Stop();
        if (_videoCapture != null)
        {
            try
            {
                _videoCapture.Release();
                _videoCapture.Dispose();
            }
            catch
            {
                // Yoksay
            }
            finally
            {
                _videoCapture = null;
            }
        }

        _btnCameraStart.Enabled = true;
        _btnCameraStop.Enabled = false;
        _lblSystemModeBadge.Text = "● MOD: GÖRÜNTÜ ANALİZİ AKTİF";
        _lblSystemModeBadge.BackColor = Color.FromArgb(94, 234, 212);
        _lblSystemModeBadge.ForeColor = Color.FromArgb(4, 47, 46);
    }

    private void CameraTimer_Tick(object? sender, EventArgs e)
    {
        if (_videoCapture == null || !_videoCapture.IsOpened())
        {
            HandleStopCamera();
            return;
        }

        using Mat frame = new();
        if (!_videoCapture.Read(frame) || frame.Empty())
        {
            return;
        }

        _currentRawFrame?.Dispose();
        _currentRawFrame = frame.Clone();

        using Mat annotated = frame.Clone();
        _lastDetections = _visionService.DetectAndAnnotateFaces(annotated);
        UpdateViewportAndResults(annotated, $"Canlı Kamera: {_lastDetections.Count} yüz bölgesi tespit edildi.");
    }

    private void HandleSelectImageFromFile()
    {
        HandleStopCamera();

        using OpenFileDialog ofd = new()
        {
            Title = "Yüz Algılama İçin Görüntü Dosyası Seçin",
            Filter = "Görüntü Dosyaları (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
            InitialDirectory = _datasetService.SampleImagesDirectory
        };

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            ProcessImageFile(ofd.FileName);
        }
    }

    private void HandleSaveCroppedFaces()
    {
        if (_currentRawFrame == null || _currentRawFrame.Empty() || _lastDetections.Count == 0)
        {
            MessageBox.Show(
                "Kaydedilecek algılanmış bir yüz bölgesi bulunamadı.",
                "Yüz Bulunamadı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            int count = _datasetService.SaveDetectedFacesToFolder(_currentRawFrame, _lastDetections);
            _lblFooterStatus.Text = $"Başarılı: Algılanan {count} yüz bölgesi 'DetectedFaces/' klasörüne ayrı görüntü olarak kaydedildi.";
            MessageBox.Show(
                $"Algılanan {count} yüz bölgesi kırpılarak aşağıdaki klasöre kaydedildi:\n\n{_datasetService.SavedFacesDirectory}",
                "Yüz Bölgeleri Kaydedildi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Yüz bölgeleri kaydedilirken hata oluştu:\n{ex.Message}", "Kayıt Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void HandleAddTrainingSample()
    {
        string newLabel = _txtNewLabel.Text.Trim();
        if (string.IsNullOrWhiteSpace(newLabel))
        {
            MessageBox.Show("Lütfen eklenecek gönüllü/örnek sınıf için bir etiket adı girin.", "Etiket Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Mat? faceRoi = null;
            if (_currentRawFrame != null && !_currentRawFrame.Empty() && _lastDetections.Count > 0)
            {
                Rect r = _lastDetections[0].BoundingBox.Intersect(new Rect(0, 0, _currentRawFrame.Width, _currentRawFrame.Height));
                if (r.Width > 10 && r.Height > 10)
                {
                    faceRoi = new Mat(_currentRawFrame, r);
                }
            }

            _datasetService.AddOrUpdateVolunteer(newLabel, faceRoi);
            faceRoi?.Dispose();

            RefreshDatasetClassList();
            _txtNewLabel.Clear();
            _lblFooterStatus.Text = $"Eğitim kümesi güncellendi: '{newLabel}' sınıfı yerel veri kümesine eklendi.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Eğitim verisi eklenirken hata:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    public void CaptureScreenshots(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        Show();
        Application.DoEvents();

        // Ekran Görüntüsü 1: Tekil Gönüllü Yüz Algılama ve Tanıma (Fatma_Ornek1 - Güven Skoru Gösterimi)
        _cmbSampleSelector.SelectedIndex = 0;
        LoadSelectedSampleImage(0);
        Refresh();
        Application.DoEvents();

        using (Bitmap bmp1 = new(Width, Height))
        {
            DrawToBitmap(bmp1, new Rectangle(0, 0, Width, Height));
            bmp1.Save(Path.Combine(outputDir, "single_face_recognition.png"), System.Drawing.Imaging.ImageFormat.Png);
        }

        // Ekran Görüntüsü 2: Çoklu Yüz İşaretleme + Yeni Eğitim Sınıfı Ekleme + Yüzleri Klasöre Kaydetme Durumu
        _txtNewLabel.Text = "Mehmet_Ornek4";
        HandleAddTrainingSample();
        _cmbSampleSelector.SelectedIndex = 2;
        LoadSelectedSampleImage(2);
        _lblFooterStatus.Text = "Başarılı: Karedeki 2 yüz ayrı ayrı işaretlendi ve 'DetectedFaces/' klasörüne kaydedildi.";
        Refresh();
        Application.DoEvents();

        using (Bitmap bmp2 = new(Width, Height))
        {
            DrawToBitmap(bmp2, new Rectangle(0, 0, Width, Height));
            bmp2.Save(Path.Combine(outputDir, "multi_face_and_training.png"), System.Drawing.Imaging.ImageFormat.Png);
        }

        Close();
    }
}
