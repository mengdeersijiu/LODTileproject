using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum NodeChangeType
{
    BecameLeaf,      // 节点变为叶子（应该创建 Quad & 纹理）
    BecameBranch,    // 节点变为分支（应该销毁自身 Quad，仅此节点）
    RemovedSubtree   // 整个子树被移除（递归销毁子树的 Quad & 纹理引用）
}

public class Node
{
    public static Queue<Node> currentAllLeaves = new Queue<Node>();
    private static Queue<Node> nextAllLeaves = new Queue<Node>();

    // 移除纹理索引相关代码
    private static Action<Node, NodeChangeType> onNodeChanged; // 改为节点变化回调

    private static int splitCount;
    private const int eventFrameSplitCountMax = 2;

    public float x;
    public float z;
    public float size;

    public Node[] children;
    public Node parent;
    public bool isLeaf = true;
    public bool parentMerged;

    public Color nodeColor; // 添加颜色用于可视化
                            // 记录叶子节点对应的 GameObject
    public GameObject nodeObj;
    public Material nodeMat; // 可以换成卫星影像纹理
    public Texture2D debugTexture;


    public int tileX;
    public int tileY;
    public int tileZ;

    // 新增：可配置参数（静态共享）
    private static float SPLIT_RATIO = 2.0f;
    private static float MERGE_RATIO = 3.0f;
    private static float HEIGHT_WEIGHT = 0.2f;
    private static int MAX_Z = 15;
    private static float MAGIC_NUM = 2.0f;

    public static void Configure(float splitRatio, float mergeRatio, float heightWeight, int maxZ, float magicNum)
    {
        SPLIT_RATIO = Mathf.Max(0.01f, splitRatio);
        MERGE_RATIO = Mathf.Max(SPLIT_RATIO + 0.01f, mergeRatio); // 确保有迟滞
        HEIGHT_WEIGHT = Mathf.Max(0f, Mathf.Min(1f, heightWeight));
        //MIN_LEAF_SIZE = Mathf.Max(1, minLeafSize);
        MAX_Z = Mathf.Max(1, maxZ);
        MAGIC_NUM = Mathf.Max(1, magicNum);
    }

    public static Node CreateRoot(float rootSize, Action<Node, NodeChangeType> onNodeChangedCallback)
    {
        onNodeChanged = onNodeChangedCallback;
        var root = new Node();
        root.size = rootSize;
        root.nodeColor = new Color(1, 0, 0); // 根节点红色
        currentAllLeaves.Enqueue(root);
        onNodeChanged?.Invoke(root, NodeChangeType.BecameLeaf); // 创建根节点对应的 GameObject
        return root;
    }

    public void SetTileIndex(int z, int x, int y)
    {
        this.tileZ = z;
        this.tileX = x;
        this.tileY = y;
    }

    public static void UpdateAllLeavesState(Vector3 camPos)
    {
        splitCount = 0;
        nextAllLeaves.Clear();//每次都更新

        while (currentAllLeaves.Count > 0)
        {
            var node = currentAllLeaves.Dequeue();
            node.UpdateState(camPos);
        }

        var tempList = currentAllLeaves;
        currentAllLeaves = nextAllLeaves;
        nextAllLeaves = tempList;
    }

    private void UpdateState(Vector3 camPos)
    {
        if (this.parentMerged)
            return;

        if (parent != null)
        {
            float parent_lodSize = parent.CalculateLodSize(camPos);
            bool allBrothersAreLeaf = true;
            for (int i = 0; i < 4; i++)
            {
                if (parent.children[i].isLeaf == false)
                    allBrothersAreLeaf = false;
            }
            if (parent.size <= parent_lodSize && allBrothersAreLeaf)
            {
                parent.Merge();
                return;
            }
        }

        float lodSize = CalculateLodSize(camPos);

        if (size > lodSize)
        {
            if (splitCount++ < eventFrameSplitCountMax)
            {
                Split();
            }
            else
            {
                nextAllLeaves.Enqueue(this);
            }
        }
        else
        {
            nextAllLeaves.Enqueue(this);
        }
    }

