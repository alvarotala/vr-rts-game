namespace Frontier.Editor
{
    public static class OrderQueueValidationBatch
    {
        public static void Run()
        {
            AttackPointerPreview.Render();
            OrderQueuePreview.Render();
            RallyPointPreview.Render();
            UnitFacingPreview.Render();
            EdgeSelectionPreview.Render();
            ResourcePickingPreview.Render();
            ProductionCancelPreview.Render();
        }
    }
}
