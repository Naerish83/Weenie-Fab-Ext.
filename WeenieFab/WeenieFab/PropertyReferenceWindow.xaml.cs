using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace WeenieFab
{
    /// <summary>
    /// Interaction logic for PropertyReferenceWindow.xaml
    /// This window loads the merged reference JSON file at runtime and
    /// populates lists for each property category.  The JSON file must be
    /// located in the same directory as the application executable or a
    /// relative path can be provided via the constructor.
    /// </summary>
    public partial class PropertyReferenceWindow : Window
    {
        public class PropertyEntry
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
        }

        public List<PropertyEntry> BoolList { get; } = new();
        public List<PropertyEntry> FloatList { get; } = new();
        public List<PropertyEntry> StringList { get; } = new();
        public List<PropertyEntry> Int64List { get; } = new();
        public List<PropertyEntry> AttributeList { get; } = new();
        public List<PropertyEntry> PositionList { get; } = new();

        private readonly string _jsonPath;

        public PropertyReferenceWindow(string jsonPath = null)
        {
            InitializeComponent();
            _jsonPath = jsonPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "weenie_merged_reference_plus_fab.json");
            try
            {
                LoadData();
                DataContext = this;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading reference data: {ex.Message}");
            }
        }

        private void LoadData()
        {
            if (!File.Exists(_jsonPath))
                throw new FileNotFoundException($"Reference file not found: {_jsonPath}");
            using var stream = File.OpenRead(_jsonPath);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;
            if (!root.TryGetProperty("master_types", out var masterTypes))
                throw new Exception("master_types not found in JSON");

            LoadCategory(masterTypes, "SType_Bool", BoolList);
            LoadCategory(masterTypes, "SType_Float", FloatList);
            LoadCategory(masterTypes, "SType_String", StringList);
            LoadCategory(masterTypes, "SType_Int64", Int64List);
            LoadCategory(masterTypes, "SType_Attribute", AttributeList);
            LoadCategory(masterTypes, "SType_Position", PositionList);
        }

        private void LoadCategory(JsonElement masterTypes, string categoryName, List<PropertyEntry> target)
        {
            if (!masterTypes.TryGetProperty(categoryName, out var list))
                return;
            foreach (var element in list.EnumerateArray())
            {
                int? id = null;
                if (element.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                    id = idProp.GetInt32();
                string name = element.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : string.Empty;
                string desc = element.TryGetProperty("description", out var descProp) ? descProp.GetString() : string.Empty;
                target.Add(new PropertyEntry { Id = id, Name = name, Description = desc });
            }
        }
    }
}