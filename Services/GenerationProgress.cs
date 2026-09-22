namespace VideoAutoWpf.Services;

public class GenerationProgress
{
    public double Percentage { get; set; }
    public string StepTitle { get; set; } = string.Empty;
    public string LogMessage { get; set; } = string.Empty;
    public bool IsError { get; set; }
}
