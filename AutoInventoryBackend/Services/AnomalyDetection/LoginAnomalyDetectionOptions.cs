namespace AutoInventoryBackend.Services.AnomalyDetection
{
    public class LoginAnomalyDetectionOptions
    {
        public int Trees { get; set; } = 100;
        public int SampleSize { get; set; } = 128;
        public double Threshold { get; set; } = 0.65;
        public int TrainingLookbackHours { get; set; } = 24;
        public int MinTrainingSamples { get; set; } = 50;
        public int CatchUpMinutes { get; set; } = 120;
        public int RetrainMinutes { get; set; } = 15;
        public int EvaluationWindowMinutes { get; set; } = 1;
        public int? RandomSeed { get; set; }
    }
}
