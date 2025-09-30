using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum NodeChangeType
{
    BecameLeaf,      // �ڵ��ΪҶ�ӣ�Ӧ�ô��� Quad & ����
    BecameBranch,    // �ڵ��Ϊ��֧��Ӧ���������� Quad�����˽ڵ㣩
    RemovedSubtree   // �����������Ƴ����ݹ����������� Quad & �������ã�
}

public class Node
{
    public static Queue<Node> currentAllLeaves = new Queue<Node>();
    private static Queue<Node> nextAllLeaves = new Queue<Node>();

    // �Ƴ�����������ش���
    private static Action<Node, NodeChangeType> onNodeChanged; // ��Ϊ�ڵ�仯�ص�

    private static int splitCount;
    private const int eventFrameSplitCountMax = 2;

    public float x;
    public float z;
    public float size;

    public Node[] children;
    public Node parent;
    public bool isLeaf = true;
    public bool parentMerged;

    public Color nodeColor; // �����ɫ���ڿ��ӻ�
                            // ��¼Ҷ�ӽڵ��Ӧ�� GameObject
    public GameObject nodeObj;
    public Material nodeMat; // ���Ի�������Ӱ������
    public Texture2D debugTexture;


    public int tileX;
    public int tileY;
    public int tileZ;

    // �����������ò�������̬����
    private static float SPLIT_RATIO = 2.0f;
    private static float MERGE_RATIO = 3.0f;
    private static float HEIGHT_WEIGHT = 0.2f;
    private static int MAX_Z = 15;
    private static float MAGIC_NUM = 2.0f;

    public static void Configure(float splitRatio, float mergeRatio, float heightWeight, int maxZ, float magicNum)
    {
        SPLIT_RATIO = Mathf.Max(0.01f, splitRatio);
        MERGE_RATIO = Mathf.Max(SPLIT_RATIO + 0.01f, mergeRatio); // ȷ���г���
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
        root.nodeColor = new Color(1, 0, 0); // ���ڵ��ɫ
        currentAllLeaves.Enqueue(root);
        onNodeChanged?.Invoke(root, NodeChangeType.BecameLeaf); // �������ڵ��Ӧ�� GameObject
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
        nextAllLeaves.Clear();//ÿ�ζ�����

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
            Debug.LogWarning($"�Ѿ������㼶�����ٷ���");
            return; // 
        }

        children = new Node[4];
        isLeaf = false;
        // ֪ͨ���ڵ�״̬�仯����Ҷ�ӱ�Ϊ��Ҷ�ӣ�
        onNodeChanged?.Invoke(this, NodeChangeType.BecameBranch);

        // Ϊ�ӽڵ���䲻ͬ��ɫ
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

        // ɾ���Լ���Ӧ�Ľڵ� GameObject
        if (nodeObj != null) GameObject.Destroy(nodeObj);
        nodeObj = null;
    }

    private void Merge()
    {
        if (children == null)
            return;

        nextAllLeaves.Enqueue(this);
        // ��ÿ���ӽڵ㣺֪ͨ��Ⱦ�㡸�������Ƴ���������Ⱦ��ݹ����� propertyBlocks / �������� / nodeObj
        for (int i = 0; i < 4; i++)
        {
            var child = children[i];
            if (child != null)
            {
                onNodeChanged?.Invoke(child, NodeChangeType.RemovedSubtree);
                // ��������ò����
                child.parent = null;
                child.parentMerged = true;
            }
        }
        // �����ӽڵ����ݽṹ���߼��㣩
        children = null;
        isLeaf = true;
        // ֪ͨ�ⲿ���Լ���ΪҶ�ӣ���Ⱦ�㴴�����ڵ�� Quad �����ض�Ӧ����
        onNodeChanged?.Invoke(this, NodeChangeType.BecameLeaf);
    }

    private float CalculateLodSize(Vector3 camPos)
    {
        // �ڵ����ģ������� y=0 ƽ�棩
        Vector3 nodeCenter = new Vector3(x + size * 0.5f, 0f, z + size * 0.5f);
        // ������߶�Ȩ�صľ���
        float dx = camPos.x - nodeCenter.x;
        float dz = camPos.z - nodeCenter.z;
        float dy = camPos.y - nodeCenter.y;
        float d = Mathf.Sqrt(dx * dx + dz * dz + (dy * HEIGHT_WEIGHT) * (dy * HEIGHT_WEIGHT));
        // �ڵ�Խ��ߣ�XZ ƽ�棩
        float diag = Mathf.Sqrt(2f) * size;
        float ratio = d / Mathf.Max(1f, diag) * MAGIC_NUM;
        // ��СҶ�ӱ���������һ����Ͳ���������
        if (tileZ >= MAX_Z)
        {
            // ���������ֻ�ϲ��������ٷ��ظ�С�ߴ�
            if (ratio > MERGE_RATIO)
                return size * 2; // Զ�ˣ���ʾ���Ժϲ�
            return size; // ���򱣳�
        }
        // ����/�ϲ� �����ж�
        if (ratio < SPLIT_RATIO)
        {
            // ����/�����Դ�ϣ����ϸ
            return size * 0.5f;
        }
        else if (ratio > MERGE_RATIO)
        {
            // Զ��/������С��ϣ������
            return size * 2;
        }
        else
        {
            // ���ֵ�ǰ�㼶
            return size;
        }
    }


    // ���ƽڵ��Gizmos
    public void DrawGizmos(Vector3 terrainOffset)
    {
        Vector3 center = terrainOffset + new Vector3(x + size / 2f, 0, z + size / 2f);
        Vector3 sizeVec = new Vector3(size, 0.1f, size);

        if (isLeaf)
        {
            // Ҷ�ӽڵ㣺ʵ�Ĳ�ɫ����
            Gizmos.color = nodeColor;
            //Gizmos.DrawCube(center, sizeVec);

            //// �߿�
            //Gizmos.color = Color.white;
            Gizmos.DrawWireCube(center, sizeVec);
        }
        else
        {
            // ��Ҷ�ӽڵ㣺�߿�
            Gizmos.color = Color.gray;
            Gizmos.DrawWireCube(center, sizeVec);
            // �ݹ�����ӽڵ�
            foreach (var child in children)
            {
                child.DrawGizmos(terrainOffset);
            }
        }
    }

    // ���ƽڵ���Ϣ��ǩ
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
