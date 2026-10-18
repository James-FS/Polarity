using System;
using Polarity.Config;
using Polarity.Simulation;
using UnityEditor;
using UnityEngine;

namespace Polarity.EditorTools
{
    [CustomEditor(typeof(NavigationGridAuthoring))]
    public sealed class NavigationGridAuthoringEditor : Editor
    {
        private string previewError;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var authoring = (NavigationGridAuthoring)target;
            EditorGUILayout.Space();
            if (!authoring.HasValidConfiguration)
                EditorGUILayout.HelpBox("配置无效：检查敌人引用、正数范围、互不重叠的层、无旋转/单位缩放及格子总数不超过65536。", MessageType.Warning);
            EditorGUILayout.LabelField("网格尺寸", $"{authoring.Columns} × {authoring.Rows}");
            EditorGUILayout.LabelField("身体检查半径", authoring.QueryRadius.ToString("0.###"));
            using (new EditorGUI.DisabledScope(Application.isPlaying || !authoring.HasValidConfiguration))
            {
                if (GUILayout.Button("重建网格预览"))
                {
                    previewError = null;
                    try { authoring.RebuildPreview(); }
                    catch (Exception exception) { previewError = exception.Message; }
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("生成A*路径预览"))
                {
                    previewError = null;
                    try { authoring.BuildPathPreview(); }
                    catch (Exception exception) { previewError = exception.Message; }
                    SceneView.RepaintAll();
                }
            }
            if (!string.IsNullOrEmpty(previewError)) EditorGUILayout.HelpBox(previewError, MessageType.Error);
            var grid = authoring.Grid;
            if (grid == null)
            {
                EditorGUILayout.HelpBox("尚未采样。修改墙、尖刺或箱子后点击重建；预览不保存进Prefab。运行时由World初始化并更新箱子占位。", MessageType.Info);
                return;
            }
            int free = 0, boundary = 0, walls = 0, hazards = 0, dynamic = 0;
            for (int i = 0; i < grid.Count; i++)
            {
                NavigationCell value = grid.GetCell(i);
                if (value == NavigationCell.Free) free++;
                if ((value & NavigationCell.Boundary) != 0) boundary++;
                if ((value & NavigationCell.StaticObstacle) != 0) walls++;
                if ((value & NavigationCell.Hazard) != 0) hazards++;
                if ((value & NavigationCell.DynamicObstacle) != 0) dynamic++;
            }
            EditorGUILayout.HelpBox($"绿：可通行 {free}\n灰：边界 {boundary} / 实体 {walls}\n红：危险 {hazards}\n橙：箱子 {dynamic}\n分类可重叠；连接线仅显示选定节点。", MessageType.Info);
            EditorGUILayout.LabelField("占位版本", grid.Version.ToString());
            EditorGUILayout.LabelField("最近更新格数", authoring.Sampler.LastResampledCells.ToString());
            if (authoring.PreviewSearch != null)
                EditorGUILayout.LabelField("预览路径", $"{authoring.PreviewSearch.Status} / 接近点路线: {authoring.PreviewSearch.IsPartial} / 展开: {authoring.PreviewSearch.ExpandedNodes}");
            EditorGUILayout.HelpBox("紫色为路径；运行时由World分配搜索预算并驱动导航意图。导航禁行不改变真实碰撞与伤害。", MessageType.None);
        }
    }
}
