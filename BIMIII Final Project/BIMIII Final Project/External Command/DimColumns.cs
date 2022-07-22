using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using BIMIII_Final_Project;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimColumns
{
    //Filter selected elemnts by column category
    public class ColumnSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            if (element.Category.Name == "Structural Columns")
            {
                return true;
            }
            return false;
        }
        public bool AllowReference(Reference refer, XYZ point)
        {
            return false;
        }
    }
    [Transaction(TransactionMode.Manual)]
    internal class ColumnDimension : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            

            bool success = true;
            #region Document 
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            #endregion

            #region WPF

            Viewer viewer = new Viewer(doc);
            viewer.ShowDialog();

            //WPF Parameters
            string ViewName = viewer.ViewName;
            string SheetName = viewer.SheetName;
            string SheetNumber = viewer.SheetNumber;
            string DimStyle = viewer.DimStyle;
            string SheetSize = viewer.SheetSize;
            double DimOffset = viewer.Offset;
            DimOffset = UnitUtils.ConvertToInternalUnits(DimOffset, UnitTypeId.Millimeters);

            #endregion

            #region Create View & Sheet
            View ColumnView = null;
            using (Transaction transaction = new Transaction(doc, "Create View"))
            {
                transaction.Start();
                View view = doc.ActiveView;
                ElementId ColumnViewId = view.Duplicate(ViewDuplicateOption.Duplicate);
                ColumnView = doc.GetElement(ColumnViewId) as View;
                ColumnView.Name = ViewName;

                //Set TitleBlock Type
                ElementId titleBlockType = new FilteredElementCollector(doc)
                   .OfCategory(BuiltInCategory.OST_TitleBlocks)
                   .WhereElementIsElementType()
                   .Cast<Element>()
                   .First(s => s.Name == SheetSize).Id;

                //Create Sheet
                ViewSheet viewSheet = null;

                viewSheet = ViewSheet.Create(doc, titleBlockType);
                viewSheet.Name = SheetName;
                viewSheet.SheetNumber = SheetNumber;

                //Insert View
                BoundingBoxUV boundingBoxUV = viewSheet.Outline;
                double X = (boundingBoxUV.Min.U + boundingBoxUV.Max.U) / 2;
                double Y = (boundingBoxUV.Min.V + boundingBoxUV.Max.V) / 2;
                double shift = UnitUtils.ConvertToInternalUnits(50, UnitTypeId.Millimeters);
                XYZ point = new XYZ(X - shift, Y, 0);
                Viewport viewport = Viewport.Create(doc, viewSheet.Id, ColumnView.Id, point);
                transaction.Commit();
            }
            #endregion

            #region Create Dimension

            ISelectionFilter selFilter = new ColumnSelectionFilter();

            IList<Reference> columns = uidoc.Selection.PickObjects(ObjectType.Element, selFilter);

            ElementId ColId = null;
            Element ColElement = null;
            Parameter LocationMarkParam = null;

            foreach (Reference refcol in columns)
            {
                if (refcol != null)
                {
                    //Retrive Column Element
                    ColId = refcol.ElementId;
                    ColElement = doc.GetElement(ColId);

                    //Get Column Location
                    LocationMarkParam = ColElement.get_Parameter(BuiltInParameter.COLUMN_LOCATION_MARK);

                    string locationParam = LocationMarkParam.AsString();

                    int index1 = locationParam.IndexOf('(');
                    int index2 = locationParam.IndexOf(')');

                    if (index1 != -1)
                    { locationParam = locationParam.Remove(index1, index2 - index1 + 1); }

                    int index3 = locationParam.IndexOf('(');
                    int index4 = locationParam.IndexOf(')');

                    if (index3 != -1)
                    { locationParam = locationParam.Remove(index3, index4 - index3 + 1); }

                    string[] gridnames = locationParam.Split('-');

                    string firstGrid = gridnames[0];
                    string secondGrid = gridnames[1];

                    //Get Parameters of column dimensions (b, h)
                    ElementId ColTypeId = ColElement.GetTypeId();
                    double colArea = 0.0;
                    double bval = doc.GetElement(ColTypeId).LookupParameter("b").AsDouble();
                    try
                    {
                        double hval = doc.GetElement(ColTypeId).LookupParameter("h").AsDouble();
                        colArea = bval * hval;
                    }
                    catch
                    {
                        colArea = bval * bval;
                    }

                    //Retrive Column Geometry
                    List<Face> faces = new List<Face>();
                    Options geomOptions = new Options();
                    geomOptions.ComputeReferences = true;

                    GeometryElement ColGeoParent = ColElement.get_Geometry(geomOptions);

                    foreach (GeometryObject geomObj in ColGeoParent)
                    {
                        Solid geomSolid = geomObj as Solid;

                        if (geomSolid != null)
                        {
                            foreach (Face geomFace in geomSolid.Faces)
                            {
                                faces.Add(geomFace);
                            }
                        }
                        else if (geomObj is GeometryInstance)
                        {
                            GeometryInstance ColInst = geomObj as GeometryInstance;

                            GeometryElement colgeo = ColInst.GetSymbolGeometry();

                            Transform transform = ColInst.Transform;

                            foreach (GeometryObject geomObj2 in colgeo)
                            {
                                geomSolid = geomObj2 as Solid;
                                if (geomSolid != null)
                                {
                                    foreach (Face geomFace in geomSolid.Faces)
                                    {
                                        faces.Add(geomFace);
                                    }
                                }
                            }
                        }
                    }

                    //Get face thats has the same area of column cross section
                    Face faceCS = faces.First(x => Math.Abs(x.Area - colArea) < 0.0001);
                    EdgeArrayArray edarrarr = faceCS.EdgeLoops;
                    EdgeArray edarr = edarrarr.get_Item(0);

                    //Select Grid 
                    foreach (string gridname in gridnames)
                    {
                        Grid grid = new FilteredElementCollector(doc)
                            .OfCategory(BuiltInCategory.OST_Grids)
                            .WhereElementIsNotElementType()
                            .Cast<Grid>()
                            .First(x => x.Name == gridname);

                        Reference gridRef = new Reference(grid);

                        Line gridLine = grid.Curve as Line;
                        double xlineDirectionX = gridLine.Direction.X;
                        double xlineDirectionY = gridLine.Direction.Y;
                        double min = double.MaxValue;
                        double temp = 0;
                        Edge refEdge = null;
                        bool HZflage = false;

                        //Select Edge
                        foreach (Edge edge in edarr)
                        {
                            Line curveEdge = edge.AsCurve() as Line;
                            double DirectionX = curveEdge.Direction.X;
                            double DirectionY = curveEdge.Direction.Y;

                            //X-Direction
                            if ((Math.Abs(xlineDirectionX) > Math.Abs(xlineDirectionY)) && (Math.Abs(DirectionX) > Math.Abs(DirectionY)))
                            {
                                HZflage = true;
                                temp = Math.Abs(gridLine.Origin.Y) - Math.Abs(curveEdge.Origin.Y);
                                if (Math.Abs(temp) < Math.Abs(min))
                                {
                                    min = temp;
                                    refEdge = edge;
                                }
                            }
                            //Y-Direction
                            else if ((Math.Abs(xlineDirectionY) > Math.Abs(xlineDirectionX)) && (Math.Abs(DirectionY) > Math.Abs(DirectionX)))
                            {
                                temp = Math.Abs(gridLine.Origin.X) - Math.Abs(curveEdge.Origin.X);
                                if (Math.Abs(temp) < Math.Abs(min))
                                {
                                    min = temp;
                                    refEdge = edge;
                                }
                            }
                        }
                        //Dimension Offset
                        Line Offset = null;
                        LocationPoint location = ColElement.Location as LocationPoint;
                        double offsetDistanceX = 0.0;
                        double offsetDistanceY = 0.0;
                        double max = (doc.GetElement(ColTypeId).LookupParameter("b").AsDouble());
                        try
                        {
                            if (doc.GetElement(ColTypeId).LookupParameter("h").AsDouble() > doc.GetElement(ColTypeId).LookupParameter("b").AsDouble())
                            {
                                max = doc.GetElement(ColTypeId).LookupParameter("h").AsDouble();
                            }
                            offsetDistanceX = location.Point.X - max / 2 - DimOffset;
                            offsetDistanceY = location.Point.Y + max / 2 + DimOffset;
                        }
                        catch
                        {
                            offsetDistanceX = location.Point.X - max / 2 - DimOffset;
                            offsetDistanceY = location.Point.Y + max / 2 + DimOffset;
                        }
                        if (HZflage == true)
                        {
                            XYZ p1 = new XYZ(offsetDistanceX, 10, 0);
                            XYZ p2 = new XYZ(offsetDistanceX, 20, 0);
                            Offset = Line.CreateBound(p1, p2);
                        }
                        else
                        {
                            XYZ p3 = new XYZ(10, offsetDistanceY, 0);
                            XYZ p4 = new XYZ(20, offsetDistanceY, 0);
                            Offset = Line.CreateBound(p3, p4);
                        }

                        //Dimension Array
                        ReferenceArray refarr = new ReferenceArray();
                        refarr.Append(refEdge.Reference);
                        refarr.Append(gridRef);

                        //Dimesion Style
                        DimensionType dimensionType = new FilteredElementCollector(doc)
                        .OfClass(typeof(DimensionType))
                        .WhereElementIsElementType()
                        .Cast<DimensionType>()
                        .First(d => d.Name == DimStyle);

                        //Draw Dimension
                        try
                        {
                            using (Transaction t = new Transaction(doc, "Dim"))
                            {
                                t.Start();
                                double tol = UnitUtils.ConvertToInternalUnits(0.01, UnitTypeId.Millimeters);
                                if (Math.Abs(min) > tol)
                                {
                                    Dimension dim = doc.Create.NewDimension(ColumnView, Offset, refarr);
                                    dim.DimensionType = dimensionType;
                                }
                                t.Commit();
                            }
                        }
                        catch (Exception exception)
                        {
                            success = false;
                            message = exception.Message;
                        }
                    }
                }
            }
            if (success == true)
            {
                return Result.Succeeded;
            }
            else
            {
                return Result.Failed;
            }
            #endregion
        }
    }
}