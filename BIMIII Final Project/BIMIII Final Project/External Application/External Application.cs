using Autodesk.Revit.UI;
using System;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace DimColumns
{
    internal class ExternalApplication : IExternalApplication
    {
        public Result OnShutdown(UIControlledApplication application)
        {

            return Result.Succeeded;

        }
        public Result OnStartup(UIControlledApplication application)
        {
            application.CreateRibbonTab("ColumnAX");

            RibbonPanel ribbonpanel = application.CreateRibbonPanel("ColumnAX", "Dimension");

            string path = Assembly.GetExecutingAssembly().Location;

            PushButtonData button = new PushButtonData("Button 1", "Column Dimension", path, "DimColumns.ColumnDimension");

            button.ToolTip = "Creates column dimensions between the nearset grids and column, it aslo reflects that into a new view and a new sheet.";

            button.LongDescription = "Use plugin window to specify dimension type, offset from column, plan view name, sheet name and sheet number.";

            Uri toolTipImage = new Uri(@"C:\Users\lenovo\source\repos\BIMIII Final Project\BIMIII Final Project\Image\tooltip1.PNG");

            button.ToolTipImage = new BitmapImage(toolTipImage);

            PushButton pushButton = ribbonpanel.AddItem(button) as PushButton;

            Uri imgpath = new Uri(@"C:\Users\lenovo\source\repos\BIMIII Final Project\BIMIII Final Project\Image\Plugin.jpeg");

            pushButton.LargeImage = new BitmapImage(imgpath);

            return Result.Succeeded;
        }
    }
}
