using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace SunkenPrism {
 public static class OpenCitadel {
  [MenuItem("Tools/Fourfold Citadel/Open Expanded Map")]
  public static void Open(){
   EditorSceneManager.OpenScene(CitadelBuilder.ScenePath);
   EditorApplication.delayCall+=()=>{
    var view=SceneView.lastActiveSceneView;
    if(view==null)view=EditorWindow.GetWindow<SceneView>();
    view.LookAt(new Vector3(-5,2,5),Quaternion.Euler(57,39,0),145,false,true);
    view.sceneLighting=true;view.Repaint();
   };
  }
 }
}
