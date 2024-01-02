using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcCap = Autodesk.AutoCAD.ApplicationServices.Application;


namespace BIMIshouCad
{
    public class Commands : IExtensionApplication
    {
        public void Initialize()
        {
            SystemObjects.DynamicLinker.LoadModule(
                "AcMPolygonObj" + Application.Version.Major + ".dbx", false, false);
        }
        public void Terminate() { }

        private static bool IsPointInside(Point3d point, Polyline pline)
        {
            double tolerance = Tolerance.Global.EqualPoint;
            using (MPolygon mpg = new MPolygon())
            {
                try
                {
                    mpg.AppendLoopFromBoundary(pline, true, tolerance);
                    return mpg.IsPointInsideMPolygon(point, tolerance).Count == 1;
                }
                catch (System.Exception)
                {
                    return false;
                }

            }
        }

        [CommandMethod("BMGaiko")]
        public void BMGaiko()
        {
            var doc = AcCap.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            PromptEntityOptions peo = new PromptEntityOptions("\nChọn đường polyline ");
            peo.SetRejectMessage("\nVui lòng chỉ chọn đường polyline.");
            peo.AddAllowedClass(typeof(Polyline), true);

            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            PromptSelectionOptions prSelOpts = new PromptSelectionOptions();
            prSelOpts.AllowDuplicates = false;
            prSelOpts.MessageForAdding = "\nChọn đối tượng để lọc ";
            prSelOpts.MessageForRemoval = "\nChọn đối tượng muốn loại ra ";
            prSelOpts.RejectObjectsFromNonCurrentSpace = false;
            prSelOpts.RejectObjectsOnLockedLayers = true;
            prSelOpts.RejectPaperspaceViewport = true;

            PromptSelectionResult prSelResult = ed.GetSelection(prSelOpts);
            if (prSelResult.Status != PromptStatus.OK) return;


            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    ObjectId polylineId = per.ObjectId;
                    Polyline polyline = tr.GetObject(polylineId, OpenMode.ForRead) as Polyline;
                    SelectionSet selectionSet = prSelResult.Value;
                    IList<DBObject> listText = new List<DBObject>();
                    IList<DBObject> listErase = new List<DBObject>();
                    IList<DBObject> listCircle = new List<DBObject>();
                    IList<DBObject> listMtext = new List<DBObject>();
                    foreach (SelectedObject ele in selectionSet)
                    {
                        DBObject obj = tr.GetObject(ele.ObjectId, OpenMode.ForWrite);

                        if (obj is DBText)
                        {
                            listText.Add(obj);
                        }
                        else if (obj is MText)
                        {
                            listMtext.Add(obj);
                        }
                        else if (obj is Circle)
                        {
                            listCircle.Add(obj);
                        }
                        else
                        {
                            listErase.Add(obj);
                        }
                    }
                    foreach (DBObject obj in listErase)
                    {
                        if (obj.ObjectId != polylineId)
                        {
                            obj.Erase();
                        }

                    }
                    foreach (DBObject obj in listText)
                    {
                        Point3d point = (obj as DBText).Position;
                        String dbTextContent = (obj as DBText).TextString;

                        if (IsPointInside(point, polyline) == false || ContainsOnlyLetters(dbTextContent))
                        {
                            obj.Erase();
                        }
                    }
                    foreach (DBObject obj in listCircle)
                    {
                        Point3d point = (obj as Circle).Center;

                        if (IsPointInside(point, polyline) == false)
                        {
                            obj.Erase();
                        }
                    }
                    foreach (DBObject obj in listMtext)
                    {
                        Point3d point = (obj as MText).Location;
                        String mTextContent = (obj as MText).Contents;

                        if (IsPointInside(point, polyline) == false || ContainsOnlyLetters(mTextContent))
                        {
                            obj.Erase();
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage("\nLỗi mất rồi.", ex.Message);
                }
                tr.Commit();
            }
        }
        private static bool ContainsOnlyLetters(string text)
        {
            foreach (char c in text)
            {
                if (!char.IsLetter(c))
                {
                    return false;
                }
            }
            return true;
        }
        private static bool ContainsOnlyNumbers(string text)
        {
            foreach (char c in text)
            {
                if (!char.IsDigit(c))
                {
                    return false;
                }
            }
            return true;
        }
    }
    
}