    private void Split()
    {
        if (tileZ >= MAX_Z)
        {
            Debug.LogWarning($"已经到最大层级，不再分裂");
            return; // 
        }

        children = new Node[4];
        isLeaf = false;
        // 通知父节点状态变化（从叶子变为非叶子）
        onNodeChanged?.Invoke(this, NodeChangeType.BecameBranch);

        // 为子节点分配不同颜色
        Color[] childColors = new Color[] {
                new Color(1, 0.5f, 0.5f),
                new Color(0.5f, 1, 0.5f),
                new Color(0.5f, 0.5f, 1),
                new Color(1, 1, 0.5f)
            };

        children[0] = new Node()
        {
            x = x,
            z = z,
            size = size * 0.5f,
            parent = this,
            nodeColor = childColors[0],
        };
        children[1] = new Node()
        {
            x = x + size * 0.5f,
            z = z,
            size = size * 0.5f,
            parent = this,
            nodeColor = childColors[1],
        };
        children[2] = new Node()
        {
            x = x,
            z = z + size * 0.5f,
            size = size * 0.5f,
            parent = this,
            nodeColor = childColors[2],
        };
        children[3] = new Node()
        {
            x = x + size * 0.5f,
            z = z + size * 0.5f,
            size = size * 0.5f,
            parent = this,
            nodeColor = childColors[3],
        };

        children[0].SetTileIndex(tileZ + 1, tileX * 2, tileY * 2 + 1);
        children[1].SetTileIndex(tileZ + 1, tileX * 2 + 1, tileY * 2 + 1);
        children[2].SetTileIndex(tileZ + 1, tileX * 2, tileY * 2);
        children[3].SetTileIndex(tileZ + 1, tileX * 2 + 1, tileY * 2);

        for (int i = 0; i < 4; i++)
        {
            nextAllLeaves.Enqueue(children[i]);
            onNodeChanged?.Invoke(children[i], NodeChangeType.BecameLeaf);
        }

        // 删除自己对应的节点 GameObject
        if (nodeObj != null) GameObject.Destroy(nodeObj);
        nodeObj = null;
    }

    private void Merge()
    {
        if (children == null)
            return;

        nextAllLeaves.Enqueue(this);
        // 对每个子节点：通知渲染层「子树被移除」，让渲染层递归清理 propertyBlocks / 纹理引用 / nodeObj
        for (int i = 0; i < 4; i++)
        {
            var child = children[i];
            if (child != null)
            {
                onNodeChanged?.Invoke(child, NodeChangeType.RemovedSubtree);
                // 解除父引用并标记
                child.parent = null;
                child.parentMerged = true;
            }
        }
        // 丢弃子节点数据结构（逻辑层）
        children = null;
        isLeaf = true;
        // 通知外部：自己变为叶子（渲染层创建父节点的 Quad 并加载对应纹理）
        onNodeChanged?.Invoke(this, NodeChangeType.BecameLeaf);
    }

    private float CalculateLodSize(Vector3 camPos)
    {
        // 节点中心（地形在 y=0 平面）
        Vector3 nodeCenter = new Vector3(x + size * 0.5f, 0f, z + size * 0.5f);
        // 计算带高度权重的距离
        float dx = camPos.x - nodeCenter.x;
        float dz = camPos.z - nodeCenter.z;
        float dy = camPos.y - nodeCenter.y;
        float d = Mathf.Sqrt(dx * dx + dz * dz + (dy * HEIGHT_WEIGHT) * (dy * HEIGHT_WEIGHT));
        // 节点对角线（XZ 平面）
        float diag = Mathf.Sqrt(2f) * size;
        float ratio = d / Mathf.Max(1f, diag) * MAGIC_NUM;
        // 最小叶子保护：到这一级别就不再往下切
        if (tileZ >= MAX_Z)
        {
            // 仅允许“保持或合并”，不再返回更小尺寸
            if (ratio > MERGE_RATIO)
                return size * 2; // 远了，提示可以合并
            return size; // 否则保持
        }
        // 分裂/合并 迟滞判定
        if (ratio < SPLIT_RATIO)
        {
            // 近且/或块相对大：希望更细
            return size * 0.5f;
        }
        else if (ratio > MERGE_RATIO)
        {
            // 远且/或块相对小：希望更粗
            return size * 2;
        }
        else
        {
            // 保持当前层级
            return size;
        }
    }


    // 绘制节点的Gizmos
    public void DrawGizmos(Vector3 terrainOffset)
    {
        Vector3 center = terrainOffset + new Vector3(x + size / 2f, 0, z + size / 2f);
        Vector3 sizeVec = new Vector3(size, 0.1f, size);

        if (isLeaf)
        {
            // 叶子节点：实心彩色矩形
            Gizmos.color = nodeColor;
            //Gizmos.DrawCube(center, sizeVec);

            //// 边框
            //Gizmos.color = Color.white;
            Gizmos.DrawWireCube(center, sizeVec);
        }
        else
        {
            // 非叶子节点：线框
            Gizmos.color = Color.gray;
            Gizmos.DrawWireCube(center, sizeVec);
            // 递归绘制子节点
            foreach (var child in children)
            {
                child.DrawGizmos(terrainOffset);
            }
        }
    }

    // 绘制节点信息标签
    public void DrawLabel(Vector3 terrainOffset)
    {
        Vector3 pos = terrainOffset + new Vector3(x + size / 2f, 1, z + size / 2f);
        //string label = $"Size: {size}\nPos: ({x},{z})";
        string label = $"Z={tileZ}\nX={tileX}\nY={tileY}";

#if UNITY_EDITOR
        UnityEditor.Handles.Label(pos, label, new GUIStyle()
        {
            normal = new GUIStyleState() { textColor = Color.red },
            fontSize = 8
        });
#endif
    }
}
