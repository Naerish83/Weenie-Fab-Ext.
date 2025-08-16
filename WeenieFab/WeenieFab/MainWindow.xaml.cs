using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using WeenieFab.Properties;
using Path = System.IO.Path;

using WeenieFab.Lookups;

namespace WeenieFab
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // public int SelectedItemID { get; set; }
        public static DataTable integerDataTable = new DataTable();
        public static DataTable integer64DataTable = new DataTable();
        public static DataTable boolDataTable = new DataTable();
        public static DataTable floatDataTable = new DataTable();
        public static DataTable stringDataTable = new DataTable();
        public static DataTable didDataTable = new DataTable();
        public static DataTable spellDataTable = new DataTable();
        public static DataTable attributeDataTable = new DataTable();
        public static DataTable attribute2DataTable = new DataTable();
        public static DataTable skillsDataTable = new DataTable();
        public static DataTable createListDataTable = new DataTable();
        public static DataTable bodypartsDataTable = new DataTable();
        public static DataTable bookInfoDataTable = new DataTable();
        public static DataTable bookPagesDataTable = new DataTable();
        public static DataTable iidDataTable = new DataTable();
        public static DataTable positionsDataTable = new DataTable();
        public static DataTable eventDataTable = new DataTable();

        public MainWindow()
        {
            InitializeComponent();

            // Make the DIDs Value column a filtered, searchable picker
            WireDidPickerColumn();

            CreateWeenieTypeList();
            CreateComboBoxLists();
            CreateDataTable();
            ClearAllDataTables();
            ClearAllFields();
            MiscSettings();

            CreateSpellList();
            GetVersion();

            Globals.FileChanged = false;

            btnGenerateBodyTable.Visibility = Visibility.Hidden;
            rtbBodyParts.Visibility = Visibility.Hidden;

            // For getting filename
            string[] args = Environment.GetCommandLineArgs();
            string fileToOpen = "";

            for (int i = 1; i <= args.Length - 1; i++)
                fileToOpen += args[i] + " ";

            // Open File if one is dragged or specified.
            if (fileToOpen.Contains(".sql"))
                OpenSqlFile(fileToOpen);
        }

        // ---------- DID Value picker column wiring ----------
        private bool _didPickerWired;

        private void WireDidPickerColumn()
        {
            if (_didPickerWired) return;

            // Try to grab the grid by name first
            var grid = this.FindName("dgDiD") as DataGrid;

            // If that fails (or the grid lives inside a tab template), locate it safely via the visual tree
            if (grid == null)
            {
                // Defer until the window is fully rendered so template parts exist
                this.Dispatcher.InvokeAsync(() =>
                {
                    var g = FindDidGridByHeuristics();
                    if (g == null) return;

                    // Hook for both autogen and explicit columns
                    g.AutoGeneratingColumn += DgDiD_AutoGeneratingColumn;
                    g.Loaded += (_, __) => ReplaceValueColumnIfNeeded(g);
                    _didPickerWired = true;
                }, System.Windows.Threading.DispatcherPriority.Loaded);

                return;
            }

            // We have the grid immediately
            grid.AutoGeneratingColumn += DgDiD_AutoGeneratingColumn;
            grid.Loaded += (_, __) => ReplaceValueColumnIfNeeded(grid);
            _didPickerWired = true;
        }

        private DataGrid FindDidGridByHeuristics()
        {
            return FindVisualChildren<DataGrid>(this)
                .FirstOrDefault(g =>
                {
                    try
                    {
                        if (g.Columns == null || g.Columns.Count < 2)
                            return false;

                        var hasProperty = g.Columns.Any(c => (c.Header?.ToString() ?? "")
                            .Equals("Property", StringComparison.OrdinalIgnoreCase));
                        var hasValue = g.Columns.Any(c => (c.Header?.ToString() ?? "")
                            .Equals("Value", StringComparison.OrdinalIgnoreCase));

                        return hasProperty && hasValue;
                    }
                    catch
                    {
                        return false;
                    }
                });
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                    yield return t;

                foreach (var grand in FindVisualChildren<T>(child))
                    yield return grand;
            }
        }

        private void DgDiD_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (!string.Equals(e.PropertyName, "Value", StringComparison.Ordinal))
                return;

            e.Column = BuildDidValueTemplateColumn();
        }

        private void ReplaceValueColumnIfNeeded(DataGrid grid)
        {
            try
            {
                // Already swapped? bail.
                if (grid.Columns.OfType<DataGridTemplateColumn>()
                                .Any(c => (c.Header?.ToString() ?? "") == "Value"))
                    return;

                // Find the existing "Value" column by header text
                var idx = -1;
                for (int i = 0; i < grid.Columns.Count; i++)
                {
                    var h = grid.Columns[i].Header?.ToString() ?? "";
                    if (h.Equals("Value", StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i;
                        break;
                    }
                }
                if (idx < 0) return;

                // capture old display index safely
                int oldDisplayIndex = 0;
                try { oldDisplayIndex = grid.Columns[idx].DisplayIndex; } catch { /* ignore */ }

                var newCol = BuildDidValueTemplateColumn();

                // remove old, insert new at same collection index
                grid.Columns.RemoveAt(idx);
                grid.Columns.Insert(idx, newCol);

                // only set DisplayIndex AFTER it’s in the collection, and clamp
                try
                {
                    int max = grid.Columns.Count - 1;
                    newCol.DisplayIndex = Math.Max(0, Math.Min(oldDisplayIndex, max));
                }
                catch { /* if DisplayIndex shuffles due to frozen columns etc., live with collection order */ }
            }
            catch
            {
                // swallow — grid might still be templating; next Loaded will retry
            }
        }


        private DataGridTemplateColumn BuildDidValueTemplateColumn()
        {
            var templateCol = new DataGridTemplateColumn { Header = "Value" };

            // Editing template → DidValuePicker
            var editFactory = new FrameworkElementFactory(typeof(DidValuePicker));

            // DidType <- [Property]
            var didTypeBinding = new Binding("[Property]")
            {
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            editFactory.SetBinding(DidValuePicker.DidTypeProperty, didTypeBinding);

            // Value <-> [Value]
            var valueBinding = new Binding("[Value]")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            editFactory.SetBinding(DidValuePicker.ValueProperty, valueBinding);

            templateCol.CellEditingTemplate = new DataTemplate { VisualTree = editFactory };

            // Read-only template: show numeric value (editor shows hex/validation)
            var cellFactory = new FrameworkElementFactory(typeof(TextBlock));
            cellFactory.SetBinding(TextBlock.TextProperty, new Binding("[Value]"));
            templateCol.CellTemplate = new DataTemplate { VisualTree = cellFactory };

            return templateCol;
        }
        // ----------------------------------------------------



        private static bool SearchForDuplicateProps(DataTable tempTable, string searchProp)
        {
            foreach (DataRow row in tempTable.Rows)
            {
                if (row[0]?.ToString() == searchProp)
                    return true;
            }
            return false;
        }

        public void CreateComboBoxLists()
        {
            List<string> integer32List = new List<string>();
            foreach (string line in File.ReadLines(@"TypeLists\Int32Types.txt"))
                integer32List.Add(line);
            cbInt32Props.ItemsSource = integer32List;
            cbInt32Props.SelectedIndex = 1;

            List<string> integer64List = new List<string>();
            foreach (string line in File.ReadLines(@"TypeLists\Int64Types.txt"))
                integer64List.Add(line);
            cbInt64Props.ItemsSource = integer64List;
            cbInt64Props.SelectedIndex = 1;

            List<string> BoolList = new List<string>();
            foreach (string line in File.ReadLines(@"TypeLists\BoolTypes.txt"))
                BoolList.Add(line);
            cbBoolProps.ItemsSource = BoolList;
            cbBoolProps.SelectedIndex = 1;

            List<string> FloatList = new List<string>();
            foreach (string line in File.ReadLines(@"TypeLists\FloatTypes.txt"))
                FloatList.Add(line);
            cbFloatProps.ItemsSource = FloatList;
            cbFloatProps.SelectedIndex = 1;

            List<string> StringList = new List<string>();
            foreach (string line in File.ReadLines(@"TypeLists\StringTypes.txt"))
                StringList.Add(line);
            cbStringProps.ItemsSource = StringList;
            cbStringProps.SelectedIndex = 1;

            List<string> DiDList = new List<string>();
            foreach (string line in File.ReadLines(@"TypeLists\DiDTypes.txt"))
                DiDList.Add(line);
            cbDiDProps.ItemsSource = DiDList;
            cbDiDProps.SelectedIndex = 1;

            List<string> SkillList = new List<string>();
            foreach (string line in File.ReadLines(@"TypeLists\SkillTypes.txt"))
                SkillList.Add(line);
            cbSkillType.ItemsSource = SkillList;
            cbSkillType.SelectedIndex = 6;

            List<string> BodyParts = new List<string>();
            foreach (string line in File.ReadLines(@"TypeLists\BodyParts.txt"))
                BodyParts.Add(line);
            cbBodyPart.ItemsSource = BodyParts;
            cbBodyPart.SelectedIndex = 0;

            try
{
    LookupRegistry.Initialize();
    var dmg = LookupRegistry.Get("DAMAGE_TYPE_INT")
                .Select(x => new { x.Id, x.Label })
                .ToList();

    if (dmg.Count == 0) throw new Exception("Empty DAMAGE_TYPE_INT table");

    cbBodyPartDamageType.ItemsSource = dmg;
    cbBodyPartDamageType.DisplayMemberPath = "Label";
    cbBodyPartDamageType.SelectedValuePath = "Id";
    cbBodyPartDamageType.SelectedValue = 1; // default to Slash
}
catch
{
    var dmgFallback = new[]
    {
        new { Id = 0,   Label = "None/Undefined" },
        new { Id = 1,   Label = "Slash" },
        new { Id = 2,   Label = "Pierce" },
        new { Id = 4,   Label = "Bludgeon" },
        new { Id = 8,   Label = "Cold" },
        new { Id = 16,  Label = "Fire" },
        new { Id = 32,  Label = "Acid" },
        new { Id = 64,  Label = "Electric" },
        new { Id = 128, Label = "Nether" }
    };
    cbBodyPartDamageType.ItemsSource = dmgFallback;
    cbBodyPartDamageType.DisplayMemberPath = "Label";
    cbBodyPartDamageType.SelectedValuePath = "Id";
    cbBodyPartDamageType.SelectedValue = 1;
}

            List<string> InstanceTypes = new List<string>();
            foreach (string line in File.ReadLines(@"TypeLists\InstanceIDTypes.txt"))
                InstanceTypes.Add(line);
            cbIidProps.ItemsSource = InstanceTypes;
            cbIidProps.SelectedIndex = 1;

            List<string> PositionTypes = new List<string>();
            foreach (string line in File.ReadLines(@"TypeLists\PositionTypes.txt"))
                PositionTypes.Add(line);
            cbPosition.ItemsSource = PositionTypes;
            cbPosition.SelectedIndex = 1;
        }

        // Testing Search
        public void CreateSpellList()
        {
            List<SpellNames> listSpellNames = new List<SpellNames>();
            foreach (string line in File.ReadLines(@"TypeLists\SpellNames.txt"))
            {
                string[] spellData = line.Split(",");
                listSpellNames.Add(new SpellNames
                {
                    SpellID = ConvertToInteger(spellData[0]),
                    SpellName = spellData.Length > 1 ? spellData[1] : ""
                });
            }
            lvSpellsList.ItemsSource = listSpellNames;
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(lvSpellsList.ItemsSource);
            view.Filter = SpellFilter;
        }

        private bool SpellFilter(object spellname)
        {
            if (string.IsNullOrEmpty(tbSpellSearch.Text))
                return true;
            return (spellname as SpellNames)?.SpellName.IndexOf(tbSpellSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void txtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(lvSpellsList.ItemsSource).Refresh();
        }

        public void CreateWeenieTypeList()
        {
            string filepath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"TypeLists\WeenieTypes.txt");
            List<string> weenieTypeList = new List<string>();
            foreach (string line in File.ReadLines(filepath))
                weenieTypeList.Add(line);
            cbWeenieType.ItemsSource = weenieTypeList;
            cbWeenieType.SelectedIndex = 1;
        }

        // Texbox Validations
        private void IntValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9-]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void FloatValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9.-]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        // Data Table Sorting
        public static DataTable ResortDataTable(DataTable dt, string colName, string direction)
        {
            dt.DefaultView.Sort = colName + " " + direction;
            dt = dt.DefaultView.ToTable();
            return dt;
        }

        // Text to Numeric Converters
        public static int ConvertToInteger(string text)
        {
            int.TryParse(text, out int i);
            return i;
        }

        public static long ConvertToLong(string text)
        {
            long.TryParse(text, out long i);
            return i;
        }

        public static uint ConvertToUInteger(string text)
        {
            // NOTE: original code used base 32 which is invalid for Convert.ToUInt32(string, int).
            // Leaving as-is if you rely on it elsewhere; consider fixing to base 10/16 as needed.
            uint i = 0;
            try { i = Convert.ToUInt32(text, 32); } catch { }
            return i;
        }

        public static float ConvertToFloat(string text)
        {
            float.TryParse(text, out float i);
            return i;
        }

        public static decimal ConvertToDecimal(string text)
        {
            decimal.TryParse(text, out decimal i);
            return i;
        }

        public static uint ConvertHexToDecimal(string hexValue)
        {
            uint i = 0;
            try { i = uint.Parse(hexValue, System.Globalization.NumberStyles.HexNumber); }
            catch { }
            return i;
        }

        public static string ConvertToHex(string value)
        {
            int i = ConvertToInteger(value);
            return i.ToString("X8");
        }

        // UI Stuff
        public void ClearAllDataTables()
        {
            integerDataTable.Clear();
            integer64DataTable.Clear();
            boolDataTable.Clear();
            floatDataTable.Clear();
            stringDataTable.Clear();
            didDataTable.Clear();
            iidDataTable.Clear();
            spellDataTable.Clear();
            attributeDataTable.Clear();
            attribute2DataTable.Clear();
            skillsDataTable.Clear();
            createListDataTable.Clear();
            bodypartsDataTable.Clear();
            bookInfoDataTable.Clear();
            bookPagesDataTable.Clear();
            positionsDataTable.Clear();
            eventDataTable.Clear();
        }

        public void ResetIndexAllDataGrids()
        {
            dgInt32.SelectedIndex = -1;
        }

        public void ClearAllFields()
        {
            tbWCID.Text = "";
            tbWeenieName.Text = "";
            tbValue.Text = "";
            tb64Value.Text = "";
            tbFloatValue.Text = "";
            tbStringValue.Text = "";

            // WAS: tbDiDValue.Text = "";
            if (this.FindName("didPicker") is DidValuePicker dp)
                dp.Value = 0;

            tbSpellId.Text = "";
            tbSpellValue.Text = "";
            tbSkillLevel.Text = "";
            tbCreateItemsDescription.Text = "";
            tbCreateItemsDestType.Text = "";
            tbCreateItemsDropRate.Text = "";
            tbCreateItemsPalette.Text = "";
            tbCreateItemsStackSize.Text = "";
            tbCreateItemsWCID.Text = "";

            tbBodyPartDamageValue.Text = "";
            tbBodyPartDamageVariance.Text = "";

            tbBodyPartArmorLevel.Text = "";
            tbBodyPartArmorLevelSlash.Text = "";
            tbBodyPartArmorLevelPierce.Text = "";
            tbBodyPartArmorLevelBludgeon.Text = "";
            tbBodyPartArmorLevelCold.Text = "";
            tbBodyPartArmorLevelFire.Text = "";
            tbBodyPartArmorLevelAcid.Text = "";
            tbBodyPartArmorLevelElectric.Text = "";
            tbBodyPartArmorLevelNether.Text = "";

            tbBodyPartBase_Height.Text = "";

            tbBodyPartQuadHighLF.Text = "";
            tbBodyPartQuadMiddleLF.Text = "";
            tbBodyPartQuadLowLF.Text = "";

            tbBodyPartQuadHighRF.Text = "";
            tbBodyPartQuadMiddleRF.Text = "";
            tbBodyPartQuadLowRF.Text = "";

            tbBodyPartQuadHighLB.Text = "";
            tbBodyPartQuadMiddleLB.Text = "";
            tbBodyPartQuadLowLB.Text = "";

            tbBodyPartQuadHighRB.Text = "";
            tbBodyPartQuadMiddleRB.Text = "";
            tbBodyPartQuadLowRB.Text = "";

            // Books
            tbMaxPages.Text = "";
            tbMaxChars.Text = "";

            tbPageID.Text = "";
            tbAuthorName.Text = "";
            tbPageText.Text = "";
            rdbBookFalse.IsChecked = true;

            // IID
            cbIidProps.SelectedIndex = 1;
            tbiidValue.Text = "";

            // Positions
            cbPosition.SelectedIndex = 1;
            tbPositionLoc.Text = "";
            tbCellID.Text = "";
            tbOriginX.Text = "";
            tbOriginY.Text = "";
            tbOriginZ.Text = "";
            tbAngleW.Text = "";
            tbAngleX.Text = "";
            tbAngleY.Text = "";
            tbAngleZ.Text = "";

            // Rich Text Boxes
            rtbEmoteScript.Document.Blocks.Clear();
            rtbBodyParts.Document.Blocks.Clear();

            // Generator Tab
            tbGenerator.Text = "";

            cbWeenieType.SelectedIndex = 1;
            cbInt32Props.SelectedIndex = 1;
            cbInt64Props.SelectedIndex = 1;
            cbBoolProps.SelectedIndex = 1;
            cbFloatProps.SelectedIndex = 1;
            cbStringProps.SelectedIndex = 1;
            cbDiDProps.SelectedIndex = 1;
            cbSkillType.SelectedIndex = 1;
            cbBodyPart.SelectedIndex = 0;
            cbBodyPartDamageType.SelectedIndex = 1;

            ClearAttributeFields();
            ClearAttribute2Fields();
        }


        public void MiscSettings()
        {
            rtbEmoteScript.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
            rtbEmoteScript.Document.PageWidth = 2000;
            if (WeenieFabUser.Default.AutoCalcHealth == true)
                chkbAutoHealth.IsChecked = true;
        }

        private void lvSpellsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Not used currently
        }

        private void GetVersion()
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(executingAssembly.Location);
            var version = fileVersionInfo.FileVersion;
            txtblockVersion.Text = "Version " + version;
        }

        // **Auto Calcs for Health/Stam/Mana and Skills**
        private void chkbAutoHealth_Changed(object sender, RoutedEventArgs e)
        {
            WeenieFabUser.Default.AutoCalcHealth = chkbAutoHealth.IsChecked == true;
            WeenieFabUser.Default.Save();
        }

        private void tbHealthCurrentLevel_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (WeenieFabUser.Default.AutoCalcHealth == true)
            {
                int attribEndurance = ConvertToInteger(tbAttribEndurance.Text) / 2;
                int finalHealth = ConvertToInteger(tbHealthCurrentLevel.Text);
                tbHealthInitLevel.Text = (finalHealth - attribEndurance).ToString();
            }
        }

        private void tbStaminaCurrentLevel_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (WeenieFabUser.Default.AutoCalcHealth == true)
            {
                int attribEndurance = ConvertToInteger(tbAttribEndurance.Text);
                int finalStamina = ConvertToInteger(tbStaminaCurrentLevel.Text);
                tbStaminaInitLevel.Text = (finalStamina - attribEndurance).ToString();
            }
        }

        private void tbManaCurrentLevel_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (WeenieFabUser.Default.AutoCalcHealth == true)
            {
                int attribSelf = ConvertToInteger(tbAttribSelf.Text);
                int finalMana = ConvertToInteger(tbManaCurrentLevel.Text);
                tbManaInitLevel.Text = (finalMana - attribSelf).ToString();
            }
        }

        private void chkbSkillCalc_Changed(object sender, RoutedEventArgs e)
        {
            WeenieFabUser.Default.AutoCalcSkill = chkbAutoHealth.IsChecked == true;
            WeenieFabUser.Default.Save();
        }

        private void tbSkillFinalLevel_TextChanged(object sender, TextChangedEventArgs e)
        {
            AutoSkillCalc();
        }

        private void cbSkillType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Intentionally left off auto-calc on change
        }

        public void AutoSkillCalc()
        {
            int strength = ConvertToInteger(tbAttribStrength.Text);
            int endur = ConvertToInteger(tbAttribEndurance.Text);
            int coord = ConvertToInteger(tbAttribCoordination.Text);
            int quick = ConvertToInteger(tbAttribQuickness.Text);
            int focus = ConvertToInteger(tbAttribFocus.Text);
            int self = ConvertToInteger(tbAttribSelf.Text);

            if (WeenieFabUser.Default.AutoCalcSkill == true)
            {
                switch (cbSkillType.SelectedIndex)
                {
                    case 6:  // MeleeD
                    case 46: // Finesse Weapons
                    case 51: // Sneak Attack
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - ((quick + coord) / 3)).ToString();
                        break;
                    case 7:  // MissileD
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - ((quick + coord) / 5)).ToString();
                        break;
                    case 14:  // Arcane Lore
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - (focus / 3)).ToString();
                        break;
                    case 15:  // Magic D
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - ((focus + self) / 7)).ToString();
                        break;
                    case 16:  // Mana C
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - ((focus + self) / 6)).ToString();
                        break;
                    case 18:  // Item Appraisal - Item Tink
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - ((focus + coord) / 2)).ToString();
                        break;
                    case 19:  // Personal Appraisal - Assess Person
                    case 20:  // Deception
                    case 27:  // Creature Appraisal - Assess Creature
                    case 35:  // Leadership
                    case 36:  // Loyalty
                    case 40:  // Salvaging
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text)).ToString();
                        break;
                    case 21:  // Healing
                    case 23:  // Lockpick
                    case 37:  // Fletching
                    case 38:  // Alchemy
                    case 39:  // Cooking
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - ((focus + coord) / 3)).ToString();
                        break;
                    case 22:  // Jump
                    case 48:  // Shield
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - ((strength + coord) / 2)).ToString();
                        break;
                    case 24:  // Run
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - quick).ToString();
                        break;
                    case 28:  // Weapon Appraisal - Weapon Tink
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - ((focus + strength) / 2)).ToString();
                        break;
                    case 29:  // Armor Appraisal - Armor Tink
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - ((focus + endur) / 2)).ToString();
                        break;
                    case 30:  // Magic Item Appraisal - Magic Item Tink
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - focus).ToString();
                        break;
                    case 31:  // Creature Magic
                    case 32:  // Item Magic
                    case 33:  // Life Magic
                    case 34:  // War Magic
                    case 43:  // Void Magic
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - ((focus + self) / 4)).ToString();
                        break;
                    case 41:  // Two Hand
                    case 44:  // Heavy Weapons
                    case 45:  // Light Weapons
                    case 52:  // Dirty Fighting
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - ((strength + coord) / 3)).ToString();
                        break;
                    case 47:  // Missile Weapons
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - coord / 2).ToString();
                        break;
                    case 49:  // Dual Wield
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - ((coord * 2) / 3)).ToString();
                        break;
                    case 50:  // Recklessness
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - ((strength + quick) / 3)).ToString();
                        break;
                    case 54:  // Summoning
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - ((endur + self) / 3)).ToString();
                        break;
                    case 1: // Axe
                    case 5: // Mace
                    case 9: // Spear
                    case 10: // Staff
                    case 11: // Sword
                    case 13: // Unarmed Combat
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - ((strength + coord) / 3)).ToString();
                        break;
                    case 2: // Bow
                    case 3: // Crossbow
                    case 12: // Thrown Weapon
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - coord / 2).ToString();
                        break;
                    case 4: // Dagger
                        tbSkillLevel.Text = (ConvertToInteger(tbSkillFinalLevel.Text) - ((quick + coord) / 3)).ToString();
                        break;
                    // Ignored (Unused)
                    case 0:
                    case 8:
                    case 17:
                    case 25:
                    case 26:
                    case 42:
                    case 53:
                    default:
                        break;
                }
            }
        }

        public void FileChanged()
        {
            Globals.FileChanged = true;
            txtBlockFileStatus.Text = "File has been changed, please save changes.";
        }

        public bool FileChangedCheck()
        {
            MessageBoxButton buttons = MessageBoxButton.YesNoCancel;
            MessageBoxImage icon = MessageBoxImage.Question;
            MessageBoxResult result = MessageBox.Show("Save current Weenie?", "Possible Unsaved Changes", buttons, icon);
            if (result == MessageBoxResult.Yes)
            {
                SaveFile();
                return true;
            }
            else if (result == MessageBoxResult.No)
                return true;
            else
                return false;
        }

        // Not used currently (button is hidden) but have ideas for this. Reuse for importing body tables.
        private void btnGenerateBodyTable_Click(object sender, RoutedEventArgs e)
        {
            string header = $"INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`)";
            string bodyparts = TableToSql.ConvertBodyPart(bodypartsDataTable, tbWCID.Text, header);
            rtbBodyParts.Document.Blocks.Clear();
            rtbBodyParts.Document.Blocks.Add(new Paragraph(new Run(bodyparts)));
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Globals.FileChanged == true)
            {
                if (!FileChangedCheck())
                    e.Cancel = true;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            var browser = new Process();
            browser.StartInfo.UseShellExecute = true;
            browser.StartInfo.FileName = e.Uri.AbsoluteUri;
            browser.Start();
            e.Handled = true;
        }
    }
}
