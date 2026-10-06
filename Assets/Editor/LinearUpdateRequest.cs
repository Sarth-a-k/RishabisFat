using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace SunkenPrism {
 [InitializeOnLoad] static class LinearUpdateRequest {
  static double safeAt;
  static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../work/linear-layout"));
  static LinearUpdateRequest(){safeAt=EditorApplication.timeSinceStartup+4;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=_=>safeAt=EditorApplication.timeSinceStartup+4;}
  static void Tick(){
   string request=Path.Combine(Folder,"editor-request.txt");
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isPlaying||EditorApplication.timeSinceStartup<safeAt||!File.Exists(request))return;
   string mode=File.ReadAllText(request).Trim();File.Delete(request);string result=Path.Combine(Folder,"editor-result.txt");File.WriteAllText(result,"RUNNING "+mode);
   try{
    if(mode=="build-all")CitadelBuilder.BuildAll();
    else if(mode=="render")CitadelBuilder.Render();
    else {
     var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName="Build/Windows/The Fourfold Citadel.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
     if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
    }
    File.WriteAllText(result,"PASS "+mode);
   }catch(Exception e){File.WriteAllText(result,"FAIL "+e);Debug.LogException(e);}
  }
 }
}
