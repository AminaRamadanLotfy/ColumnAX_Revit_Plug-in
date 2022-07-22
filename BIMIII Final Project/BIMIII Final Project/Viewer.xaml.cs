using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DimColumns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BIMIII_Final_Project
{
    /// <summary>
    /// Interaction logic for Viewer.xaml
    /// </summary>
    public partial class Viewer : Window
    {
        // fields
        public Document document;
        public string DimStyle;
        public string ViewName;
        public string SheetName;
        public string SheetNumber;
        public string SheetSize;
        public double Offset;

        // constructor
        public Viewer(Document doc)
        {
            document = doc;
            InitializeComponent();

            DisplayComboBoxItem();

        }
         public void DisplayComboBoxItem()
        {
           
            List<DimensionType> elements = new FilteredElementCollector(document)
                .OfClass(typeof(DimensionType))
                .WhereElementIsElementType()
                .Cast<DimensionType>().ToList();
            
            foreach (Element dimensionTypename in elements)
            {
                Text2.Items.Add(dimensionTypename.Name.ToString());
            }

            List<Element> titleBlockType = new FilteredElementCollector(document)
               .OfClass(typeof(FamilySymbol))
               .OfCategory(BuiltInCategory.OST_TitleBlocks)
               .WhereElementIsElementType()
               .ToList();

            foreach (Element SheetSize in titleBlockType)
            {
                Text6.Items.Add(SheetSize.Name.ToString());
            }
        }
       
        public void Ok(object sender, RoutedEventArgs e)
        {
            double.TryParse(Text1.Text, out Offset);
            DimStyle = Text2.Text;
            ViewName = Text3.Text;
            SheetName = Text4.Text;
            SheetNumber = Text5.Text;
            SheetSize = Text6.Text;
            this.Close();
        }
    }
}
