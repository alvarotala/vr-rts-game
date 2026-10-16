namespace Frontier.Editor
{
    public static class OrderQueueValidationBatch
    {
        public static void Run()
        {
            QueuedInputChecks.Run();
            OrderQueuePreview.Render();
            UnitFacingPreview.Render();
            EdgeSelectionPreview.Render();
            ResourcePickingPreview.Render();
            ProductionCancelPreview.Render();
        }
    }
}
