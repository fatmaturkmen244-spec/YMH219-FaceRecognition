using OpenCvSharp;
using FaceRecognitionApp.Models;

namespace FaceRecognitionApp.Services;

/// <summary>
/// Yerel gönüllü veri kümesini (en az iki örnek sınıf/etiket), örnek test görüntülerini
/// ve algılanan yüzlerin ayrı klasöre kaydedilmesini yönetir.
/// </summary>
public sealed class SampleDatasetService
{
    private readonly List<VolunteerProfile> _profiles = [];
    public string SampleImagesDirectory { get; }
    public string SavedFacesDirectory { get; }
    public string CascadeModelPath { get; }

    public IReadOnlyList<VolunteerProfile> Profiles => _profiles;

    public SampleDatasetService()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        SampleImagesDirectory = Path.Combine(baseDir, "SampleData");
        SavedFacesDirectory = Path.Combine(baseDir, "DetectedFaces");
        CascadeModelPath = Path.Combine(baseDir, "Models", "haarcascade_frontalface_default.xml");

        Directory.CreateDirectory(SampleImagesDirectory);
        Directory.CreateDirectory(SavedFacesDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(CascadeModelPath)!);

        InitializeDefaultProfilesAndSamples();
    }

    private void InitializeDefaultProfilesAndSamples()
    {
        // En az 2 gönüllü/yerel örnek sınıf (Föy gereksinimi) + 3. örnek sınıf
        _profiles.Clear();
        _profiles.Add(new VolunteerProfile("Fatma_Ornek1", 4, new Scalar(195, 175, 145)));
        _profiles.Add(new VolunteerProfile("Ahmet_Ornek2", 3, new Scalar(155, 185, 205)));
        _profiles.Add(new VolunteerProfile("Zeynep_Ornek3", 3, new Scalar(185, 155, 195)));

        // Örnek test görüntülerini oluştur (Kamera olmayan bilgisayarlarda doğrudan test için)
        CreateSampleImage1(Path.Combine(SampleImagesDirectory, "Ornek1_Fatma_Portre.png"));
        CreateSampleImage2(Path.Combine(SampleImagesDirectory, "Ornek2_Ahmet_Portre.png"));
        CreateMultiFaceSampleImage(Path.Combine(SampleImagesDirectory, "Ornek3_Coklu_Yuz_Testi.png"));
    }

    public void AddOrUpdateVolunteer(string label, Mat? faceRoi = null)
    {
        string cleanLabel = label.Trim();
        if (string.IsNullOrWhiteSpace(cleanLabel))
        {
            throw new ArgumentException("Eklenecek kişi/sınıf etiketi boş olamaz.");
        }

        Scalar avgColor = faceRoi != null && !faceRoi.Empty()
            ? Cv2.Mean(faceRoi)
            : new Scalar(175, 175, 175);

        VolunteerProfile? existing = _profiles.FirstOrDefault(p =>
            p.Label.Equals(cleanLabel, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.SampleCount++;
        }
        else
        {
            _profiles.Add(new VolunteerProfile(cleanLabel, 1, avgColor));
        }

        if (faceRoi != null && !faceRoi.Empty())
        {
            string sampleFile = Path.Combine(SampleImagesDirectory, $"Egitim_{cleanLabel}_{DateTime.Now:HHmmss}.png");
            Cv2.ImWrite(sampleFile, faceRoi);
        }
    }

    public int SaveDetectedFacesToFolder(Mat sourceFrame, IReadOnlyList<DetectedFaceInfo> detectedFaces)
    {
        Directory.CreateDirectory(SavedFacesDirectory);
        int savedCount = 0;

        foreach (DetectedFaceInfo face in detectedFaces)
        {
            Rect safeRect = face.BoundingBox.Intersect(new Rect(0, 0, sourceFrame.Width, sourceFrame.Height));
            if (safeRect.Width <= 10 || safeRect.Height <= 10)
            {
                continue;
            }

            using Mat cropped = new(sourceFrame, safeRect);
            string fileName = $"Yuz_{face.Index}_{face.PredictedLabel}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string fullPath = Path.Combine(SavedFacesDirectory, fileName);
            Cv2.ImWrite(fullPath, cropped);
            savedCount++;
        }

        return savedCount;
    }

    private static void CreateSampleImage1(string path)
    {
        using Mat canvas = new(440, 580, MatType.CV_8UC3, new Scalar(34, 42, 56));
        DrawBackgroundGrid(canvas);
        DrawSyntheticPerson(canvas, new Point(290, 210), new Size(92, 115), new Scalar(195, 175, 145), new Scalar(65, 45, 35), "GONULLU SINIF #1: FATMA");
        Cv2.ImWrite(path, canvas);
    }

    private static void CreateSampleImage2(string path)
    {
        using Mat canvas = new(440, 580, MatType.CV_8UC3, new Scalar(38, 48, 44));
        DrawBackgroundGrid(canvas);
        DrawSyntheticPerson(canvas, new Point(290, 215), new Size(95, 118), new Scalar(155, 185, 205), new Scalar(40, 50, 60), "GONULLU SINIF #2: AHMET");
        Cv2.ImWrite(path, canvas);
    }

    private static void CreateMultiFaceSampleImage(string path)
    {
        using Mat canvas = new(440, 580, MatType.CV_8UC3, new Scalar(30, 36, 52));
        DrawBackgroundGrid(canvas);
        DrawSyntheticPerson(canvas, new Point(175, 215), new Size(80, 102), new Scalar(195, 175, 145), new Scalar(60, 40, 30), "COKLU YUZ TESTI (FATMA & AHMET & ZEYNEP)");
        DrawSyntheticPerson(canvas, new Point(395, 215), new Size(82, 104), new Scalar(155, 185, 205), new Scalar(35, 45, 55), string.Empty);
        Cv2.ImWrite(path, canvas);
    }

    private static void DrawBackgroundGrid(Mat mat)
    {
        Scalar gridColor = new(48, 58, 74);
        for (int x = 0; x < mat.Width; x += 40)
        {
            Cv2.Line(mat, new Point(x, 0), new Point(x, mat.Height), gridColor, 1);
        }
        for (int y = 0; y < mat.Height; y += 40)
        {
            Cv2.Line(mat, new Point(0, y), new Point(mat.Width, y), gridColor, 1);
        }
    }

    private static void DrawSyntheticPerson(Mat mat, Point center, Size axes, Scalar skinTone, Scalar hairColor, string bannerText)
    {
        // Omuz/Gövde
        Cv2.Ellipse(mat, new Point(center.X, center.Y + axes.Height + 48), new Size(axes.Width + 42, 75), 0, 180, 360, new Scalar(90, 110, 140), -1, LineTypes.AntiAlias);

        // Saç ve Baş silüeti (Yüz algılayıcı işaretçi piksel imzası ile)
        Cv2.Ellipse(mat, new Point(center.X, center.Y - 12), new Size(axes.Width + 8, axes.Height + 10), 0, 0, 360, hairColor, -1, LineTypes.AntiAlias);
        Cv2.Ellipse(mat, center, axes, 0, 0, 360, skinTone, -1, LineTypes.AntiAlias);

        // Gözler
        int eyeY = center.Y - axes.Height / 5;
        int eyeOffsetX = axes.Width / 3;
        Cv2.Circle(mat, new Point(center.X - eyeOffsetX, eyeY), 11, Scalar.White, -1, LineTypes.AntiAlias);
        Cv2.Circle(mat, new Point(center.X + eyeOffsetX, eyeY), 11, Scalar.White, -1, LineTypes.AntiAlias);
        Cv2.Circle(mat, new Point(center.X - eyeOffsetX, eyeY), 5, new Scalar(35, 35, 35), -1, LineTypes.AntiAlias);
        Cv2.Circle(mat, new Point(center.X + eyeOffsetX, eyeY), 5, new Scalar(35, 35, 35), -1, LineTypes.AntiAlias);

        // Kaşlar
        Cv2.Ellipse(mat, new Point(center.X - eyeOffsetX, eyeY - 14), new Size(16, 4), 0, 190, 350, hairColor, 3, LineTypes.AntiAlias);
        Cv2.Ellipse(mat, new Point(center.X + eyeOffsetX, eyeY - 14), new Size(16, 4), 0, 190, 350, hairColor, 3, LineTypes.AntiAlias);

        // Burun ve Ağız
        Cv2.Line(mat, new Point(center.X, eyeY + 6), new Point(center.X - 5, center.Y + 18), new Scalar(120, 110, 100), 2, LineTypes.AntiAlias);
        Cv2.Ellipse(mat, new Point(center.X, center.Y + axes.Height / 3), new Size(26, 10), 0, 15, 165, new Scalar(95, 70, 145), 3, LineTypes.AntiAlias);

        if (!string.IsNullOrEmpty(bannerText))
        {
            Cv2.PutText(mat, bannerText, new Point(18, 30), HersheyFonts.HersheySimplex, 0.58, new Scalar(200, 230, 220), 1, LineTypes.AntiAlias);
        }
    }
}
