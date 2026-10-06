using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
namespace SunkenPrism {
 public static class SourceInspection {
  public static void Run(){
   var sb=new StringBuilder();
   foreach(var obj in AssetDatabase.LoadAllAssetsAtPath("Assets/Imported/modularDungeon.fbx"))if(obj is Mesh m)sb.AppendLine(m.name+" | "+m.bounds.center+" | "+m.bounds.size+" | "+m.vertexCount+" | "+m.subMeshCount);
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported/modularDungeon.fbx");
   foreach(var t in prefab.GetComponentsInChildren<Transform>())sb.AppendLine("NODE "+t.name+" pos="+t.localPosition+" rot="+t.localEulerAngles+" scale="+t.localScale);
   Directory.CreateDirectory("Documentation");File.WriteAllText("Documentation/Imported-Source-Inventory.txt",sb.ToString());Debug.Log("SOURCE_INSPECTION_COMPLETE");
  }
 }
}
