namespace Frontier.Editor
{
    public static class ResearchBuildingValidationBatch
    {
        public static void Run()
        {
            BuildingVisualPreview.Render();
            ProductionCancelPreview.Render();
            HudLayoutPreview.Render();
            CatalogReferencePreview.ValidateAndRender();
            LocalizationPreview.Run();
        }
    }
}
