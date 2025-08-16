namespace WeenieFab.Adapters
{
    public interface IModelViewerAdapter
    {
        void Open(uint did);
        void OpenClothed(uint wcid, uint paletteDid);
    }

    public static class ModelViewerAdapter
    {
        // Will be wired later (e.g., ExternalViewerAdapter or ACViewerAdapter)
        public static IModelViewerAdapter? Instance { get; set; }
    }
}
