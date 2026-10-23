namespace Frontier.Editor
{
    public static class MenuRefinementValidationBatch
    {
        public static void ValidateCompactSelection()
        {
            HudLayoutPreview.Render();
            SelectionGridPreview.Render();
            ProductionQueuePreview.Render();
        }

        public static void Run()
        {
            WindowScalePreview.Render();
            HudLayoutPreview.Render();
            OrderQueuePreview.Render();
            SelectionGridPreview.Render();
            // Cancellation also validates help, production, menu flow and the wheel.
            ProductionCancelPreview.Render();
            ControlGroupPreview.Render();
            StartupMenuPreview.Render();
            LocalizationPreview.Run();
        }
    }
}
