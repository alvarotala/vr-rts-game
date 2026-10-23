namespace Frontier.Editor
{
    public static class OrderQueueGridValidationBatch
    {
        public static void Run()
        {
            OrderQueuePreview.Render();
            HudLayoutPreview.Render();
            ProductionCancelPreview.Render();
            QueuedInputChecks.Run();
            LocalizationPreview.Run();
        }
    }
}
