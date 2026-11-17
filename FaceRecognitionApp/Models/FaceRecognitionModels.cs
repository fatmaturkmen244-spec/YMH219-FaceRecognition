using OpenCvSharp;

namespace FaceRecognitionApp.Models;

/// <summary>
/// Görüntü üzerinde algılanan ve tanınan tek bir yüz bölgesinin bilgilerini tutar.
/// </summary>
public sealed class DetectedFaceInfo
{
    public int Index { get; }
    public Rect BoundingBox { get; }
    public string PredictedLabel { get; }
    public double ConfidencePercent { get; }

    public DetectedFaceInfo(int index, Rect boundingBox, string predictedLabel, double confidencePercent)
    {
        Index = index;
        BoundingBox = boundingBox;
        PredictedLabel = predictedLabel;
        ConfidencePercent = Math.Clamp(confidencePercent, 0.0, 99.9);
    }

    public string ToFormattedSummary()
    {
        return $"Yüz #{Index}: {PredictedLabel,-16} | Güven: %{ConfidencePercent:0.0} | Konum: [{BoundingBox.X},{BoundingBox.Y} {BoundingBox.Width}x{BoundingBox.Height}]";
    }

    public override string ToString() => ToFormattedSummary();
}

/// <summary>
/// Yerel örnek veri kümesindeki bir gönüllü sınıfını / etiketi temsil eder.
/// </summary>
public sealed class VolunteerProfile
{
    public string Label { get; }
    public int SampleCount { get; set; }
    public Scalar ReferenceColor { get; }
    public Mat? ReferenceHistogram { get; set; }

    public VolunteerProfile(string label, int sampleCount, Scalar referenceColor, Mat? referenceHistogram = null)
    {
        Label = label;
        SampleCount = sampleCount;
        ReferenceColor = referenceColor;
        ReferenceHistogram = referenceHistogram;
    }

    public override string ToString()
    {
        return $"👤 {Label}  ({SampleCount} yerel örnek)";
    }
}
