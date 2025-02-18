﻿using ColossalFramework;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// Low level PO wrapper, only accessed by high level

namespace MoveIt
{
    internal class PO_LogicEnabled : IPO_Logic
    {
        internal static Assembly POAssembly = null;
        internal Type tPOLogic = null, tPOMod = null, tPO = null, tPInfo = null, tPUtils = null, tVertex = null;
        internal object POLogic = null;

        private readonly BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        internal PO_LogicEnabled()
        {
            POAssembly = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.Length >= 17 && assembly.FullName.Substring(0, 17) == "ProceduralObjects")
                {
                    POAssembly = assembly;
                    break;
                }
            }

            if (POAssembly == null)
            {
                throw new NullReferenceException("PO Assembly not found [PO-F3]");
            }

            tPOLogic = POAssembly.GetType("ProceduralObjects.ProceduralObjectsLogic");
            tPOMod = POAssembly.GetType("ProceduralObjects.ProceduralObjectsMod");
            tPO = POAssembly.GetType("ProceduralObjects.Classes.ProceduralObject");
            tPInfo = POAssembly.GetType("ProceduralObjects.Classes.ProceduralInfo");
            tPUtils = POAssembly.GetType("ProceduralObjects.Classes.ProceduralUtils");
            tVertex = POAssembly.GetType("ProceduralObjects.Classes.Vertex");
            POLogic = UnityEngine.Object.FindObjectOfType(tPOLogic);
        }

        protected Type _tPOLogic = null;

        public List<IPO_Object> Objects
        {
            get
            {
                List<IPO_Object> objects = new List<IPO_Object>();

                var objectList = tPOLogic.GetField("proceduralObjects", flags).GetValue(POLogic);
                if (MoveItTool.POOnlySelectedAreVisible)
                {
                    objectList = tPOLogic.GetField("pObjSelection", flags).GetValue(POLogic);
                }

                int count = (int)objectList.GetType().GetProperty("Count").GetValue(objectList, null);
                for (int i = 0; i < count; i++)
                {
                    var v = objectList.GetType().GetMethod("get_Item").Invoke(objectList, new object[] { i });
                    IPO_Object o = new PO_ObjectEnabled(v);
                    objects.Add(o);
                }

                return objects;
            }
        }

        public uint Clone(uint originalId, Vector3 position)
        {
            Type tCPO = POAssembly.GetType("ProceduralObjects.Classes.CacheProceduralObject");
            var cache = Activator.CreateInstance(tCPO, new[] { GetPOById(originalId).GetProceduralObject() });

            //int id = (int)originalId - 1;
            //var cache = new CacheProceduralObject(Logic.proceduralObjects[id]);

            uint newId = 0;
            for (int i = 1; i < int.MaxValue; i++)
            {
                bool found = false;
                foreach (IPO_Object o in Objects)
                {
                    if (o.Id == i)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    newId = (uint)i;
                    break;
                }
            }
            if (newId == 0)
            {
                throw new Exception($"Failed to find new ID for clone of {originalId}!");
            }

            Debug.Log($"Count: {Objects.Count}, New ID: {newId}");

            //int newId = Logic.proceduralObjects.GetNextUnusedId();
            //Debug.Log($"Cloning {originalId - 1} to {newId}(?), {position}\n{cache.baseInfoType}: {cache}");

            string basePrefabName = (string)tCPO.GetField("basePrefabName").GetValue(cache);
            PropInfo propInfo = Resources.FindObjectsOfTypeAll<PropInfo>().FirstOrDefault((PropInfo info) => info.name == basePrefabName);
            Debug.Log($"propInfo:{propInfo}");
            //PropInfo propInfo = Resources.FindObjectsOfTypeAll<PropInfo>().FirstOrDefault((PropInfo info) => info.name == cache.basePrefabName);

            //var obj = tPOLogic.GetMethod("PlaceCacheObject", new Type[] { tCPO, typeof(bool) }).Invoke(POLogic, new[] { cache, false });
            //Debug.Log(tPO.GetField("id").GetValue(obj));
            //tPOLogic.GetField("proceduralObjects", flags).GetValue(POLogic).GetType().GetMethod("Add", new Type[] { tPO }).Invoke(tPOLogic.GetField("proceduralObjects", flags).GetValue(POLogic), new[] { obj });

            //var obj = Logic.PlaceCacheObject(cache, false);
            //ProceduralObject obj = new ProceduralObject(cache, newId, position);
            //Logic.proceduralObjects.Add(obj);

            return newId;
            //return 0;
        }

        public void Delete(IPO_Object obj)
        {
            var poList = tPOLogic.GetField("proceduralObjects", flags).GetValue(POLogic);
            var poSelList = tPOLogic.GetField("pObjSelection", flags).GetValue(POLogic);

            poList.GetType().GetMethod("Remove", flags, null, new Type[] { tPO }, null).Invoke(poList, new object[] { obj.GetProceduralObject() });
            poSelList.GetType().GetMethod("Remove", flags, null, new Type[] { tPO }, null).Invoke(poSelList, new object[] { obj.GetProceduralObject() });
        }

        internal IPO_Object GetPOById(uint id)
        {
            foreach (IPO_Object po in Objects)
            {
                if (po.Id == id)
                    return po;
            }

            return null;
        }

        private object AvailableProcInfos
        {
            get => tPOLogic.GetField("availableProceduralInfos").GetValue(POLogic);
        }

        public IPO_Object ConvertToPO(Instance instance)
        {

            if (AvailableProcInfos == null)
                tPOLogic.GetField("availableProceduralInfos").SetValue(POLogic, tPUtils.GetMethod("CreateProceduralInfosList").Invoke(null, null));
            if ((int)AvailableProcInfos.GetType().GetProperty("Count").GetValue(AvailableProcInfos, null) == 0)
                tPOLogic.GetField("availableProceduralInfos").SetValue(POLogic, tPUtils.GetMethod("CreateProceduralInfosList").Invoke(null, null));

            // Most code adapted from PO

            try
            {
                object procInfo = null;
                int count, i;

                if (instance is MoveableProp mp)
                {
                    count = (int)AvailableProcInfos.GetType().GetProperty("Count").GetValue(AvailableProcInfos, null);
                    for (i = 0; i < count; i++)
                    {
                        var info = AvailableProcInfos.GetType().GetMethod("get_Item").Invoke(AvailableProcInfos, new object[] { i });
                        if (info == null) continue;

                        if ((PropInfo)info.GetType().GetField("propPrefab").GetValue(info) == (PropInfo)instance.Info.Prefab)
                        {
                            procInfo = info;
                            break;
                        }
                    }

                    if (procInfo == null) throw new NullReferenceException("procInfo is null when converting to PO");

                    if ((bool)procInfo.GetType().GetField("isBasicShape").GetValue(procInfo) && 
                        (int)tPOLogic.GetField("basicTextures").GetValue(POLogic).GetType().GetProperty("Count").GetValue(tPOLogic.GetField("basicTextures").GetValue(POLogic), null) > 0)
                    {
                        Debug.Log("FFF");
                        tPOLogic.GetField("currentlyEditingObject").SetValue(POLogic, null);
                        tPOLogic.GetField("chosenProceduralInfo").SetValue(POLogic, procInfo);
                    }
                    else
                    {
                        tPOLogic.GetMethod("SpawnObject", new Type[] { tPInfo, typeof(Texture2D) }).Invoke(POLogic, new[] { procInfo, null });
                        var v = tVertex.GetMethod("CreateVertexList", new Type[] { tPO }).Invoke(null, new[] { tPOLogic.GetField("currentlyEditingObject").GetValue(POLogic) });
                        tPOLogic.GetField("tempVerticesBuffer").SetValue(POLogic, v);
                    }
                }
                else if (instance is MoveableBuilding mb)
                {
                    count = (int)AvailableProcInfos.GetType().GetProperty("Count").GetValue(AvailableProcInfos, null);
                    for (i = 0; i < count; i++)
                    {
                        var info = AvailableProcInfos.GetType().GetMethod("get_Item").Invoke(AvailableProcInfos, new object[] { i });
                        if (info == null) continue;

                        if ((BuildingInfo)info.GetType().GetField("buildingPrefab").GetValue(info) == (BuildingInfo)instance.Info.Prefab)
                        {
                            procInfo = info;
                            break;
                        }
                    }

                    if (procInfo == null) throw new NullReferenceException("procInfo is null when converting to PO");

                    if ((bool)procInfo.GetType().GetField("isBasicShape").GetValue(procInfo) &&
                        (int)tPOLogic.GetField("basicTextures").GetValue(POLogic).GetType().GetProperty("Count").GetValue(tPOLogic.GetField("basicTextures").GetValue(POLogic), null) > 0)
                    {
                        tPOLogic.GetField("currentlyEditingObject").SetValue(POLogic, null);
                        tPOLogic.GetField("chosenProceduralInfo").SetValue(POLogic, procInfo);
                    }
                    else
                    {
                        tPOLogic.GetMethod("SpawnObject", new Type[] { tPInfo, typeof(Texture2D) }).Invoke(POLogic, new[] { procInfo, null });
                        var v = tVertex.GetMethod("CreateVertexList", new Type[] { tPO }).Invoke(null, new[] { tPOLogic.GetField("currentlyEditingObject").GetValue(POLogic) });
                        if (tPOLogic.GetField("tempVerticesBuffer") != null)
                        { // 1.6
                            tPOLogic.GetField("tempVerticesBuffer").SetValue(POLogic, v);
                        }
                        else
                        { // 1.5
                            tPOLogic.GetField("temp_storageVertex").SetValue(POLogic, v);
                        }
                    }
                }

                object poObj = tPOLogic.GetField("currentlyEditingObject").GetValue(POLogic);
                tPOLogic.GetField("pObjSelection", flags).GetValue(POLogic).GetType().GetMethod("Add", new Type[] { tPO }).Invoke(tPOLogic.GetField("pObjSelection", flags).GetValue(POLogic), new[] { poObj });
                return new PO_ObjectEnabled(poObj);
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        public static string getVersion()
        {
            try
            {
                Assembly poAssembly = null;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.FullName.Length >= 17 && assembly.FullName.Substring(0, 17) == "ProceduralObjects")
                    {
                        poAssembly = assembly;
                        break;
                    }
                }
                if (poAssembly == null)
                {
                    return "(Failed [PO-F1])";
                }

                Type tPO = poAssembly.GetType("ProceduralObjects.ProceduralObjectsMod");
                object version = tPO.GetField("VERSION", BindingFlags.Public | BindingFlags.Static).GetValue(null);

                return version.ToString();
            }
            catch (Exception e)
            {
                Debug.Log($"PO INTERATION FAILED\n" + e);
            }

            return "(Failed [PO-F2])";
        }
    }


    internal class PO_ObjectEnabled : IPO_Object
    {
        private object procObj;

        public uint Id { get; set; } // The InstanceID.NetLane value
        public bool Selected { get; set; }
        private int ProcId { get => (int)Id - 1; set => Id = (uint)value + 1; }

        internal Type tPOLogic = null, tPOMod = null, tPO = null;

        public Vector3 Position
        {
            get => (Vector3)tPO.GetField("m_position").GetValue(procObj);
            set => tPO.GetField("m_position").SetValue(procObj, value);

        }
        private Quaternion Rotation
        {
            get => (Quaternion)tPO.GetField("m_rotation").GetValue(procObj);
            set => tPO.GetField("m_rotation").SetValue(procObj, value);
        }

        public string Name
        {
            get
            {
                string name = (string)tPO.GetField("basePrefabName").GetValue(procObj);
                if (name.Length < 35)
                    return "[PO]" + name;
                return "[PO]" + name.Substring(0, 35);
            }
            set { }
        }

        public float Angle
        {
            get
            {
                float a = -Rotation.eulerAngles.y % 360f;
                if (a < 0) a += 360f;
                return a * Mathf.Deg2Rad;
            }

            set
            {
                float a = -(value * Mathf.Rad2Deg) % 360f;
                if (a < 0) a += 360f;
                
                Quaternion q = Rotation;
                q.eulerAngles = new Vector3(Rotation.eulerAngles.x, a, Rotation.eulerAngles.z);
                Rotation = q;
            }
        }

        private Info_POEnabled _info = null;
        public IInfo Info
        {
            get
            {
                if (_info == null)
                    _info = new Info_POEnabled(this);

                return _info;
            }
            set => _info = (Info_POEnabled)value;
        }

        public object GetProceduralObject()
        {
            return procObj;
        }

        public PO_ObjectEnabled(object obj)
        {
            tPOLogic = PO_LogicEnabled.POAssembly.GetType("ProceduralObjects.3Logic");
            tPOMod = PO_LogicEnabled.POAssembly.GetType("ProceduralObjects.ProceduralObjectsMod");
            tPO = PO_LogicEnabled.POAssembly.GetType("ProceduralObjects.Classes.ProceduralObject");

            procObj = obj;
            ProcId = (int)tPO.GetField("id").GetValue(procObj);
        }

        public void SetPositionY(float h)
        {
            Position = new Vector3(Position.x, h, Position.z);
        }

        public float GetDistance(Vector3 location)
        {
            return Vector3.Distance(Position, location);
        }

        public void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color color)
        {
            float size = 4f;
            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(cameraInfo, color, Position, size, Position.y - 100f, Position.y + 100f, renderLimits: false, alphaBlend: true);
        }

        public string DebugQuaternion()
        {
            return $"{Id}:{Rotation.w},{Rotation.x},{Rotation.y},{Rotation.z}";
        }
    }


    public class Info_POEnabled : IInfo
    {
        private PO_ObjectEnabled _obj = null;

        public Info_POEnabled(object i)
        {
            _obj = (PO_ObjectEnabled)i;
        }

        public string Name => _obj.Name;

        public PrefabInfo Prefab { get; set; } = null;
    }
}
