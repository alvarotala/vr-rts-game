namespace Frontier.Editor
{
    public static class FogRevealValidationBatch
    {
        public static void Run()
        {
            UnitRevealPreview.Run();
            FogPreview.Run();
            LightingPreview.Render();
            HitFeedbackPreview.Render();
            DeathVisualPreview.Run();
        }
    }
}
