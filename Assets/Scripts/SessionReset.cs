using System;
using System.Reflection;
using UnityEngine;

public static class SessionReset
{
    public static int ResetAll()
    {
        int n = 0;
        Assembly asm = typeof(SessionReset).Assembly;
        foreach (Type t in asm.GetTypes())
        {
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.GetParameters().Length != 0 || m.ContainsGenericParameters) continue;
                var attr = m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                if (attr == null || attr.loadType != RuntimeInitializeLoadType.SubsystemRegistration) continue;
                try { m.Invoke(null, null); n++; }
                catch (Exception e) { Debug.LogWarning("SessionReset: " + t.Name + "." + m.Name + " failed: " + e.Message); }
            }
        }
        return n;
    }
}
