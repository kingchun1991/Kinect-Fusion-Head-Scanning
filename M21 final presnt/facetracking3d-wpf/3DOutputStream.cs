using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Kinect.Toolkit.FaceTracking;
using System.Diagnostics;
using System.Windows.Media.Media3D;
using System.Windows.Media;


namespace FaceTracking3D
{
    class _3DOutputStream
    {
        public static void output(Point3DCollection v,PointCollection vt, Point3DCollection vn,Int32Collection f){
            //Debug.Print("hey");
        using(StreamWriter sw = new StreamWriter("output.obj")){
            //sw.Write("#\n# OBJ file created by Microsoft Kinect Fusion\n#\n");
            for (int pointIndex = 0; pointIndex < vt.Count; pointIndex++)
                sw.WriteLine("v " + (float)v[pointIndex].X + " " + (float)v[pointIndex].Y + " " + (float)v[pointIndex].Z);
            for (int pointIndex = 0; pointIndex < vt.Count; pointIndex++)
                sw.WriteLine("vt " + vt[pointIndex].X + " " + vt[pointIndex].Y);
            /*for (int pointIndex = 0; pointIndex < vn.Count; pointIndex++)
                sw.WriteLine("vn " + vn[pointIndex].X + " " + vn[pointIndex].Y + " " + -vn[pointIndex].Z);*/
            for (int pointIndex = 0; pointIndex <f.Count; pointIndex+=3)
                sw.WriteLine("f " + (f[pointIndex+2]+1) + "/" + (f[pointIndex+2]+1) + " " + (f[pointIndex+1]+1) + "/" + (f[pointIndex+1]+1) + " " + (f[pointIndex]+1) + "/" + (f[pointIndex]+1));
                //sw.WriteLine("f " + f[pointIndex + 2] + " "  + f[pointIndex + 1] + " " + f[pointIndex]);
         }
      }
    }
}
