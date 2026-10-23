namespace Frontier.Editor
{
    public static class RallyPointValidationBatch
    {
        public static void RunWheelAndMenu(){RallyPointInputChecks.Run();ActionWheelPreview.Render();}
        public static void Run()
        {
            RallyPointInputChecks.Run();
            RunPresentation();
        }
        public static void RunPresentation()
        {
            RallyPointPreview.Render();
            UnitFacingPreview.Render();
            QueuedInputChecks.Run();
            OrderQueuePreview.Render();
            ProductionCancelPreview.Render();
        }
    }
}
