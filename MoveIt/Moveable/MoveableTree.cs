﻿using UnityEngine;

using System.Collections.Generic;
using ColossalFramework.Math;


namespace MoveIt
{
    public class MoveableTree : Instance
    {
        public class TreeState : InstanceState
        {
            public bool single;
        }

        public override HashSet<ushort> segmentList
        {
            get
            {
                return new HashSet<ushort>();
            }
        }

        public MoveableTree(InstanceID instanceID) : base(instanceID) { }

        public override InstanceState GetState()
        {
            TreeState state = new TreeState();

            state.instance = this;

            uint tree = id.Tree;
            state.info = info;

            state.position = TreeManager.instance.m_trees.m_buffer[tree].Position;
            state.terrainHeight = TerrainManager.instance.SampleOriginalRawHeightSmooth(state.position);

            state.single = TreeManager.instance.m_trees.m_buffer[tree].Single;

            return state;
        }

        public override void SetState(InstanceState state)
        {
            InstanceState treeState = state as InstanceState;
            if (treeState == null) return;

            uint tree = id.Tree;
            TreeManager.instance.MoveTree(tree, treeState.position);
            TreeManager.instance.UpdateTreeRenderer(tree, true);
        }

        public override Vector3 position
        {
            get
            {
                if (id.IsEmpty) return Vector3.zero;
                return TreeManager.instance.m_trees.m_buffer[id.Tree].Position;
            }
        }

        public override float angle
        {
            get { return 0f; }
        }

        public override bool isValid
        {
            get
            {
                if (id.IsEmpty) return false;
                return TreeManager.instance.m_trees.m_buffer[id.Tree].m_flags != 0;
            }
        }

        public override void Transform(InstanceState state, ref Matrix4x4 matrix4x, float deltaHeight, float deltaAngle, Vector3 center, bool followTerrain)
        {
            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);
            newPosition.y = state.position.y + deltaHeight;

            if (followTerrain)
            {
                newPosition.y = newPosition.y + TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition) - state.terrainHeight;
            }

            Move(newPosition, 0);
        }

        public override void Move(Vector3 location, float angle)
        {
            if (!isValid) return;

            uint tree = id.Tree;
            TreeManager.instance.MoveTree(tree, location);
            TreeManager.instance.UpdateTreeRenderer(tree, true);
        }

        public override void SetHeight(float height)
        {
            Vector3 newPosition = position;
            newPosition.y = height;

            uint tree = id.Tree;
            TreeManager.instance.MoveTree(tree, newPosition);
            TreeManager.instance.UpdateTreeRenderer(tree, true);
        }

        public override Instance Clone(InstanceState state, ref Matrix4x4 matrix4x, float deltaHeight, float deltaAngle, Vector3 center, bool followTerrain, Dictionary<ushort, ushort> clonedNodes)
        {
            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);
            newPosition.y = state.position.y + deltaHeight;

            if (followTerrain)
            {
                newPosition.y = newPosition.y + TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition) - state.terrainHeight;
            }

            Instance cloneInstance = null;

            TreeInstance[] buffer = TreeManager.instance.m_trees.m_buffer;
            uint tree = id.Tree;

            uint clone;
            if (TreeManager.instance.CreateTree(out clone, ref SimulationManager.instance.m_randomizer,
                buffer[tree].Info, newPosition, buffer[tree].Single))
            {
                InstanceID cloneID = default(InstanceID);
                cloneID.Tree = clone;
                cloneInstance = new MoveableTree(cloneID);
            }

            return cloneInstance;
        }

        public override Instance Clone(InstanceState instanceState)
        {
            TreeState state = instanceState as TreeState;

            Instance cloneInstance = null;

            uint clone;
            if (TreeManager.instance.CreateTree(out clone, ref SimulationManager.instance.m_randomizer,
                state.info as TreeInfo, state.position, state.single))
            {
                InstanceID cloneID = default(InstanceID);
                cloneID.Tree = clone;
                cloneInstance = new MoveableTree(cloneID);
            }

            return cloneInstance;
        }

        public override void Delete()
        {
            if (isValid) TreeManager.instance.ReleaseTree(id.Tree);
        }

        public override Bounds GetBounds(bool ignoreSegments = true)
        {
            TreeInstance[] buffer = TreeManager.instance.m_trees.m_buffer;
            uint tree = id.Tree;
            TreeInfo info = buffer[tree].Info;

            Randomizer randomizer = new Randomizer(tree);
            float scale = info.m_minScale + (float)randomizer.Int32(10000u) * (info.m_maxScale - info.m_minScale) * 0.0001f;
            float radius = Mathf.Max(info.m_generatedInfo.m_size.x, info.m_generatedInfo.m_size.z) * scale;

            return new Bounds(buffer[tree].Position, new Vector3(radius, 0, radius));
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color toolColor, Color despawnColor)
        {
            if (!isValid) return;

            uint tree = id.Tree;
            TreeManager treeManager = TreeManager.instance;
            TreeInfo treeInfo = treeManager.m_trees.m_buffer[tree].Info;
            Vector3 position = treeManager.m_trees.m_buffer[tree].Position;
            Randomizer randomizer = new Randomizer(tree);
            float scale = treeInfo.m_minScale + (float)randomizer.Int32(10000u) * (treeInfo.m_maxScale - treeInfo.m_minScale) * 0.0001f;
            float alpha = 1f;
            TreeTool.CheckOverlayAlpha(treeInfo, scale, ref alpha);
            toolColor.a *= alpha;
            TreeTool.RenderOverlay(cameraInfo, treeInfo, position, scale, toolColor);
        }

        public override void RenderCloneOverlay(InstanceState state, ref Matrix4x4 matrix4x, Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain, RenderManager.CameraInfo cameraInfo, Color toolColor)
        {
            uint tree = id.Tree;

            TreeInfo info = TreeManager.instance.m_trees.m_buffer[tree].Info;
            Randomizer randomizer = new Randomizer(tree);
            float scale = info.m_minScale + (float)randomizer.Int32(10000u) * (info.m_maxScale - info.m_minScale) * 0.0001f;
            float brightness = info.m_minBrightness + (float)randomizer.Int32(10000u) * (info.m_maxBrightness - info.m_minBrightness) * 0.0001f;

            TreeTool.RenderOverlay(cameraInfo, info, state.position, scale, toolColor);
        }

        public override void RenderCloneGeometry(InstanceState state, ref Matrix4x4 matrix4x, Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain, RenderManager.CameraInfo cameraInfo, Color toolColor)
        {
            uint tree = id.Tree;

            TreeInfo info = TreeManager.instance.m_trees.m_buffer[tree].Info;
            Randomizer randomizer = new Randomizer(tree);
            float scale = info.m_minScale + (float)randomizer.Int32(10000u) * (info.m_maxScale - info.m_minScale) * 0.0001f;
            float brightness = info.m_minBrightness + (float)randomizer.Int32(10000u) * (info.m_maxBrightness - info.m_minBrightness) * 0.0001f;

            TreeInstance.RenderInstance(cameraInfo, info, state.position, scale, brightness);
        }
    }
}
